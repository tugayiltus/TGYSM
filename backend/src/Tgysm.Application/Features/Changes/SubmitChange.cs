using FluentValidation;
using Tgysm.Application.Common;

namespace Tgysm.Application.Features.Changes;

public sealed record SubmitChangeCommand(string Number, uint Version);

public sealed record WithdrawChangeCommand(string Number, uint Version);

internal sealed class SubmitChangeValidator : AbstractValidator<SubmitChangeCommand>
{
    public SubmitChangeValidator() => RuleFor(c => c.Number).NotEmpty();
}

internal sealed class WithdrawChangeValidator : AbstractValidator<WithdrawChangeCommand>
{
    public WithdrawChangeValidator() => RuleFor(c => c.Number).NotEmpty();
}

/// <summary>Draft → pending approval; needs a window, the three plans and an Uygulama task (409 changes.not_ready).</summary>
public sealed class SubmitChangeHandler(IValidator<SubmitChangeCommand> validator, ChangeWork work, ChangeApprovalFlow flow)
{
    public async Task<ChangeDetail> HandleAsync(SubmitChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version,
            (change, viewer, now, ct) => flow.RequestAsync(change, viewer.UserId, now, ct), cancellationToken);
    }
}

/// <summary>The author takes a pending request back; the change returns to draft.</summary>
public sealed class WithdrawChangeHandler(IValidator<WithdrawChangeCommand> validator, ChangeWork work, ChangeApprovalFlow flow)
{
    public async Task<ChangeDetail> HandleAsync(WithdrawChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, (change, viewer, now, ct) =>
        {
            if (viewer.UserId != change.CreatedById)
            {
                throw new ForbiddenException();
            }

            return flow.WithdrawAsync(change, viewer.UserId, now, ct);
        }, cancellationToken);
    }
}
