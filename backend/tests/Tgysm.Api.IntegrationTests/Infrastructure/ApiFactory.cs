using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>API host on a throw-away PostgreSQL database.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestingEnvironment = "Testing";
    public const string PublicBaseUrl = "http://localhost:5173";
    private const int GenerousLoginLimit = 10_000;

    public PostgresDatabase Database { get; } = new();

    /// <summary>Attachment storage of this host; removed on dispose.</summary>
    public string FilesRoot { get; } = Path.Combine(Path.GetTempPath(), "tgysm-test-files-" + Guid.NewGuid().ToString("N"));

    /// <summary><c>Email:Mode=Pickup</c>'s <c>.eml</c> folder for this host; removed on dispose.</summary>
    public string EmailPickupFolder { get; } = Path.Combine(Path.GetTempPath(), "tgysm-test-email-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Replaces the real <c>IEmailSender</c> (Infrastructure) for every test in the collection, so
    /// <c>Every_application_handler_resolves</c> and every other test that never touches email keep working without
    /// a real transport, while <c>EmailSenderTests</c> can still program failures on it directly.
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>High by default so tests that log in many times never hit the rate limit.</summary>
    protected virtual int LoginPermitLimitPerMinute => GenerousLoginLimit;

    /// <summary>High by default for the same reason as <see cref="LoginPermitLimitPerMinute"/>.</summary>
    protected virtual int ForgotPasswordPermitLimitPerMinute => GenerousLoginLimit;

    /// <summary>High by default for the same reason as <see cref="LoginPermitLimitPerMinute"/>.</summary>
    protected virtual int SetPasswordPermitLimitPerMinute => GenerousLoginLimit;

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

        if (Directory.Exists(EmailPickupFolder))
        {
            Directory.Delete(EmailPickupFolder, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", Database.ConnectionString);
        builder.UseSetting("Files:RootPath", FilesRoot);
        builder.UseSetting("Security:LoginPermitLimitPerMinute", LoginPermitLimitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Security:ForgotPasswordPermitLimitPerMinute", ForgotPasswordPermitLimitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Security:SetPasswordPermitLimitPerMinute", SetPasswordPermitLimitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("App:PublicBaseUrl", PublicBaseUrl);
        // Mode/PickupFolder are set even though EmailSender below replaces IEmailSender outright: Every_application_
        // handler_resolves still resolves EmailOptions-dependent types, and a real PickupFolderEmailSender is
        // exercised directly (not through DI) by the pickup-mode .eml test.
        builder.UseSetting("Email:Mode", "Pickup");
        builder.UseSetting("Email:From", "TGYSM <no-reply@tgysm.local>");
        builder.UseSetting("Email:PickupFolder", EmailPickupFolder);
        builder.ConfigureTestServices(services => services.AddScoped<IEmailSender>(_ => EmailSender));
    }
}
