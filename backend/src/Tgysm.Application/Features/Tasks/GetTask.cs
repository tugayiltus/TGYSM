namespace Tgysm.Application.Features.Tasks;

public sealed record GetTaskQuery(string Number);

public sealed class GetTaskHandler(TaskScope scope, TaskWork work)
{
    public async Task<TaskDetail> HandleAsync(GetTaskQuery query, CancellationToken cancellationToken)
    {
        var (task, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await work.DetailAsync(task.Id, viewer, cancellationToken);
    }
}
