namespace Tgysm.Domain.Tickets;

public enum TicketKind
{
    Incident,
    Request,
}

public enum TicketStatus
{
    Open,
    InProgress,
    OnHold,
    Completed,
    Cancelled,
}

public enum HoldReason
{
    User,
    ManagerApproval,
    Vendor,
    TechnicalTeam,
    ThirdParty,
    ScheduledDate,
    Other,
}

/// <summary>Impact and urgency levels of the priority matrix.</summary>
public enum Level
{
    High,
    Medium,
    Low,
}

public enum Priority
{
    P1,
    P2,
    P3,
    P4,
}

public enum TicketOutcome
{
    Resolved,
    ServiceRequestRequired,
    Fulfilled,
    ApprovalRejected,
}

public enum TransferReason
{
    ExpertiseNeeded,
    WrongAssignment,
    OtherTeamResponsibility,
    AuthorizationNeeded,
    Workload,
    Escalation,
    Location,
    Other,
}

public enum Visibility
{
    Public,
    Internal,
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Void,
}

public enum ScanStatus
{
    Pending,
    Clean,
    Rejected,
}

public enum TicketEventKind
{
    Created,
    Assigned,
    Transferred,
    StatusChanged,
    PublicComment,
    InternalNote,
    PriorityChanged,
    ApprovalRequested,
    ApprovalDecided,
    ApprovalVoided,
    ApprovalReassigned,
    RelatedRequestCreated,
    AssetLinked,
    AssetUnlinked,
    Completed,
    Cancelled,
}
