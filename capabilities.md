# TGYSM Capabilities

## Overview

ITSM platform for one company (multiple departments and locations). Users: employees, IT staff, admins. Served as one web application; hosting target not yet decided (Phase 6). Out of scope for the first release: multi-tenancy, SSO/LDAP, native mobile app, CMDB, change/problem management (`tgysm.md` §2).

Status legend: **Available** = implemented and tested; **Planned** = later phase (`tgysm.md` §19).

| Area | Status |
|---|---|
| Local login, forced password change, `/admin` users, departments, locations, teams, audit | Available (Phase 1) |
| IT inventory: assets, lifecycle, assignments, CSV import, "my assets" | Available (Phase 2) |
| Incidents and requests: intake, queues, assignment, approvals, comments/files, timeline, related requests, ticket–asset links | Available (Phase 3) |
| Email (Phase 4): worker host, transactional outbox, ticket notification events, admin-managed templates, sending with retry (SMTP or pickup folder), deliveries list and retry, password set links with forgot-password, `/admin/email-templates` and `/admin/email-deliveries` pages | Available (Phase 4, on branch `phase-4-sla-email`) |
| SLA (Phase 4): business calendar and holidays, versioned policy, SLA instances per ticket, monitor job with warning and breach events, `sla_risk` queue, SLA block on tickets, `/admin/sla/*` pages | Available (Phase 4, on branch `phase-4-sla-email`) |
| `/admin/system` worker status page, cleanup job | Available (Phase 4, on branch `phase-4-sla-email`) |
| Export runner | Planned |
| Knowledge base and reports (Phase 5), release (Phase 6) | Planned |

## Tech stack

| Component | Choice | Decision |
|---|---|---|
| Backend | .NET 10 modular monolith, Clean Architecture (`Tgysm.Domain` → `Application` → `Infrastructure` → `Api`), Minimal APIs, FluentValidation 12 | ADR-0001 |
| Database / ORM | PostgreSQL 17, EF Core 10 + Npgsql 10, snake_case, `uuid` v7 keys; `xmin` concurrency, application-managed `version` on `users`; advisory locks for cross-row rules; `pg_trgm` for substring search | ADR-0002 |
| Web | React 19, TypeScript ~5.9 (strict), Vite 8, Tailwind CSS v4, Radix UI, React Router, TanStack Query 5, react-hook-form + zod 4, i18next; types generated from OpenAPI | ADR-0003, ADR-0008 |
| Auth | Local accounts, PBKDF2 (`PasswordHasher<T>`), cookie session, antiforgery header, lockout, login rate limit | ADR-0004 |
| Roles | `employee`, `it_staff`, `admin`; one per user | ADR-0005 |
| Language | English code/URLs, Turkish UI via i18n | ADR-0006 |
| Operator CLI | `setup-admin` (first admin, once), `reset-password` (temporary password from the console) | ADR-0007 |
| Tickets | One table for incidents and requests; numbers from a counter table | ADR-0009, ADR-0011 |
| Assets | Lifecycle state machine, one active assignment per asset | ADR-0010 |
| Attachments | Local disk store outside the web root with random keys, magic-byte checks, scanner port (development scanner marks type-checked files clean) | ADR-0012 |
| Ticket access | Employees own tickets, IT staff own + their teams', admins all; everything else 404 | ADR-0013 |
| Tests | xUnit v3 on Microsoft.Testing.Platform, NetArchTest, coverlet.MTP; Vitest + React Testing Library (jsdom 29); Playwright (360×800, 1280×800) | ADR-0008 |
| Background worker | `Tgysm.Worker` (.NET Generic Host, references `Application` and `Infrastructure`, never `Api`); one `BackgroundService` per job on a `PeriodicTimer`; batches claimed with `SELECT … FOR UPDATE SKIP LOCKED` row leases (`locked_until`), so several instances can run side by side; one DI scope per unit of work | ADR-0014 |
| Data Protection | `AddDataProtection()` with the shared application name `Tgysm`, so the Worker can read secrets the API protected. Key persistence is not configured yet (open item, see `SECURITY.md`) | ADR-0004, ADR-0015 |
| Outbox and email | Transactional outbox (`notification_outbox`), dispatcher job, sender job with retry; `IEmailSender` implemented by MailKit 4.18 SMTP (production) and a pickup folder of `.eml` files (development, tests); at-least-once delivery | ADR-0015 |
| Email templates | Database rows per event type, plain text, fixed `{{variable}}` allow-list per event, Turkish defaults seeded by migration | ADR-0016 |
| SLA calculation | Domain `BusinessTimeCalculator` over `BusinessCalendarRules` (time zone, weekly working ranges, holidays); DST handled by converting local boundaries back to UTC. `SlaInstance` state machine per ticket and metric (`first_response`, `resolution`), versioned policy, `SlaMonitor` job, calendar changes re-plan running timers | ADR-0018 |
| Password set links | Random 32-byte tokens, only a hash is stored (`password_tokens`, purposes `activation` 72 h and `reset` 1 h, single use). Sent by email, redeemed at `POST /auth/set-password`; the rest of the account flow is under "Auth & permissions" | ADR-0017 |

## Configuration

Hosts: `Tgysm.Api` and `Tgysm.Worker`. Both fail at startup on an invalid `App` section; the Worker also fails at startup when `Email:Mode` is missing or unknown, or when the `EmailSender` lease is not longer than `Email:Smtp:TimeoutSeconds`.

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
| `Worker:Jobs:<Job>:IntervalSeconds`, `BatchSize`, `LeaseSeconds` | Worker | Per job. `OutboxDispatcher` and `EmailSender`: 30 s / 50 / 60 s. `SlaMonitor`: 60 s, batch 200. `Cleanup`: hourly (3600 s). A non-positive interval falls back to the job default and logs a warning. |
| `Worker:Jobs:<Job>:IntervalSeconds` | API | The API reads the same section (duplicated in its `appsettings.json`, default 60 s) to judge a job stale for `/health` and `/admin/system/workers`. |
| `Security:LoginPermitLimitPerMinute`, `ForgotPasswordPermitLimitPerMinute`, `SetPasswordPermitLimitPerMinute` | API | Per-client-IP limits per minute; defaults 10, 5, 5. `tools/e2e.sh` raises the login limit and shortens the two job intervals, and sets `App:PublicBaseUrl`, `Email:Mode=Pickup`, `Email:PickupFolder` for the run (E2E only). |

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
| GET / POST / PUT | `/assets/{id}` | Card; create/update descriptive fields only (`version` on update). Tag and serial unique after normalization (`assets.tag_taken`, `assets.serial_taken`) |
| POST | `/assets/{id}/assign`, `/return`, `/transfer`, `/maintenance/start`, `/maintenance/end`, `/lost`, `/found`, `/retire` | Lifecycle operations with `version`; invalid transition 409 `assets.invalid_transition` |
| GET | `/assets/{id}/history` | Movements, newest first |
| GET | `/assets/{id}/tickets` | Linked tickets within the viewer's ticket scope |
| GET | `/me/assets` | Signed-in user's active assignments (any role) |
| GET | `/assets/import/template` | UTF-8 CSV with BOM, Turkish headers, one example row using a real active location |
| POST | `/assets/import/preview` | Multipart `file` (≤ 10 MB, ≤ 5,000 rows) → job with parsed rows and per-row errors; nothing written to the inventory |
| POST | `/assets/import/{jobId}/commit` | Inserts all rows in one transaction; refused while errors exist (`imports.has_errors`); a second commit returns the same result |

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
| GET | `/tickets/{number}/attachments/{id}` | Download of clean, visible files only; `Content-Disposition: attachment`, `nosniff` |
| GET | `/approvals/mine` (admin) | Pending approvals assigned to me plus unassigned ones |
| POST | `/approvals/{id}/approve`, `/reject` (admin) | With the approval's `version`; reject needs a reason |

Ticket list and detail carry an `sla` block (one entry per metric: `metric`, `dueAt`, `remainingSeconds`, `isPaused`, `isWarned`, `isBreached`, `result`). `GET /tickets?queue=sla_risk` (IT staff and admins only, else 403) lists tickets with a running or paused instance that is warned or breached; the workspace shows it as "SLA Riski".

Status codes: 400 `validation.*` and field codes (`imports.*`, `attachments.*` on `file`/`files`), 401 `auth.unauthenticated`, 403 `common.forbidden` / `auth.password_change_required` / `auth.csrf_invalid`, 404 `common.not_found`, 409 business rules and `concurrency.conflict`, 429 `auth.rate_limited`. Responses carrying a temporary password send `Cache-Control: no-store`.

## Data models

| Entity | Key fields |
|---|---|
| `users` | first/last name, email + `normalized_email` (unique, invariant lower), `search_name`, `role` (CHECK), department, location, `is_active`, `must_change_password`, `password_hash`, `security_stamp`, `access_failed_count`, `lockout_end`, `version` |
| `departments` | `name`, `normalized_name` (unique), `approver_user_id` (active admin), `is_active` |
| `locations` | `name`, `normalized_name`, `parent_id` (tree); unique `(parent_id, normalized_name)` NULLS NOT DISTINCT |
| `teams` / `team_memberships` | `name`, `normalized_name` (unique), `is_default_intake` (unique where true); membership unique `(team_id, user_id)` |
| `audit_events` | `occurred_at`, `actor_user_id`, `action`, `entity_type`, `entity_id`, `reason`, `old_values`/`new_values` (jsonb), `correlation_id`; triggers reject UPDATE, DELETE and TRUNCATE |
| `assets` | `asset_tag` + `normalized_asset_tag` (unique), `type`, `brand`, `model`, `serial_number` + `normalized_serial_number` (unique when set), `status`, location, department, warranty/purchase dates, `notes`; trigram indexes for search |
| `asset_assignments` | asset, user, assigned/returned at and by, `request_ticket_id`, note; unique active assignment per asset |
| `asset_movements` | append-only history: kind, from/to status, user, actor, reason, `user_responsibility_retained` |
| `import_jobs` | file name, `status` (`previewed`, `committed`), row counts, `rows`/`errors` (jsonb), creator, committed at |
| `categories` / `request_types` | category tree (two levels) with `scope`; request type with `requires_approval` |
| `tickets` | `number` (unique), `kind`, subject + `search_text`, description, category, location, requester, team, assignee, `status`, `hold_reason`/`hold_note`, impact, urgency, priority, request type, `requires_approval`, `root_cause`, `resolution`, `outcome`, `cancel_reason`, timestamps; CHECKs tie hold reason, resolution, cancel reason and request type to status/kind |
| `ticket_counters` | `(kind, year)` → `last_value` |
| `approvals` | ticket, approver (nullable), `status` (`pending`, `approved`, `rejected`, `void`), reason, decided at/by; one pending per ticket |
| `ticket_assignments`, `ticket_events` | append-only assignment history and timeline (`kind`, `visibility`, `data` jsonb) |
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
- Password set links (ADR-0017): activation links last 72 hours, reset links 1 hour, each works once. A user's unused links are revoked when the user sets a password, when an admin changes the email address, and when an admin issues a temporary password. An account email queued before an email change is never sent to the new address. A forgot-password request is skipped (no link, no email) while the account has an unused reset link younger than 15 minutes; this also counts a link an admin issued. Every forgot-password request answers 202 and does the same work on the request path for any address, so the response does not reveal whether an account exists.
- Development only: the API trusts `X-Forwarded-Proto` from the loopback Vite proxy so Secure cookies work on `http://localhost`.

## UI / pages

- Account links: `/forgot-password` (always shows the same confirmation) and `/set-password?token=…` (link target for activation and reset emails). The user create and reset dialogs offer "email link" (default) or "temporary password".

- `/login`, `/change-password`, `/` home with role-based cards ("Bir şey çalışmıyor", "Bir şey istiyorum", "Kayıtlarım", "Zimmetlerim", IT: "İş listem", "Envanter", admin: "Onaylar", "Yönetim paneli"), `/profile`.
- Tickets: `/tickets` (my tickets), `/tickets/new/incident`, `/tickets/new/request` (approval hint, files uploaded right after creation), `/tickets/:number` (details, approval box, actions, linked assets and related tickets, replies/internal notes with files, timeline).
- IT: `/workspace` (queue cards with counts) → `/workspace/queue?queue=…`; `/assets`, `/assets/:id` (card, allowed operations only, history), `/assets/import` (template, preview with row errors, commit).
- `/assets/mine` for everyone; `/approvals` for admins (approve, reject with reason, reassign unassigned).
- Admin: `/admin/users`, `/admin/departments`, `/admin/locations` (tree), `/admin/teams` (members, intake flag), `/admin/categories` (tree), `/admin/request-types`, `/admin/email-templates` (edit subject and body, allowed variables listed), `/admin/email-deliveries` (filters, retry), `/admin/sla/calendar` (time zone, weekly hours, holidays; on a 409 offers to reload the server copy), `/admin/sla/policy` (target matrix, pause rules, saves as a new version), `/admin/system` (worker job status), `/admin/audit`.
- SLA on tickets: the ticket detail and list show an SLA badge per timer (remaining time, paused, warned, breached, met); `/workspace` has the "SLA Riski" queue.
- Mobile-first: cards under 768 px, tables above; main menu is a drawer under 1024 px; bottom-sheet dialogs on phones; no horizontal scroll at 360 px (checked by E2E).
- Accessibility: labelled fields, errors linked with `aria-describedby`, focus moves to the first server field error, row buttons announce their record, visible focus, keyboard-only login verified by E2E.

## Features & business logic

- **First admin / recovery:** `tools/setup-admin.sh` works once and refuses when any user exists (ADR-0007). `tools/reset-password.sh --email …` issues a temporary password from the console; the user must change it at next sign-in.
- **Users:** email unique ignoring case; department/location must be active when newly assigned; the last active admin cannot be demoted or deactivated (serialized with an advisory lock); a user's own sign-ins and password change do not make an admin's open edit stale.
- **Search and names:** Turkish-aware keys (`tr-TR` lower case, dotless ı folded to i), so "IT Destek" and "it destek" are the same name and "yazici" finds "Yazıcı".
- **Locations:** a location cannot move under itself or a descendant; a parent with active children cannot be deactivated; a child of an inactive parent cannot be reactivated.
- **Teams:** only active IT staff and admins can be members; inactive teams accept no new members; exactly one team can be the default intake team (moving the flag is serialized).
- **Asset lifecycle (ADR-0010):** `in_stock` → assign → `assigned`; return, transfer, maintenance (optionally keeping the user responsible), lost/found, retire. Operations are serialized; concurrent handovers give one active assignment and 409 for the rest. Every operation writes a movement and an audit row.
- **Handover against a request:** only to the open request's requester, and only after a required approval is granted (`assets.request_mismatch`, `tickets.approval_required`).
- **CSV import:** strict UTF-8 (`imports.encoding` explains how to save from Excel), `,` or `;`, dates `yyyy-MM-dd` or `dd.MM.yyyy`; location by full or unique trailing path; duplicates inside the file and against the database reported per row; `assigned`/`lost` refused; one bad row blocks the whole commit; commit re-checks conflicts and is idempotent.
- **Ticket lifecycle:** open → in_progress → completed; open/in_progress ↔ on_hold (reason, "other" needs a note); cancel with a reason; no reopen. Completion needs a user-visible resolution; incidents need an outcome, `service_request_required` needs a related request; requests needing approval need a granted approval. Completed tickets still accept comments.
- **Priority:** impact × urgency matrix (P1–P4), default medium/medium = P3; IT changes it with a reason, old and new values audited.
- **Assignment:** IT claims unassigned tickets of their team (concurrent claims: one wins); transfer needs a reason ("other" needs a note) and an assignee who is an active member of the target team.
- **Approvals:** approver is the requester's department approver when that is an active admin other than the requester, otherwise unassigned until an admin reassigns it. Only the assigned approver decides; approve opens the request, reject cancels it with outcome `approval_rejected`; concurrent decisions: one wins. Cancelling a ticket voids its pending approval.
- **Numbers:** `INC-YYYY-NNNNNN` / `REQ-YYYY-NNNNNN` from a counter row locked in the creating transaction, so concurrent creation never repeats a number.
- **Comments and files:** employees write public replies on their own tickets; internal notes need someone working on the ticket. Files inherit the comment's visibility; extension must match content; a failed save deletes stored files. Comments and links do not conflict with a status change made at the same moment.
- **Concurrency:** stale edits return 409 `concurrency.conflict`; the UI shows a Turkish message; dialogs send the version the user saw when opening them.
- **Worker:** `Tgysm.Worker` runs four jobs: `OutboxDispatcher`, `EmailSender`, `SlaMonitor` (every 60 s) and `Cleanup` (hourly); `ExportRunner` is planned (ADR-0014). Each iteration runs in its own DI scope, a failure is logged and recorded and never stops the loop, and every run upserts a `worker_heartbeats` row (error text in `last_error`). Claims use `RowLease` (`FOR UPDATE SKIP LOCKED`); a crashed instance's rows become claimable again after `locked_until`. Email deliveries and outbox events are also re-claimed one row at a time by a conditional update on the lease value before processing, and completion writes are guarded by that ownership, so a run whose lease expired mid-batch cannot send or dispatch a row again or move a `sent` row back to `pending` (ADR-0014). The Worker has no signed-in user: audit rows it writes carry no user id and a generated correlation id.
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
- **Cleanup:** hourly, with a 30-day retention cutoff. Deletes password tokens that expired before the cutoff; `dispatched` or `skipped` outbox events older than the cutoff whose deliveries are all `sent` or `failed` (deliveries go with the event); `password_reset_requested` events (they hold an email address) that are no longer `pending` and are older than the cutoff; and `worker_heartbeats` rows not updated since the cutoff.
- **Audit:** security, admin, asset and ticket events are written in the same transaction as the change; passwords, hashes and tokens are never recorded.

<!-- DOC-BASELINE: 550302d -->
_Documentation baseline: current as of commit 550302d (2026-10-02). Refresh with `git log --oneline 550302d..HEAD`._
