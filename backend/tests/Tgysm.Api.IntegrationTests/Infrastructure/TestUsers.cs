using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

public static class TestUsers
{
    public const string DefaultPassword = "Test-password-2026";

    public static async Task<User> CreateAsync(
        ApiFactory factory,
        UserRole role,
        string email,
        bool mustChangePassword = false,
        Guid? departmentId = null,
        Guid? locationId = null,
        string firstName = "Test",
        string lastName = "User")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var now = DateTimeOffset.UtcNow;

        var user = User.Create(firstName, lastName, email, role, departmentId, locationId, passwords.Hash(DefaultPassword), now);
        if (!mustChangePassword)
        {
            user.ChangePassword(passwords.Hash(DefaultPassword), now);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }
}
