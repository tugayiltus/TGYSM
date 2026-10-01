using Tgysm.Domain.Common;

namespace Tgysm.Application.Common;

/// <summary>The signed-in user's role may not use this feature.</summary>
public sealed class ForbiddenException() : Exception("The current user may not do this.")
{
    public string Code { get; } = ErrorCodes.Forbidden;
}
