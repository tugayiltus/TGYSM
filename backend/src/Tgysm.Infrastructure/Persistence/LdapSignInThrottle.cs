using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>One upsert per failure, so parallel attempts cannot overwrite each other's count (ADR-0030).</summary>
public sealed class LdapSignInThrottle(AppDbContext db) : ILdapSignInThrottle
{
    public const string TableName = "ldap_sign_in_failures";

    public Task<bool> IsLockedAsync(string key, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.LdapSignInFailures.AsNoTracking().AnyAsync(f => f.Key == key && f.LockoutEnd != null && f.LockoutEnd > now, cancellationToken);

    public async Task RegisterFailureAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var threshold = User.MaxFailedSignInAttempts;
        var lockoutEnd = now + User.LockoutDuration;
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO ldap_sign_in_failures (key, failed_count, lockout_end) VALUES ({key}, 1, NULL)
            ON CONFLICT (key) DO UPDATE SET
                failed_count = CASE WHEN ldap_sign_in_failures.failed_count + 1 >= {threshold} THEN 0 ELSE ldap_sign_in_failures.failed_count + 1 END,
                lockout_end = CASE WHEN ldap_sign_in_failures.failed_count + 1 >= {threshold} THEN {lockoutEnd} ELSE ldap_sign_in_failures.lockout_end END
            """,
            cancellationToken);
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken) =>
        db.LdapSignInFailures.Where(f => f.Key == key).ExecuteDeleteAsync(cancellationToken);
}
