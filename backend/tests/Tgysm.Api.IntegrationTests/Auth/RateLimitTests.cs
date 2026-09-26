using System.Net;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Auth;

public sealed class LowRateLimitApiFactory : ApiFactory
{
    public const int Limit = 2;

    protected override int LoginPermitLimitPerMinute => Limit;
}

public sealed class RateLimitTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Login_attempts_over_the_limit_get_429()
    {
        var client = ApiClient.Create(factory);
        var credentials = new { email = "nobody@example.com", password = "Wrong-password-1" };

        for (var attempt = 0; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var allowed = await client.PostAsync(ApiClient.LoginPath, credentials);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var limited = await client.PostAsync(ApiClient.LoginPath, credentials);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await ApiClient.ReadCodeAsync(limited));
    }
}
