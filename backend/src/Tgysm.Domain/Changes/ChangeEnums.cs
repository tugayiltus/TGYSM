namespace Tgysm.Domain.Changes;

/// <summary>Change states (ADR-0032): Taslak, Onay bekliyor, Planlandı, Uygulanıyor, Doğrulama, Kapalı, İptal.</summary>
public enum ChangeStatus
{
    Draft,
    PendingApproval,
    Scheduled,
    Implementing,
    Verifying,
    Closed,
    Cancelled,
}

/// <summary>Result of a closed change: Başarılı, Geri alındı, Başarısız.</summary>
public enum ChangeOutcome
{
    Successful,
    RolledBack,
    Failed,
}

/// <summary>Task phases of a change: Hazırlık, Uygulama, Doğrulama, Geri dönüş.</summary>
public enum ChangePhase
{
    Preparation,
    Implementation,
    Verification,
    Backout,
}

public enum ChangeApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Returned,
    Withdrawn,
    Void,
}

public enum ChangeEventKind
{
    Created,
    Updated,
    Submitted,
    Withdrawn,
    Approved,
    Rejected,
    Returned,
    ApprovalVoided,
    ImplementationStarted,
    ImplementationFinished,
    BackoutStarted,
    Closed,
    Cancelled,
    AssetsChanged,
}

/// <summary>Why a ticket is tied to a change: the ticket led to the change, or the change led to the ticket.</summary>
public enum ChangeTicketLinkKind
{
    Prompted,
    Caused,
}
