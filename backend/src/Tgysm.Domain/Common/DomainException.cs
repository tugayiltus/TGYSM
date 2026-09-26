namespace Tgysm.Domain.Common;

/// <summary>A business rule was violated. <see cref="Code"/> is a stable <see cref="ErrorCodes"/> value.</summary>
public sealed class DomainException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
