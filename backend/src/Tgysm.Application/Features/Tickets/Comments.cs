using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public sealed record UploadedFile(string FileName, long Length, Func<Stream> OpenReadStream);

public sealed record AddCommentCommand(string Number, string? Body, Visibility? Visibility, IReadOnlyList<UploadedFile> Files);

public sealed record AttachmentItem(Guid Id, string OriginalName, string ContentType, long SizeBytes, ScanStatus ScanStatus);

public sealed record CommentItem(Guid Id, string Body, Visibility Visibility, Guid AuthorId, string AuthorName, DateTimeOffset CreatedAt, IReadOnlyList<AttachmentItem> Attachments);

public sealed record TimelineItem(Guid Id, TicketEventKind Kind, Visibility Visibility, string? ActorName, JsonElement Data, DateTimeOffset OccurredAt);

public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);

internal sealed class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentValidator()
    {
        RuleFor(c => c.Body).NotEmpty().When(c => c.Files.Count == 0);
        RuleFor(c => c.Body).MaximumLength(TicketComment.BodyMaxLength);
        RuleFor(c => c.Visibility).IsInEnum();
    }
}

/// <summary>
/// Public reply or internal note with up to five files (spec §3.7). Employees write public replies on their own tickets;
/// internal notes need someone who works on the ticket. Files inherit the comment's visibility.
/// </summary>
public sealed class AddCommentHandler(
    IAppDbContext db, IValidator<AddCommentCommand> validator, TicketScope scope, IFileStore files, IFileScanner scanner, TimeProvider clock)
{
    public const string FilesField = "files";

    public async Task<CommentItem> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var contents = await ReadFilesAsync(command.Files, cancellationToken);
        var (ticket, viewer) = await scope.LoadAsync(command.Number, cancellationToken);
        var visibility = command.Visibility ?? Visibility.Public;
        if (visibility == Visibility.Internal && !viewer.CanWork(ticket))
        {
            throw new DomainException(ErrorCodes.CommentInternalNotAllowed, "Only people working on the ticket write internal notes.");
        }

        var now = clock.GetUtcNow();
        var comment = TicketComment.Create(ticket.Id, viewer.UserId, command.Body ?? string.Empty, visibility, now);
        var stored = new List<string>();
        try
        {
            foreach (var (file, content, contentType) in contents)
            {
                var key = await files.SaveAsync(content, cancellationToken);
                stored.Add(key);
                var attachment = TicketAttachment.Create(ticket.Id, comment.Id, file.FileName, contentType, content.Length, key, visibility, viewer.UserId, now);
                attachment.MarkScanned(await scanner.IsCleanAsync(key, cancellationToken));
                db.TicketAttachments.Add(attachment);
            }

            db.TicketComments.Add(comment);
            TicketTimeline.Record(db, ticket, visibility == Visibility.Public ? TicketEventKind.PublicComment : TicketEventKind.InternalNote, visibility, viewer.UserId, now,
                new { commentId = comment.Id, files = contents.Count });
            await db.RunInTransactionAsync(async ct =>
            {
                await db.SaveChangesAsync(ct);
                await TicketActivity.TouchAsync(db, ticket.Id, now, ct);
            }, cancellationToken);
        }
        catch
        {
            // Nothing references the stored files when the save fails.
            stored.ForEach(files.Delete);
            throw;
        }

        return (await CommentQueries.ListAsync(db, ticket.Id, viewer, cancellationToken)).Single(c => c.Id == comment.Id);
    }

    private static async Task<List<(UploadedFile File, byte[] Content, string ContentType)>> ReadFilesAsync(IReadOnlyList<UploadedFile> uploads, CancellationToken cancellationToken)
    {
        if (uploads.Count > AttachmentTypes.MaxFilesPerUpload)
        {
            throw FileError(ErrorCodes.AttachmentTooMany, "At most five files per upload.");
        }

        var result = new List<(UploadedFile, byte[], string)>();
        foreach (var upload in uploads)
        {
            if (upload.Length > AttachmentTypes.MaxBytes)
            {
                throw FileError(ErrorCodes.AttachmentTooLarge, "A file is larger than 10 MB.");
            }

            await using var stream = upload.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            var content = buffer.ToArray();
            var contentType = AttachmentTypes.Detect(upload.FileName, content)
                ?? throw FileError(ErrorCodes.AttachmentTypeNotAllowed, "File type is not allowed.");
            result.Add((upload, content, contentType));
        }

        return result;
    }

    private static ValidationException FileError(string code, string message) =>
        new([new ValidationFailure(FilesField, message) { ErrorCode = code }]);
}

internal static class CommentQueries
{
    private const string NameSeparator = " ";

    public static async Task<IReadOnlyList<CommentItem>> ListAsync(IAppDbContext db, Guid ticketId, TicketViewer viewer, CancellationToken cancellationToken)
    {
        var comments = db.TicketComments.AsNoTracking().Where(c => c.TicketId == ticketId);
        var attachments = db.TicketAttachments.AsNoTracking().Where(a => a.TicketId == ticketId);
        if (!viewer.SeesInternal)
        {
            comments = comments.Where(c => c.Visibility == Visibility.Public);
            attachments = attachments.Where(a => a.Visibility == Visibility.Public);
        }

        var files = (await attachments.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(cancellationToken))
            .ToLookup(a => a.CommentId, a => new AttachmentItem(a.Id, a.OriginalName, a.ContentType, a.SizeBytes, a.ScanStatus));
        var rows = await (
            from c in comments
            join u in db.Users on c.AuthorId equals u.Id
            orderby c.CreatedAt, c.Id
            select new { c.Id, c.Body, c.Visibility, c.AuthorId, Name = u.FirstName + NameSeparator + u.LastName, c.CreatedAt }).ToListAsync(cancellationToken);
        return [.. rows.Select(r => new CommentItem(r.Id, r.Body, r.Visibility, r.AuthorId, r.Name, r.CreatedAt, [.. files[r.Id]]))];
    }
}

public sealed record ListCommentsQuery(string Number);

public sealed class ListCommentsHandler(IAppDbContext db, TicketScope scope)
{
    public async Task<IReadOnlyList<CommentItem>> HandleAsync(ListCommentsQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await CommentQueries.ListAsync(db, ticket.Id, viewer, cancellationToken);
    }
}

public sealed record GetTimelineQuery(string Number);

/// <summary>Timeline in order; employees see public events only.</summary>
public sealed class GetTimelineHandler(IAppDbContext db, TicketScope scope)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<TimelineItem>> HandleAsync(GetTimelineQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        var events = db.TicketEvents.AsNoTracking().Where(e => e.TicketId == ticket.Id);
        if (!viewer.SeesInternal)
        {
            events = events.Where(e => e.Visibility == Visibility.Public);
        }

        var rows = await events.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.Visibility,
                e.DataJson,
                e.OccurredAt,
                Actor = db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new TimelineItem(r.Id, r.Kind, r.Visibility, r.Actor, JsonSerializer.Deserialize<JsonElement>(r.DataJson), r.OccurredAt))];
    }
}

public sealed record DownloadAttachmentQuery(string Number, Guid AttachmentId);

/// <summary>Only clean files of a ticket in scope, and internal files only for IT and admins; everything else is 404.</summary>
public sealed class DownloadAttachmentHandler(IAppDbContext db, TicketScope scope, IFileStore files)
{
    public async Task<AttachmentDownload> HandleAsync(DownloadAttachmentQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        var attachment = await db.TicketAttachments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == query.AttachmentId && a.TicketId == ticket.Id && a.ScanStatus == ScanStatus.Clean, cancellationToken);
        if (attachment is null || (attachment.Visibility == Visibility.Internal && !viewer.SeesInternal))
        {
            throw new NotFoundException(AuditEntityTypes.Attachment);
        }

        var stream = files.OpenRead(attachment.StorageKey) ?? throw new NotFoundException(AuditEntityTypes.Attachment);
        return new AttachmentDownload(stream, attachment.ContentType, attachment.OriginalName);
    }
}
