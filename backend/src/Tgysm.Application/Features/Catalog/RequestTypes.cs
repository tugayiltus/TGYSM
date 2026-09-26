using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Catalog;

public sealed record RequestTypeItem(Guid Id, string Name, bool RequiresApproval, bool IsActive, uint Version)
{
    public static RequestTypeItem From(RequestType r) => new(r.Id, r.Name, r.RequiresApproval, r.IsActive, r.Version);
}

public sealed record CreateRequestTypeCommand(string Name, bool RequiresApproval);

public sealed record UpdateRequestTypeCommand(Guid Id, uint Version, string Name, bool RequiresApproval);

public sealed record SetRequestTypeActiveCommand(Guid Id, uint Version, bool IsActive);

internal sealed class CreateRequestTypeValidator : AbstractValidator<CreateRequestTypeCommand>
{
    public CreateRequestTypeValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(RequestType.NameMaxLength);
}

internal sealed class UpdateRequestTypeValidator : AbstractValidator<UpdateRequestTypeCommand>
{
    public UpdateRequestTypeValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(RequestType.NameMaxLength);
}

internal static class RequestTypeRules
{
    public const string NameTakenMessage = "Another request type uses this name.";

    public static async Task EnsureNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.RequestTypes.AnyAsync(r => r.NormalizedName == key && r.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.RequestTypeNameTaken, NameTakenMessage);
        }
    }
}

public sealed class ListRequestTypesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<RequestTypeItem>> HandleAsync(CancellationToken cancellationToken) =>
        await db.RequestTypes.AsNoTracking().OrderBy(r => r.NormalizedName)
            .Select(r => new RequestTypeItem(r.Id, r.Name, r.RequiresApproval, r.IsActive, r.Version))
            .ToListAsync(cancellationToken);
}

public sealed class CreateRequestTypeHandler(IAppDbContext db, IValidator<CreateRequestTypeCommand> validator, IAuditLog audit)
{
    public async Task<RequestTypeItem> HandleAsync(CreateRequestTypeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await RequestTypeRules.EnsureNameFreeAsync(db, command.Name, null, cancellationToken);
        var type = RequestType.Create(command.Name, command.RequiresApproval);
        db.RequestTypes.Add(type);
        audit.Record(AuditActions.RequestTypeCreated, AuditEntityTypes.RequestType, type.Id, newValues: new { type.Name, type.RequiresApproval });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.RequestTypeNameTaken, RequestTypeRules.NameTakenMessage, cancellationToken);
        return RequestTypeItem.From(type);
    }
}

public sealed class UpdateRequestTypeHandler(IAppDbContext db, IValidator<UpdateRequestTypeCommand> validator, IAuditLog audit)
{
    public async Task<RequestTypeItem> HandleAsync(UpdateRequestTypeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var type = await db.RequestTypes.SingleOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.RequestType);
        db.SetExpectedVersion(type, command.Version);
        await RequestTypeRules.EnsureNameFreeAsync(db, command.Name, type.Id, cancellationToken);
        var before = new { type.Name, type.RequiresApproval };
        type.Update(command.Name, command.RequiresApproval);
        audit.Record(AuditActions.RequestTypeUpdated, AuditEntityTypes.RequestType, type.Id, before, new { type.Name, type.RequiresApproval });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.RequestTypeNameTaken, RequestTypeRules.NameTakenMessage, cancellationToken);
        return RequestTypeItem.From(type);
    }
}

public sealed class SetRequestTypeActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<RequestTypeItem> HandleAsync(SetRequestTypeActiveCommand command, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.SingleOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.RequestType);
        if (type.IsActive == command.IsActive)
        {
            return RequestTypeItem.From(type);
        }

        db.SetExpectedVersion(type, command.Version);
        if (command.IsActive)
        {
            type.Reactivate();
        }
        else
        {
            type.Deactivate();
        }

        audit.Record(command.IsActive ? AuditActions.RequestTypeReactivated : AuditActions.RequestTypeDeactivated, AuditEntityTypes.RequestType, type.Id);
        await db.SaveChangesAsync(cancellationToken);
        return RequestTypeItem.From(type);
    }
}
