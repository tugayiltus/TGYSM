using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Security;

internal sealed class PasswordChangedRequirement : IAuthorizationRequirement;

internal sealed class PasswordChangedHandler : AuthorizationHandler<PasswordChangedRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PasswordChangedRequirement requirement)
    {
        if (context.User.FindFirstValue(SessionClaims.MustChangePassword) == bool.FalseString)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, ErrorCodes.PasswordChangeRequired));
        }

        return Task.CompletedTask;
    }
}
