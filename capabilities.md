# TGYSM Capabilities

## Overview

ITSM platform for one company (multiple departments and locations). Users: employees, IT staff, admins. Served as one web application; hosting target not yet decided (Phase 6). Out of scope for the first release: multi-tenancy, SSO/LDAP, native mobile app, CMDB, change/problem management (`tgysm.md` §2).

Status legend: **Available** = implemented and tested; **Planned** = later phase (`tgysm.md` §19).

| Area | Status |
|---|---|
| Local login, forced password change, `/admin` users, departments, locations, teams, audit | Available (Phase 1) |
| IT inventory and asset management (Phase 2), incidents/requests (Phase 3) | Planned |
| SLA and email (Phase 4), knowledge base and reports (Phase 5), release (Phase 6) | Planned |

## Tech stack

| Component | Choice | Decision |
|---|---|---|
| Backend | .NET 10 modular monolith, Clean Architecture (`Tgysm.Domain` → `Application` → `Infrastructure` → `Api`), Minimal APIs, FluentValidation 12 | ADR-0001 |
| Database / ORM | PostgreSQL 17, EF Core 10 + Npgsql 10, snake_case, `uuid` v7 keys; `xmin` concurrency, application-managed `version` on `users`; advisory locks for cross-row rules | ADR-0002 |
| Web | React 19, TypeScript ~5.9 (strict), Vite 8, Tailwind CSS v4, Radix UI, React Router, TanStack Query 5, react-hook-form + zod 4, i18next; types generated from OpenAPI | ADR-0003, ADR-0008 |
| Auth | Local accounts, PBKDF2 (`PasswordHasher<T>`), cookie session, antiforgery header, lockout, login rate limit | ADR-0004 |
| Roles | `employee`, `it_staff`, `admin`; one per user | ADR-0005 |
| Language | English code/URLs, Turkish UI via i18n | ADR-0006 |
| First admin | `setup-admin` CLI | ADR-0007 |
| Tests | xUnit v3 on Microsoft.Testing.Platform, NetArchTest, coverlet.MTP; Vitest + React Testing Library; Playwright (360×800, 1280×800) | ADR-0008 |
| Background worker, email | Planned (Phase 4) | — |

## API

All routes are under `/api/v1`. Errors are RFC 7807 ProblemDetails with a stable `code` and an optional `errors` map (field → codes). Unknown `/api/*` routes return 404 `common.not_found`.

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
| Teams | `GET/POST /admin/teams`, `GET/PUT /admin/teams/{id}`, `POST …/{id}/deactivate`, `…/reactivate`, `POST …/{id}/members` `{ userId }`, `DELETE …/{id}/members/{userId}` |
| Audit | `GET /admin/audit-events` (`action`, `entityType`, `entityId`, `actorUserId`, `from`, `to`, `page`, `pageSize`), newest first |

Status codes: 400 `validation.*` (field codes), 401 `auth.unauthenticated`, 403 `common.forbidden` / `auth.password_change_required` / `auth.csrf_invalid`, 404 `common.not_found`, 409 business rules and `concurrency.conflict`, 429 `auth.rate_limited`. Responses carrying a temporary password send `Cache-Control: no-store`.

## Data models

| Entity | Key fields |
|---|---|
| `users` | first/last name, email + `normalized_email` (unique, invariant lower), `search_name`, `role` (CHECK), department, location, `is_active`, `must_change_password`, `password_hash`, `security_stamp`, `access_failed_count`, `lockout_end`, `version` |
| `departments` | `name`, `normalized_name` (unique), `approver_user_id` (active admin), `is_active` |
| `locations` | `name`, `normalized_name`, `parent_id` (tree); unique `(parent_id, normalized_name)` NULLS NOT DISTINCT |
| `teams` / `team_memberships` | `name`, `normalized_name` (unique); membership unique `(team_id, user_id)` |
| `audit_events` | `occurred_at`, `actor_user_id`, `action`, `entity_type`, `entity_id`, `reason`, `old_values`/`new_values` (jsonb), `correlation_id`; triggers reject UPDATE, DELETE and TRUNCATE |

Relationships: user → department, location (optional); department → approver user; location → parent location; membership → team, user. All foreign keys are `RESTRICT`; records are deactivated, never deleted.

## Auth & permissions

- Session cookie `__Host-tgysm` (Secure, HttpOnly, SameSite=Strict, 8-hour sliding). Every request re-reads the user: inactive users and changed security stamps end the session. Role change, (de)activation, password change and admin reset rotate the stamp.
- Antiforgery token required in `X-CSRF-TOKEN` on every unsafe `/api` request, including login.
- Lockout: 5 failed logins → 15 minutes; the counter is updated atomically. Inactive and locked accounts cost the same hash check as others.
- Password: 12–128 characters with a letter and a digit. Temporary passwords: 16 random characters, shown once.
- Fallback and default authorization policies require a signed-in user whose password change is not pending; `/api/v1/admin/*` also requires the `admin` role.
- Development only: the API trusts `X-Forwarded-Proto` from the loopback Vite proxy so Secure cookies work on `http://localhost`.

## UI / pages

- `/login`, `/change-password` (forced after temporary passwords), `/` home with role-based cards, `/profile`.
- `/admin/users`, `/admin/departments`, `/admin/locations` (tree), `/admin/teams` (members dialog), `/admin/audit` (Turkish action labels, before/after details).
- Mobile-first: cards under 768 px, tables above; drawer menu under 1024 px, sidebar above; bottom-sheet dialogs on phones; no horizontal scroll at 360 px.
- Accessibility: labelled fields, errors linked with `aria-describedby`, focus moves to the first server field error, row buttons announce their record, visible focus, keyboard-only login verified by E2E.

## Features & business logic

- **First admin:** `tools/setup-admin.sh` works once; refuses when any user exists (ADR-0007).
- **Users:** email unique ignoring case; department/location must be active when newly assigned; the last active admin cannot be demoted or deactivated (serialized with an advisory lock, so two admins cannot remove each other at once); a user's own sign-ins and password change do not make an admin's open edit stale.
- **Search and names:** Turkish-aware keys (`tr-TR` lower case, dotless ı folded to i), so "IT Destek" and "it destek" are the same name and "IŞIK" finds "Işık".
- **Locations:** a location cannot move under itself or a descendant (checked under an advisory lock); a parent with active children cannot be deactivated; a child of an inactive parent cannot be reactivated.
- **Teams:** only active IT staff and admins can be members; inactive teams accept no new members.
- **Concurrency:** stale edits return 409 `concurrency.conflict`; the UI shows a Turkish message and reloads admin lists.
- **Audit:** security and admin events are written in the same transaction as the change; passwords, hashes and tokens are never recorded.

<!-- DOC-BASELINE: 512a3e5 -->
_Documentation baseline: current as of commit 512a3e5 (2026-09-26). Refresh with `git log --oneline 512a3e5..HEAD`._
