# TGYSM Capabilities

## Overview

ITSM platform for one company (multiple departments and locations). Users: employees, IT staff, admins. Served as one HTTPS web application per company host: a Docker Compose stack (PostgreSQL, API with the SPA, Worker, nginx with TLS, nightly backup) on one on-premises server or cloud VM (ADR-0024); operator procedures in `docs/operations.md`. Out of scope for the first release: multi-tenancy, SSO/LDAP, native mobile app, CMDB, change/problem management (`tgysm.md` §2).

Status legend: **Available** = implemented and tested; **Planned** = later phase (`tgysm.md` §19).

| Area | Status |
|---|---|
| Local login, forced password change, `/admin` users, departments, locations, teams, audit | Available (Phase 1) |
| IT inventory: assets, lifecycle, assignments, CSV import, "my assets" | Available (Phase 2) |
| Device catalog (admin-managed brands and models), server-generated `DMR-` asset tags, device choice on new incidents | Available (UI/UX overhaul) |
| CoreUI interface: left sidebar for the app and `/admin`, breadcrumbs with context return, URL-held list filters, report period presets, Chart.js charts | Available (UI/UX overhaul) |
| Incidents and requests: intake, queues, assignment, approvals, comments/files, timeline, related requests, ticket–asset links | Available (Phase 3) |
| Email (Phase 4): worker host, transactional outbox, ticket notification events, admin-managed templates, sending with retry (SMTP or pickup folder), deliveries list and retry, password set links with forgot-password, `/admin/email-templates` and `/admin/email-deliveries` pages | Available (Phase 4) |
| SLA (Phase 4): business calendar and holidays, versioned policy, SLA instances per ticket, monitor job with warning and breach events, `sla_risk` queue, SLA block on tickets, `/admin/sla/*` pages | Available (Phase 4) |
| `/admin/system` worker status page, cleanup job | Available (Phase 4) |
| Knowledge base: articles with visibility and status, revisions, Turkish-aware search, links to tickets, public-reply guard, `/kb*` pages, ticket article box and reply picker, "Bilgi Bankası" menu entry | Available (Phase 5) |
| Reports: incident, request, operations, transfers, asset and warranty reports, drill-down rows, synchronous Excel export up to 5,000 rows, background export jobs up to 100,000 rows with download, `/reports*` pages, "Raporlar" menu entry | Available (Phase 5) |
| CoreUI template visual system: shared page/list/form cards, dark sidebar with icons, sticky header with theme and account menus, light/dark/auto color modes, layout-stability and mobile-first guards | Available (UI cohesion, ADR-0026) |
| Release (Phase 6): Docker Compose deployment with nginx TLS, `migrate` CLI, persisted Data Protection keys, proxy trust, nightly backups, restore and restore drill, acceptance evidence, pilot seed and performance measurement, CI and Dependabot, end-user guide with PDF, operator manual | Available (Phase 6) |

## Tech stack

| Component | Choice | Decision |
|---|---|---|
| Backend | .NET 10 modular monolith, Clean Architecture (`Tgysm.Domain` → `Application` → `Infrastructure` → `Api`), Minimal APIs, FluentValidation 12 | ADR-0001 |
| Database / ORM | PostgreSQL 17, EF Core 10 + Npgsql 10, snake_case, `uuid` v7 keys; `xmin` concurrency, application-managed `version` on `users`; advisory locks for cross-row rules; `pg_trgm` for substring search | ADR-0002 |
| Web | React 19, TypeScript ~5.9 (strict), Vite 8, CoreUI 5 (`@coreui/react`, `@coreui/coreui` SCSS on Bootstrap 5, `@coreui/icons`, light/dark/auto color modes stored under `tgysm-theme` and applied before first paint), React Router, TanStack Query 5, react-hook-form + zod 4, i18next; types generated from OpenAPI | ADR-0003, ADR-0008, ADR-0021, ADR-0026 |
| Auth | Local accounts, PBKDF2 (`PasswordHasher<T>`), cookie session, antiforgery header, lockout, login rate limit | ADR-0004 |
| Roles | `employee`, `it_staff`, `admin`; one per user | ADR-0005 |
| Language | English code/URLs, Turkish UI via i18n | ADR-0006 |
| Operator CLI | `Tgysm.Api` commands: `setup-admin` (first admin, once), `reset-password` (temporary password from the console), `migrate` (EF Core migrations, then pending post-migration data steps; `--print-latest` prints the newest migration the build knows, without a database), `seed-pilot` (`--scale <factor>`, Development/Pilot only) | ADR-0007 |
| Tickets | One table for incidents and requests; numbers from a counter table | ADR-0009, ADR-0011 |
| Assets | Lifecycle state machine, one active assignment per asset; brand and model from an admin-managed catalog (`brands`, `asset_models`); tags `DMR-` + six digits from PostgreSQL sequence `asset_tag_seq` | ADR-0010, ADR-0023 |
| Attachments | Local disk store outside the web root with random keys, magic-byte checks, scanner port (development scanner marks type-checked files clean) | ADR-0012 |
| Ticket access | Employees own tickets, IT staff own + their teams', admins all; everything else 404 | ADR-0013 |
| Tests | xUnit v3 on Microsoft.Testing.Platform, NetArchTest, coverlet.MTP; Vitest + React Testing Library (jsdom 29); Playwright (360×800, 1280×800) | ADR-0008 |
| Background worker | `Tgysm.Worker` (.NET Generic Host, references `Application` and `Infrastructure`, never `Api`); one `BackgroundService` per job on a `PeriodicTimer`; batches claimed with `SELECT … FOR UPDATE SKIP LOCKED` row leases (`locked_until`), so several instances can run side by side; one DI scope per unit of work | ADR-0014 |
| Knowledge base | Articles with `employees`/`internal` visibility and `draft`/`published`/`archived` status, append-only revisions, search over a Turkish-folded `search_text` with a trigram GIN index, Markdown rendered in the browser with `react-markdown` without raw HTML | ADR-0019 |
| Reports | One `ReportFilter` and one query builder per report family; every metric's figure, drill-down rows and Excel export share one predicate; Excel files by ClosedXML 0.105; background exports in `export_jobs`, built by the Worker's `ExportRunner` job into the file store; charts by Chart.js 4 through `@coreui/react-chartjs` (lazy `DistributionChart`, own `vendor-charts` chunk) | ADR-0020, ADR-0022 |
| File store | `LocalFileStore` (`Files:RootPath`) holds attachments and export files; the API and the Worker must share one absolute folder | ADR-0012, ADR-0020 |
| Frontend chunks | Vite (Rolldown) `codeSplitting` groups: `vendor-react`, `vendor-router`, `vendor-tanstack`, `vendor-coreui-icons`, `vendor-coreui`, `vendor-forms`, `vendor-markdown`, `vendor-charts`, `vendor-other`, `app-shared`, `feature-common` (`kb/api.ts`, `assets/api.ts`, `admin/shared/`), `feature-admin`, `feature-assets`, `feature-kb`, `feature-reports` (without `DistributionChart`), `feature-tickets`; warning limit 500 KB. Admin pages, `AdminLayout` and the home page are lazy routes | ADR-0008 |
| Data Protection | `AddDataProtection()` with the shared application name `Tgysm`, so the Worker can read secrets the API protected; key ring persisted to `DataProtection:KeysPath` (file system, required outside Development, shared by API and Worker, backed up). Keys unencrypted at rest, folder 0700 owned by the non-root `app` user; certificate encryption not configured (`SECURITY.md`) | ADR-0004, ADR-0015, ADR-0024 |
| Deployment | One multi-stage `Dockerfile`: images `tgysm-api` (API + SPA in `wwwroot`) and `tgysm-worker`, both on `mcr.microsoft.com/dotnet/aspnet:10.0` (the Worker needs the ASP.NET runtime for Data Protection), non-root. `deploy/compose.yaml`: `postgres:17`, one-shot `migrate`, `api`, `worker`, `nginx:stable` (TLS 1.2/1.3, HTTP→HTTPS, resolves `api` through Docker DNS `127.0.0.11` so a recreated container is found), `backup`, profile `restore`. Settings from `deploy/.env` | ADR-0024 |
| Backup and restore | `pg_dump -Fc` + file store + key ring + `manifest.json` in one `tar.gz` per night, 14-day retention, restore with refusals and row-count check, automated restore drill | ADR-0025 |
| CI and dependencies | GitHub Actions `ci.yml` (PostgreSQL 17 service, `tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh`, both image builds; artifacts on failure), Dependabot weekly grouped minor/patch for NuGet, npm, Docker, GitHub Actions | — |
| Outbox and email | Transactional outbox (`notification_outbox`), dispatcher job, sender job with retry; `IEmailSender` implemented by MailKit 4.18 SMTP (production) and a pickup folder of `.eml` files (development, tests); at-least-once delivery | ADR-0015 |
| Email templates | Database rows per event type, plain text, fixed `{{variable}}` allow-list per event, Turkish defaults seeded by migration | ADR-0016 |
| SLA calculation | Domain `BusinessTimeCalculator` over `BusinessCalendarRules` (time zone, weekly working ranges, holidays); DST handled by converting local boundaries back to UTC. `SlaInstance` state machine per ticket and metric (`first_response`, `resolution`), versioned policy, `SlaMonitor` job, calendar changes re-plan running timers | ADR-0018 |
| Password set links | Random 32-byte tokens, only a hash is stored (`password_tokens`, purposes `activation` 72 h and `reset` 1 h, single use). Sent by email, redeemed at `POST /auth/set-password`; the rest of the account flow is under "Auth & permissions" | ADR-0017 |

## Configuration

Hosts: `Tgysm.Api` and `Tgysm.Worker`. Both fail at startup on an invalid `App` section and, outside Development, on an empty `Files:RootPath` or an empty or unwritable `DataProtection:KeysPath`; the API also fails on an invalid `Http:KnownProxyNetworks` entry; the Worker also fails at startup when `Email:Mode` is missing or unknown, or when the `EmailSender` lease is not longer than `Email:Smtp:TimeoutSeconds`.

| Key | Host | Meaning |
|---|---|---|
| `App:PublicBaseUrl` | API, Worker | Absolute `http`/`https` URL used to build links in email (`ticketUrl`). Empty or relative fails startup. Development: `http://localhost:5173`. |
| `App:TimeZone` | API, Worker | IANA zone for dates in email; default `Europe/Istanbul`. |
| `Email:Mode` | Worker | `Smtp` or `Pickup`. No default in `appsettings.json`; Development sets `Pickup`. |
| `Email:From` | Worker | Sender address, e.g. `TGYSM <no-reply@tgysm.local>`. |
| `Email:Smtp:Host`, `Port` (587), `UseStartTls` (true), `UserName` | Worker | SMTP transport settings. |
| `Email:Smtp:TimeoutSeconds` | Worker | Deadline for one whole SMTP send (default 30). The `EmailSender` lease (`Worker:Jobs:EmailSender:LeaseSeconds`) must be greater, else startup fails. |
| `Email:Smtp:Password` | Worker | Secret: user-secrets or environment only; never in `appsettings`, logs, audit or `last_error`. |
| `Email:PickupFolder` | Worker | Folder for `.eml` files in `Pickup` mode. `tools/run.sh` and `tools/e2e.sh` pass an absolute `tmp/email-pickup` path. |
| `Files:RootPath` | API, Worker | Folder for attachments and export files. Both hosts must point at the same absolute folder (the Worker writes export files the API serves); required outside Development, where startup fails without it. Empty in Development means the user's local application data folder (`tgysm/files`). Both hosts log the resolved root at startup (`File store root: …`). `tools/e2e.sh` sets a per-run temp folder. |
| `DataProtection:KeysPath` | API, Worker | Key ring folder, shared by both hosts (ADR-0024). Required outside Development; empty in Development keeps the framework's per-user default. Docker images set `/data/keys`. |
| `Http:KnownProxyNetworks` | API | CIDR list of reverse proxies whose `X-Forwarded-For` / `X-Forwarded-Proto` are applied outside Development (ADR-0024). Compose passes `TGYSM_SUBNET`. Development trusts only the loopback Vite proxy. |
| `Worker:Jobs:<Job>:IntervalSeconds`, `BatchSize`, `LeaseSeconds` | Worker | Per job. `OutboxDispatcher` and `EmailSender`: 30 s / 50 / 60 s. `SlaMonitor`: 60 s, batch 200. `Cleanup`: hourly (3600 s). `ExportRunner`: 30 s / 5 / 600 s (Development 5 s). A non-positive interval falls back to the job default and logs a warning. |
| `Worker:Jobs:<Job>:IntervalSeconds` | API | The API reads the same section (duplicated in its `appsettings.json`, default 60 s) to judge a job stale for `/health` and `/admin/system/workers`. |
| `Security:LoginPermitLimitPerMinute`, `ForgotPasswordPermitLimitPerMinute`, `SetPasswordPermitLimitPerMinute` | API | Per-client-IP limits per minute; defaults 10, 5, 5. `tools/e2e.sh` raises the login limit and shortens the two job intervals, and sets `App:PublicBaseUrl`, `Email:Mode=Pickup`, `Email:PickupFolder` for the run (E2E only). |

Deployment settings (`deploy/.env`, template `deploy/.env.example`, read by `tools/compose.sh`): `TGYSM_DEPLOYMENT` (unset on production; `pilot` only on a pilot or test host, required by `tools/seed-pilot.sh --compose`), `POSTGRES_PASSWORD`, `PUBLIC_BASE_URL` (→ `App:PublicBaseUrl`), `HTTP_PORT`/`HTTPS_PORT`, `TZ`, `APP_TIME_ZONE` (→ `App:TimeZone`), `TGYSM_SUBNET` (→ `Http:KnownProxyNetworks`), `EMAIL_MODE`, `EMAIL_FROM`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_STARTTLS`, `SMTP_USERNAME`, `SMTP_PASSWORD` (→ `Email:*`), `BACKUP_TIME`, `BACKUP_RETENTION_DAYS`, `BACKUP_HOST_DIR`. The image tag `TGYSM_VERSION` is not set in `deploy/.env`: `tools/deploy.sh` records the deployed Git short hash in `deploy/.version` (git-ignored) and `tools/compose.sh` exports it; a `TGYSM_VERSION` in the shell environment overrides the file. The images set `Files:RootPath=/data/files` and `DataProtection:KeysPath=/data/keys`; compose sets `Email:PickupFolder=/data/files/email-pickup`. Meaning of each: `docs/operations.md`.

## API

All routes are under `/api/v1`. Errors are RFC 7807 ProblemDetails with a stable `code` and an optional `errors` map (field → codes). Unknown `/api/*` routes return 404 `common.not_found`. Every unsafe request needs the `X-CSRF-TOKEN` header, multipart uploads included.

### Auth

| Method | Path | Behavior |
|---|---|---|
| GET | `/auth/csrf` | Anonymous. Returns `{ token }` and sets the antiforgery cookie. |
| POST | `/auth/login` | Anonymous, rate limited per IP (`Security:LoginPermitLimitPerMinute`, default 10). `{ email, password }` → `{ mustChangePassword }` and session cookie. Every failure: 401 `auth.invalid_credentials`. |
| POST | `/auth/logout` | Ends the session (204). |
| GET | `/auth/me` | Current user (`MeDto`). Allowed while a password change is pending. |
| POST | `/auth/forgot-password` | Anonymous, rate limited per IP. `{ email }` → always 202, same work for every address; the Worker resolves the account later. A reset link is sent only for an active account without an unused reset link younger than 15 minutes. |
| POST | `/auth/set-password` | Anonymous, rate limited per IP, one call per link. `{ token, newPassword }` → 204. Every invalid, used or expired token: 400 `auth.token_invalid`. Sets the password, clears the forced change, ends other sessions and voids the user's other unused links. |
| POST | `/auth/change-password` | `{ currentPassword, newPassword }` → 204; re-issues the cookie; other sessions end. |

### Admin (`admin` policy)

| Area | Endpoints |
|---|---|
| Users | `GET /admin/users` (`search`, `isActive`, `role`, `sort`, `page`, `pageSize` ≤ 100), `GET /admin/users/{id}`, `POST /admin/users` → 201 `{ userId, temporaryPassword }`, `PUT /admin/users/{id}`, `POST /admin/users/{id}/deactivate`, `/reactivate` (`{ version }`), `POST /admin/users/{id}/reset-password` → `{ userId, temporaryPassword }` |
| Departments | `GET/POST /admin/departments`, `PUT /admin/departments/{id}`, `POST …/{id}/deactivate`, `…/reactivate` |
| Locations | `GET /admin/locations` (flat list, max 2000), `POST`, `PUT /{id}`, `POST …/{id}/deactivate`, `…/reactivate` |
| Teams | `GET/POST /admin/teams`, `GET/PUT /admin/teams/{id}` (`isDefaultIntake` optional: `true` moves the intake flag here, `null` keeps it), `POST …/{id}/deactivate` (also clears the flag), `…/reactivate`, `POST …/{id}/members` `{ userId }`, `DELETE …/{id}/members/{userId}` |
| Categories | `GET/POST /admin/categories`, `PUT /{id}` (`name`, `parentId`, `scope` = `incident`/`request`/`both`), `POST …/{id}/deactivate`, `…/reactivate`; at most two levels (`categories.too_deep`) |
| Request types | `GET/POST /admin/request-types`, `PUT /{id}` (`name`, `requiresApproval`), `POST …/{id}/deactivate`, `…/reactivate` |
| Brands (ADR-0023) | `GET/POST /admin/brands` (`name`), `PUT /admin/brands/{id}` (`version`, `name`: rename also rewrites the brand name on every asset of the brand), `POST …/{id}/deactivate`, `…/reactivate`; never deleted; duplicate names after normalization 409 `assets.catalog_duplicate` |
| Models (ADR-0023) | `GET /admin/models` (`brandId`, `type`), `POST /admin/models` (`brandId`, `type`, `name`), `PUT /admin/models/{id}` (`version`, `name`: rewrites the model name on its assets), `POST …/{id}/deactivate`, `…/reactivate`; unique per brand and type |
| Audit | `GET /admin/audit-events` (`action`, `entityType`, `entityId`, `actorUserId`, `from`, `to`, `page`, `pageSize`), newest first |

Users: `POST /admin/users` and `POST /admin/users/{id}/reset-password` take `delivery` = `EmailLink` (default: an activation or reset link is emailed, no password is shown; `temporaryPassword` is null) or `TemporaryPassword` (shown once, forced change at next sign-in).

### Admin: email templates and deliveries (`admin` policy)

| Method | Path | Behavior |
|---|---|---|
| GET | `/admin/email-templates` | One template per event type (`id`, `eventType`, `subject`, `body`, `updatedBy`, `updatedAt`, `version`). |
| PUT | `/admin/email-templates/{eventType}` | `{ version, subject, body }`. 400 `email_templates.unknown_variable` for a `{{name}}` outside the known list, 400 `email_templates.variable_not_allowed` for a known name not allowed for that event; 404 unknown event type; 409 on a stale `version`. Audited (`email_templates.updated`). |
| GET | `/admin/email-deliveries` | Filters `status` (`pending`, `sending`, `sent`, `failed`), `eventType`, `recipient` (exact email), `from`, `to`; paging. |
| POST | `/admin/email-deliveries/{id}/retry` | Only a `failed` delivery: resets attempts and makes it due now; any other status is 409 `email_deliveries.not_failed`. For a failed `account_activation` / `password_reset` email a new link (new token, older unused ones revoked) is issued onto the same event; for an inactive user or one whose address changed since it was queued: 409 `email_deliveries.recipient_unavailable`. Audited (`email_delivery.retried`). |

### Admin: SLA (`admin` policy)

| Method | Path | Behavior |
|---|---|---|
| GET, PUT | `/admin/sla/calendar` | Company business calendar: `{ id, version, timeZone, hours[{ weekday, startsAt, endsAt }], updatedAt }`. PUT takes `{ version, timeZone, hours }`; 400 `sla.invalid_time_zone` / `sla.invalid_hours` / `sla.calendar_empty`, 409 on a stale `version`. A weekday not listed is closed. Running timers are re-planned. |
| GET, POST | `/admin/sla/holidays` | List, or add `{ date, name }` (409 `sla.holiday_exists`). Running timers are re-planned. |
| DELETE | `/admin/sla/holidays/{id}` | 204; 404 if unknown. Running timers are re-planned. |
| GET, POST | `/admin/sla/policy` | GET the current version (`versionId`, `number`, `targets`, `pauseRules`). POST creates the next version from `{ targets[{ kind, priority, requestTypeId?, metric, targetMinutes, warningPercent }], pauseRules[{ holdReason, pausesFirstResponse, pausesResolution }] }`; the kind × priority × metric matrix must be covered by base targets (400 `sla.target_missing`). Existing versions never change. |

### Admin: system (`admin` policy)

| Method | Path | Behavior |
|---|---|---|
| GET | `/admin/system/workers` | `{ status, jobs[{ job, instance, lastRunAt, lastError, intervalSeconds, stale }] }`. `status` is `degraded` when any job's newest heartbeat is older than 3 × its interval, else `ok`. |

### Health

| Method | Path | Behavior |
|---|---|---|
| GET | `/health` | Anonymous. `{ status }` only (`ok` or `degraded`, same rule as above); job names and timestamps stay behind the admin endpoint. |

### Reference lists

| Method | Path | Access | Behavior |
|---|---|---|---|
| GET | `/reference/locations` | signed in | Active locations with full path ("Kampüs / Bina") |
| GET | `/reference/departments` | signed in | Active departments |
| GET | `/reference/categories?kind=` | signed in | Active categories for `incident` or `request`, children of inactive parents left out |
| GET | `/reference/request-types` | signed in | Active request types with `requiresApproval` |
| GET | `/reference/users?search=` | IT staff, admin | Active users by name or email, max 20 |
| GET | `/reference/teams` | IT staff, admin | Active teams with their active IT members (transfer picker) |

### Assets (IT staff, admin)

| Method | Path | Behavior |
|---|---|---|
| GET | `/assets` | `search` (tag, serial, brand, model), `type`, `status`, `locationId`, paging |
| GET / POST / PUT | `/assets/{id}` | Card; create/update take `modelId` (type, brand and model come from the catalog) and descriptive fields (`version` on update). The server assigns the tag on create (`DMR-000001`, …; a number an existing tag already holds is skipped) and never changes it; a client-sent tag is ignored. An inactive model is refused (`assets.model_inactive`) except the asset's own current one. Serial unique after normalization (`assets.serial_taken`) |
| GET | `/assets/catalog` | Active brands with their active models (`id`, `type`, `name`) for the asset form |
| POST | `/assets/{id}/assign`, `/return`, `/transfer`, `/maintenance/start`, `/maintenance/end`, `/lost`, `/found`, `/retire` | Lifecycle operations with `version`; invalid transition 409 `assets.invalid_transition` |
| GET | `/assets/{id}/history` | Movements, newest first |
| GET | `/assets/{id}/tickets` | Linked tickets within the viewer's ticket scope |
| GET | `/me/assets` | Signed-in user's active assignments (any role) |
| GET | `/assets/import/template` | UTF-8 CSV with BOM, Turkish headers, one example row using a real active location |
| POST | `/assets/import/preview` | Multipart `file` (≤ 10 MB, ≤ 5,000 rows) → job with parsed rows and per-row errors; nothing written to the inventory |
| POST | `/assets/import/{jobId}/commit` | Inserts all rows in one transaction; refused while errors exist (`imports.has_errors`); re-resolves each model, so a model retired since the preview refuses the commit (`assets.model_inactive`) and a renamed one is written with its new name; a second commit returns the same result |

### Knowledge base (signed in; writes IT staff or admin)

| Method | Path | Behavior |
|---|---|---|
| GET | `/kb/articles` | Paged list (`page`, `pageSize`), newest update first. Query: `search`, `categoryId`, `status`, `visibility`. Employees get only `published` + `employees` articles and their `status`/`visibility` filters are ignored. With `search`, title matches sort first |
| GET | `/kb/articles/{id}` | Article detail; an article the caller may not read is 404 |
| POST | `/kb/articles` | Create a draft (IT staff, admin): `title` (≤ 200), `summary` (≤ 500), `body` Markdown (≤ 50,000), `categoryId`, `visibility`. 201 |
| PUT | `/kb/articles/{id}` | Edit with `version` (409 on a stale version); an `archived` article must be restored first (`knowledge.invalid_transition`) |
| POST | `/kb/articles/{id}/publish`, `/archive`, `/restore` | Status actions with `version`; archived restores to `draft`; other transitions give 409 `knowledge.invalid_transition` |
| GET | `/kb/articles/{id}/revisions` | Revision history (IT staff, admin) |

### Tickets (signed in; scope rules in "Auth & permissions")

| Method | Path | Behavior |
|---|---|---|
| POST | `/tickets/incidents`, `/tickets/requests` | JSON create for yourself → 201 detail. Goes to the default intake team (`tickets.no_intake_team` without one), P3. Requests whose type needs approval start `on_hold`/`manager_approval` with a pending approval |
| GET | `/tickets` | `queue` = `mine`, `team`, `assigned_to_me`, `unassigned`, `on_hold`; `status`, `kind`, `priority`, `search` (ticket number or Turkish-insensitive subject), paging; newest activity first |
| GET | `/tickets/{number}` | Detail with latest approval, related tickets in scope, `canWork`; `rootCause` hidden from employees |
| POST | `/tickets/{number}/claim`, `/transfer`, `/start`, `/hold`, `/resume`, `/complete`, `/cancel`, `/priority` | Work actions for people working on the ticket (others 404); body carries `version` |
| POST | `/tickets/{number}/approval/reassign` (admin), `/approval/invalidate` | Give a pending approval to another admin with a reason; void a granted approval after a scope change (new pending approval, back on hold) |
| POST | `/tickets/{number}/related-request` | Incident → request with copied subject, description, requester, location and linked assets; links both tickets |
| GET / POST / DELETE | `/tickets/{number}/assets`, `/assets/{assetId}` | Linked assets; workers link any asset, the requester only one assigned to them; duplicates `common.duplicate` |
| POST | `/tickets/{number}/comments` | Multipart `body`, `visibility` (`public`/`internal`), up to 5 `files` of 10 MB (PDF, PNG, JPEG, TXT, CSV) |
| GET | `/tickets/{number}/comments`, `/timeline` | Employees see public items only |
| GET / POST / DELETE | `/tickets/{number}/articles`, `/articles/{articleId}` | Knowledge articles linked to a ticket. GET: same scope as the ticket (404 outside it); employees get only employee-visible, published links. POST (body `articleId`) and DELETE: IT staff and admin only (employees 403); duplicate link `knowledge.article_already_linked` (409) |
| GET | `/tickets/{number}/attachments/{id}` | Download of clean, visible files only; `Content-Disposition: attachment`, `nosniff` |
| GET | `/approvals/mine` (admin) | Pending approvals assigned to me plus unassigned ones |
| POST | `/approvals/{id}/approve`, `/reject` (admin) | With the approval's `version`; reject needs a reason |

Ticket list and detail carry an `sla` block (one entry per metric: `metric`, `dueAt`, `remainingSeconds`, `isPaused`, `isWarned`, `isBreached`, `result`). `GET /tickets?queue=sla_risk` (IT staff and admins only, else 403) lists tickets with a running or paused instance that is warned or breached; the workspace shows it as "SLA Riski".

Status codes: 400 `validation.*` and field codes (`imports.*`, `attachments.*` on `file`/`files`), 401 `auth.unauthenticated`, 403 `common.forbidden` / `auth.password_change_required` / `auth.csrf_invalid`, 404 `common.not_found`, 409 business rules and `concurrency.conflict`, 429 `auth.rate_limited`. Responses carrying a temporary password send `Cache-Control: no-store`.

### Reports (IT staff or admin; employees 403)

| Method | Path | Behavior |
|---|---|---|
| GET | `/reports/{report}` | `report` is `incidents`, `requests`, `operations`, `transfers`, `assets` or `warranty` (otherwise 400 `reports.unknown_report`). Returns `{ report, filter, scopeLabel, figures }`; `scopeLabel` is `all` (admin; also asset and warranty reports for IT staff) or `teams` (IT staff: tickets whose current team is one of theirs) |
| GET | `/reports/{report}/drilldown` | Rows behind one figure. Query: `metric` (required; unknown gives 400 `reports.unknown_metric`), `bucket`, `part` (`numerator` default, or `denominator` for ratios), `page`, `pageSize`, plus the filter |
| GET | `/reports/{report}/export` | Same rows as the drill-down as an `.xlsx` stream (`tgysm-<report>-<metric>-<yyyyMMddHHmm>.xlsx`). Above 5,000 rows (`ReportExportLimits.SyncMaxRows`): 409 `reports.export_too_large` |
| POST | `/reports/exports` | Queues a background export. Same query parameters as the synchronous export (`report`, `metric`, `bucket`, `part`, filter), validated like the drill-down. 202 with the job item `{ id, report, metric, status, rowCount, createdAt, expiresAt }`. More than 100,000 rows (`ReportExportLimits.JobMaxRows`): 409 `reports.export_too_large`. Already 3 pending or running jobs of this user (`ReportExportLimits.MaxQueuedPerUser`): 409 `reports.export_queue_full`. Audited (`reports.export_requested`, entity `export_job`) |
| GET | `/reports/exports` | The caller's own background exports, newest first, paged (`page`, `pageSize`). A `ready` job past `expires_at` is listed as `expired` even before cleanup ran |
| GET | `/reports/exports/{id}/download` | The `.xlsx` file of a ready job. Signed-in users only (not the report role policy), so a creator demoted since the request gets 404 instead of a 403 that confirms the job. 404 when the caller is not the creator, is no longer active IT staff or admin, or the job `failed`; 409 `reports.export_not_ready` while `pending` or `running`; 410 `reports.export_expired` after the 24 h lifetime or when the file is gone. Sends `X-Content-Type-Options: nosniff`. Audited (`reports.export_downloaded`) |

Filter query parameters (all optional): `from`, `to` (`yyyy-MM-dd`, whole days in the company time zone, `to` inclusive, range 2000-01-01..2100-12-31, `from` after `to` gives 400 `reports.invalid_filter`), `kind`, `priority`, `categoryId`, `teamId`, `assigneeId`, `departmentId`, `locationId`, `status`, `slaState` (`met`, `breached`, `open_breached`, `at_risk`), `assetType`, `assetStatus`. Ticket reports use the ticket fields and asset reports the asset ones; the rest is ignored. The incident and request reports fix `kind` themselves.

Metrics by report (key: definition, date basis). Calendar time and SLA time are always reported apart; a ratio carries numerator and denominator, and a zero denominator gives no value.

| Report | Metric key | Definition | Date basis |
|---|---|---|---|
| `incidents` | `volume` | Incidents created in the window | creation |
| `incidents` | `first_response_time` | Average first-response timer, calendar (start to stop) and SLA (`elapsed_seconds`); cancelled timers do not count | first response (timer stop) |
| `incidents` | `resolution_time` | Average calendar and SLA time from creation to completion of incidents completed as `resolved` | completion |
| `incidents` | `mttr` | Total resolution seconds ÷ resolved incidents; buckets `mttr_calendar` and `mttr_sla` | completion |
| `incidents` | `open_backlog` | Open incidents now by age in whole days: `0-1`, `2-3`, `4-7`, `8+` | none (snapshot) |
| `incidents` | `sla_compliance` | Resolution timer `met` ÷ completed incidents whose SLA counts (not `service_request_required`, not `approval_rejected`) | completion |
| `incidents` | `excluded_outcomes` | Closed without counting toward SLA: buckets `cancelled`, `service_request_required` | completion |
| `requests` | `volume`, `sla_compliance` | As for incidents, for requests | creation / completion |
| `requests` | `fulfilment_time` | Average calendar and SLA time from creation to completion of requests completed as `fulfilled` | completion |
| `requests` | `approval_wait` | Average `decided_at` − creation of approved or rejected approvals, one per request | approval decision |
| `requests` | `rejection_rate` | Requests rejected at approval ÷ requests that ended by completion or rejection (a plain cancellation is apart) | completion |
| `requests` | `excluded_outcomes` | Buckets `cancelled` (not by rejection), `approval_rejected` | completion |
| `operations` | `team_load`, `assignee_load` | Open tickets now by current team / assignee (`unassigned` bucket) | none (snapshot) |
| `operations` | `by_category`, `by_location`, `by_department` | Tickets created in the window by category, location, requester department (`none` bucket for missing values) | creation |
| `operations` | `team_completed` | Tickets completed in the window by the team that held them at completion (target of the last assignment up to then, else the current team) | completion |
| `operations` | `hold_reasons` | Tickets on hold now by hold reason | none (snapshot) |
| `transfers` | `transfer_count` | Assignment rows in the window where the team changed (one per row) | assignment row time |
| `transfers` | `reassignments` | Assignment rows in the window with the same team and a changed assignee | assignment row time |
| `transfers` | `transfer_rate` | Tickets created in the window with at least one transfer in the window ÷ tickets created in the window | creation |
| `transfers` | `incident_to_request` | Incident-to-request relations created in the window whose incident is in scope, each once | relation time |
| `assets` | `by_type`, `by_status`, `by_location` | Assets as they are now by type, status, location | none (snapshot) |
| `assets` | `active_assignments` | Assets with an open assignment | none (snapshot) |
| `assets` | `stock` | Assets in `in_stock` | none (snapshot) |
| `assets` | `maintenance_lost_retired` | Assets in `maintenance`, `lost` or `retired`, by status | none (snapshot) |
| `warranty` | `expired` | Assets whose warranty ended before today | none |
| `warranty` | `expiring` | Warranty ends from today (or a later `from`) to `to`, inclusive; `to` defaults to today + 90 days | none |
| `warranty` | `no_warranty_date` | Assets without a warranty date (in no other warranty figure) | none |

Transfer figures are scoped by the ticket: only rows of tickets the viewer may see count, and a drill-down entry is the ticket, repeated once per row.

Report export audit: `reports.exported` (entity `report`) for a synchronous export and `reports.export_requested` (entity `export_job`) for a queued one record report, metric, bucket, part and filter, never the rows; `reports.export_downloaded` records the job id. Error codes: `reports.unknown_report`, `reports.unknown_metric`, `reports.invalid_filter` (400), `reports.export_too_large`, `reports.export_queue_full`, `reports.export_not_ready` (409), `reports.export_expired` (410); `reports.export_attempts_exhausted` is stored as a failed job's `last_error`.

## Data models

| Entity | Key fields |
|---|---|
| `users` | first/last name, email + `normalized_email` (unique, invariant lower), `search_name`, `role` (CHECK), department, location, `is_active`, `must_change_password`, `password_hash`, `security_stamp`, `access_failed_count`, `lockout_end`, `version` |
| `departments` | `name`, `normalized_name` (unique), `approver_user_id` (active admin), `is_active` |
| `locations` | `name`, `normalized_name`, `parent_id` (tree); unique `(parent_id, normalized_name)` NULLS NOT DISTINCT |
| `teams` / `team_memberships` | `name`, `normalized_name` (unique), `is_default_intake` (unique where true); membership unique `(team_id, user_id)` |
| `audit_events` | `occurred_at`, `actor_user_id`, `action`, `entity_type`, `entity_id`, `reason`, `old_values`/`new_values` (jsonb), `correlation_id`; triggers reject UPDATE, DELETE and TRUNCATE |
| `brands` / `asset_models` | catalog: brand `name` + `normalized_name` (unique), `is_active`; model `brand_id`, `type`, `name` + `normalized_name` (unique per brand and type), `is_active` |
| `assets` | `asset_tag` + `normalized_asset_tag` (unique), `model_id` (required FK to `asset_models`), `type`, `brand`, `model` (server-maintained snapshots of the catalog names), `serial_number` + `normalized_serial_number` (unique when set), `status`, location, department, warranty/purchase dates, `notes`; trigram indexes for search |
| `asset_assignments` | asset, user, assigned/returned at and by, `request_ticket_id`, note; unique active assignment per asset |
| `asset_movements` | append-only history: kind, from/to status, user, actor, reason, `user_responsibility_retained` |
| `import_jobs` | file name, `status` (`previewed`, `committed`), row counts, `rows`/`errors` (jsonb), creator, committed at |
| `categories` / `request_types` | category tree (two levels) with `scope`; request type with `requires_approval` |
| `tickets` | `number` (unique), `kind`, subject + `search_text`, description, category, location, requester, team, assignee, `status`, `hold_reason`/`hold_note`, impact, urgency, priority, request type, `requires_approval`, `root_cause`, `resolution`, `outcome`, `cancel_reason`, timestamps; CHECKs tie hold reason, resolution, cancel reason and request type to status/kind |
| `ticket_counters` | `(kind, year)` → `last_value` |
| `approvals` | ticket, approver (nullable), `status` (`pending`, `approved`, `rejected`, `void`), reason, decided at/by; one pending per ticket |
| `ticket_assignments`, `ticket_events` | append-only assignment history and timeline (`kind`, `visibility`, `data` jsonb); event ids from `SequentialId` (monotonic v7 within the process) |
| `maintenance_steps` | `id`, `completed_at`: post-migration data steps already run (written only by `PostMigrationRunner`, not in the EF model) |
| `ticket_comments`, `ticket_attachments` | body + visibility; file name, type, size, random `storage_key`, visibility, `scan_status` |
| `ticket_relations`, `ticket_assets` | incident ↔ request, ticket ↔ asset; unique pairs |

| `notification_outbox` | `event_type`, `ticket_id`, `user_id`, `payload` (jsonb: ids and reason codes only, always `actorId`), `protected_secret` (Data Protection–encrypted, account events only), `status` (`pending`, `dispatched`, `skipped`, `failed`), `attempts`, `last_error`, `locked_until`, `created_at`, `dispatched_at` |
| `email_templates` | `event_type` (unique), `subject`, `body`, `updated_by`, `updated_at`, `xmin`; ten rows seeded with Turkish defaults |
| `email_deliveries` | `outbox_id`, `recipient_user_id`, `recipient_email`, `template_type`, `subject`, `body` (null until rendered for account events), `status`, `attempts`, `next_attempt_at`, `locked_until`, `last_error` (max 500 characters, credentials redacted), `sent_at`; unique `(outbox_id, recipient_user_id)` |
| `password_tokens` | `user_id`, `purpose` (`activation`, `reset`), `token_hash` (unique; the token itself is never stored), `expires_at`, `used_at`, `created_at` |
| `business_calendar`, `business_hours`, `holidays` | one company calendar (`time_zone_id`, `version`), one working range per open weekday, holiday dates with names |
| `sla_policy_versions`, `sla_targets`, `sla_pause_rules` | immutable policy versions (one `is_current`); targets per kind, priority, optional request type and metric (`target_minutes`, `warning_percent`); which hold reasons pause which timer |
| `sla_instances` | per ticket and metric: `policy_version_id`, `target_minutes`, `warning_percent`, `elapsed_seconds`, `running_since`, `due_at`, `warn_at`, `warned_at`, `breached_at`, `stopped_at`, `result` (`met`, `breached`, `cancelled`); no `xmin` token (ADR-0018) |
| `sla_pauses`, `sla_events` | pause periods with the hold reason; append-only log (`started`, `paused`, `resumed`, `target_changed`, `warning`, `breached`, `stopped`, `calendar_changed`) |
| `knowledge_articles` | `title`, `summary`, `body` (Markdown), `category_id`, `visibility` (`employees`, `internal`), `status` (`draft`, `published`, `archived`), `author_id`, `published_at`, `revision`, `search_text` (folded title + summary + body + category, trigram GIN index), `xmin` token |
| `article_revisions` | Append-only: one row per content save with title, summary, body, visibility, category, editor, time |
| `ticket_articles` | Ticket–article link, unique per pair |
| `export_jobs` | `report`, `metric`, `bucket`, `part`, `filters` (jsonb), `status` (`pending`, `running`, `ready`, `failed`, `expired`; CHECK), `row_count`, `file_key`, `last_error` (≤ 500, sanitized), `created_by` → `users`, `created_at`, `started_at`, `finished_at`, `expires_at`, `attempts`, `locked_until` (row lease), `xmin` token; indexes `(created_by, created_at)` and `status` |
| `worker_heartbeats` | `job`, `instance`, `last_run_at`, `last_error`; unique `(job, instance)` |

Relationships: all foreign keys are `RESTRICT`; records are deactivated or closed, never deleted.

## Auth & permissions

- Session cookie `__Host-tgysm` (Secure, HttpOnly, SameSite=Strict, 8-hour sliding). Every request re-reads the user: inactive users and changed security stamps end the session. Role change, (de)activation, password change and admin reset rotate the stamp.
- Antiforgery token required in `X-CSRF-TOKEN` on every unsafe `/api` request, including login and uploads.
- Lockout: 5 failed logins → 15 minutes; the counter is updated atomically. Inactive and locked accounts cost the same hash check as others.
- Password: 12–128 characters with a letter and a digit. Temporary passwords: 16 random characters, shown once.
- Fallback and default authorization policies require a signed-in user whose password change is not pending; `/admin/*` and `/approvals/*` need `admin`; `/assets/*` (except `/me/assets`), `/reference/users` and `/reference/teams` need IT staff or admin.
- Ticket scope (ADR-0013): employees see their own tickets and public content only; IT staff see their own tickets and their teams' tickets with internal content; admins see all. Work actions need admin or membership in the ticket's team. Out-of-scope numbers and ids return 404 everywhere, including attachments.
- `/admin/email-templates`, `/admin/email-deliveries`, `/admin/sla/*` and `/admin/system/*` sit under the `admin` policy like the rest of `/admin/*`. `GET /health` and the account-link endpoints (`/auth/forgot-password`, `/auth/set-password`) are anonymous.
- Knowledge base (ADR-0019): any signed-in user may call the read endpoints, but employees see only `published` + `employees` articles; any other article is 404 on list, detail and search, and its ticket links are not shown to them. Create, edit, status actions and revisions need IT staff or admin. Linking and unlinking articles on a ticket need IT staff or admin and a ticket in their scope.
- Reports (ADR-0020): `/reports/*` need IT staff or admin (employees 403), except `/reports/exports/{id}/download`, which needs a signed-in user and checks in the handler that the caller created the job and is still active IT staff or admin (otherwise 404). Admins see all tickets; IT staff see only tickets whose current `team_id` is one of their teams, not tickets they merely requested. Asset and warranty reports show all assets to both roles. A background export is built under the creator's scope at run time; a creator who is inactive or no longer IT staff or admin by then fails the job (`last_error` `common.forbidden`) without a file.
- Password set links (ADR-0017): activation links last 72 hours, reset links 1 hour, each works once. A user's unused links are revoked when the user sets a password, when an admin changes the email address, and when an admin issues a temporary password. An account email queued before an email change is never sent to the new address. A forgot-password request is skipped (no link, no email) while the account has an unused reset link younger than 15 minutes; this also counts a link an admin issued. Every forgot-password request answers 202 and does the same work on the request path for any address, so the response does not reveal whether an account exists.
- Development only: the API trusts `X-Forwarded-Proto` from the loopback Vite proxy so Secure cookies work on `http://localhost`.

## UI / pages

- Knowledge base: `/kb` (search, category, and for IT staff status and visibility filters; list of cards), `/kb/:id` (article rendered from Markdown, revision history for IT staff), `/kb/new` and `/kb/:id/edit` (IT staff and admin: editor, publish, archive, restore). On a ticket, IT staff see an article box to link and unlink articles and a picker that inserts an employee-visible article link into a reply; employees see the linked articles that are visible to them. Markdown is shown without raw HTML and links open with `rel="noopener noreferrer"`.
- Reports (IT staff and admin; an employee opening a `/reports*` URL sees the 403 page `ForbiddenPage` instead of being sent home): `/reports` opens `/reports/incidents`; `/reports/:report` shows tabs for the six reports, a filter bar whose values live in the URL (bookmarkable; each tab keeps only the filters its report understands), the scope note ("Kapsam: tüm kayıtlar" or "Kapsam: yalnızca ekiplerinizin kayıtları"), and figure cards (headline value, calendar and SLA lines, or a distribution with a lazy Chart.js bar chart hidden from screen readers and the same numbers as links). Period reports open on the last 30 days and offer presets (Son 7 gün, Son 30 gün, Bu ay, Geçen ay, Bu yıl); active filters show as removable chips; filters apply as they change, with one "Temizle" button, and sit behind "Filtreler (n)" on phones. `/reports/:report/list?metric=…` is the drill-down list with paging and the export button: it downloads at once, and on 409 `reports.export_too_large` offers a background export and links to "Dışa aktarımlarım". `/reports/exports` lists the user's background exports (status badge, rows, created, expires, download link for `ready` jobs) and polls every 5 s while a job is `pending` or `running`.
- Layout (ADR-0021): CoreUI shell with a left sidebar and a header for both the app and `/admin`. The app sidebar groups Kişisel (Ana sayfa, Kayıtlarım, Zimmetlerim, Bilgi bankası), IT operasyonu (İş listem, Kuyruk, Envanter) and Analiz (Raporlar, Dışa aktarımlarım) by role; `/admin` has its own sidebar (Genel bakış, Organizasyon, Cihaz kataloğu, Hizmet kataloğu, SLA, Bildirimler, Sistem ve denetim) and a header link "Uygulamaya dön"; admins reach it from "Yönetim". From 992 px the sidebar is fixed and can be narrowed; below that it is an off-canvas panel behind the menu button. Phones show the account as initials. ADR-0026: a dark sidebar with an icon on every menu route (narrow mode shows icons only), a sticky header with the area switch, the theme menu (light, dark, auto) and an avatar account menu, and the breadcrumb in a header strip; below 992 px the menu panel opens with its links inside the viewport.
- Page header and breadcrumbs: every page has one `PageHeader` (H1 and actions) and a breadcrumb trail built from route metadata (`routeMeta.ts`); parent items are links, long last items truncate. Lists pass their URL as `returnTo`, so a ticket opened from a filtered queue or an article opened from search leads back to that list; article edit returns to the article.
- Lists keep filters, search and page in the URL (tickets, assets). Detail pages put the action/summary column first on phones. Forms with unsaved changes ask before leaving.
- Device catalog: `/admin/brands` and `/admin/models` (create, rename, deactivate, reactivate). The asset form picks type, then brand (searchable), then model; changing the type clears brand and model; there is no tag field and a toast shows the generated tag. A new incident can name one of the requester's assigned devices ("İlgili cihaz", optional).
- Account links: `/forgot-password` (always shows the same confirmation) and `/set-password?token=…` (link target for activation and reset emails). The user create and reset dialogs offer "email link" (default) or "temporary password".

- `/login`, `/change-password`, `/` home with role-based cards ("Bir şey çalışmıyor", "Bir şey istiyorum", "Kayıtlarım", "Zimmetlerim", IT: "İş listem", "Envanter", admin: "Onaylar", "Yönetim paneli"), `/profile`.
- Tickets: `/tickets` (my tickets), `/tickets/new/incident`, `/tickets/new/request` (approval hint, files uploaded right after creation), `/tickets/:number` (details, approval box, actions, linked assets and related tickets, replies/internal notes with files, timeline).
- IT: `/workspace` (queue cards with counts) → `/workspace/queue?queue=…`; `/assets`, `/assets/:id` (card, allowed operations only, history), `/assets/import` (template, preview with row errors, commit).
- `/assets/mine` for everyone; `/approvals` for admins (approve, reject with reason, reassign unassigned).
- Admin: `/admin/users`, `/admin/departments`, `/admin/locations` (tree), `/admin/teams` (members, intake flag), `/admin/categories` (tree), `/admin/request-types`, `/admin/email-templates` (edit subject and body, allowed variables listed), `/admin/email-deliveries` (filters, retry), `/admin/sla/calendar` (time zone, weekly hours, holidays; on a 409 offers to reload the server copy), `/admin/sla/policy` (target matrix, pause rules, saves as a new version), `/admin/system` (worker job status), `/admin/audit`.
- SLA on tickets: the ticket detail and list show an SLA badge per timer (remaining time, paused, warned, breached, met); `/workspace` has the "SLA Riski" queue.
- Page building blocks (ADR-0026): `PageCard`, `DataCard` (filters, edge-to-edge list, paging), `FormSection`, `StackTable` (stacks into cards below 992 px), `CheckField`, one `FieldLabel` style; loading skeletons reserve their space; `scrollbar-gutter: stable`. Guard tests reject raw inputs/tables/cards, hard-coded colors and new `max-width` queries; E2E checks dark mode persistence and layout shift under 0.1. The breadcrumb overflow menu stays inside the viewport down to 320 px.
- Mobile-first: cards under 768 px, tables above; sidebar off-canvas under 992 px; no horizontal scroll at 360 px (checked by E2E) and at 320/390 px (browser QA).
- Accessibility: labelled fields, errors linked with `aria-describedby`, focus moves to the first server field error, row buttons announce their record, visible focus, keyboard-only login verified by E2E.

## Features & business logic

- **First admin / recovery:** `tools/setup-admin.sh` works once and refuses when any user exists (ADR-0007). `tools/reset-password.sh --email …` issues a temporary password from the console; the user must change it at next sign-in. On a deployed stack the same commands run as `tools/compose.sh run --rm api setup-admin …` / `reset-password …` (`docs/operations.md`).
- **Users:** email unique ignoring case; department/location must be active when newly assigned; the last active admin cannot be demoted or deactivated (serialized with an advisory lock); a user's own sign-ins and password change do not make an admin's open edit stale.
- **Search and names:** Turkish-aware keys (`tr-TR` lower case, dotless ı folded to i), so "IT Destek" and "it destek" are the same name and "yazici" finds "Yazıcı".
- **Locations:** a location cannot move under itself or a descendant; a parent with active children cannot be deactivated; a child of an inactive parent cannot be reactivated.
- **Teams:** only active IT staff and admins can be members; inactive teams accept no new members; exactly one team can be the default intake team (moving the flag is serialized).
- **Asset lifecycle (ADR-0010):** `in_stock` → assign → `assigned`; return, transfer, maintenance (optionally keeping the user responsible), lost/found, retire. Operations are serialized; concurrent handovers give one active assignment and 409 for the rest. Every operation writes a movement and an audit row.
- **Handover against a request:** only to the open request's requester, and only after a required approval is granted (`assets.request_mismatch`, `tickets.approval_required`).
- **Device catalog and tags (ADR-0023):** brand and model names are unique by `CatalogName.Normalize` (whitespace collapsed, Turkish-aware key). Renames rewrite the asset snapshots in the same transaction; asset create, edit and import commit take the same `SerializationLocks.AssetCatalog` lock, so no asset keeps a name read before a rename. Tags come from `asset_tag_seq`, are never reused (a rolled-back save leaves a gap) and skip numbers that a pre-existing tag already holds.
- **Incident device:** `POST /tickets/incidents` takes an optional `assetId`; it must be a device actively assigned to the requester (`tickets.asset_not_assigned_to_requester`) and is linked to the new ticket; the link is recorded as an `AssetLinked` timeline event after `Created` and a `TicketAssetLinked` audit entry, like a later link.
- **Work actions and replies:** ticket work buttons are disabled while a reply is being sent or the ticket is refetching, because a reply changes the ticket version and a dialog keeps the version it opened with.
- **CSV import:** strict UTF-8 (`imports.encoding` explains how to save from Excel), `,` or `;`, dates `yyyy-MM-dd` or `dd.MM.yyyy`; location by full or unique trailing path; brand, type and model must exist in the active catalog (`assets.import_catalog_missing`, "Tanım eksik"); an empty tag is generated at commit (the preview uses no numbers), and a legacy tag with the reserved `DMR-` prefix is refused (`assets.import_reserved_tag`); duplicates inside the file and against the database reported per row; `assigned`/`lost` refused; one bad row blocks the whole commit; commit re-checks conflicts and is idempotent.
- **Ticket closing time:** `tickets.completed_at` is set when a ticket is completed, cancelled, or cancelled by approval rejection (`approval_rejected`). It is the "closed at" instant that reports use as the completion date basis. No migration backfills rows closed earlier.
- **Knowledge base search (ADR-0019, ADR-0006):** the key is the trimmed text lower-cased with `tr-TR` rules (`I`→`ı`, `İ`→`i`), then `ı` folded to `i`, so "Yazıcı" and "yazici" match (stored text is folded the same way) and "Isik", "ısık" and "İsik" match each other. Other diacritics (`ş`, `ç`, `ğ`, `ö`, `ü`) are not folded: "sifre" does not find "şifre", "gorev" does not find "görev". Input with no letter or digit means no search. Matching is substring over `search_text` through `LIKE '%key%'` (`LikePattern.Containing`, with `%`, `_` and `\` escaped so they match literally), which the trigram GIN index serves; ticket search uses the same pattern. An article summary may be empty. Markdown headings render one level down (`#` → `<h2>`, capped at `<h6>`) so the article title stays the page's only `<h1>`. A session whose user no longer exists gets 401 on knowledge base calls, not a server error.
- **Article revisions:** only create and edit (content changes) bump `revision` and write an `article_revisions` row; publish, archive and restore keep it. Visibility changes, publish, archive and restore are audited (`knowledge.visibility_changed`, `knowledge.published`, `knowledge.archived`, `knowledge.restored`; also `knowledge.created`, `knowledge.updated`, `tickets.article_linked`, `tickets.article_unlinked`).
- **Public-reply guard:** a public ticket comment whose body contains a `/kb/{uuid}` link to an `internal` article is refused with `knowledge.internal_article_in_public_reply`; the check is on the server, the reply picker is a convenience.
- **Report consistency:** a figure, its drill-down rows and its export are produced from the same filtered query and metric predicate, so counts agree. Exports write one data sheet plus a `Filtreler` sheet with the filters and scope; Turkish headers come from `ExportHeaders.tr.json`. Cell values are stored as text or numbers, never as formulas.
- **Background export (ADR-0020, ADR-0014):** a job is created `pending` (per-user cap of 3 pending or running jobs checked under `SerializationLocks.ExportQueue`). `ExportRunner` claims due jobs (`pending`, or `running` with an expired lease) with a row lease, renews each job's lease before working on it and skips a job another run took over. Each start counts an attempt; a job that has already started 3 runs (`ReportExportLimits.MaxAttempts`) is failed with `reports.export_attempts_exhausted` instead of running again, so a job that crashes the Worker cannot loop forever. The runner reads the same rows as the drill-down under the creator's current scope, fails the job with `reports.export_too_large` when it has grown past 100,000 rows, writes the file through `IReportFileWriter` into `IFileStore` and marks it `ready` with `row_count` and `expires_at` = now + 24 h. A failed write leaves no file; a failure is recorded from a fresh scope, only while this run still holds the lease, with a sanitized `last_error`.
- **Dev services:** `tools/dev-service.sh install` registers three launchd user agents (`com.tgysm.dev.api`, `.worker`, `.web`) that start at login and restart on exit (10 s throttle); logs in `tmp/logs/`. API and Worker start through `tools/dev-dotnet-host.sh`, which builds into `tmp/dev-artifacts/<name>/build`, copies a good build to `current/`, and falls back to the last good build when the build fails.
- **Ticket lifecycle:** open → in_progress → completed; open/in_progress ↔ on_hold (reason, "other" needs a note); cancel with a reason; no reopen. Completion needs a user-visible resolution; incidents need an outcome, `service_request_required` needs a related request; requests needing approval need a granted approval. Completed tickets still accept comments.
- **Priority:** impact × urgency matrix (P1–P4), default medium/medium = P3; IT changes it with a reason, old and new values audited.
- **Assignment:** IT claims unassigned tickets of their team (concurrent claims: one wins); transfer needs a reason ("other" needs a note) and an assignee who is an active member of the target team.
- **Approvals:** approver is the requester's department approver when that is an active admin other than the requester, otherwise unassigned until an admin reassigns it. Only the assigned approver decides; approve opens the request, reject cancels it with outcome `approval_rejected`; concurrent decisions: one wins. Cancelling a ticket voids its pending approval.
- **Numbers:** `INC-YYYY-NNNNNN` / `REQ-YYYY-NNNNNN` from a counter row locked in the creating transaction, so concurrent creation never repeats a number.
- **Comments and files:** employees write public replies on their own tickets; internal notes need someone working on the ticket. Files inherit the comment's visibility; extension must match content; a failed save deletes stored files. Comments and links do not conflict with a status change made at the same moment.
- **Concurrency:** stale edits return 409 `concurrency.conflict`; the UI shows a Turkish message; dialogs send the version the user saw when opening them.
- **Worker:** `Tgysm.Worker` runs five jobs: `OutboxDispatcher`, `EmailSender`, `SlaMonitor` (every 60 s), `ExportRunner` (every 30 s) and `Cleanup` (hourly) (ADR-0014). Each iteration runs in its own DI scope, a failure is logged and recorded and never stops the loop, and every run upserts a `worker_heartbeats` row (error text in `last_error`). Claims use `RowLease` (`FOR UPDATE SKIP LOCKED`); a crashed instance's rows become claimable again after `locked_until`. Email deliveries and outbox events are also re-claimed one row at a time by a conditional update on the lease value before processing, and completion writes are guarded by that ownership, so a run whose lease expired mid-batch cannot send or dispatch a row again or move a `sent` row back to `pending` (ADR-0014). The Worker has no signed-in user: audit rows it writes carry no user id and a generated correlation id.
- **Notification events:** ticket, approval and SLA code add an event through `INotificationOutbox` before the single save, so the event commits with the change and a rollback leaves none. Events and recipients (resolved at dispatch time from current data, inactive users never receive email): `ticket_created` (requester), `ticket_assigned` (new assignee, or team members when unassigned; not the actor), `public_reply` (requester, or assignee/team when the requester wrote it; not the author), `approval_requested` (approver), `approval_decided` (requester), `ticket_completed` (requester), `sla_warning` and `sla_breached` (assignee, or the team's active members when unassigned), `account_activation` and `password_reset` (the account). `password_reset_requested` is internal (the normalized address in its payload, resolved by the dispatcher; no delivery of its own). Every event carries the acting user's id in its payload; payloads hold ids and reason codes only.
- **Dispatch:** `OutboxDispatcher` claims pending events, resolves recipients, renders subject and body from the event's template and inserts `email_deliveries` rows, one event per DI scope. No recipient marks the event `skipped`. A failing event records an attempt on itself only (same back-off and limit as deliveries), so one bad event cannot block the rest; after 5 failed attempts it becomes `failed`. The failure is recorded from a fresh DI scope so the failed save is not retried. Rendered subject and body are cut to the column limits (200 and 10,000 characters, never inside a surrogate pair) so a long ticket subject cannot make the insert fail.
- **Sending:** `EmailSender` claims due deliveries in id order (`sending`, leased), sends through `IEmailSender`, marks `sent`. A failure adds an attempt and schedules the next one after 1, 5, 15 and 60 minutes; the 5th failed attempt sets `failed`. A failure is recorded in a fresh scope, only while the row is still `sending` under this run's lease. Each send is bounded by `Email:Smtp:TimeoutSeconds`. A delivery left `sending` by a crashed worker is reclaimed once its lease expires. Delivery is at-least-once: a crash after the transport accepted the message but before `sent` was saved can repeat one email (ADR-0015).
- **Transports:** `Smtp` uses MailKit (STARTTLS when `UseStartTls`, credentials only when configured); `Pickup` writes `.eml` files, so local runs and tests need no mail server. Send errors are sanitized (type name and message, `password`/`secret`/`token`/`authorization` pairs redacted, cut to 500 characters) before they reach `last_error`.
- **Email templates:** plain text only. Variables are `{{name}}` substitutions from a fixed per-event list: ticket events `ticketNumber`, `subject`, `status`, `ticketUrl`, `recipientName`; `ticket_completed` adds `resolution`, `completedAt`; SLA events add `slaMetric`, `dueAt`; account events allow only `recipientName`, `actionUrl`, `expiresAt`. An unset variable renders as empty text. Dates use the company time zone (`App:TimeZone`) as `dd.MM.yyyy HH:mm`. Subject (max 200 characters) and body (max 10,000) are required; variables are checked in both.
- **Account-link emails:** the link token is stored only as `protected_secret` (ASP.NET Core Data Protection, purpose `Tgysm.Notifications.AccountLinkToken`). The body renders at send time; after `sent` or a terminal `failed` the secret is cleared and the delivery body becomes `[cleared]`. A retryable failure keeps the secret so the next attempt can render the link. Link: `App:PublicBaseUrl` + `/set-password?token=…`. Admin retry of a failed account email issues a new token onto the same event and revokes older unused ones; the old secret is not reused.
- **Business time (ADR-0018):** `BusinessTimeCalculator.SecondsBetween` and `AddSeconds` work over `BusinessCalendarRules` (zone, one optional working range per weekday, holidays). Each UTC boundary is converted to local time per day and clipped to the working range; holidays and closed days count zero; DST gaps lose the skipped hour. Results are UTC. `sla.calendar_empty` is the error for a calendar with no working time.
- **SLA instances (ADR-0018):** creating a ticket starts a `first_response` and a `resolution` instance on the current policy version (a request-type target overrides the priority target; a request starting `on_hold` for approval starts with resolution paused). A running instance keeps the targets and pause rules of the version it started on. A hold pauses the timers its reason's rule names; resume and approval grant restart them. A priority change gives each instance the new priority's target of its own version and resets the warning while still below the threshold. First response stops at the first public reply by IT staff or an admin other than the requester; both stop on completion (result `met` or `breached`) and cancellation (`cancelled`). Transfers never reset a timer.
- **Calendar change re-plans running timers (ADR-0018):** changing the calendar or adding or removing a holiday recomputes `due_at` and `warn_at` of running (not paused, not stopped) instances: elapsed business time under the old rules is kept, the remainder runs under the new rules, and a `calendar_changed` event is logged. The re-planner locks the instances it changes. Known limit: a ticket action that read the old calendar just before the change and saves just after it computes its own due time under the old rules until the next pause, resume or priority change.
- **SLA monitor:** `SlaMonitor` marks running instances past `warn_at` or `due_at`. Each mark is a conditional update (`warned_at` / `breached_at` still empty, not stopped), one transaction per instance, so of several monitors one wins and only it writes the `sla_events` row and the `sla_warning` / `sla_breached` outbox event. A breach of an instance never warned also warns first. It writes only those columns, so it never fails a user's ticket action. Paused instances do not warn or breach.
- **Worker status and health:** each job's newest heartbeat is judged stale after 3 × its interval; `GET /admin/system/workers` lists jobs, `GET /health` returns only `ok` or `degraded`.
- **Cleanup:** hourly, with a 30-day retention cutoff. Deletes password tokens that expired before the cutoff; `dispatched` or `skipped` outbox events older than the cutoff whose deliveries are all `sent` or `failed` (deliveries go with the event); `password_reset_requested` events (they hold an email address) that are no longer `pending` and are older than the cutoff; and `worker_heartbeats` rows not updated since the cutoff. It also expires `ready` export jobs past `expires_at`, in batches of 100 (`CleanupHandler.ExportExpiryBatchSize`): the file is deleted first, then the job is marked `expired` (xmin-guarded); a file that cannot be deleted is logged and its job stays `ready` for a later run.
- **Audit:** security, admin, asset and ticket events are written in the same transaction as the change; passwords, hashes and tokens are never recorded.
- **Migrate command:** `migrate` applies pending EF Core migrations, then every post-migration step not yet in `maintenance_steps`, each in its own transaction with its record (a failed step changes nothing and runs again next time). Step `RecomputeCatalogNamesStep` recomputes `brands.normalized_name` and `asset_models.normalized_name` with `CatalogName.Normalize`, so keys no longer depend on the database `lc_ctype`; it writes in two passes (row-unique placeholder, then the final key) because a new key may equal another row's old key under the unique index. Output names the applied migrations; a failure prints `Migration failed: …` and exits 1, and compose then keeps `api` and `worker` stopped.
- **Timeline order:** timeline queries order by time, then id. `Guid.CreateVersion7` is random within one millisecond, so event ids come from `SequentialId` (monotonic within the process) and events written in the same instant keep their creation order.
- **Deployment (ADR-0024):** `tools/deploy.sh` checks for `deploy/.env` and the certificate pair, runs `tools/lint.sh` and `tools/test.sh` unless `--skip-gates`, refuses a working tree with uncommitted changes, builds both images on fresh base images (`docker build --pull`) tagged with the Git short hash and `latest`, records the hash in `deploy/.version`, pulls `postgres` and `nginx`, starts the stack and waits up to 180 s for `/api/v1/health` through nginx. `tools/docker-build.sh --allow-dirty` builds a `<hash>-dirty` test image and never moves `latest`. Every service's container log rotates (`json-file`, 5 files of 10 MB). `api` and `worker` start only after `migrate` exits 0; `api` reports healthy through `GET /api/v1/health`.
- **Backup (ADR-0025):** the `backup` service runs at `BACKUP_TIME` in `TZ`; `tools/backup.sh` runs one on demand. Dump, row counts and newest migration id come from one exported snapshot (`pg_export_snapshot` + `pg_dump --snapshot`), so the manifest describes exactly what the dump holds; the manifest also carries `dbSha256` (SHA-256 of `db.dump`) and `fileCount`. Archives are written under a temporary name, renamed when complete, mode 600; expired archives are deleted only after a successful run, and never by a safety backup (`BACKUP_SKIP_RETENTION=true`).
- **Restore (ADR-0025):** `tools/restore.sh <archive> [--force] [--no-safety-backup]` refuses before writing (exit 2 not an archive, a `db.dump` that does not match `dbSha256`, a missing `dbSha256` or a file count that differs from the manifest; 3 newer build than `migrate --print-latest`; 4 non-empty database without `--force`). With `--force` on a database that has tables it first takes a safety backup (`tools/backup.sh --safety`: one-off backup container, retention skipped so an expired archive being restored from the backup folder survives) and stops if that fails; a refusal restarts exactly the services that ran before; after writing it checks row counts against the manifest (exit 5), runs `migrate` and starts the stack. A failure while writing leaves the application stopped on purpose. `tools/restore-drill.sh` exercises the real scripts and images on two throw-away compose projects and writes `docs/release/restore-drill.md`.
- **Pilot seed:** `seed-pilot` (`tools/seed-pilot.sh`, `--compose` for a deployed stack) runs only in `Development` or `Pilot` and on a database without tickets. Through the application handlers it writes departments, a location tree, intake and field teams, 8 IT staff, 50 employees, 2 approvers, categories (including "TGYSM Pilot"), request types, a device catalog, 500 assets, 2,000 tickets over 90 days and 20 articles (`--scale` multiplies, at most 10; fixed random seed, so the same scale gives the same data). Accounts use `@pilot.example.com` and one pilot password printed once. It closes the outbox rows it created, so seeded notifications are never sent; SLA breach notifications for open seeded tickets still follow later, so a pilot host runs with a relay that drops that domain or `EMAIL_MODE=Pickup` while only seed data exists. Child location names carry no parent prefix, so CSV import paths resolve them.
- **Performance measurement:** `tools/perf-measure.sh` times lists, searches, the workspace queue, every report, a drill-down, synchronous Excel export and CSV preview/commit of 5,000 rows (one warm-up per operation, median and p95) and writes `docs/release/performance.md`; it refuses the development API port 5080 because the CSV commit writes 5,000 assets per run.
- **Acceptance evidence:** `docs/release/acceptance.md` maps each `tgysm.md` §20 criterion to tests, scripts or documents.
- **User guide:** `tools/guide-screenshots.sh` shoots the pages in `frontend/guide/manifest.ts` at 390×844 and 1280×800 from the seeded pilot database; `tools/build-user-guide.sh` builds `docs/user-guides/combined.md` and the A4 `combined.pdf` and checks anchors and images.

<!-- DOC-BASELINE: dfdae77 -->
_Documentation baseline: current as of commit dfdae77 (2026-10-10). Refresh with `git log --oneline dfdae77..HEAD`._
