using Tgysm.Domain.Common;

namespace Tgysm.Domain.Assets;

/// <summary>One handover of an asset to a user; active while <see cref="ReturnedAt"/> is null (ADR-0010).</summary>
public sealed class AssetAssignment : Entity
{
    public const int NoteMaxLength = 500;

    private AssetAssignment()
    {
    }

    public Guid AssetId { get; private init; }
    public Guid UserId { get; private init; }
    public DateTimeOffset AssignedAt { get; private init; }
    public Guid AssignedBy { get; private init; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public Guid? ReturnedBy { get; private set; }

    /// <summary>Request ticket the handover fulfils, if any.</summary>
    public Guid? RequestTicketId { get; private init; }

    public string? Note { get; private init; }

    public bool IsActive => ReturnedAt is null;

    internal static AssetAssignment Open(Guid assetId, Guid userId, Guid actorUserId, Guid? requestTicketId, string? note, DateTimeOffset now) => new()
    {
        AssetId = assetId,
        UserId = userId,
        AssignedAt = now,
        AssignedBy = actorUserId,
        RequestTicketId = requestTicketId,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
    };

    internal void Close(Guid actorUserId, DateTimeOffset now)
    {
        ReturnedAt = now;
        ReturnedBy = actorUserId;
    }
}
