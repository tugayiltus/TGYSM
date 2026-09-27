# TGYSM Capabilities

## Overview

ITSM platform for one company (multiple departments and locations). Users: employees, IT staff, admins. Served as one web application; hosting target not yet decided (Phase 6). Out of scope for the first release: multi-tenancy, SSO/LDAP, native mobile app, CMDB, change/problem management (`tgysm.md` §2).

Status legend: **Available** = implemented and tested; **Planned** = later phase (`tgysm.md` §19).

| Area | Status |
|---|---|
| Local login, forced password change, `/admin` users, departments, locations, teams, audit | Available (Phase 1) |
| IT inventory: assets, lifecycle, assignments, CSV import, "my assets" | Available (Phase 2) |
| Incidents and requests: intake, queues, assignment, approvals, comments/files, timeline, related requests, ticket–asset links | Available (Phase 3) |
| SLA and email (Phase 4), knowledge base and reports (Phase 5), release (Phase 6) | Planned |

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
| Background worker, email | Planned (Phase 4) | — |

## API

All routes are under `/api/v1`. Errors are RFC 7807 ProblemDetails with a stable `code` and an optional `errors` map (field → codes). Unknown `/api/*` routes return 404 `common.not_found`. Every unsafe request needs the `X-CSRF-TOKEN` header, multipart uploads included.

### Auth

| Method | Path | Behavior |
|---|---|---|
| GET | `/auth/csrf` | Anonymous. Returns `{ token }` and sets the antiforgery cookie. |
| POST | `/auth/login` | Anonymous, rate limited per IP (`Security:LoginPermitLimitPerMinute`, default 10). `{ email, password }` → `{ mustChangePassword }` and session cookie. Every failure: 401 `auth.invalid_credentials`. |
| POST | `/auth/logout` | Ends the session (204). |
| GET | `/auth/me` | Current user (`MeDto`). Allowed while a password change is pending. |
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

Relationships: all foreign keys are `RESTRICT`; records are deactivated or closed, never deleted.

## Auth & permissions

- Session cookie `__Host-tgysm` (Secure, HttpOnly, SameSite=Strict, 8-hour sliding). Every request re-reads the user: inactive users and changed security stamps end the session. Role change, (de)activation, password change and admin reset rotate the stamp.
- Antiforgery token required in `X-CSRF-TOKEN` on every unsafe `/api` request, including login and uploads.
- Lockout: 5 failed logins → 15 minutes; the counter is updated atomically. Inactive and locked accounts cost the same hash check as others.
- Password: 12–128 characters with a letter and a digit. Temporary passwords: 16 random characters, shown once.
- Fallback and default authorization policies require a signed-in user whose password change is not pending; `/admin/*` and `/approvals/*` need `admin`; `/assets/*` (except `/me/assets`), `/reference/users` and `/reference/teams` need IT staff or admin.
- Ticket scope (ADR-0013): employees see their own tickets and public content only; IT staff see their own tickets and their teams' tickets with internal content; admins see all. Work actions need admin or membership in the ticket's team. Out-of-scope numbers and ids return 404 everywhere, including attachments.
- Development only: the API trusts `X-Forwarded-Proto` from the loopback Vite proxy so Secure cookies work on `http://localhost`.

## UI / pages

- `/login`, `/change-password`, `/` home with role-based cards ("Bir şey çalışmıyor", "Bir şey istiyorum", "Kayıtlarım", "Zimmetlerim", IT: "İş listem", "Envanter", admin: "Onaylar", "Yönetim paneli"), `/profile`.
- Tickets: `/tickets` (my tickets), `/tickets/new/incident`, `/tickets/new/request` (approval hint, files uploaded right after creation), `/tickets/:number` (details, approval box, actions, linked assets and related tickets, replies/internal notes with files, timeline).
- IT: `/workspace` (queue cards with counts) → `/workspace/queue?queue=…`; `/assets`, `/assets/:id` (card, allowed operations only, history), `/assets/import` (template, preview with row errors, commit).
- `/assets/mine` for everyone; `/approvals` for admins (approve, reject with reason, reassign unassigned).
- Admin: `/admin/users`, `/admin/departments`, `/admin/locations` (tree), `/admin/teams` (members, intake flag), `/admin/categories` (tree), `/admin/request-types`, `/admin/audit`.
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
- **Audit:** security, admin, asset and ticket events are written in the same transaction as the change; passwords, hashes and tokens are never recorded.

<!-- DOC-BASELINE: dcdb41f -->
_Documentation baseline: current as of commit dcdb41f (2026-09-27). Refresh with `git log --oneline dcdb41f..HEAD`._
