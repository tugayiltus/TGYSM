using System.Security.Claims;
using Tgysm.Application.Abstractions;

namespace Tgysm.Api.Http;

/// <summary>Actor from the HTTP user; outside a request (CLI) the actor is anonymous with a generated correlation id.</summary>
internal sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private const string CliCorrelationPrefix = "cli-";
    private readonly string _fallbackCorrelationId = CliCorrelationPrefix + Guid.NewGuid().ToString("N");

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string CorrelationId => accessor.HttpContext?.TraceIdentifier ?? _fallbackCorrelationId;
}
