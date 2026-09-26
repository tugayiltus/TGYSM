using FluentValidation;
using Tgysm.Domain.Assets;

namespace Tgysm.Application.Features.Assets.Lifecycle;

public sealed record AssignAssetCommand(Guid AssetId, uint Version, Guid UserId, Guid? RequestTicketId, string? Note);

public sealed record ReturnAssetCommand(Guid AssetId, uint Version, Guid LocationId);

public sealed record TransferAssetCommand(Guid AssetId, uint Version, Guid UserId, string? Note);

public sealed record StartMaintenanceCommand(Guid AssetId, uint Version, bool RetainResponsibility);

public sealed record EndMaintenanceCommand(Guid AssetId, uint Version);

public sealed record MarkAssetLostCommand(Guid AssetId, uint Version, string Reason);

public sealed record MarkAssetFoundCommand(Guid AssetId, uint Version, Guid LocationId);

public sealed record RetireAssetCommand(Guid AssetId, uint Version, string Reason);

internal sealed class AssignAssetValidator : AbstractValidator<AssignAssetCommand>
{
    public AssignAssetValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(AssetAssignment.NoteMaxLength);
    }
}

internal sealed class TransferAssetValidator : AbstractValidator<TransferAssetCommand>
{
    public TransferAssetValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Note).MaximumLength(AssetAssignment.NoteMaxLength);
    }
}

internal sealed class ReturnAssetValidator : AbstractValidator<ReturnAssetCommand>
{
    public ReturnAssetValidator() => RuleFor(c => c.LocationId).NotEmpty();
}

internal sealed class MarkAssetFoundValidator : AbstractValidator<MarkAssetFoundCommand>
{
    public MarkAssetFoundValidator() => RuleFor(c => c.LocationId).NotEmpty();
}

internal sealed class MarkAssetLostValidator : AbstractValidator<MarkAssetLostCommand>
{
    public MarkAssetLostValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(AssetMovement.ReasonMaxLength);
}

internal sealed class RetireAssetValidator : AbstractValidator<RetireAssetCommand>
{
    public RetireAssetValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(AssetMovement.ReasonMaxLength);
}
