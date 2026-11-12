using Tgysm.Domain.Tasks;

namespace Tgysm.Domain.Notifications;

/// <summary>Turkish label for each <see cref="WorkTaskStatus"/>, for the server-rendered <c>status</c> email variable (see <see cref="TicketStatusLabels"/>).</summary>
public static class TaskStatusLabels
{
    private static readonly IReadOnlyDictionary<WorkTaskStatus, string> Labels = new Dictionary<WorkTaskStatus, string>
    {
        [WorkTaskStatus.Open] = "Açık",
        [WorkTaskStatus.InProgress] = "İşlemde",
        [WorkTaskStatus.OnHold] = "Beklemede",
        [WorkTaskStatus.Completed] = "Tamamlandı",
        [WorkTaskStatus.Cancelled] = "İptal edildi",
    };

    public static string Of(WorkTaskStatus status) =>
        Labels.TryGetValue(status, out var label) ? label : status.ToString();
}
