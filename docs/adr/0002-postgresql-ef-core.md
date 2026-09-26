# ADR-0002: PostgreSQL with EF Core and Npgsql

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The owner selected PostgreSQL (`tgysm.md` §1). Business rules need relational constraints (unique email, unique asset number, one active assignment per asset), transactions and optimistic concurrency. Local development has no Docker; PostgreSQL 17 runs from Homebrew.

## Decision

- EF Core 10 with `Npgsql.EntityFrameworkCore.PostgreSQL` 10 and `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`).
- Primary keys are `uuid` v7 generated in the domain (`Guid.CreateVersion7()`), so ids exist before insert and sort by time.
- Timestamps are `timestamptz` in UTC (`DateTimeOffset`).
- Optimistic concurrency uses PostgreSQL `xmin` (`uint Version` mapped with `IsRowVersion()`). A conflict surfaces as HTTP 409 `concurrency.conflict`.
- Business uniqueness is enforced by unique indexes. Names compare case-insensitively via a `normalized_name` column written with the `tr-TR` culture.
- `audit_events` is append-only: a trigger rejects `UPDATE` and `DELETE`.
- Migrations are versioned in `Tgysm.Infrastructure/Persistence/Migrations` and applied with `tools/migrate.sh`.
- Integration tests run against a real local PostgreSQL 17: each test run creates and drops a `tgysm_test_<random>` database.

## Alternatives Considered

### Alternative 1: Dapper with hand-written SQL
- **Pros**: full SQL control, no ORM overhead.
- **Cons**: manual mapping, manual migrations, manual change tracking and concurrency.
- **Why not**: CRUD-heavy admin areas and migrations are cheaper with EF Core.

### Alternative 2: Testcontainers for integration tests
- **Pros**: isolated, reproducible database per run.
- **Cons**: requires Docker, which is not installed on the development machine.
- **Why not**: the owner chose local PostgreSQL 17. The test fixture reads `TGYSM_TEST_ADMIN_CONNECTION`, so a container can be plugged in later without code changes.

## Consequences

### Positive
- Constraints and concurrency are enforced by the database, not only by code.
- Tests exercise real PostgreSQL behavior (triggers, unique indexes, `xmin`).

### Negative
- Tests need a running local PostgreSQL.

### Risks
- Turkish sort order depends on the database collation. Revisit with an ICU collation when list sorting matters to users.

## Amendment 2026-09-26

- `users` uses an application-managed `version` column instead of `xmin` (commit 8b3357d). Sign-in bookkeeping and a user's own password change update the row often and made admins' open edits stale; the domain now bumps `version` only on admin-editable changes (profile, role, activation). Other tables keep `xmin`.
- Rules that span rows run in one transaction under a PostgreSQL advisory lock (`IAppDbContext.RunSerializedAsync`, keys in `SerializationLocks`): last active admin and location cycles (commit 8b3357d). Plain optimistic concurrency cannot see a write to a different row.
- The failed-login counter is updated with a single `UPDATE … SET access_failed_count = access_failed_count + 1` so parallel attempts cannot lose counts.
- `audit_events` also rejects `TRUNCATE` (statement trigger) and has an index on `(actor_user_id, occurred_at)` (commit 512a3e5).
