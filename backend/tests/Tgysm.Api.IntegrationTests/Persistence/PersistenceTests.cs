using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class PersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string PasswordHash = "hash";
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await database.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User NewUser(string email) =>
        User.Create("Ali", "Yılmaz", email, UserRole.Employee, null, null, PasswordHash, Now);

    [Fact]
    public async Task Duplicate_email_differing_only_in_case_is_rejected()
    {
        await using (var first = database.CreateDbContext())
        {
            first.Users.Add(NewUser("Ali@Example.com"));
            await first.SaveChangesAsync(Ct);
        }

        await using var second = database.CreateDbContext();
        second.Users.Add(NewUser("ali@example.com"));

        var error = await Assert.ThrowsAsync<DomainException>(() => second.SaveChangesAsync(Ct));
        Assert.Equal(ErrorCodes.Duplicate, error.Code);
    }

    [Fact]
    public async Task Audit_rows_cannot_be_updated_or_deleted()
    {
        await using var db = database.CreateDbContext();
        db.AuditEvents.Add(AuditEvent.Create(Now, null, AuditActions.UserCreated, AuditEntityTypes.User, Guid.NewGuid(), null, null, null, "test"));
        await db.SaveChangesAsync(Ct);

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync("UPDATE audit_events SET action = 'tampered'", Ct));
        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync("DELETE FROM audit_events", Ct));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, update.SqlState);
        Assert.Equal(PostgresErrorCodes.RestrictViolation, delete.SqlState);
    }

    [Fact]
    public async Task Stale_write_throws_concurrency_exception()
    {
        var user = NewUser("stale@example.com");
        await using (var seed = database.CreateDbContext())
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync(Ct);
        }

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();
        var firstCopy = await first.Users.SingleAsync(u => u.Id == user.Id, Ct);
        var secondCopy = await second.Users.SingleAsync(u => u.Id == user.Id, Ct);

        firstCopy.ChangeRole(UserRole.Admin, Now);
        await first.SaveChangesAsync(Ct);
        secondCopy.ChangeRole(UserRole.ItStaff, Now);

        await Assert.ThrowsAsync<ConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Expected_version_mismatch_throws_concurrency_exception()
    {
        await using var db = database.CreateDbContext();
        var user = NewUser("expected@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);

        db.SetExpectedVersion(user, user.Version + 1);
        user.ChangeRole(UserRole.Admin, Now);

        await Assert.ThrowsAsync<ConcurrencyException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Root_locations_with_same_name_are_rejected_but_siblings_under_other_parents_are_allowed()
    {
        await using var db = database.CreateDbContext();
        var campusA = Location.Create("Kampüs A", null);
        var campusB = Location.Create("Kampüs B", null);
        db.Locations.AddRange(campusA, campusB, Location.Create("Bina 1", campusA.Id), Location.Create("Bina 1", campusB.Id));
        await db.SaveChangesAsync(Ct);

        await using var other = database.CreateDbContext();
        other.Locations.Add(Location.Create("kampüs a", null));

        var error = await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct));
        Assert.Equal(ErrorCodes.Duplicate, error.Code);
    }
}
