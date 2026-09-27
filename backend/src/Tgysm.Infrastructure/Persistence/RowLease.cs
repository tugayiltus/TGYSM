using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>
/// Claims a batch of rows for exclusive processing, safe when several worker instances run the same query at
/// once (ADR-0014). The mapped type must have an <c>id</c> primary key column and a nullable <c>locked_until</c>
/// timestamptz column; <paramref name="sqlPredicate"/> is a raw SQL WHERE-clause fragment supplied by the caller
/// (a job's own constant, never external input) that narrows which rows are eligible.
/// </summary>
public static class RowLease
{
    private const string IdColumn = "id";
    private const string LockedUntilColumn = "locked_until";

    public static async Task<List<T>> ClaimAsync<T>(
        DbSet<T> set,
        string sqlPredicate,
        int batch,
        TimeSpan lease,
        CancellationToken cancellationToken)
        where T : class
    {
        var context = set.GetService<ICurrentDbContext>().Context;
        var table = set.EntityType.GetSchemaQualifiedTableName()
            ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped to a table.");
        var leaseSeconds = (int)lease.TotalSeconds;

        var sql = $@"
            UPDATE {table} SET {LockedUntilColumn} = now() + ({{0}} * interval '1 second')
            WHERE {IdColumn} IN (
                SELECT {IdColumn} FROM {table}
                WHERE {sqlPredicate}
                ORDER BY {IdColumn}
                LIMIT {{1}}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING {table}.*";

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await set.FromSqlRaw(sql, leaseSeconds, batch).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }
}
