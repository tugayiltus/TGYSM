using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Worker;

/// <summary>
/// The Worker composes Application and Infrastructure without Tgysm.Api and validates every handler at start. Ports that
/// the login settings handlers need must therefore come from Infrastructure (ADR-0030).
/// </summary>
public sealed class WorkerServicesTests
{
    [Fact]
    public void Infrastructure_supplies_the_ports_the_login_settings_handlers_need()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ILoginProviderCache>());
        Assert.NotNull(provider.GetService<IEntraConnectionTester>());
    }
}
