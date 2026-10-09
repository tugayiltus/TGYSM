# Phase 6 — First Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Ruling (plan format, carried over from Phase 2–5 and the CoreUI plan, owner-approved):** tasks list files, interfaces and tests instead of full inline code; code is written once, under TDD, from the spec. Each task: write the listed failing tests, watch them fail, implement, run the task command, record the result in the ledger.

**Goal:** Make TGYSM releasable: Docker Compose deployment with HTTPS, persisted Data Protection keys and nightly backups; a proven restore; evidence for all 20 acceptance criteria of `tgysm.md` §20; pilot seed and performance measurement; end-user guide with screenshots and A4 PDF; operator manual; CI and Dependabot; the three CoreUI review minors.

**Architecture:** One multi-stage `Dockerfile` builds an `api` image (API + SPA in `wwwroot`) and a `worker` image. `deploy/compose.yaml` runs `postgres`, a one-shot `migrate` (new API CLI command), `api`, `worker`, `nginx` (TLS from mounted company certificate) and `backup` (nightly `pg_dump` + files + keys). Shell scripts in `tools/` wrap every operation; restore is proven by `tools/restore-drill.sh` in an isolated compose project.

**Tech Stack:** .NET 10, EF Core + Npgsql, PostgreSQL 17; React 19 + Vite 8; Docker (Colima on the dev Mac), Docker Compose v2, nginx stable, `postgres:17`; Playwright; GitHub Actions; Dependabot; `md-to-pdf` or headless Chrome for the guide PDF.

**Spec:** `docs/superpowers/specs/2026-10-09-phase-6-release-design.md` (requirements `tgysm.md` §19–§21).

## Global Constraints

- All earlier constraints apply: English code/URLs/DB, Turkish UI text only in `frontend/src/shared/i18n/locales/tr/*.json`, zero warnings, no `eslint-disable`, no `NoWarn`/`#pragma`, no magic literals (named constants, shell `readonly` variables), every repeatable operation as a `tools/` script (`#!/usr/bin/env bash`, `set -euo pipefail`, `cd` to repo root), TDD, stage named paths only, no AI attribution anywhere, no push and no merge without owner approval.
- Keep unchanged: architecture (ADR-0001), API contracts (only new CLI commands, no new HTTP endpoints), authorization, business rules, UI (except the breadcrumb overflow fix).
- Secrets never in the repository: `deploy/.env` and `deploy/certs/*` (except `.gitkeep`) are git-ignored; `deploy/.env.example` documents every key with a placeholder.
- Images run as a non-root user; no secret in any image layer; `.dockerignore` excludes `bin/`, `obj/`, `node_modules/`, `tmp/`, `test-results/`, `e2e-logs/`, `deploy/.env`, `deploy/certs/`.
- Outside Development, startup fails with the key name when `Files:RootPath`, `DataProtection:KeysPath` or `App:PublicBaseUrl` is empty.
- Backup archive name `tgysm-YYYYMMDD-HHMMSS.tar.gz` with `manifest.json`; retention default 14 days (`BACKUP_RETENTION_DAYS`), time default `02:30` (`BACKUP_TIME`) in `TZ` (default `Europe/Istanbul`); retention runs only after a successful backup.
- `seed-pilot` refuses unless the environment is `Development` or `Pilot` and the database has no tickets.
- User guide in Turkish (product UI language), PDF always A4, built only through `tools/build-user-guide.sh`.
- Colima runs only while a Docker task runs (`colima start` / `colima stop`); PostgreSQL and the dev services follow the dev-services rule (restart after builds/E2E).
- Each task ends with `tools/lint.sh` and `tools/test.sh` exit 0; tasks touching the UI also `tools/e2e.sh`; Docker tasks also their own smoke command. Commit per task after green gates.
- ADR-0024 (Compose deployment + Data Protection keys) lands with Task 5; ADR-0025 (backup and restore) with Task 7.

## Review Focus

- A fresh host where the `keys`/`files` volumes are owned by root while the containers run as non-root must not start half-working: API and Worker fail at startup with a clear "cannot write <path>" message (Task 2 test + Task 5 smoke on fresh volumes).
- Restoring an archive taken by an older app version into a newer stack must end on the newer schema, and an archive from a newer version must be refused before data is written (Task 7: manifest migration id check, then `migrate` after restore).
- A failed nightly backup (database unreachable, disk full) must keep every earlier archive and exit non-zero (Task 6 test: forced `pg_dump` failure leaves the previous archive and does not run retention).
- A request reaching the API with a spoofed `X-Forwarded-For` from outside the trusted proxy network must not change the address the login rate limiter sees (Task 3 test).
- Running `seed-pilot` against a database that already has real tickets, or in `Production`, must refuse without writing anything (Task 12 test).

---

### Task 1: `migrate` CLI command

**Files:**
- Create: `backend/src/Tgysm.Api/Cli/MigrateCommand.cs`.
- Modify: `backend/src/Tgysm.Api/Program.cs` (dispatch like `setup-admin`).
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Setup/MigrateCommandTests.cs`.

**Interfaces:**
- Produces: `MigrateCommand.Name = "migrate"`; `MigrateCommand.RunAsync(IServiceProvider, TextWriter output, TextWriter error) : Task<int>` — applies pending migrations through `AppDbContext.Database.MigrateAsync`, prints `Applied <n> migration(s); latest <id>` or `Database is up to date (<id>)`, returns 0; on exception prints the message and returns 1.

- [ ] Step 1: Failing tests: on an empty database created by the test (own name, dropped afterwards) the command returns 0 and `__EFMigrationsHistory` holds every migration of the assembly; a second run returns 0 and prints "up to date"; with a connection string to a non-existent host it returns 1 and writes the error.
- [ ] Step 2: Run `dotnet test --solution backend/Tgysm.slnx --filter-class '*MigrateCommandTests'` (through `tools/with-db.sh` when PostgreSQL is stopped) — expect FAIL (type missing).
- [ ] Step 3: Implement; Program dispatch `if (args is [MigrateCommand.Name]) return await MigrateCommand.RunAsync(app.Services, Console.Out, Console.Error);`.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `feat: add migrate command to the API CLI`.

### Task 2: Persisted Data Protection keys and production configuration checks

**Files:**
- Create: `backend/src/Tgysm.Infrastructure/Security/DataProtectionOptions.cs` (section `DataProtection`, `KeysPath`), `backend/src/Tgysm.Infrastructure/Security/DataProtectionSetup.cs`.
- Modify: `backend/src/Tgysm.Infrastructure/DependencyInjection.cs` (replace bare `AddDataProtection()`), `backend/src/Tgysm.Api/appsettings.json`, `backend/src/Tgysm.Worker/appsettings.json` (`"DataProtection": { "KeysPath": "" }`), startup check next to `FileStoreRootLog` (API and Worker).
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Security/DataProtectionKeysTests.cs`, `backend/tests/Tgysm.Application.Tests/` or integration config tests next to the existing `Files:RootPath` startup tests.

**Interfaces:**
- Produces: config key `DataProtection:KeysPath`; when set, keys persist to that folder (`PersistKeysToFileSystem`), application name stays `Tgysm`; outside Development an empty value fails startup with `DataProtection:KeysPath must be set outside Development…`; an unwritable folder fails startup with `Cannot write Data Protection keys to <path>`.

- [ ] Step 1: Failing tests: `Production` with empty `App:PublicBaseUrl` fails startup naming the key; two separate service providers configured with the same temp `KeysPath` — a payload protected by the first unprotects in the second (simulates API → Worker and restore); `Production` environment with empty `KeysPath` fails host start naming the key; read-only folder fails naming the path (Review Focus); `Development` with empty value still uses the default (existing tests unchanged).
- [ ] Step 2: Run the new test classes — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `feat: persist Data Protection keys to a shared folder outside Development`.

### Task 3: Trusted proxy networks for forwarded headers

**Files:**
- Create: `backend/src/Tgysm.Api/Http/ProxyTrustOptions.cs` (section `Http`, `KnownProxyNetworks` string array of CIDR).
- Modify: `backend/src/Tgysm.Api/ApiApplicationExtensions.cs:24-27` (outside Development: `XForwardedFor | XForwardedProto`, `KnownNetworks` from options, `KnownProxies` cleared; Development unchanged), `ApiServiceCollectionExtensions.cs` (options + validation), `appsettings.json` (`"Http": { "KnownProxyNetworks": [] }`).
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Hardening/ForwardedHeadersTests.cs`.

**Interfaces:**
- Produces: `Http:KnownProxyNetworks` (e.g. `172.16.0.0/12`); an invalid CIDR fails startup naming the value.

- [ ] Step 1: Failing tests (factory in `Production` with a temp files/keys path): request from a trusted network with `X-Forwarded-For: 203.0.113.7` and `X-Forwarded-Proto: https` → `HttpContext.Connection.RemoteIpAddress` seen by the login rate-limit partition is `203.0.113.7` (assert through a probe endpoint registered only in the test host, or by exhausting the limit for one forwarded address while another forwarded address still logs in); request from an untrusted address with a spoofed `X-Forwarded-For` → rate limit keyed on the real address (Review Focus); `KnownProxyNetworks = ["not-a-cidr"]` fails startup.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `feat: trust forwarded headers only from configured proxy networks`.

### Task 4: Container images

**Files:**
- Create: `Dockerfile` (stages `frontend-build` on `node:22-bookworm-slim`, `dotnet-build` on `mcr.microsoft.com/dotnet/sdk:10.0`, targets `api` on `mcr.microsoft.com/dotnet/aspnet:10.0` and `worker` on `mcr.microsoft.com/dotnet/runtime:10.0`; `USER app`; `ASPNETCORE_HTTP_PORTS=8080`; API copies `frontend/dist` into its `wwwroot`), `.dockerignore`, `tools/docker-build.sh` (builds both targets, tags `tgysm-api:<git short sha>` and `:latest`, same for worker).
- Modify: `.gitignore` (`deploy/.env`, `deploy/certs/*`, `!deploy/certs/.gitkeep`, `deploy/backups/`).
- Test: `tools/docker-build.sh` then a smoke run documented in the step (no unit test can cover an image).

**Interfaces:**
- Produces: images `tgysm-api` and `tgysm-worker`; API container serves `/` (SPA `index.html`) and `/api/v1/health` on 8080; `docker run tgysm-api migrate` works.

- [ ] Step 1: Failing check: `tools/docker-build.sh` (script missing → FAIL).
- [ ] Step 2: Write `Dockerfile`, `.dockerignore`, script. `colima start`.
- [ ] Step 3: Smoke: run `tgysm-api` with `ASPNETCORE_ENVIRONMENT=Development` against the host PostgreSQL (`host.docker.internal` / Colima host address) → `GET /` returns HTML with the app title, `GET /api/v1/health` 200; `docker run --rm tgysm-api id -u` is not 0; `docker history --no-trunc` shows no `.env` or connection string.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0. `colima stop`.
- [ ] Step 5: Commit `build: add Dockerfile for the API and Worker images`.

### Task 5: Compose stack, nginx TLS and deploy script

**Files:**
- Create: `deploy/compose.yaml`, `deploy/nginx/tgysm.conf` (80 → 301 HTTPS; 443 TLS 1.2+, `client_max_body_size` above the 10 MB upload limit with a named margin, proxy headers `X-Forwarded-For`/`-Proto`, `proxy_read_timeout` for exports), `deploy/.env.example`, `deploy/certs/.gitkeep`, `tools/compose.sh`, `tools/gen-self-signed-cert.sh`, `tools/deploy.sh`, `docs/adr/0024-docker-compose-deployment.md` (+ index row).
- Test: `tools/deploy.sh` smoke on Colima with a self-signed certificate.

**Interfaces:**
- Consumes: Task 1 `migrate`, Task 2 `DataProtection:KeysPath`, Task 3 `Http:KnownProxyNetworks`, Task 4 images.
- Produces: services `postgres`, `migrate`, `api`, `worker`, `nginx` (backup added in Task 6); volumes `pgdata`, `files`, `keys`, `backups`; compose network subnet fixed in the file and passed as `Http__KnownProxyNetworks__0`; `tools/compose.sh <args>` = `docker compose -f deploy/compose.yaml --env-file deploy/.env <args>`; `tools/deploy.sh [--skip-gates]`.

- [ ] Step 1: Failing check: `tools/deploy.sh` missing → FAIL.
- [ ] Step 2: Write files. Volumes get their owner fixed by a tiny init step (or `user` + pre-created folders in the image) so the non-root containers can write `files` and `keys` on a fresh host (Review Focus).
- [ ] Step 3: Smoke on Colima with fresh volumes: `tools/gen-self-signed-cert.sh`, a test `deploy/.env` (from the example), `tools/deploy.sh --skip-gates` → exit 0; `curl -k https://localhost/api/v1/health` 200; `http://localhost` redirects to HTTPS; `tools/compose.sh run --rm api setup-admin …` prints a temporary password; sign in through HTTPS in Playwright (one quick script) and upload an attachment; restart `worker` and `api` — the user stays signed in (keys persisted); `tools/compose.sh down` (keep volumes), `up` again — still signed in.
- [ ] Step 4: Write ADR-0024 (Compose, images, SPA served by the API, keys on a shared volume, proxy trust; alternatives: Windows/IIS, nginx serving the SPA, single container). `tools/lint.sh`, `tools/test.sh` — exit 0. `colima stop`.
- [ ] Step 5: Commit `feat: add Docker Compose deployment with nginx TLS and deploy script`.

### Task 6: Nightly backup and manual backup

**Files:**
- Create: `deploy/backup/backup.sh` (one run: `pg_dump -Fc`, `tar` of `/data/files` and `/data/keys`, `manifest.json` with app commit (`APP_VERSION` env), latest migration id, row counts of every user table, file count; writes `tgysm-<UTC timestamp>.tar.gz` atomically via a temp name; then retention), `deploy/backup/scheduler.sh` (loop: sleep until next `BACKUP_TIME` in `TZ`, run `backup.sh`, log result), `tools/backup.sh` (runs `backup.sh` once in the `backup` service), `tools/tests/backup-test.sh` (shell test harness).
- Modify: `deploy/compose.yaml` (service `backup` on `postgres:17`, mounts `files`/`keys` read-only and `backups`), `deploy/.env.example` (`BACKUP_TIME`, `BACKUP_RETENTION_DAYS`, `TZ`).

**Interfaces:**
- Produces: archive layout `db.dump`, `files/…`, `keys/…`, `manifest.json` (`appVersion`, `latestMigration`, `createdAt`, `tables: {name: rows}`, `fileCount`); exit 0 on success, non-zero on any failure.

- [ ] Step 1: Failing shell tests (`tools/tests/backup-test.sh`, run against the Compose stack from Task 5): archive created with all four parts and a valid manifest; an archive older than retention is deleted only after a successful run; with the database stopped the run exits non-zero, writes no new archive, deletes nothing (Review Focus); scheduler next-run calculation for `02:30` in `Europe/Istanbul` (pure function in `scheduler.sh`, tested with fixed `now` values incl. after 02:30).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Tests pass; `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `feat: add nightly and manual backups of database, files and keys`.

### Task 7: Restore and restore drill

**Files:**
- Create: `deploy/backup/restore.sh` (inside the backup image: verify archive and manifest, refuse non-empty target database unless `--force`, refuse when `latestMigration` is newer than the image's newest migration, `pg_restore`, restore files and keys), `tools/restore.sh <archive> [--force]` (stops `api`/`worker`, runs restore, runs `migrate`, starts the stack), `tools/restore-drill.sh`, `docs/release/restore-drill.md` (written by the drill), `docs/adr/0025-backup-and-restore.md` (+ index row).

**Interfaces:**
- Consumes: Task 6 archive format, Task 1 `migrate`.
- Produces: `tools/restore-drill.sh` exit 0 only when every check passes; report table (check, expected, actual, result).

- [ ] Step 1: Failing check: `tools/restore-drill.sh` missing → FAIL.
- [ ] Step 2: Implement drill: on the source stack create data (admin, employee, ticket with attachment, a ready background export, a deactivated user), sign in as the employee and keep the cookie, run `tools/backup.sh`; create project `tgysm-drill` (own volumes, port 8443), restore the archive, then check: health OK; row counts equal the manifest; attachment and export download with the same size; the kept cookie still reads `/api/v1/auth/me` on the drill stack; the deactivated user's sign-in is refused; an archive whose manifest names a future migration is refused before writing (Review Focus); restoring an archive into a non-empty drill database without `--force` is refused; tear down the drill project.
- [ ] Step 3: Run the drill on Colima — exit 0; report written.
- [ ] Step 4: Write ADR-0025 (pg_dump + files + keys, nightly, 14 days, restore checks; alternatives: WAL/PITR, volume snapshots). `tools/lint.sh`, `tools/test.sh` — exit 0. `colima stop`.
- [ ] Step 5: Commit `feat: add restore and an automated restore drill`.

### Task 8: Catalog name normalization independent of database locale

**Files:**
- Create: `backend/src/Tgysm.Infrastructure/Persistence/PostMigration/{IPostMigrationStep,PostMigrationRunner,RecomputeCatalogNamesStep}.cs`, migration `MaintenanceSteps` (via `tools/add-migration.sh MaintenanceSteps`: table `maintenance_steps`).
- Modify: `MigrateCommand.cs` (runs `PostMigrationRunner` after `MigrateAsync`), `tools/migrate.sh` (builds and runs the API `migrate` command instead of `dotnet ef database update`, so dev and E2E databases get the post-migration steps too), the integration-test database setup in `ApiFactory` (runs the runner after migrating; reset truncates `maintenance_steps` only where a test needs it).
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Assets/CatalogNormalizationTests.cs`.

**Interfaces:**
- Produces: `IPostMigrationStep { string Id; Task RunAsync(AppDbContext, CancellationToken) }`, `PostMigrationRunner.RunPendingAsync(AppDbContext, CancellationToken) : Task<IReadOnlyList<string>>` (ids it ran), `RecomputeCatalogNamesStep` (`Id = "2026-10-recompute-catalog-names"`; recomputes `brands.normalized_name` and `asset_models.normalized_name` with `CatalogName.Normalize`); table `maintenance_steps(id text primary key, completed_at timestamptz)`.
- Ruling: spec §8 says "a migration recomputes … in C#"; an EF migration cannot call domain code, so the step runs right after migrations inside `migrate` and is recorded once — same effect, through the application.

- [ ] Step 1: Failing tests: a brand row written with a SQL-lowercased key that differs from `CatalogName.Normalize` (`"ÇELİK"` stored as `"Çeli̇k"`-style ASCII-only lowercase) is corrected by `migrate`; a second `migrate` does not run the step again; two brands whose corrected keys collide → `migrate` fails naming both names (no silent merge).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `fix: recompute catalog name keys with the domain rule after migrations`.

### Task 9: Incident device link writes timeline and audit

**Files:**
- Modify: `backend/src/Tgysm.Application/Features/Tickets/CreateTicket.cs:101-108`.
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Tickets/IncidentAssetTests.cs`.

**Interfaces:**
- Consumes: the event/audit shape used in `Links.cs` (`TicketEventKind.AssetLinked`, `AuditActions.TicketAssetLinked`).

- [ ] Step 1: Failing test: creating an incident with `assetId` produces one `AssetLinked` timeline event (actor = requester) and one `TicketAssetLinked` audit row in the same transaction; unlinking afterwards shows both events in order.
- [ ] Step 2: Run `--filter-class '*IncidentAssetTests'` — expect FAIL.
- [ ] Step 3: Implement (reuse the `Links.cs` recording helper; extract it if needed).
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `fix: record the device link of a new incident in its timeline and audit`.

### Task 10: Breadcrumb overflow menu stays on screen

**Files:**
- Modify: `frontend/src/shared/navigation/Breadcrumbs.tsx`, `frontend/src/styles/_layout.scss`.
- Test: `frontend/e2e/navigation.spec.ts` (new test).

- [ ] Step 1: Failing E2E (mobile-360 plus a 320 px check): an article with a 120-character title reached through a path with a collapsed middle crumb; open the overflow menu; `expectNoHorizontalScroll` and the menu item text is fully visible (wraps).
- [ ] Step 2: `E2E_PROJECTS=mobile-360 tools/e2e.sh e2e/navigation.spec.ts` — expect FAIL.
- [ ] Step 3: `max-width: calc(100vw - 2 * var(--tgysm-gutter))` on the menu, `white-space: normal` and `overflow-wrap: anywhere` on items (named SCSS variables).
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh` — exit 0.
- [ ] Step 5: Commit `fix: keep the breadcrumb overflow menu inside narrow screens`.

### Task 11: Acceptance evidence and gap tests

**Files:**
- Create: `docs/release/acceptance.md` (criteria 1–20: criterion text in Turkish as in `tgysm.md`, evidence, result, commit), gap tests as found, expected:
  - `backend/tests/Tgysm.Api.IntegrationTests/Reports/CrossTeamLeakTests.cs` (criterion 5),
  - `backend/tests/Tgysm.Api.IntegrationTests/Notifications/WorkerRestartTests.cs` (criterion 12),
  - `frontend/src/shared/i18n/usedKeys.test.ts` (criterion 15, only if `TranslationCoverageTests` and existing i18n tests do not cover keys used in source),
  - `backend/tests/Tgysm.Api.IntegrationTests/Reports/TurkishCharactersExportTests.cs` (criterion 15: `ç ğ ı İ ö ş ü` round-trip in CSV import, Excel export, email body),
  - `frontend/e2e/keyboard.spec.ts` (criterion 16: keyboard-only new incident as employee, claim + reply as IT, create user as admin).

**Interfaces:**
- Produces: a table every later task and the final sign-off update; no product behavior change unless a gap test fails for a real defect (then fix under TDD and ledger it).

- [ ] Step 1: Map each criterion to existing tests by searching the test suites (record test class/method names). List the gaps.
- [ ] Step 2: Write the gap tests; run them. A gap test that passes at once is coverage that was missing (ledger that it passed first time and why it is still meaningful: name the production change that would make it fail, and prove it by temporarily breaking that code — e.g. remove the team predicate in `ReportScope` — then restore).
- [ ] Step 3: Fix any real defect found, test-first.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh` — exit 0. Fill the table (criteria 18, 19, 20 marked "Task 13", "Task 7", "Task 17").
- [ ] Step 5: Commit `test: acceptance evidence for the first release`.

### Task 12: Pilot seed loader

**Files:**
- Create: `backend/src/Tgysm.Api/Cli/SeedPilotCommand.cs`, `backend/src/Tgysm.Application/Features/Pilot/PilotSeed.cs` (+ `PilotSeedVolumes` constants: employees 50, IT staff 8, assets 500, tickets 2,000, days back 90, articles 20), `tools/seed-pilot.sh` (local: runs the API dll; compose: `tools/compose.sh run --rm -e ASPNETCORE_ENVIRONMENT=Pilot api seed-pilot`).
- Modify: `Program.cs` dispatch; `docs/user-guides` later reference the "TGYSM Pilot" category it creates.
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Setup/SeedPilotCommandTests.cs`.

**Interfaces:**
- Produces: `seed-pilot [--scale <factor>]` (factor scales volumes, default 1; tests use a small factor); creates data only through application handlers, an admin first if none exists; prints the pilot password once; creates the "TGYSM Pilot" incident category.

- [ ] Step 1: Failing tests: refuses in `Production` (exit 1, nothing written); refuses when any ticket exists (Review Focus); with scale 0.02 creates the expected counts, every asset has a model, every ticket has an SLA instance, tickets spread over all statuses, audit rows exist; deterministic with a fixed seed (`Random(PilotSeed.RandomSeed)`).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0; run `tools/seed-pilot.sh` once on a fresh `tgysm_pilot` database locally (full scale) and record time and counts in the ledger.
- [ ] Step 5: Commit `feat: add pilot seed loader for development and pilot environments`.

### Task 13: Performance measurement

**Files:**
- Create: `tools/perf-measure.sh` (starts the stack on a seeded `tgysm_pilot` database or targets `PERF_BASE_URL`), `frontend/perf/measure.ts` (Playwright `request` context: signs in as IT and admin, calls each endpoint `PERF_RUNS` times, records median and p95), `docs/release/performance.md` (written by the script: data volume, machine, Git commit, table).

**Interfaces:**
- Consumes: Task 12 seed.
- Produces: measurements for ticket list + search, workspace queue, asset list + search, all six reports, one drill-down page, synchronous Excel export, CSV preview + commit of 5,000 rows (generated file with catalog names from the seed).

- [ ] Step 1: Failing check: script missing → FAIL.
- [ ] Step 2: Implement; run; the script fails when any call returns non-2xx.
- [ ] Step 3: Review numbers; anything over 2 s median goes to the owner in the final message (no silent tuning).
- [ ] Step 4: `tools/lint.sh` (perf TS included in lint/typecheck) — exit 0.
- [ ] Step 5: Commit `test: measure list, report and import timings at pilot volume`.

### Task 14: CI and Dependabot

**Files:**
- Create: `.github/workflows/ci.yml`, `.github/dependabot.yml`.
- Modify: scripts that assume Homebrew/macOS (`tools/with-db.sh`, `tools/db-up.sh`, `tools/e2e.sh`, `tools/test.sh` as needed): when `CI=true`, skip `brew services` and use `TGYSM_DB_HOST`/`TGYSM_DB_USER`/`PGPASSWORD`; behavior on the Mac unchanged.

**Interfaces:**
- Produces: CI job `build` (ubuntu-latest; `postgres:17` service with user `tgysm`; .NET 10 + Node 22 with caches; `tools/lint.sh`; `tools/test.sh`; `npx playwright install --with-deps chromium`; `tools/e2e.sh`; `docker build --target api` and `--target worker`; upload `frontend/test-results`, `frontend/e2e-logs` on failure).

- [ ] Step 1: Failing check: `act` is not used; instead validate locally with `actionlint` (brew) — workflow missing → FAIL.
- [ ] Step 2: Write workflow and Dependabot config; make scripts CI-aware.
- [ ] Step 3: Local scripts still green on the Mac (`tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh`). CI itself runs only after the owner approves a push; the first green CI run is recorded in the final sign-off.
- [ ] Step 4: `actionlint` exit 0.
- [ ] Step 5: Commit `ci: add GitHub Actions build and Dependabot updates`.

### Task 15: Guide tooling (screenshots and combined PDF)

**Files:**
- Create: `tools/guide-screenshots.sh`, `frontend/guide/screenshots.ts` (Playwright script driven by `frontend/guide/manifest.ts`: id, role, path, optional setup action, views `mobile` 390×844 and `desktop` 1280×800), `tools/build-user-guide.sh` (order from `docs/user-guides/README.md`; rewrite cross-page and `http://localhost…` links to anchors; render HTML; check every `](#…)` link has an `id`; every referenced image exists and every image in `images/` is referenced; render `combined.pdf` with `@page { size: A4; }`; `pdfinfo` must report ≈595×842 pts).

**Interfaces:**
- Consumes: Task 12 seed (screenshots run on a seeded E2E-style database).
- Produces: `docs/user-guides/images/<id>-<view>.png`; `combined.md`; `combined.pdf`.

- [ ] Step 1: Failing check: scripts missing → FAIL.
- [ ] Step 2: Implement; manifest starts with the pages of Task 16.
- [ ] Step 3: Dry run on the current two pages: build succeeds, PDF is A4.
- [ ] Step 4: `tools/lint.sh` — exit 0.
- [ ] Step 5: Commit `docs: add user-guide screenshot and PDF build tools`.

### Task 16: User guide pages (Turkish)

**Files:**
- Create: `docs/user-guides/{incidents-and-requests,my-tickets,my-assets,knowledge-base,it-workspace,inventory,reports,approvals,admin-users-and-organization,admin-catalogs,admin-sla,admin-email-and-system,first-setup,feedback}.md`, images via Task 15.
- Modify: `docs/user-guides/{README,getting-started,troubleshooting,PROGRESS}.md`, `frontend/guide/manifest.ts`.

- [ ] Step 1: Write each page task by task for the end user (what you want to do → steps with the exact Turkish UI labels → what you see → tips), with screenshots; `first-setup.md` covers the admin's first-data order; `feedback.md` explains reporting pilot problems through "Bir şey çalışmıyor" in category "TGYSM Pilot".
- [ ] Step 2: `tools/guide-screenshots.sh`, `tools/build-user-guide.sh` — exit 0; `pdfinfo` A4; anchor check clean.
- [ ] Step 3: Every UI label in the guide exists in `tr/*.json` (a small check in the build script: quoted labels in `**…**` must be found in the locale files).
- [ ] Step 4: Update `PROGRESS.md` (all pages done, screenshots done).
- [ ] Step 5: Commit `docs: write the end-user guide with screenshots and PDF`.

### Task 17: Operator manual and project docs

**Files:**
- Create: `docs/operations.md`.
- Modify: `README.md`, `capabilities.md` (deployment, backup/restore, CLI commands `migrate`/`seed-pilot`, configuration keys, baseline marker), `SECURITY.md` (keys gap closed, proxy trust, non-root containers, `.env` secrets, Dependabot replaces the TODO), `CLAUDE.md` (new tools, current state), `docs/adr/README.md` (rows 0024, 0025).

- [ ] Step 1: Write `docs/operations.md`: host requirements; first install (Docker, `.env` from example, certificate files, `tools/deploy.sh`, first admin through `tools/compose.sh run --rm api setup-admin …`); updates (pull, `tools/deploy.sh`, rollback to the previous image tag and when a rollback needs a restore); backups (location, retention, copy off the host); restore and the drill; logs (`tools/compose.sh logs`), health, `/admin/system`; SMTP password rotation; keys and why losing them breaks pending account links.
- [ ] Step 2: Update project docs per `~/.claude/guides/docs-updates.md` (owner asked for docs as part of the phase).
- [ ] Step 3: Docs reference only files and commands that exist (grep check of every `tools/…` path mentioned).
- [ ] Step 4: `tools/lint.sh` — exit 0.
- [ ] Step 5: Commit `docs: operator manual and release documentation`.

### Task 18: Release sign-off

**Files:**
- Modify: `docs/release/acceptance.md` (final commit hash, all rows green), `docs/release/restore-drill.md` (re-run on the release commit).

- [ ] Step 1: On the final commit: `tools/build.sh`, `tools/e2e.sh`, `tools/deploy.sh` on Colima, `tools/restore-drill.sh` — all exit 0.
- [ ] Step 2: Fill the acceptance table with the commit; every row has passing evidence.
- [ ] Step 3: Final whole-branch review (fresh reviewer, most capable model); fix Critical/Important test-first; minors to the ledger.
- [ ] Step 4: Ask the owner (AskUserQuestion) to approve merge to `master` and push (CI then runs on GitHub; record its result).
- [ ] Step 5: Commit `docs: first-release sign-off`; after approval merge, push, restart dev services, `colima stop`.
