using Tgysm.Api.Cli;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Setup;

[Collection(ApiCollection.Name)]
public sealed class ResetPasswordCliTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CliSuccess = 0;
    private const int CliFailure = 1;
    private const string Prefix = "Temporary password (shown once): ";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Cli_issues_a_temporary_password_that_signs_in_and_forces_a_change()
    {
        await TestUsers.CreateAsync(factory, UserRole.Admin, "admin@example.com");
        var output = new StringWriter();

        var exitCode = await ResetPasswordCommand.RunAsync(factory.Services, ["--email", "ADMIN@example.com"], output, TextWriter.Null);
        var temporary = output.ToString().Split('\n').Single(l => l.StartsWith(Prefix, StringComparison.Ordinal))[Prefix.Length..].Trim();
        var client = ApiClient.Create(factory);
        using var login = await client.PostAsync(ApiClient.LoginPath, new { email = "admin@example.com", password = temporary });

        Assert.Equal(CliSuccess, exitCode);
        Assert.True(login.IsSuccessStatusCode);
        Assert.True((await client.ReadJsonAsync<System.Text.Json.JsonElement>(login)).GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Cli_refuses_unknown_email_and_missing_options()
    {
        var error = new StringWriter();

        var unknown = await ResetPasswordCommand.RunAsync(factory.Services, ["--email", "yok@example.com"], TextWriter.Null, error);
        var missing = await ResetPasswordCommand.RunAsync(factory.Services, [], TextWriter.Null, error);

        Assert.Equal(CliFailure, unknown);
        Assert.Equal(CliFailure, missing);
        Assert.Contains("No account", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }
}
