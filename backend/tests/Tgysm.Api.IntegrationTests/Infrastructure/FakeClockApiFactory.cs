using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>An API host whose <see cref="TimeProvider"/> is a <see cref="FakeTimeProvider"/> the test advances by hand.</summary>
public sealed class FakeClockApiFactory : ApiFactory
{
    public FakeTimeProvider Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
