# Phase 2–3 Assets and Tickets — Design

- Date: 2026-09-26
- Owner: Tugay İLTUŞ
- Requirements source: `tgysm.md` v3.0 §5–§8, §13, §16, §17, §19–§20
- Builds on: Phase 0–1 (`docs/superpowers/specs/2026-09-26-phase-0-1-foundation-design.md`), baseline commit `cfd2509`
- Scope: Phase 2 (IT inventory and asset management) and Phase 3 (incident and request management) in one spec and one plan. SLA, email, knowledge base, reports and the worker stay out (Phases 4–6).

## 1. Agreed understanding

**Owner decisions**

| Topic | Decision |
|---|---|
| Structure | One spec and one plan for Phases 2 and 3 |
| Attachments | Local folder outside the web root behind `IFileStore`; content-type check by magic bytes; `IFileScanner` port. Development scanner marks type-checked files clean. A production scanner is chosen in Phase 6 (ADR). |
| Default intake team | A team flag `is_default_intake`; exactly zero or one team has it. No category → team routing. |
| Ticket storage | One `tickets` table with nullable kind-specific columns guarded by CHECK constraints, instead of separate `IncidentDetails` / `RequestDetails` tables (ADR). |
| Language rules | Unchanged: English code, identifiers, tables, endpoints and URLs; Turkish UI text only through i18n. |
| Delivery | TDD, gates as Phase 1, specialist reviews at the end, local commits only after green gates; no push. |

**Assumptions**

- Requests also carry a priority (IT sets it); default P3 like incidents.
- Categories have at most two levels.
- An admin is the only approver role (ADR-0005); the approver is the requester's department approver.

**Success criteria** (`tgysm.md` §20 items 4–10 as they apply)

1. An employee cannot read another user's ticket, internal note, attachment or assignment by changing an ID (404, no existence leak).
2. IT staff cannot reach tickets of teams they are not in, through the list, search, detail or attachment download.
3. An unapproved request cannot be completed; nobody approves their own request; a device cannot be handed over against an unapproved request.
4. Completing a ticket closes it directly; no user confirmation step.
5. Concurrent handovers of one asset create exactly one active assignment; return and transfer history is kept.
6. CSV import reports duplicate and invalid rows, writes nothing when any row fails, and a repeated commit does not duplicate assets.
7. Ticket–asset links are visible from both sides, and access scope still applies.
8. Ticket numbers are unique under concurrent creation.
9. Phase 2–3 screens work at 360 px without horizontal scroll and by keyboard.
10. Unit, integration (real PostgreSQL 17) and E2E tests pass; line coverage ≥ 80% for Domain, Application and frontend `src`; zero warnings.

## 2. Data model

Conventions from Phase 1: snake_case, `uuid` v7, `timestamptz` UTC, `xmin` concurrency (users keep their application-managed `version`), advisory locks for cross-row rules, deactivate instead of delete.

### 2.1 Assets (Phase 2)

| Table | Columns | Constraints |
|---|---|---|
| `assets` | `asset_tag`, `normalized_asset_tag`, `type` (`desktop`, `laptop`, `phone`, `tablet`), `brand`, `model`, `serial_number`, `normalized_serial_number`, `status` (`in_stock`, `assigned`, `maintenance`, `lost`, `retired`), `location_id` (required), `department_id`, `warranty_ends_on` (date), `purchased_on` (date), `notes` (≤ 1000), `created_at`, `updated_at` | unique `normalized_asset_tag`; unique `normalized_serial_number` where not null; CHECK on `type`, `status` |
| `asset_assignments` | `asset_id`, `user_id`, `assigned_at`, `assigned_by`, `returned_at`, `returned_by`, `request_ticket_id`, `note` | unique `asset_id` where `returned_at IS NULL` (one active assignment) |
| `asset_movements` | `asset_id`, `kind`, `from_status`, `to_status`, `user_id`, `actor_user_id`, `reason`, `user_responsibility_retained`, `occurred_at` | append-only (no update path) |
| `import_jobs` | `file_name`, `status` (`previewed`, `committed`, `failed`), `total_rows`, `valid_rows`, `error_rows`, `rows` (jsonb, parsed valid rows), `errors` (jsonb), `created_by`, `created_at`, `committed_at` | commit is idempotent |

Normalization: asset tag and serial number are trimmed, inner whitespace removed, upper-cased with the invariant culture. An unknown serial number is `NULL`, never a placeholder like `YOK`.

### 2.2 Tickets (Phase 3)

| Table | Columns | Constraints |
|---|---|---|
| `categories` | `name`, `normalized_name`, `parent_id`, `applies_to` (`incident`, `request`, `both`), `is_active` | at most two levels; unique `(parent_id, normalized_name)` NULLS NOT DISTINCT |
| `request_types` | `name`, `normalized_name`, `requires_approval`, `is_active` | unique `normalized_name` |
| `teams.is_default_intake` | bool | unique where true |
| `tickets` | `number`, `kind` (`incident`, `request`), `subject`, `description`, `category_id`, `location_id`, `requester_id`, `team_id` (required), `assignee_id`, `status` (`open`, `in_progress`, `on_hold`, `completed`, `cancelled`), `hold_reason`, `impact`, `urgency`, `priority` (`p1`–`p4`), `request_type_id`, `detection`, `root_cause`, `resolution`, `outcome`, `cancel_reason`, `created_at`, `updated_at`, `completed_at` | unique `number`; CHECK: `request_type_id` required only for requests; `hold_reason` required iff `on_hold`; `resolution` required when `completed`; `cancel_reason` required when `cancelled` |
| `ticket_counters` | `kind`, `year`, `last_value` | PK `(kind, year)`; incremented with `INSERT … ON CONFLICT DO UPDATE … RETURNING` |
| `approvals` | `ticket_id`, `approver_id` (nullable), `status` (`pending`, `approved`, `rejected`, `void`), `reason`, `created_at`, `decided_at`, `decided_by` | unique `ticket_id` where `status = 'pending'` |
| `ticket_assignments` | `ticket_id`, `from_team_id`, `to_team_id`, `from_assignee_id`, `to_assignee_id`, `reason`, `note`, `actor_user_id`, `occurred_at` | append-only |
| `ticket_comments` | `ticket_id`, `author_id`, `body` (plain text ≤ 4000), `visibility` (`public`, `internal`), `created_at` | — |
| `ticket_attachments` | `ticket_id`, `comment_id`, `original_name`, `content_type`, `size_bytes`, `storage_key`, `visibility`, `scan_status` (`pending`, `clean`, `rejected`), `uploaded_by`, `created_at` | — |
| `ticket_events` | `ticket_id`, `kind`, `visibility`, `actor_user_id`, `data` (jsonb), `occurred_at` | append-only; the timeline |
| `ticket_relations` | `incident_id`, `request_id`, `created_at` | unique pair |
| `ticket_assets` | `ticket_id`, `asset_id`, `created_at` | unique pair |

Priority matrix (impact × urgency, levels `high`, `medium`, `low`):

| Impact \ Urgency | high | medium | low |
|---|---|---|---|
| high | p1 | p2 | p3 |
| medium | p2 | p3 | p4 |
| low | p3 | p4 | p4 |

## 3. Workflows

### 3.1 Asset lifecycle

| From | Operation → to |
|---|---|
| `in_stock` | assign → `assigned`; start maintenance → `maintenance`; mark lost → `lost`; retire (reason) → `retired` |
| `assigned` | return (location) → `in_stock`; transfer (new user) → `assigned` (old assignment closed, new opened, one transaction); start maintenance (`userResponsibilityRetained`; if false the assignment closes); mark lost (assignment closed, history kept); retire (reason; assignment closed) |
| `maintenance` | end maintenance → `assigned` when responsibility was retained and the assignment is still open, else `in_stock`; mark lost; retire |
| `lost` | found → `in_stock`; retire |
| `retired` | terminal |

- Every operation writes the asset, an `asset_movements` row and an audit event in one transaction.
- The assignee must be an active user. Handover against a request that requires approval is refused unless the approval is `approved`.
- Status is never edited directly; card updates change descriptive fields only.

### 3.2 CSV import

1. `GET /assets/import/template`: UTF-8 with BOM, Turkish column headers, one example row.
2. `POST /assets/import/preview` (multipart, ≤ 10 MB, ≤ 5,000 data rows): parses, validates and stores an `import_jobs` row with parsed valid rows and per-row errors.
   - Required fields, allowed type and status (`in_stock`, `maintenance`, `retired`; `assigned` and `lost` are refused — handovers use the assignment flow).
   - Location by full path ("Kampüs / Bina", Turkish-normalized, must be active and unique); department by name (optional).
   - Duplicates of asset tag or serial number inside the file and against the database.
3. `POST /assets/import/{jobId}/commit`: allowed only when `error_rows = 0`; inserts all rows in one transaction with movements and one audit event; a second commit of a committed job returns the same result without inserting.
4. CSV never updates existing assets and never creates users or assignments. Cells are read as text; nothing is evaluated.

### 3.3 Ticket lifecycle

```text
open → in_progress → completed
          ↕
       on_hold
open / in_progress / on_hold → cancelled
```

- New tickets go to the default intake team; without one, creation fails with `tickets.no_intake_team` (admin must set it).
- Employees create incidents and requests for themselves; impact and urgency default to medium (P3). IT changes impact/urgency with a reason; the old and new priority are recorded.
- `hold` requires a hold reason (`user`, `manager_approval`, `vendor`, `technical_team`, `third_party`, `scheduled_date`, `other`); `other` also requires a note.
- `complete` requires a user-visible resolution; incidents also take an outcome (`resolved` or `service_request_required`; the latter needs a related request). Requests requiring approval need an approved approval.
- `cancel` is IT/admin only, with a reason. Approval rejection cancels with outcome `approval_rejected`.
- No reopen. Completed tickets accept only additional comments.

### 3.4 Approval

1. A request whose type requires approval starts `on_hold` / `manager_approval` with a pending approval for the requester's department approver.
2. No approver, or the requester is the approver → pending approval with `approver_id` NULL; an admin reassigns it with a reason (audited).
3. Approve → ticket `open`. Reject (reason) → ticket `cancelled`, outcome `approval_rejected`.
4. IT can invalidate an approved approval after a scope change (reason) → old approval `void`, new pending approval, ticket back to `on_hold`.
5. Only the assigned approver decides; concurrent decisions: one succeeds, the other gets 409.

### 3.5 Assignment and transfer

- IT staff claim tickets of their own teams (`claim`); concurrent claims: one wins.
- Transfer changes team and/or assignee with reason (`expertise_needed`, `wrong_assignment`, `other_team_responsibility`, `authorization_needed`, `workload`, `escalation`, `location`, `other`); `other` requires a note. The assignee must be an active member of the target team.
- Every change writes `ticket_assignments`, a timeline event and audit.

### 3.6 Access scope

| Role | Tickets visible | Content |
|---|---|---|
| Employee | own (requester) | public comments, public attachments, public events |
| IT staff | own + tickets of teams they belong to | all |
| Admin | all | all |

A ticket outside scope returns 404 on every endpoint, including attachment download and relation lists. Asset history lists linked tickets only to IT staff and admins.

### 3.7 Comments, attachments, timeline

- Public replies and internal notes are separate; employees write public replies on their own tickets only.
- Attachments inherit the visibility of their comment (or public when uploaded with the ticket). Limits: 10 MB each, 5 per request. Allowed: PDF, PNG, JPEG (magic bytes checked) and TXT, CSV (valid UTF-8, no NUL). Stored under a configurable folder outside the web root with random keys.
- `IFileScanner.ScanAsync` runs after storage; only `clean` files download. The development scanner returns clean for files that passed the type check.
- Timeline events: created, assigned, transferred, status changed, public comment, internal note, priority changed, approval requested/decided/voided, related request created, asset linked/unlinked, completed, cancelled. Each has a visibility.

### 3.8 Related request from an incident

`POST /tickets/{number}/related-request`: creates a request with subject, description, requester, location and linked assets copied (no internal notes or internal files), links both tickets and starts the request's own approval.

## 4. API (all under `/api/v1`)

| Area | Endpoints |
|---|---|
| Assets (IT, admin) | `GET/POST /assets`, `GET/PUT /assets/{id}`, `POST /assets/{id}/assign`, `/return`, `/transfer`, `/maintenance/start`, `/maintenance/end`, `/lost`, `/found`, `/retire`, `GET /assets/{id}/history` |
| Import (IT, admin) | `GET /assets/import/template`, `POST /assets/import/preview`, `POST /assets/import/{jobId}/commit` |
| Own assets | `GET /me/assets` |
| Reference (signed in) | `GET /reference/categories`, `/reference/request-types`, `/reference/locations` |
| Tickets | `POST /tickets/incidents`, `POST /tickets/requests` (multipart with attachments), `GET /tickets` (`queue` = `mine`, `team`, `assigned_to_me`, `unassigned`, `on_hold`; `status`, `kind`, `priority`, `search`, paging), `GET /tickets/{number}` |
| Ticket actions | `POST /tickets/{number}/claim`, `/transfer`, `/start`, `/hold`, `/resume`, `/complete`, `/cancel`, `/priority`, `/related-request`, `POST/DELETE /tickets/{number}/assets` |
| Comments, files | `POST /tickets/{number}/comments` (multipart), `GET /tickets/{number}/attachments/{id}` |
| Approvals (admin) | `GET /approvals/mine`, `POST /approvals/{id}/approve`, `/reject`, `POST /tickets/{number}/approval/reassign`, `/invalidate` |
| Admin | `/admin/categories`, `/admin/request-types` (CRUD + deactivate/reactivate); `PUT /admin/teams/{id}` gains `isDefaultIntake` |

## 5. UI (English routes, Turkish text)

- Employee home cards: "Bir şey çalışmıyor" → `/tickets/new/incident`, "Bir şey istiyorum" → `/tickets/new/request`, "Kayıtlarım" → `/tickets`, "Zimmetlerim" → `/assets/mine`.
- Ticket detail `/tickets/:number`: header (number, subject, status, team, assignee, priority), related assets and tickets, approval box, timeline, reply/internal-note tabs with attachments, role-based actions (claim, transfer, hold/resume, related request, complete, cancel).
- IT workspace `/workspace`: cards (assigned to me, my teams, on hold, completed today) opening filtered queues `/workspace/queue?queue=…`.
- Inventory `/assets`, asset card `/assets/:id` (operations + history), import `/assets/import` (upload → preview table with row errors → commit).
- Approvals `/approvals` (admin).
- Admin: `/admin/categories` (tree), `/admin/request-types`; team form gains the intake flag.
- Same mobile-first and accessibility rules as Phase 1; row actions name their record.

## 6. Testing

- Domain unit tests: asset state machine, priority matrix, ticket state machine, attachment type sniffing, number formatting.
- Integration tests on PostgreSQL 17: every workflow rule above, IDOR and scope checks, CSV atomicity and idempotency, race tests (number generation, claim, approval decision, handover).
- Frontend tests: forms, detail actions per role, queue filters, import preview, approval decisions.
- E2E (360 and 1280 px): employee opens an incident → IT claims, writes internal note and public reply → completes → employee sees resolution but not the note; request with approval → admin approves → IT hands over an asset → employee sees it under "Zimmetlerim".
- Coverage gates unchanged (≥ 80% lines).

## 7. ADRs to add

- `0009-single-ticket-table` — one table with kind-specific columns.
- `0010-asset-lifecycle-and-assignment-consistency` — state machine, partial unique index, movements.
- `0011-ticket-numbering` — counter table per kind and year.
- `0012-attachment-storage-and-scanning` — file store port, type sniffing, scanner port, production scanner deferred.
- `0013-ticket-access-scope` — requester/team/admin scope and 404 on out-of-scope.

## 8. Out of scope

SLA timers and calendars, email notifications and outbox, worker, knowledge base, reports/export, reopen, rich text, e-signature, CSV updates of existing assets, production antivirus.
