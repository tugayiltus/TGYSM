using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Tgysm.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>API host on a throw-away PostgreSQL database.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestingEnvironment = "Testing";
    private const int GenerousLoginLimit = 10_000;

    public PostgresDatabase Database { get; } = new();

    /// <summary>Attachment storage of this host; removed on dispose.</summary>
    public string FilesRoot { get; } = Path.Combine(Path.GetTempPath(), "tgysm-test-files-" + Guid.NewGuid().ToString("N"));

    /// <summary>High by default so tests that log in many times never hit the rate limit.</summary>
    protected virtual int LoginPermitLimitPerMinute => GenerousLoginLimit;

    public async ValueTask InitializeAsync() => await Database.InitializeAsync();

    public Task ResetAsync() => Database.ResetAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
        if (Directory.Exists(FilesRoot))
        {
            Directory.Delete(FilesRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", Database.ConnectionString);
        builder.UseSetting("Files:RootPath", FilesRoot);
        builder.UseSetting("Security:LoginPermitLimitPerMinute", LoginPermitLimitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
