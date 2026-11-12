using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tasks;

public sealed record StartTaskCommand(string Number, uint Version);

public sealed record HoldTaskCommand(string Number, uint Version, HoldReason Reason, string? Note);

public sealed record ResumeTaskCommand(string Number, uint Version);

public sealed record CompleteTaskCommand(string Number, uint Version, string ResultNote);

public sealed record CancelTaskCommand(string Number, uint Version, string Reason);

public sealed record ReopenTaskCommand(string Number, uint Version);

internal sealed class HoldTaskValidator : AbstractValidator<HoldTaskCommand>
{
    public HoldTaskValidator()
    {
        RuleFor(c => c.Reason).IsInEnum();
        RuleFor(c => c.Note).MaximumLength(WorkTask.ReasonMaxLength);
    }
}

internal sealed class CompleteTaskValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskValidator() => RuleFor(c => c.ResultNote).NotEmpty().MaximumLength(WorkTask.NoteMaxLength);
}

internal sealed class CancelTaskValidator : AbstractValidator<CancelTaskCommand>
{
    public CancelTaskValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(WorkTask.ReasonMaxLength);
}

/// <summary>One status change: history row and audit entry in the same save. No task action touches the linked ticket.</summary>
internal static class TaskStatusChange
{
    public static void Record(IAppDbContext db, IAuditLog audit, WorkTask task, WorkTaskStatus before, TaskEventKind kind, Guid actorId, DateTimeOffset now, object? data = null)
    {
        TaskHistory.Record(db, task, kind, actorId, now, data);
        audit.Record(AuditActions.TaskStatusChanged, AuditEntityTypes.Task, task.Id, new { Status = before }, new { task.Status });
    }
}

public sealed class StartTaskHandler(IAppDbContext db, TaskWork work, IAuditLog audit)
{
    public Task<TaskDetail> HandleAsync(StartTaskCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, (task, viewer, now) =>
        {
            var before = task.Status;
            task.Start(now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Started, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed class HoldTaskHandler(IAppDbContext db, IValidator<HoldTaskCommand> validator, TaskWork work, IAuditLog audit)
{
    public async Task<TaskDetail> HandleAsync(HoldTaskCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, (task, viewer, now) =>
        {
            var before = task.Status;
            task.Hold(command.Reason, command.Note, now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Held, viewer.UserId, now, new { command.Reason, command.Note });
            return Task.CompletedTask;
        }, cancellationToken);
    }
}

public sealed class ResumeTaskHandler(IAppDbContext db, TaskWork work, IAuditLog audit)
{
    public Task<TaskDetail> HandleAsync(ResumeTaskCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, (task, viewer, now) =>
        {
            var before = task.Status;
            task.Resume(now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Resumed, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed class CompleteTaskHandler(IAppDbContext db, IValidator<CompleteTaskCommand> validator, TaskWork work, IAuditLog audit)
{
    public async Task<TaskDetail> HandleAsync(CompleteTaskCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, (task, viewer, now) =>
        {
            var before = task.Status;
            task.Complete(command.ResultNote, now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Completed, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
    }
}

public sealed class CancelTaskHandler(IAppDbContext db, IValidator<CancelTaskCommand> validator, TaskWork work, IAuditLog audit)
{
    public async Task<TaskDetail> HandleAsync(CancelTaskCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, (task, viewer, now) =>
        {
            var before = task.Status;
            task.Cancel(command.Reason, now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Cancelled, viewer.UserId, now, new { command.Reason });
            return Task.CompletedTask;
        }, cancellationToken);
    }
}

public sealed class ReopenTaskHandler(IAppDbContext db, TaskWork work, IAuditLog audit)
{
    public Task<TaskDetail> HandleAsync(ReopenTaskCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, (task, viewer, now) =>
        {
            var before = task.Status;
            task.Reopen(now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Reopened, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}
