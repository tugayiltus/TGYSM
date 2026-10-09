using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Security;

/// <summary>
/// The API protects an account-link token and the Worker unprotects it when it renders the email (ADR-0015). The two
/// hosts have different content roots, which Data Protection mixes into the purpose by default, so the protector
/// must pin a shared application name.
/// </summary>
public sealed class AccountLinkProtectorSharingTests : IDisposable
{
    private const string Token = "account-link-token";
    private readonly string _apiRoot = TempFolder("api-root");
    private readonly string _workerRoot = TempFolder("worker-root");
    private readonly string _keyFolder = TempFolder("keys");

    private static string TempFolder(string name) => Path.Combine(Path.GetTempPath(), $"tgysm-dp-{name}-{Guid.NewGuid():N}");

    public void Dispose()
    {
        foreach (var folder in new[] { _keyFolder, _apiRoot, _workerRoot })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private ServiceProvider BuildHost(string contentRoot)
    {
        Directory.CreateDirectory(contentRoot);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = contentRoot });
        // The shared key folder now comes from configuration (ADR-0024), as it does on a server.
        builder.Configuration["DataProtection:KeysPath"] = _keyFolder;
        builder.Services.AddInfrastructure();
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void A_secret_protected_by_one_host_is_readable_by_a_host_with_another_content_root()
    {
        using var api = BuildHost(_apiRoot);
        using var worker = BuildHost(_workerRoot);

        var protectedSecret = api.GetRequiredService<IAccountLinkProtector>().Protect(Token);

        Assert.Equal(Token, worker.GetRequiredService<IAccountLinkProtector>().Unprotect(protectedSecret));
    }
}
