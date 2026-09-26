using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tickets;

/// <summary>A stored file (ADR-0012). Only <see cref="ScanStatus.Clean"/> files can be downloaded.</summary>
public sealed class TicketAttachment : Entity
{
    public const int NameMaxLength = 255;
    public const int ContentTypeMaxLength = 100;
    public const int StorageKeyMaxLength = 100;

    private TicketAttachment()
    {
    }

    public Guid TicketId { get; private set; }
    public Guid? CommentId { get; private set; }
    public string OriginalName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public Visibility Visibility { get; private set; }
    public ScanStatus ScanStatus { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TicketAttachment Create(
        Guid ticketId, Guid? commentId, string originalName, string contentType, long sizeBytes, string storageKey, Visibility visibility, Guid uploadedBy, DateTimeOffset now)
    {
        var name = Path.GetFileName(originalName.Trim());
        return new TicketAttachment
        {
            TicketId = ticketId,
            CommentId = commentId,
            OriginalName = name.Length > NameMaxLength ? name[^NameMaxLength..] : name,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            Visibility = visibility,
            ScanStatus = ScanStatus.Pending,
            UploadedBy = uploadedBy,
            CreatedAt = now,
        };
    }

    public void MarkScanned(bool clean) => ScanStatus = clean ? ScanStatus.Clean : ScanStatus.Rejected;
}
