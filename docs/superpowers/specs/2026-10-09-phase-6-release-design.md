# Phase 6 — First Release — Design

- Date: 2026-10-09
- Owner: Tugay İLTUŞ
- Requirements: `tgysm.md` §19 (Phase 6 row), §20 (first-release acceptance criteria 1–20), §21 (open decisions)
- Builds on: Phases 1–5 and the CoreUI UI/UX overhaul on `master` (commit `523075f`)
- Branch: `phase-6-release`
- Out of scope: Kubernetes, several application servers, WAL archiving / point-in-time recovery, an in-app feedback form, new product features.

## 1. Agreed understanding

**Goal.** Make TGYSM releasable: a reproducible Docker Compose deployment with HTTPS, persisted secrets and nightly backups; a proven restore; evidence for every §20 acceptance criterion; an end-user guide with screenshots and a PDF; an operator manual; pilot tooling (seed data, performance measurement, first-data setup guide); CI and dependency updates. Close the three minors deferred from the CoreUI review.

**Owner decisions (2026-10-09)**

| Topic | Decision |
|---|---|
| Hosting | Docker Compose (one host; the same files run on an on-premises server or a cloud VM). |
| Backup | Nightly `pg_dump` (custom format) plus the file store and the Data Protection keys in one `tar.gz`, kept 14 days; manual backup and restore scripts. |
| HTTPS | Company certificate and key mounted from a folder into nginx; a self-signed pair can be generated for a test host. |
| Pilot | First-data setup guide, pilot seed loader, performance measurement, and a documented way for pilot users to report problems. |
| Guide screenshots | Taken automatically by Playwright from seeded data, mobile and desktop. |
| Deferred CoreUI minors | All three are in scope. |
| Dependency updates | Dependabot. |
| CI | GitHub Actions: lint, tests, E2E and Docker image build on every push and pull request. |

**Kept unchanged.** Architecture (ADR-0001), API contracts, authorization, business rules, UI. The API keeps serving the SPA itself (`UseStaticFiles` + `MapFallbackToFile`).

**Success criteria.** `tools/deploy.sh` brings up a working HTTPS stack from a clean host with only `.env` and the certificate supplied; `tools/restore-drill.sh` restores a backup into an empty stack and passes all its checks; `docs/release/acceptance.md` names passing evidence for each of the 20 criteria at one commit; the user guide and `combined.pdf` (A4) cover every user-facing feature area; CI is green on the release commit.

## 2. Deployment (Docker Compose)

### 2.1 Images

One multi-stage `Dockerfile` at the repository root:

- `frontend-build` (Node 22): `npm ci`, `npm run build`.
- `dotnet-build` (.NET 10 SDK): `dotnet publish` of `Tgysm.Api` and `Tgysm.Worker` in Release.
- Target `api` (ASP.NET runtime image): published API plus `frontend/dist` copied into the API's `wwwroot`; runs as a non-root user; listens on HTTP `8080` inside the network.
- Target `worker` (.NET runtime image): published Worker; non-root.

No secrets are baked into images. `.dockerignore` keeps `bin/`, `obj/`, `node_modules/`, `tmp/`, test results and `.env` out of the build context.

### 2.2 Services (`deploy/compose.yaml`)

| Service | Image | Role |
|---|---|---|
| `postgres` | `postgres:17` | Database; volume `pgdata`; not published to the host |
| `migrate` | `api` | One-shot `dotnet Tgysm.Api.dll migrate`; `api` and `worker` start only after it completes successfully |
| `api` | `api` | API + SPA; healthcheck `GET /api/v1/health` |
| `worker` | `worker` | Background jobs |
| `nginx` | `nginx` (stable) | TLS termination on 443, HTTP 80 redirects to HTTPS, proxies to `api:8080`; certificate and key from `deploy/certs/` (mounted read-only) |
| `backup` | `postgres:17` + small shell loop | Nightly backup (§3) |

Shared volumes: `files` (`Files:RootPath=/data/files`, API and Worker), `keys` (Data Protection, API and Worker), `backups` (backup output; also bind-mountable to a host folder).

### 2.3 Application changes

- **`migrate` CLI command** in `Tgysm.Api/Cli`, next to `setup-admin` and `reset-password`: applies pending EF Core migrations (`Database.MigrateAsync`) and exits 0, or non-zero with the error logged. `setup-admin` and `reset-password` run the same way through `docker compose run --rm api …`.
- **Data Protection key persistence:** a configuration key `DataProtection:KeysPath`; when set, `PersistKeysToFileSystem` on that folder (shared by API and Worker). Required outside Development: startup fails with a clear message when it is empty, like `Files:RootPath`. Closes the "Known gap" in `SECURITY.md`.
- **Forwarded headers:** outside Development the API trusts `X-Forwarded-For` and `X-Forwarded-Proto` only from the networks listed in `Http:KnownProxyNetworks` (CIDR list; the compose file sets the internal Docker network). Login rate limiting then sees the real client address.
- **Production settings** come from environment variables in `deploy/.env` (never committed; `deploy/.env.example` documents every key): connection string, `App:PublicBaseUrl`, `App:TimeZone`, `Email:Mode=Smtp` and `Email:Smtp:*`, `Files:RootPath`, `DataProtection:KeysPath`, `Http:KnownProxyNetworks`, PostgreSQL password.
- Config validation tests cover the new keys (empty keys path outside Development fails; a bad CIDR fails).

### 2.4 Scripts

- `tools/deploy.sh`: runs `tools/build.sh` (lint + tests) unless `--skip-gates` is given explicitly, builds the images, then `docker compose up -d` (migrate first, then the rest), then waits for the health check through nginx.
- `tools/compose.sh`: thin wrapper that always passes `-f deploy/compose.yaml --env-file deploy/.env` (used by the other scripts and the operator manual).
- `tools/gen-self-signed-cert.sh`: writes a test certificate pair into `deploy/certs/` (never committed; `deploy/certs/.gitkeep` only).

## 3. Backup and restore

- **Nightly backup** (`backup` service): at a configured local time (`BACKUP_TIME`, default `02:30`), `pg_dump -Fc`, plus `tar` of `/data/files` and `/data/keys`, into `tgysm-YYYYMMDD-HHMMSS.tar.gz` with a `manifest.json` (app commit, migration id, table row counts, file count). Archives older than `BACKUP_RETENTION_DAYS` (default 14) are deleted. A failed run exits non-zero and is visible in the service log.
- **`tools/backup.sh`**: the same backup on demand (runs the backup service's script once).
- **`tools/restore.sh <archive>`**: restores into a stack whose database is empty: refuses when the target database has user tables unless `--force` is given; `pg_restore`, then files and keys, then starts `api`/`worker`.
- **`tools/restore-drill.sh`** (acceptance criterion 19): creates an isolated compose project (own name, volumes and port), restores the latest archive, then checks: health OK; table row counts equal the manifest; a known attachment and a known export file download with matching size; a session cookie issued by the source stack before the backup is still valid on the restored stack (keys restored); a deactivated user cannot sign in; finally removes the drill project. Prints a pass/fail table and writes it to `docs/release/restore-drill.md`.

## 4. Acceptance evidence (§20)

`docs/release/acceptance.md` has one row per criterion 1–20: criterion, evidence (test class/method, E2E spec, script output, or document), result, commit. Every criterion must point to evidence that ran green on the release commit.

Gaps found by a first pass must get tests before the table is signed off. Expected gaps (to be confirmed against the code at the start of the task):

- (5) one integration test that an IT user cannot reach another team's ticket through list search, report figures, drill-down rows and Excel export.
- (12) one integration test that a pending outbox row and an email delivery left by a stopped worker are processed after a new worker instance starts.
- (15) a frontend test that every `t()` key used in source exists in `tr/*.json` (if the existing coverage tests do not already guarantee it), plus Turkish characters in CSV, Excel and email output.
- (16) keyboard-only checks for one form flow per role in E2E (login is already covered).

Criterion 18 is answered by §5.2 and criterion 19 by §3; criterion 20 by the documentation tasks.

## 5. Pilot

### 5.1 Seed loader

`tools/seed-pilot.sh` runs a new CLI command `seed-pilot` that refuses unless `ASPNETCORE_ENVIRONMENT` is `Development` or `Pilot` and the database has no tickets. It creates, through the application handlers (so rules and audit apply): departments, a location tree, an intake team and a second team, IT staff, about 50 employees, approvers, categories, request types, a device catalog, about 500 assets with assignments, about 2,000 tickets over the past 90 days in all statuses with comments, published and internal articles. Generated users get a known pilot password printed once. Volumes are named constants.

### 5.2 Performance measurement

`tools/perf-measure.sh` (against a seeded stack) times, with several runs each and median/95th percentile: ticket list and search, workspace queue, asset list and search, each report, a drill-down page, synchronous Excel export, CSV preview and commit of 5,000 rows. Results go to `docs/release/performance.md` with the data volume and machine description. No numeric targets (criterion 18); obvious outliers are reported to the owner, not fixed silently.

### 5.3 First-data guide and feedback

User-guide pages "İlk kurulum" (admin: departments, locations, intake team, approvers, categories, request types, SLA calendar and policy, device catalog, CSV import, first users) and "Sorun bildirme ve geri bildirim" (pilot users report problems through "Bir şey çalışmıyor" in a dedicated "TGYSM Pilot" category that the seed and the setup guide create).

## 6. Documentation

### 6.1 End-user guide (`docs/user-guides/`, Turkish, product UI language)

Pages, one per feature area: `getting-started.md` (sign-in, first password, layout and menu, breadcrumbs), `incidents-and-requests.md`, `my-tickets.md`, `my-assets.md`, `knowledge-base.md`, `it-workspace.md` (queues, claiming, work actions, replies and notes, linking assets and articles), `inventory.md` (assets, lifecycle, CSV import), `reports.md`, `approvals.md`, `admin-users-and-organization.md`, `admin-catalogs.md` (device and service catalog), `admin-sla.md`, `admin-email-and-system.md`, `first-setup.md`, `feedback.md`, `troubleshooting.md`. `README.md` is the index, `PROGRESS.md` tracks status and screenshots.

- `tools/guide-screenshots.sh`: starts the E2E-style stack with seeded data, runs a Playwright script that visits the pages listed in a manifest at mobile (390×844) and desktop (1280×800) and writes `docs/user-guides/images/<page>-<view>.png`. Every referenced image exists and every image is referenced (checked by the guide build).
- `tools/build-user-guide.sh`: concatenates pages in `README.md` order into `combined.md` (cross-page and localhost links rewritten to in-document anchors), renders `combined.pdf` in A4, checks the anchors, and verifies the page size with `pdfinfo`.

### 6.2 Operator manual

`docs/operations.md` (English, for the operator): host requirements, first install (`.env`, certificate, `tools/deploy.sh`, `setup-admin` through compose), updates and rollback (image tags, migrate), backup location and retention, restore and the drill, logs, health and `/admin/system`, rotating the SMTP password, where the keys live and why losing them breaks pending account links.

### 6.3 Project docs

`README.md`, `capabilities.md` (deployment, backup, CLI commands, baseline), `SECURITY.md` (keys gap closed, proxy trust, Dependabot, container users, secrets in `.env`), `CLAUDE.md` (new tools and current state). ADR-0024 (Docker Compose deployment and Data Protection key storage) and ADR-0025 (backup and restore) with index rows.

## 7. CI and dependency updates

- `.github/workflows/ci.yml`: on push and pull request: checkout, .NET 10 and Node 22 set-up with caches, PostgreSQL 17 service container, `tools/lint.sh`, `tools/test.sh`, Playwright browsers, `tools/e2e.sh`, `docker build` of both targets (no push). Test and E2E artifacts uploaded on failure. Scripts read the database host and user from the existing `TGYSM_DB_*` / `TGYSM_TEST_ADMIN_CONNECTION` variables; any script that assumes Homebrew or macOS is made portable (no behavior change locally).
- `.github/dependabot.yml`: weekly updates for `nuget` (`/backend`), `npm` (`/frontend`), `docker` (`/`), `github-actions` (`/`); grouped minor/patch updates per ecosystem.

## 8. Deferred CoreUI minors

- **Catalog normalization:** a migration recomputes `brands.normalized_name` and `asset_models.normalized_name` with `CatalogName.Normalize` in C# (data step through the application, not SQL `lower()`), so results no longer depend on the database `lc_ctype`. Integration test: a name with `İ`/`I`/`Ç` normalizes the same way as the domain rule. The v4 ids created by the earlier migration stay (valid uuids; documented).
- **Incident device link:** creating an incident with `assetId` records the `AssetLinked` timeline event and the `TicketAssetLinked` audit entry, as `Links.cs` does. Integration test.
- **Breadcrumb overflow menu:** max width of the viewport minus the gutter and wrapping items. E2E at 320 px with a 120-character middle crumb opened.

## 9. Error handling

- `migrate` failure stops the stack from starting `api`/`worker` (compose dependency on successful completion); the log names the failing migration.
- Missing `DataProtection:KeysPath`, `Files:RootPath` or `App:PublicBaseUrl` outside Development fails startup with the key name.
- Backup failure: non-zero exit, logged, previous archives untouched; the retention step runs only after a successful backup.
- Restore refuses a non-empty target without `--force`; a checksum or manifest mismatch stops before data is written.

## 10. Testing

- Unit/integration tests for the `migrate` and `seed-pilot` commands (refusal rules), configuration validation, forwarded-header trust, key persistence (two contexts sharing a folder unprotect each other's payload), and §4/§8 tests.
- Script tests: `tools/restore-drill.sh` is itself the restore test; `tools/deploy.sh` is exercised on the local Docker host during the task that adds it.
- All existing gates stay: `tools/build.sh`, `tools/e2e.sh`; CI runs them.

## 11. Delivery

One branch `phase-6-release`; tasks committed after their gates; ADRs with their commits; docs updated at the end per the project rule; merge to `master` and push only with the owner's approval. Docker must be available on the development machine for the deployment, backup and restore tasks.
