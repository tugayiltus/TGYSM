using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;

namespace Tgysm.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DependencyInjectionTests(ApiFactory factory)
{
    private const string HandlerSuffix = "Handler";

    [Fact]
    public async Task Every_application_handler_resolves()
    {
        var handlerTypes = typeof(Tgysm.Application.AssemblyMarker).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && t.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal))
            .ToList();
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.NotEmpty(handlerTypes);
        Assert.All(handlerTypes, type => Assert.NotNull(scope.ServiceProvider.GetRequiredService(type)));
    }
}
