namespace Tgysm.Application.Abstractions;

/// <summary>Who performs the current operation (HTTP user or CLI) and the correlation id for audit.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }

    string CorrelationId { get; }
}
