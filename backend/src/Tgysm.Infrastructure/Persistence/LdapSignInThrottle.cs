using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>One upsert per attempt, so parallel attempts cannot overwrite each other's count (ADR-0030).</summary>
public sealed class LdapSignInThrottle(AppDbContext db) : ILdapSignInThrottle
{
    public const string TableName = "ldap_sign_in_failures";

    public async Task<bool> TryAttemptAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var threshold = User.MaxFailedSignInAttempts;
        var lockoutEnd = now + User.LockoutDuration;
        // The row lock of the upsert serializes attempts on one key; a locked row is not updated and returns nothing.
        // An expired lockout starts a new count.
        var allowed = await db.Database.SqlQuery<string>(
                $"""
                INSERT INTO ldap_sign_in_failures (key, failed_count, lockout_end) VALUES ({key}, 1, NULL)
                ON CONFLICT (key) DO UPDATE SET
                    failed_count = CASE WHEN (CASE WHEN ldap_sign_in_failures.lockout_end IS NULL THEN ldap_sign_in_failures.failed_count ELSE 0 END) + 1 >= {threshold}
                        THEN 0 ELSE (CASE WHEN ldap_sign_in_failures.lockout_end IS NULL THEN ldap_sign_in_failures.failed_count ELSE 0 END) + 1 END,
                    lockout_end = CASE WHEN (CASE WHEN ldap_sign_in_failures.lockout_end IS NULL THEN ldap_sign_in_failures.failed_count ELSE 0 END) + 1 >= {threshold}
                        THEN {lockoutEnd} ELSE NULL END
                WHERE ldap_sign_in_failures.lockout_end IS NULL OR ldap_sign_in_failures.lockout_end <= {now}
                RETURNING key AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return allowed.Count > 0;
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken) =>
        db.LdapSignInFailures.Where(f => f.Key == key).ExecuteDeleteAsync(cancellationToken);
}
