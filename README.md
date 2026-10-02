# TGYSM

TGYSM is a web-based ITSM platform for one company with several departments and locations. Employees report faults and request services; IT staff work team queues and manage IT assets; admins manage users, organization, approvals, SLA rules and reports. The UI is Turkish and mobile-first; code and URLs are English. Current state: Phases 1–4 done and merged to `master`; Phase 5 is in progress on branch `phase-5-knowledge-reports` — local login and the `/admin` area (users, organization, teams, categories, request types, audit); IT inventory with asset lifecycle, assignments and CSV import; incidents and requests with queues, assignment, approvals, comments, attachments and timeline. Phase 4: a worker host, a transactional outbox with ticket notification events, admin-managed email templates, email sending with retry (SMTP or pickup folder), an admin deliveries list with retry, password set links (account activation, reset, forgot-password), an SLA business calendar, versioned policy, per-ticket timers with warning and breach events, an SLA risk queue, and a worker status page with a `/api/v1/health` probe. Phase 5 so far: a knowledge base (articles with visibility and status, revisions, Turkish-aware search, ticket links, `/kb` pages) and the report API (incident, request, operations, transfers, asset and warranty reports, drill-down rows, synchronous Excel export). Still open in Phase 5: background export jobs and download, the reports UI, and Phase 5 E2E/review.

## Architecture

| Layer | Project / folder | Responsibility |
|---|---|---|
| Domain | `backend/src/Tgysm.Domain` | Entities, business rules, state transitions; no framework dependency |
| Application | `backend/src/Tgysm.Application` | Use-case handlers, validation, ports, audit recording |
| Infrastructure | `backend/src/Tgysm.Infrastructure` | EF Core + Npgsql, migrations, ticket number counter, password hashing, local attachment store, outbox claiming, email transports (MailKit SMTP, pickup folder), account-link protector |
| Worker | `backend/src/Tgysm.Worker` | Generic Host running `OutboxDispatcher`, `EmailSender`, `SlaMonitor` and `Cleanup` jobs on timers, row leases (`FOR UPDATE SKIP LOCKED`), heartbeats in `worker_heartbeats` (ADR-0014); references Application and Infrastructure, never Api |
| API | `backend/src/Tgysm.Api` | Minimal API endpoints under `/api/v1`, cookie auth, antiforgery, ProblemDetails, `setup-admin` and `reset-password` CLI |
| Web | `frontend/` | React SPA: tickets, IT workspace, inventory, approvals and `/admin` |

Solution: `backend/Tgysm.slnx`. Reports and exports: see ADR-0019 (knowledge base) and ADR-0020 (reports and Excel export). Notification flow: handlers write `notification_outbox` events in the same transaction as the change; the Worker dispatches them into `email_deliveries` and sends them (ADR-0015, ADR-0016). Browser and API are same-origin; Vite proxies `/api` in development (ADR-0003).

## Stack

- Backend: .NET 10, ASP.NET Core Minimal APIs, EF Core 10 + Npgsql 10, FluentValidation 12; `TreatWarningsAsErrors=true`, `Nullable=enable`.
- Database: PostgreSQL 17 with `pg_trgm` (ADR-0002).
- Email: MailKit 4.18 SMTP in production, pickup folder of `.eml` files in development and tests; selected by `Email:Mode`. Other keys: `App:PublicBaseUrl` (absolute URL for links in email), `App:TimeZone`, `Email:Smtp:TimeoutSeconds`, `Worker:Jobs:<Job>:*`. See `capabilities.md` (Configuration). The SMTP password comes from user-secrets or the environment only.
- Frontend: React 19, TypeScript ~5.9 (strict), Vite 8, Tailwind CSS v4, Radix UI, React Router, TanStack Query 5, react-hook-form + zod 4, i18next, `react-markdown` for article bodies (ADR-0008, ADR-0019). Build chunks include `vendor-markdown` and `feature-kb`.
- Excel export: ClosedXML 0.105 (ADR-0020).
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
| Plan task status from the SDD ledger, branch, recent commits, uncommitted changes | `tools/progress.sh [plan-file]` |

The launchd services run `tools/dev-api.sh`, `tools/dev-worker.sh` and `tools/dev-web.sh`; API and Worker go through `tools/dev-dotnet-host.sh`, which builds into `tmp/dev-artifacts/` and starts the last good build when the current build fails. Stop the services (`tools/dev-service.sh stop`) before `tools/e2e.sh`, which needs the same ports, and restart them afterwards.

Requires a running local PostgreSQL 17 (`brew services start postgresql@17`). Development in Chrome or Firefox (Secure cookies on `http://localhost`).

## Quality gates

- C#: zero warnings (`TreatWarningsAsErrors`), `dotnet format --verify-no-changes`.
- TypeScript/React: ESLint `--max-warnings=0`, no `eslint-disable`, `tsc -b` clean.
- Tests green; Domain + Application and frontend line coverage ≥ 80%.
- `TranslationCoverageTests` green (every error code and audit action has Turkish text).

## Documentation

- [`capabilities.md`](capabilities.md) — features and tech stack
- [`SECURITY.md`](SECURITY.md) — security policy
- [`docs/adr/`](docs/adr/README.md) — architecture decisions
- [`docs/user-guides/`](docs/user-guides/README.md) — end-user guide
- [`CLAUDE.md`](CLAUDE.md) — agent working notes
- [`tgysm.md`](tgysm.md) — product analysis and roadmap
