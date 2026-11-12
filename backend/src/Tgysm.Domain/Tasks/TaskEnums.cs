namespace Tgysm.Domain.Tasks;

/// <summary>Task states (ADR-0031). Named apart from <c>System.Threading.Tasks.TaskStatus</c>.</summary>
public enum WorkTaskStatus
{
    Open,
    InProgress,
    OnHold,
    Completed,
    Cancelled,
}

public enum TaskEventKind
{
    Created,
    Assigned,
    Transferred,
    Started,
    Held,
    Resumed,
    Completed,
    Cancelled,
    Reopened,
    Commented,
}
