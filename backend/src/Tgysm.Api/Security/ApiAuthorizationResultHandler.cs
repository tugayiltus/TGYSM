using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Tgysm.Api.Http;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Security;

/// <summary>Writes 401/403 as ProblemDetails with a code instead of empty responses or redirects.</summary>
internal sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            var passwordChangePending = authorizeResult.AuthorizationFailure?.FailureReasons
                .Any(reason => reason.Message == ErrorCodes.PasswordChangeRequired) == true;
            await ApiProblem.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                passwordChangePending ? ErrorCodes.PasswordChangeRequired : ErrorCodes.Forbidden);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
