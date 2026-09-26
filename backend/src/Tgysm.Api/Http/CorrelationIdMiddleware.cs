using Tgysm.Domain.Audit;

namespace Tgysm.Api.Http;

/// <summary>Accepts a safe <c>X-Correlation-Id</c> from the client or keeps the generated trace id; echoes it back.</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        if (IsValid(incoming))
        {
            context.TraceIdentifier = incoming;
        }

        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        return next(context);
    }

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= AuditEvent.CorrelationIdMaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
