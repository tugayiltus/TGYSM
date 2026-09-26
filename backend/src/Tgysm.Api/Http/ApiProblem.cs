using Microsoft.AspNetCore.Mvc;

namespace Tgysm.Api.Http;

/// <summary>RFC 7807 body with a stable <c>code</c> and optional per-field <c>errors</c> (field → error codes).</summary>
internal static class ApiProblem
{
    public const string ContentType = "application/problem+json";
    public const string CodeKey = "code";
    public const string ErrorsKey = "errors";

    public static ProblemDetails Create(int status, string code, IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = code };
        problem.Extensions[CodeKey] = code;
        if (errors is not null)
        {
            problem.Extensions[ErrorsKey] = errors;
        }

        return problem;
    }

    public static Task WriteAsync(HttpContext context, int status, string code, IDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Create(status, code, errors), options: null, contentType: ContentType, context.RequestAborted);
    }
}
