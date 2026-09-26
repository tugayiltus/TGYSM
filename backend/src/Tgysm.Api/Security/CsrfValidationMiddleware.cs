using Microsoft.AspNetCore.Antiforgery;
using Tgysm.Api.Http;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Security;

/// <summary>Requires a valid antiforgery token in <see cref="HeaderName"/> on every unsafe /api request (ADR-0004).</summary>
internal sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.CsrfInvalid);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiRoutes.ApiPrefix)
        && !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method));
}
