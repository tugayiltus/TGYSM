# ADR-0025: Nightly Backup of Database, Files and Keys, with an Automated Restore Drill

**Date**: 2026-10-09
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The first release must prove that a backup of the database and the files can be restored on a new environment with relations and session security intact (`tgysm.md` §20 item 19; §21 left RPO, period and retention open). The owner chose a nightly `pg_dump` plus a file archive, kept 14 days, on 2026-10-09 (`docs/superpowers/specs/2026-10-09-phase-6-release-design.md` §3). Since ADR-0024 the Data Protection key ring lives in a volume; without it a restored server cannot read pending account-link secrets or existing session cookies. Related: ADR-0012 (file store), ADR-0020 (export files), ADR-0024 (deployment).

## Decision

- The `backup` service (image `postgres:17`) runs `deploy/backup/scheduler.sh`, which runs `deploy/backup/backup.sh` every night at `BACKUP_TIME` (default 02:30) in `TZ` (default `Europe/Istanbul`). `tools/backup.sh` runs one backup on demand.
- One archive `tgysm-YYYYMMDD-HHMMSS.tar.gz` (UTC) holds `db.dump` (`pg_dump -Fc`), the file store (`files/`), the key ring (`keys/`) and `manifest.json` (app version, newest migration, creation time, file count, exact row count of every table). The dump, the counts and the migration id come from one exported snapshot, so the manifest describes exactly what the dump holds. Archives are written under a temporary name, renamed when complete, and created with mode 600 (`umask 077`): they contain the keys.
- Retention (`BACKUP_RETENTION_DAYS`, default 14) deletes expired archives only after a successful backup; a failed run exits non-zero and leaves every earlier archive. Archives go to the volume `backups` or to the host folder `BACKUP_HOST_DIR`.
- `tools/restore.sh <archive> [--force]` stops the application, runs the profile service `restore` (`deploy/backup/restore.sh`), then `migrate`, then starts the stack. Before writing, it refuses a file that is not a TGYSM archive (exit 2), an archive whose newest migration is newer than the image knows (`migrate --print-latest`, exit 3), and a database that already has tables unless `--force` (exit 4); after such a refusal the services that were running are started again. After restoring it compares the row counts with the manifest (exit 5 on any difference).
- `tools/restore-drill.sh` proves the whole path on two throw-away compose projects (never the production stack): it seeds a source stack (attachment, ready background export, deactivated user, a signed-in employee), backs it up, checks both refusals, restores into an empty target and checks health, row counts, the attachment and export downloads, the employee's pre-backup session and the deactivated user's refused sign-in. It writes `docs/release/restore-drill.md`.

## Alternatives Considered

### Alternative 1: WAL archiving and point-in-time recovery
- **Pros**: recovery to any moment; smaller loss window.
- **Cons**: base backups plus continuous WAL shipping and a recovery procedure to learn and test; more disk.
- **Why not**: single-company scale; the owner chose nightly backups. Can be added later without changing the archive format.

### Alternative 2: Docker volume snapshots (host or cloud level)
- **Pros**: fast; no database tooling.
- **Cons**: depends on the host's storage; a snapshot of a running database volume is only crash-consistent; harder to check per backup.
- **Why not**: the archive is portable to any new host and its manifest makes every restore verifiable.

### Alternative 3: Separate archives for database and files
- **Pros**: smaller single files.
- **Cons**: two things to keep together; a restore can mix generations.
- **Why not**: one archive per backup is one consistent unit.

## Consequences

### Positive
- A restore is verified, not assumed: counts against the manifest on every restore, and the drill exercises sessions, files and keys end to end.
- Restores from a newer build or over live data are refused before anything is written.

### Negative
- Up to one day of data can be lost (RPO 24 h) unless an operator runs `tools/backup.sh` before risky work.
- Files written after the database snapshot may appear in the archive without a database row (harmless extra files); a file the cleanup job deletes during the backup can be missing.

### Risks
- Archives stay on the same host unless the operator copies them elsewhere (`docs/operations.md`); a lost host loses its backups.
- Archives contain the key ring and the full database: anyone who can read them can read all data and unprotect account-link tokens until they expire. Keep the backup folder access-restricted.

Implemented in commits of Phase 6 Tasks 6 and 7 (backup scripts and archive permissions; restore, drill and `migrate --print-latest`).

## Amendment 2026-10-10

Implementing commits: `bfc9892` (nightly and manual backups of database, files and keys), `c25a74c` (archives readable by their owner only) and `9c4c96b` (restore, restore drill and `migrate --print-latest`).
