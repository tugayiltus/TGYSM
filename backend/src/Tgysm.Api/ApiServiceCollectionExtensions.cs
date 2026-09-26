using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api;

internal static class ApiServiceCollectionExtensions
{
    private const string SessionCookieName = "__Host-tgysm";
    private const string AntiforgeryCookieName = "__Host-tgysm-af";
    private const string UnknownClient = "unknown";
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .Validate(o => o.LoginPermitLimitPerMinute > 0 && o.SessionHours > 0, "Security limits must be positive.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

            // Numbers stay numbers in the contract (no quoted numbers), so generated TS types are plain `number`.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;
                options.Events.OnRedirectToLogin = context => ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
                options.Events.OnRedirectToAccessDenied = context => ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
            });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<SecurityOptions>>((cookie, security) => cookie.ExpireTimeSpan = TimeSpan.FromHours(security.Value.SessionHours));

        var passwordChanged = new PasswordChangedRequirement();
        var signedInWithChangedPassword = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(passwordChanged).Build();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(signedInWithChangedPassword)
            // A bare RequireAuthorization() must not skip the pending-password gate.
            .SetDefaultPolicy(signedInWithChangedPassword)
            .AddPolicy(Policies.PasswordChangePending, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(passwordChanged).RequireRole(UserRoleNames.Admin));
        services.AddSingleton<IAuthorizationHandler, PasswordChangedHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfValidationMiddleware.HeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(
                ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited));
            options.AddPolicy(RateLimitPolicies.Login, context =>
            {
                var limit = context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value.LoginPermitLimitPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = LoginWindow, QueueLimit = 0 });
            });
        });

        return services;
    }
}
