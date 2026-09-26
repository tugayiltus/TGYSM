using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.Cli;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Setup;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Setup;

[Collection(ApiCollection.Name)]
public sealed class SetupFirstAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CliSuccess = 0;
    private const int CliFailure = 1;
    private static readonly SetupFirstAdminCommand Command = new("admin@example.com", "Tugay", "İltuş");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creates_admin_with_temporary_password_when_no_user_exists()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetupFirstAdminHandler>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var result = await handler.HandleAsync(Command, Ct);

        var admin = await db.Users.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(result.UserId, admin.Id);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
        Assert.Equal(TemporaryPasswordGenerator.Length, result.TemporaryPassword.Length);
        Assert.True(passwords.Verify(admin.PasswordHash, result.TemporaryPassword));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.FirstAdminCreated && a.EntityId == admin.Id, Ct));
    }

    [Fact]
    public async Task Refuses_when_any_user_exists()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, "existing@example.com");
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetupFirstAdminHandler>();

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(Command, Ct));

        Assert.Equal(ErrorCodes.SetupAlreadyCompleted, error.Code);
    }

    [Fact]
    public async Task Cli_prints_temporary_password_once_and_then_refuses()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] args = ["--email", "admin@example.com", "--first-name", "Tugay", "--last-name", "İltuş"];

        var first = await SetupAdminCommand.RunAsync(factory.Services, args, output, error);
        var second = await SetupAdminCommand.RunAsync(factory.Services, args, output, error);

        Assert.Equal(CliSuccess, first);
        Assert.Contains("Temporary password", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(CliFailure, second);
        Assert.Contains("already exists", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_prints_usage_when_options_are_missing()
    {
        var error = new StringWriter();

        var exitCode = await SetupAdminCommand.RunAsync(factory.Services, ["--email", "admin@example.com"], TextWriter.Null, error);

        Assert.Equal(CliFailure, exitCode);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }
}
