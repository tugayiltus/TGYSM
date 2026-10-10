# TGYSM

TGYSM is a web-based ITSM platform for one company with several departments and locations. Employees report faults and request services; IT staff work team queues and manage IT assets; admins manage users, organization, approvals, SLA rules and reports. The UI is Turkish and mobile-first; code and URLs are English. Current state: Phases 1–5 done and merged to `master` — local login and the `/admin` area (users, organization, teams, categories, request types, audit); IT inventory with asset lifecycle, assignments and CSV import; incidents and requests with queues, assignment, approvals, comments, attachments and timeline. Phase 4: a worker host, a transactional outbox with ticket notification events, admin-managed email templates, email sending with retry (SMTP or pickup folder), an admin deliveries list with retry, password set links (account activation, reset, forgot-password), an SLA business calendar, versioned policy, per-ticket timers with warning and breach events, an SLA risk queue, and a worker status page with a `/api/v1/health` probe. Phase 5: a knowledge base (articles with visibility and status, revisions, Turkish-aware search, ticket links, `/kb` pages, "Bilgi Bankası" menu entry) and reports (incident, request, operations, transfers, asset and warranty reports with filters, drill-down lists, synchronous Excel export up to 5,000 rows, background export jobs up to 100,000 rows with a "Dışa aktarımlarım" download page, `/reports` pages and a "Raporlar" menu entry for IT staff and admins). UI/UX overhaul: CoreUI shell with left sidebar, breadcrumbs, device catalog and generated `DMR-` asset tags (ADR-0021–0023), then the CoreUI template visual system with light, dark and auto color modes (ADR-0026). Phase 6 (first release): Docker Compose deployment behind nginx with TLS (ADR-0024), nightly backups with restore and an automated restore drill (ADR-0025), acceptance evidence (`docs/release/acceptance.md`), pilot seed loader and performance measurement, GitHub Actions CI and Dependabot, the end-user guide with screenshots and PDF, and the operator manual [`docs/operations.md`](docs/operations.md). Served as one HTTPS site per company host.

## Architecture

| Layer | Project / folder | Responsibility |
|---|---|---|
| Domain | `backend/src/Tgysm.Domain` | Entities, business rules, state transitions; no framework dependency |
| Application | `backend/src/Tgysm.Application` | Use-case handlers, validation, ports, audit recording |
| Infrastructure | `backend/src/Tgysm.Infrastructure` | EF Core + Npgsql, migrations, ticket number counter, password hashing, local file store (attachments and export files), outbox and export job claiming, email transports (MailKit SMTP, pickup folder), account-link protector, Excel writer (ClosedXML) |
| Worker | `backend/src/Tgysm.Worker` | Generic Host running `OutboxDispatcher`, `EmailSender`, `SlaMonitor`, `ExportRunner` (background Excel exports) and `Cleanup` jobs on timers, row leases (`FOR UPDATE SKIP LOCKED`), heartbeats in `worker_heartbeats` (ADR-0014); references Application and Infrastructure, never Api |
| API | `backend/src/Tgysm.Api` | Minimal API endpoints under `/api/v1`, cookie auth, antiforgery, ProblemDetails, serves the built SPA from `wwwroot`; CLI commands `setup-admin`, `reset-password`, `migrate` (migrations + post-migration data steps; `--print-latest`) and `seed-pilot` (`backend/src/Tgysm.Api/Cli/`) |
| Web | `frontend/` | React SPA on CoreUI (left sidebar, breadcrumbs, light/dark/auto color modes): tickets, IT workspace, inventory, approvals, knowledge base, reports and `/admin` (including the device catalog) |
| Deployment | `Dockerfile`, `deploy/` | Images `tgysm-api` and `tgysm-worker`; `deploy/compose.yaml` with `postgres`, one-shot `migrate`, `api`, `worker`, `nginx` (TLS), `backup` and the `restore` profile; `deploy/nginx/`, `deploy/backup/` (ADR-0024, ADR-0025) |
| CI | `.github/` | GitHub Actions workflow `ci.yml` (lint, tests, E2E, both image builds on every push and pull request); Dependabot `dependabot.yml` |

Solution: `backend/Tgysm.slnx`. Device catalog and generated asset tags: ADR-0023. Knowledge base: ADR-0019. Reports and exports: ADR-0020 — small exports stream from the API; larger ones become `export_jobs` rows that the Worker's `ExportRunner` builds into the file store and the API serves for download. Notification flow: handlers write `notification_outbox` events in the same transaction as the change; the Worker dispatches them into `email_deliveries` and sends them (ADR-0015, ADR-0016). Browser and API are same-origin; Vite proxies `/api` in development (ADR-0003). In production nginx terminates TLS and proxies to the API, which trusts forwarded headers only from `Http:KnownProxyNetworks` (ADR-0024).

## Stack

- Backend: .NET 10, ASP.NET Core Minimal APIs, EF Core 10 + Npgsql 10, FluentValidation 12; `TreatWarningsAsErrors=true`, `Nullable=enable`.
- Database: PostgreSQL 17 with `pg_trgm` (ADR-0002).
- Email: MailKit 4.18 SMTP in production, pickup folder of `.eml` files in development and tests; selected by `Email:Mode`. Other keys: `App:PublicBaseUrl` (absolute URL for links in email), `App:TimeZone`, `Email:Smtp:TimeoutSeconds`, `Worker:Jobs:<Job>:*`. See `capabilities.md` (Configuration). The SMTP password comes from user-secrets or the environment only.
- Frontend: React 19, TypeScript ~5.9 (strict), Vite 8, CoreUI 5 (`@coreui/react` + `@coreui/coreui` SCSS, left sidebar layout, ADR-0021; template visual system, `@coreui/icons`, light/dark/auto color modes, ADR-0026), React Router, TanStack Query 5, react-hook-form + zod 4, i18next, `react-markdown` 10 for article bodies (ADR-0008, ADR-0019), Chart.js 4 through `@coreui/react-chartjs` for report charts (ADR-0020, ADR-0022).
- Build chunks (`frontend/vite.config.ts`, Rolldown `codeSplitting`, warning limit 500 KB): `vendor-react`, `vendor-router`, `vendor-tanstack`, `vendor-coreui-icons`, `vendor-coreui`, `vendor-forms`, `vendor-markdown`, `vendor-charts`, `vendor-other`, `app-shared`, `feature-common`, `feature-admin`, `feature-assets`, `feature-kb`, `feature-reports`, `feature-tickets`. The chart component loads lazily, so `vendor-charts` loads only with the first chart; admin pages and the home page are lazy routes.
- Excel export: ClosedXML 0.105 (ADR-0020).
- File store: `Files:RootPath` (API and Worker). Outside Development it is required (startup fails without it) and both hosts must point at the same absolute folder, because the Worker writes export files that the API serves; both log the resolved root at startup. Empty in Development means the user's local application data folder.
- Data Protection: key ring in `DataProtection:KeysPath`, shared by API and Worker; required outside Development (ADR-0024).
- Deployment: Docker Compose, `postgres:17`, `nginx:stable`, images on `mcr.microsoft.com/dotnet/aspnet:10.0` running as non-root `app`; settings from `deploy/.env` (template `deploy/.env.example`). Operator manual: `docs/operations.md`.
- CI: GitHub Actions (`.github/workflows/ci.yml`), Dependabot weekly for NuGet, npm, Docker and GitHub Actions (`.github/dependabot.yml`).
- Tests: xUnit v3 on Microsoft.Testing.Platform (`global.json`; run `dotnet test --solution backend/Tgysm.slnx`), NetArchTest, coverlet.MTP, Vitest + React Testing Library (jsdom 29), Playwright.

## Commands

| Task | Command |
|---|---|
| Prepare local database | `tools/db-up.sh` |
| Apply migrations | `tools/migrate.sh` |
| Create first admin | `tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>` |
| Issue a temporary password | `tools/reset-password.sh --email <email>` |
| Add a migration | `tools/add-migration.sh <Name>` |
| Run API + Worker + web in this terminal | `tools/run.sh` (API `http://localhost:5080`, web `http://localhost:5173`; Worker writes `.eml` files to `tmp/email-pickup`); refuses to start while the launchd dev services run |
| Keep API + Worker + web running in the background (macOS launchd user agents, start at login, restart on exit; logs in `tmp/logs/`) | `tools/dev-service.sh install\|uninstall\|start\|stop\|restart\|status\|logs` |
| Lint | `tools/lint.sh` |
| Tests (+ coverage gates) | `tools/test.sh` |
| E2E (stop `tools/run.sh` or the dev services first; it uses the same ports; also starts the Worker, with `Email:Mode=Pickup` into a per-run temp folder and 2 s job intervals, E2E only) | `tools/e2e.sh` |
| Regenerate API types | `tools/gen-api-types.sh` |
| Build | `tools/build.sh` |
| Lint the GitHub Actions workflows (`actionlint`) | `tools/ci-lint.sh` |
| Check that every `tools/`… and `deploy/`… path named in the docs exists | `tools/check-doc-paths.sh [file ...]` |
| Pilot seed data into a local `tgysm_pilot` database (`--fresh` drops and recreates it, `--drop` removes it) | `tools/seed-pilot.sh [--fresh\|--drop] [--scale <factor>]` |
| Performance measurement at pilot volume (writes `docs/release/performance.md`) | `PERF_PASSWORD=<pilot password> tools/perf-measure.sh` |
| User-guide screenshots from the pilot database | `GUIDE_PASSWORD=<pilot password> tools/guide-screenshots.sh [id ...]` |
| User guide `combined.md` + A4 `combined.pdf` | `tools/build-user-guide.sh` |
| Start local PostgreSQL for one command | `tools/with-db.sh <command>` |
| Plan task status from the SDD ledger, branch, recent commits, uncommitted changes | `tools/progress.sh [plan-file]` |

The launchd services run `tools/dev-api.sh`, `tools/dev-worker.sh` and `tools/dev-web.sh`; API and Worker go through `tools/dev-dotnet-host.sh`, which builds into `tmp/dev-artifacts/` and starts the last good build when the current build fails. Stop the services (`tools/dev-service.sh stop`) before `tools/e2e.sh`, which needs the same ports, and restart them afterwards.

### Deployment (Docker)

Full procedure, backups, restore, rollback and key handling: [`docs/operations.md`](docs/operations.md).

| Task | Command |
|---|---|
| Build images `tgysm-api`, `tgysm-worker` (tags: Git short hash, `latest`) | `tools/docker-build.sh` |
| Deploy: gates, images, `compose up`, wait for health through nginx | `tools/deploy.sh [--skip-gates]` |
| Any compose command on the stack (`deploy/compose.yaml` + `deploy/.env`) | `tools/compose.sh <args>` |
| Self-signed test certificate in `deploy/certs/` | `tools/gen-self-signed-cert.sh [host-name]` |
| Backup now | `tools/backup.sh` |
| Restore an archive | `tools/restore.sh <archive.tar.gz> [--force]` |
| Restore drill on throw-away stacks (writes `docs/release/restore-drill.md`) | `tools/restore-drill.sh` |

On macOS, Docker runs in Colima; Colima shares only the home folder with containers.

Requires a running local PostgreSQL 17 (`brew services start postgresql@17`). Development in Chrome or Firefox (Secure cookies on `http://localhost`).

## Quality gates

- C#: zero warnings (`TreatWarningsAsErrors`), `dotnet format --verify-no-changes`.
- TypeScript/React: ESLint `--max-warnings=0`, no `eslint-disable`, `tsc -b` clean.
- Tests green; Domain + Application and frontend line coverage ≥ 80%.
- `TranslationCoverageTests` green (every error code and audit action has Turkish text).
- CI (`.github/workflows/ci.yml`) runs `tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh` and both image builds on every push and pull request; a dependency bump merges only with a green run.

## Documentation

- [`capabilities.md`](capabilities.md) — features and tech stack
- [`SECURITY.md`](SECURITY.md) — security policy
- [`docs/adr/`](docs/adr/README.md) — architecture decisions
- [`docs/operations.md`](docs/operations.md) — operator manual (install, update, rollback, backup, restore, keys)
- [`docs/release/`](docs/release/acceptance.md) — acceptance evidence, `performance.md`, `restore-drill.md`
- [`docs/user-guides/`](docs/user-guides/README.md) — end-user guide (Turkish, `combined.pdf`)
- [`CLAUDE.md`](CLAUDE.md) — agent working notes
- [`tgysm.md`](tgysm.md) — product analysis and roadmap
