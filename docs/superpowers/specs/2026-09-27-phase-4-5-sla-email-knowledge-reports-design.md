# Phase 4–5 SLA, Email, Knowledge Base and Reports — Design

- Date: 2026-09-27
- Owner: Tugay İLTUŞ
- Requirements source: `tgysm.md` v3.0 §3.1, §9–§12, §15–§17, §19–§21
- Builds on: Phase 2–3 (`docs/superpowers/specs/2026-09-26-phase-2-3-assets-and-tickets-design.md`), commit `5ea39bc`
- Scope: Phase 4 (worker, outbox, email, account links, SLA) and Phase 5 (knowledge base, reports, Excel export) in one spec. Two implementation plans: Phase 4 first, Phase 5 second. Phase 6 (release hardening, pilot, restore drill, production SMTP/scanner/hosting) stays out.

## 1. Agreed understanding

**Owner decisions**

| Topic | Decision |
|---|---|
| Structure | One spec for Phases 4 and 5; one plan per phase, Phase 4 delivered and committed before Phase 5 starts |
| Background work | A separate `Tgysm.Worker` process (.NET Generic Host) using the same Application layer; safe with several instances through row leases taken with `FOR UPDATE SKIP LOCKED` |
| Email | Transactional outbox written in the same transaction as the change; worker expands events into per-recipient deliveries and sends them with bounded retry. MailKit SMTP in production, a pickup folder (`.eml` files) in development and tests |
| Templates | Stored in the database, seeded with Turkish defaults, edited in `/admin`; plain text with a fixed variable list per event |
| Account links | Activation and password-reset links (single use, time limited, stored as hash) replace temporary passwords as the default; the temporary password stays as an admin fallback and the CLI recovery path |
| SLA | Versioned policy, one business calendar (working hours, holidays, time zone), per-ticket SLA instances that accumulate business time; pure calculation in Domain |
| Knowledge base | Articles with revisions, draft/published/archived, employee/internal visibility, Turkish-insensitive trigram search (existing `TurkishText` + `pg_trgm`) |
| Reports | Server-side report queries sharing one filter/scope builder with drill-down and export; Excel via ClosedXML; large exports as background jobs with expiring download |
| Delivery | TDD, gates as Phase 1–3, specialist reviews at the end of each phase, local commits only after green gates; no push |

**Assumptions** (confirm before go-live, `tgysm.md` §21)

- Company time zone `Europe/Istanbul`; working hours Monday–Friday 09:00–18:00; holidays entered by an admin.
- Seed SLA targets (business time, first response / resolution): P1 30 min / 4 h, P2 1 h / 8 h, P3 4 h / 3 days, P4 8 h / 5 days. Requests use the same table unless a request type overrides it. Warning at 75 % of the target. These are samples, not commitments.
- Seed pause rules: holds with reason `user` or `manager_approval` pause resolution only; every other hold reason pauses nothing.
- Activation link valid 72 h, reset link 1 h.
- Synchronous Excel export up to 5,000 rows; above that a background job; files expire after 24 h.

**Success criteria** (`tgysm.md` §20 items 5, 11–14, 17)

1. SLA results stay correct across holidays, working-hour boundaries, the time zone, pauses and priority changes; a transfer never resets a timer.
2. An outbox event is committed atomically with its change; a rolled-back change leaves no event. After a worker or SMTP restart, pending work is processed; an SMTP outage never rolls back or fails the user's action.
3. Two worker instances never send the same delivery at the same time; one warning and one breach event at most per SLA instance.
4. Tokens, passwords and SMTP secrets never appear in logs, audit, API responses or stored email bodies after sending.
5. Internal and draft articles are invisible to employees in search, lists, ticket links and direct URLs (404).
6. A report figure, its drill-down list and its Excel export return the same rows for the same filters and scope; IT staff never reach tickets outside their teams through any of them.
7. Unit, integration (real PostgreSQL 17) and E2E tests pass; line coverage ≥ 80 % for Domain, Application and frontend `src`; zero warnings.

## 2. Worker (A)

- New project `backend/src/Tgysm.Worker` (Worker SDK) referencing Application and Infrastructure, never Api (architecture test).
- Jobs, each a `BackgroundService` on a `PeriodicTimer`: `OutboxDispatcher`, `EmailSender`, `SlaMonitor`, `ExportRunner`, `Cleanup`. Intervals from `Worker:<Job>:IntervalSeconds`; a non-positive value is clamped to the default and logged.
- One DI scope and one `DbContext` per unit of work. Failures are logged and recorded; the loop keeps running.
- Claiming: `SELECT … FOR UPDATE SKIP LOCKED LIMIT n` inside a short transaction sets `locked_until = now + lease`; work runs outside that transaction; completion is a conditional update on the lease owner. A crashed worker's rows become claimable when the lease expires.
- `worker_heartbeats` (`job`, `instance`, `last_run_at`, `last_error`): shown on `/admin/system`. Health endpoint on the API reports stale jobs.
- `tools/run.sh` starts the worker next to the API and Vite; `tools/e2e.sh` starts it against the E2E database.

## 3. Outbox and email (B)

### 3.1 Data

| Table | Columns | Rules |
|---|---|---|
| `notification_outbox` | `event_type`, `ticket_id`, `user_id`, `payload` (jsonb, ids and reason codes only), `protected_secret` (Data Protection–encrypted, account links only), `created_at`, `status` (`pending`, `dispatched`, `skipped`), `locked_until`, `dispatched_at` | written in the handler's transaction |
| `email_deliveries` | `outbox_id`, `recipient_user_id`, `recipient_email`, `template_type`, `subject`, `body`, `status` (`pending`, `sending`, `sent`, `failed`), `attempts`, `next_attempt_at`, `locked_until`, `last_error` (sanitized, ≤ 500), `sent_at` | unique (`outbox_id`, `recipient_user_id`) |
| `email_templates` | `event_type` (unique), `subject`, `body`, `updated_by`, `updated_at`, `xmin` | seeded by migration |

### 3.2 Flow

1. A handler that changes a ticket or account adds an outbox event through `INotificationOutbox` before its single save.
2. `OutboxDispatcher` claims pending events, resolves recipients from current data, renders subject and body with the template, inserts deliveries and marks the event `dispatched` in one transaction. No recipient → `skipped`.
3. `EmailSender` claims due deliveries (`sending` with a lease), sends through `IEmailSender`, then marks `sent`. Failure: `attempts + 1`, back-off 1 min, 5 min, 15 min, 1 h, 6 h; after 5 attempts `failed`.
4. Admin `/admin/email-deliveries` lists deliveries (filters: status, event, recipient, date) and retries a `failed` one (resets attempts, audited).
5. Account-link bodies are rendered at send time from `protected_secret`; after `sent` or `failed` the secret and the stored body of that delivery are cleared (body replaced with a fixed marker).
6. At-least-once: a crash after SMTP accepted the message but before `sent` can repeat one email; the unique delivery key prevents application-level duplicates. Documented, not hidden.

### 3.3 Events and recipients

| Event | Recipients | Not sent to |
|---|---|---|
| `ticket_created` | requester | — |
| `ticket_assigned` (claim, transfer, admin assign) | new assignee; if the transfer names only a team, its active members | the actor |
| `public_reply` | requester when IT wrote it; assignee (or team) when the requester wrote it | the author |
| `approval_requested` | assigned approver | — |
| `approval_decided` | requester | — |
| `ticket_completed` | requester (user-visible resolution only) | — |
| `sla_warning`, `sla_breached` | assignee, else active team members | — |
| `account_activation`, `password_reset` | the user | — |

Inactive users never receive email. Internal notes, internal attachments and hold notes never reach a template.

### 3.4 Templates

- Plain text only; no HTML, so no markup injection. Variables `{{name}}` from a fixed list per event: `ticketNumber`, `subject`, `status`, `resolution` (completed only), `completedAt`, `ticketUrl`, `recipientName`, `actionUrl` and `expiresAt` (account events only), `slaMetric` and `dueAt` (SLA events only).
- Saving a template with an unknown or not-allowed variable fails with `email_templates.unknown_variable`. Rendering escapes nothing (plain text) and never evaluates expressions.
- Dates render in the company time zone with Turkish formatting.

### 3.5 Configuration

`Email:Mode` = `Smtp` | `Pickup`, `Email:From`, `Email:Smtp:Host|Port|UseStartTls|UserName|Password` (secret, user-secrets / environment only), `Email:PickupFolder`, `App:PublicBaseUrl` (link base). `Email:Mode` missing → worker refuses to start.

## 4. Account links (C)

- `password_tokens`: `user_id`, `purpose` (`activation`, `reset`), `token_hash` (SHA-256 of 32 random bytes, base64url), `expires_at`, `used_at`, `created_at`. Issuing a token voids unused tokens of the same user and purpose.
- `POST /admin/users` and `POST /admin/users/{id}/reset-password` take `delivery` = `email_link` (default) or `temporary_password`. `email_link` → 201/200 without a password, activation or reset event in the outbox. `temporary_password` keeps today's behavior (`Cache-Control: no-store`).
- `POST /auth/forgot-password { email }` → always 202, rate limited per IP; active users only get a reset event. No response difference reveals whether the account exists.
- `POST /auth/set-password { token, newPassword }` → 204. Conditional update `used_at IS NULL AND expires_at > now` decides; failure → 400 `auth.token_invalid` (one code for unknown, used and expired). Success sets the password, clears `must_change_password`, rotates the security stamp (ends other sessions) and audits.
- UI: `/set-password?token=…` (activation and reset) and `/forgot-password`; the token is removed from the address bar after load.
- `Cleanup` deletes tokens 30 days after expiry.

## 5. SLA (D)

### 5.1 Data

| Table | Columns | Rules |
|---|---|---|
| `business_calendar` | single row: `time_zone` (IANA), `updated_by`, `xmin` | one company calendar |
| `business_hours` | `weekday`, `starts_at`, `ends_at` (local `time`) | at most one range per weekday; empty weekday = closed |
| `holidays` | `date`, `name` | unique `date` |
| `sla_policy_versions` | `version`, `created_by`, `created_at`, `is_current` | one current (partial unique index); never edited, a change creates a new version |
| `sla_targets` | `policy_version_id`, `ticket_kind`, `priority`, `request_type_id` (nullable), `metric` (`first_response`, `resolution`), `target_minutes`, `warning_percent` | unique per version/kind/priority/type/metric |
| `sla_pause_rules` | `policy_version_id`, `hold_reason`, `pauses_first_response`, `pauses_resolution` | |
| `sla_instances` | `ticket_id`, `metric`, `policy_version_id`, `target_minutes`, `warning_percent`, `elapsed_seconds`, `running_since`, `due_at`, `warn_at`, `warned_at`, `breached_at`, `stopped_at`, `result` (`met`, `breached`, `cancelled`), `xmin` | unique (`ticket_id`, `metric`) |
| `sla_pauses` | `sla_instance_id`, `hold_reason`, `started_at`, `ended_at` | |
| `sla_events` | `sla_instance_id`, `kind` (`started`, `paused`, `resumed`, `target_changed`, `warning`, `breached`, `stopped`), `occurred_at`, `data` (jsonb: old/new target) | append-only |

Calendar and holiday changes apply to future accumulation only; elapsed time already stored is not recomputed. Calendar, holiday and policy changes are audited.

### 5.2 Calculation

- Domain `BusinessTimeCalculator` (no framework dependency; input: time zone, weekly hours, holidays): `BusinessSecondsBetween(fromUtc, toUtc)` and `AddBusinessSeconds(fromUtc, seconds)`. Converts to local time per day, clips to working ranges, skips holidays and closed days. DST handled by converting each local boundary back to UTC.
- Domain `SlaInstance` state machine: `Start`, `Pause`, `Resume`, `ChangeTarget`, `Stop(result)`. `elapsed_seconds` grows on pause and stop; `due_at` / `warn_at` are recomputed on start, resume and target change; both are `NULL` while paused.
- Storage in UTC; display in the calendar time zone.

### 5.3 Rules

- Ticket creation → both instances with the current policy version (request-type target wins over the priority target). Requests that start `on_hold` for approval start with the resolution instance paused (`manager_approval` rule).
- First response stops at the first public comment by IT staff or an admin who is not the requester. Automatic email, assignment and internal notes do not count.
- Hold/resume pauses/resumes per `sla_pause_rules` of the instance's own policy version.
- Priority change → `ChangeTarget` with the same policy version; start time and elapsed time kept; old/new target in `sla_events`; an earlier breach stays recorded.
- Transfer and claim do not touch SLA.
- Complete → stop with `met` or `breached`; cancel (incl. approval rejection) → `cancelled`; incident outcome `service_request_required` is reported separately (§7).
- `SlaMonitor` every 60 s: instances with `warn_at ≤ now AND warned_at IS NULL` (or `due_at` / `breached_at`) are marked by a conditional `ExecuteUpdate`; only the row that updated writes the `sla_events` row and the outbox event. Duplicate warnings are impossible across instances.
- Ticket list and detail gain SLA columns (remaining / overdue, paused marker). IT workspace gains the "SLA Riski" card (warned or breached, not stopped).

### 5.4 Admin

`/admin/sla`: calendar (time zone, weekly hours, holidays), current policy (targets table, pause rules) with "save as new version". `GET/PUT /admin/sla/calendar`, `GET/POST/DELETE /admin/sla/holidays`, `GET /admin/sla/policy`, `POST /admin/sla/policy` (new version).

## 6. Knowledge base (E)

### 6.1 Data

| Table | Columns | Rules |
|---|---|---|
| `knowledge_articles` | `title` (≤ 200), `summary` (≤ 500), `body` (Markdown, ≤ 50,000), `category_id` (existing `categories`), `visibility` (`employees`, `internal`), `status` (`draft`, `published`, `archived`), `author_id`, `published_at`, `updated_by`, `updated_at`, `search_text`, `xmin` | trigram GIN index on `search_text` (title + summary + body + category, `TurkishText.ToSearchKey`) |
| `article_revisions` | `article_id`, `revision`, `title`, `summary`, `body`, `visibility`, `category_id`, `edited_by`, `edited_at` | append-only, one per save |
| `ticket_articles` | `ticket_id`, `article_id`, `linked_by`, `linked_at` | unique pair |

### 6.2 Rules

- IT staff and admins create, edit, publish, archive and restore (archived → draft). Publish, archive and visibility changes are audited.
- Employees see only `published` + `employees`; everything else is 404, including direct URLs, search and ticket links.
- Ticket links: IT links an article to a ticket in scope. The employee view of the ticket lists only links to articles visible to employees; internal links stay IT-only. The public-reply "insert article" picker offers employee-visible articles only, and the backend refuses a public reply that references an internal article id (`knowledge.internal_article_in_public_reply`).
- Markdown rendered in the browser without raw HTML (`react-markdown`, no `rehype-raw`); links open with `rel="noopener noreferrer"`.

### 6.3 API and UI

- `GET /kb/articles` (`search`, `categoryId`, `status` and `visibility` for IT, paging), `GET /kb/articles/{id}`, `POST /kb/articles`, `PUT /kb/articles/{id}` (`xmin`), `POST /kb/articles/{id}/publish|archive|restore`, `GET /kb/articles/{id}/revisions`; `POST/DELETE /tickets/{number}/articles`.
- UI: employee home card "Nasıl Yapılır?" → `/kb`; article page `/kb/:id`; IT editor `/kb/new`, `/kb/:id/edit` with preview; revision list; article box on ticket detail.

## 7. Reports (F)

### 7.1 Scope and filters

- `ReportFilter`: date range (created or completed, by report), kind, priority, category, team, assignee, department, location, status, SLA state (`met`, `breached`, `open_breached`, `at_risk`).
- `ReportScope` applies `TicketScope` rules: admin all, IT staff tickets of their teams, employees 403. Asset and warranty reports: IT staff and admins.
- Every metric is computed from one query builder; drill-down and export reuse the same builder with the metric's own predicate, so numbers, lists and files agree.

### 7.2 Reports

| Report | Figures |
|---|---|
| Incidents | volume, first response time, resolution time, MTTR (calendar and SLA time, labeled separately), open backlog with ageing buckets (0–1, 2–3, 4–7, 8+ days), SLA compliance |
| Requests | volume, fulfilment time, approval waiting time, rejection rate, SLA compliance |
| Operations | load per team/assignee (current), distribution by category/location/department, hold reasons |
| Transfers | transfer count and rate, reassignments, incident → request relations |
| Assets | by type/status/location, active assignments, stock, maintenance/lost/retired |
| Warranty | expired, and expiring in the selected period |

- MTTR = total resolution time of completed incidents with outcome `resolved` in the period ÷ their count. SLA compliance = completed eligible tickets with `met` ÷ completed eligible tickets; cancelled, rejected and `service_request_required` are counted apart. Every ratio returns numerator and denominator; a zero denominator returns `null` and the UI shows "Veri yok".
- Historical team performance uses the team at completion (from `ticket_assignments`), separate from current load.

### 7.3 API and UI

- `GET /reports/{report}` → figures with numerator/denominator and a `metric` key per figure; `GET /reports/{report}/drilldown?metric=…&<filters>` → paged ticket or asset list.
- `GET /reports/{report}/export?<filters>` → `.xlsx` stream when ≤ 5,000 rows, else 409 `reports.export_too_large`; `POST /reports/exports` → background job; `GET /reports/exports` (mine), `GET /reports/exports/{id}/download`.
- `export_jobs`: `report`, `filters` (jsonb), `status` (`pending`, `running`, `ready`, `failed`, `expired`), `row_count`, `file_key`, `created_by`, `created_at`, `expires_at`, `locked_until`. Download: creator only, re-checks the creator's current role and scope (inactive or demoted → 404); expired → 410 `reports.export_expired`. `Cleanup` deletes expired files. Exports are audited with report and filters.
- Excel: ClosedXML; one sheet of rows plus a "Filtreler" sheet listing the filters and scope; Turkish headers from a server-side resource used only for export.
- UI `/reports` (IT, admin): report tabs, filter bar kept in the URL, figure cards and charts (Recharts, own `vendor-charts` chunk, route lazy-loaded), click a figure → drill-down list `/reports/:report/list?metric=…`, export button, "Dışa aktarımlarım" list.

## 8. Testing (G)

- Domain unit: `BusinessTimeCalculator` (holiday, weekend, before/after hours, range crossing midnight UTC, a DST zone), `SlaInstance` transitions and target change, template variable validation and rendering, token expiry rules, article visibility.
- Integration (PostgreSQL 17): outbox atomic with the change (rollback → no event); two concurrent dispatchers/senders claim disjoint rows; SMTP failure → retries → `failed` → admin retry; pickup-folder send; account link single use under a race and no user enumeration; SLA through create/hold/resume/priority/transfer/complete with a fixed `TimeProvider`; monitor dedupe with two concurrent sweeps; KB IDOR and search visibility; report figure = drill-down count = export row count per metric; export download after demotion → 404.
- Frontend: set/forgot password, template editor errors, SLA admin forms, KB editor/preview/visibility, report filters in URL, "Veri yok".
- E2E (360 and 1280 px): admin creates user → activation email read from the pickup folder → user sets password and signs in; employee finds a published article and cannot open an internal one; IT completes a ticket → completion email in the pickup folder contains the resolution and not the internal note; admin opens a report figure, drills down, exports.
- Coverage gates unchanged (≥ 80 % lines); `TranslationCoverageTests` covers new error codes, audit actions and email event names.

## 9. ADRs to add

- `0014-worker-process-and-row-leases` — separate worker host, `SKIP LOCKED` leases, heartbeats.
- `0015-transactional-outbox-and-email-delivery` — outbox + deliveries, retry policy, at-least-once, pickup folder.
- `0016-database-email-templates` — plain text, fixed variables.
- `0017-password-set-links` — hashed single-use tokens, temporary password as fallback (amends ADR-0004 / ADR-0007 flows).
- `0018-sla-calculation-model` — versioned policy, business calendar, accumulated instances.
- `0019-knowledge-base` — revisions, visibility, trigram search, Markdown without raw HTML.
- `0020-reporting-and-excel-export` — shared filter/scope builder, ClosedXML, export jobs.

## 10. Out of scope

Inbound email / reply-by-email, HTML email, per-user notification preferences, multiple calendars or per-team SLA, OLA/underpinning contracts, automatic ticket → article generation, article comments/ratings, scheduled report emails, PDF export, production SMTP provider and hosting (Phase 6), retention/anonymization jobs beyond token and export cleanup.
