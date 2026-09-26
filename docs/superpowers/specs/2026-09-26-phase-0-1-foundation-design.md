# Phase 0–1 Foundation — Design

- Date: 2026-09-26
- Owner: Tugay İLTUŞ
- Source of requirements: `tgysm.md` v3.0 (§1, §3, §4, §13–§17, §19, §20)
- Scope: Phase 0 (design, ADRs) and Phase 1 (foundation) only. Phase 2+ (assets, tickets, SLA, email, knowledge base, reports) is out of scope.

## 1. Agreed understanding

**What the owner said**

- Build the ITSM platform described in `tgysm.md`.
- Code, identifiers, database names, API endpoints and URL/route paths are English. Only user-visible UI text is Turkish.
- Mobile-first, user-friendly React + TypeScript web app. A native mobile app comes later, not now.
- Database: PostgreSQL.
- No push to GitHub and no commits until tests pass and the owner approves. Local `git init` only.
- This session: Phase 0 + Phase 1, then stop for approval.

**Decisions taken during design**

| Topic | Decision |
|---|---|
| Dev/test database | Local Homebrew PostgreSQL 17 (no Docker on the machine) |
| ADR file names | `docs/adr/NNNN-kebab-case-title.md` (global rule; overrides `tgysm.md` §18.1 naming) |
| User guide folder | `docs/user-guides/` (global rule; overrides `tgysm.md` §18.2) |
| First admin | CLI command, no HTTP setup path |
| UI kit | Tailwind CSS v4 + Radix UI primitives |
| Use-case pattern | Plain handler classes + FluentValidation (no MediatR) |
| Frontend data/forms | TanStack Query, React Router, react-hook-form + zod, i18next |
| Namespace | `Tgysm.*` |

**Assumptions (not stated by the owner)**

- The owner and the target institution use Europe/Istanbul time. Timestamps are stored in UTC and displayed in `tr-TR` format.
- Phase 1 has no email. Admin-driven password reset replaces "forgot password" until Phase 4.

**Success criteria for Phase 1** (`tgysm.md` §20 items 1, 2, 3, 4, 15, 16, 17 as they apply to Phase 1)

1. Anonymous users cannot create accounts. Employee and IT staff users get 403 from every `/api/v1/admin/*` endpoint.
2. After the first admin exists, `setup-admin` refuses to run. No HTTP setup path exists.
3. First login forces a password change. Deactivating a user invalidates their session on the next request.
4. A non-admin cannot read another user's data by changing an ID.
5. Turkish characters, missing i18n keys and suffix helpers are verified by tests.
6. Phase 1 screens work at 360 px width without horizontal scroll and are keyboard-usable.
7. Unit, integration (real PostgreSQL) and E2E tests pass. Line coverage is 80% or more for Domain, Application and frontend `src`.
8. Build and lint pass with zero warnings.

## 2. Architecture and repository layout

```
TGYSM/
  backend/
    Tgysm.slnx
    Directory.Build.props      TreatWarningsAsErrors, Nullable, net10.0
    Directory.Packages.props   central package versions
    src/
      Tgysm.Domain/            entities, business rules, state transitions; no EF/HTTP dependency
      Tgysm.Application/       use-case handlers + FluentValidation, ports, access scope
      Tgysm.Infrastructure/    EF Core + Npgsql, Identity stores, migrations
      Tgysm.Api/               Minimal APIs, authentication, policies, ProblemDetails, composition root, setup-admin CLI
    tests/
      Tgysm.Domain.Tests/
      Tgysm.Application.Tests/
      Tgysm.Api.IntegrationTests/   WebApplicationFactory + local PostgreSQL 17
      Tgysm.ArchitectureTests/      layer dependency rules (NetArchTest)
  frontend/                    Vite + React + TypeScript
  docs/adr/  docs/user-guides/
  tools/
  CLAUDE.md  README.md  capabilities.md  SECURITY.md
```

- Dependency direction: Domain has no project references. Application references Domain. Infrastructure references Application and Domain. Api references all. Architecture tests enforce this.
- The API does not use `DbContext` directly for business data. Endpoints call Application handlers.
- No Worker project in Phase 1. It arrives with the outbox and SLA work in Phase 4.
- Same-origin deployment. In development, Vite proxies `/api` to the API. In production, the API serves the built SPA.
- API routes: `/api/v1/...`. SPA routes: `/login`, `/change-password`, `/profile`, `/admin/users`, `/admin/departments`, `/admin/locations`, `/admin/teams`, `/admin/audit`.
- Error contract: RFC 7807 ProblemDetails with a stable `code` extension (for example `auth.invalid_credentials`, `users.email_taken`, `concurrency.conflict`, `validation.failed`). Validation errors also carry per-field codes. The frontend maps codes to Turkish text.

## 3. Data model (Phase 1)

Conventions: snake_case names, `uuid` v7 primary keys, `timestamptz` in UTC, PostgreSQL `xmin` as the concurrency token on mutable tables. A concurrency conflict returns 409 `concurrency.conflict`.

| Table | Key columns | Constraints |
|---|---|---|
| `users` | first_name, last_name, email, normalized_email, search_name, role, department_id, location_id, is_active, must_change_password, password_hash, security_stamp, access_failed_count, lockout_end, created_at, updated_at | unique `normalized_email`; `role` CHECK in (`employee`, `it_staff`, `admin`); FKs to departments and locations |
| `departments` | name, is_active, approver_user_id (nullable; used by approvals in Phase 3) | unique `name` |
| `locations` | name, parent_id (self FK), is_active | unique (`parent_id`, `name`), NULLS NOT DISTINCT |
| `teams` | name, is_active | unique `name` |
| `team_memberships` | team_id, user_id, created_at | unique (`team_id`, `user_id`) |
| `audit_events` | occurred_at, actor_user_id, action, entity_type, entity_id, reason, old_values jsonb, new_values jsonb, correlation_id | trigger rejects UPDATE and DELETE (append-only) |

- One role per user, stored in `users.role`. No Identity role tables. `tgysm.md` §4 defines three fixed roles and no direct permissions.
- Reference data and users are deactivated, never deleted.
- A location cannot become its own ancestor.
- Turkish-aware search: `search_name` is written by the application in lower case with the `tr-TR` culture. Search terms are normalized the same way before a `LIKE` match.

## 4. Authentication and authorization

- ASP.NET Core Identity via `AddIdentityCore<User>` with cookie authentication.
- Cookie: `__Host-tgysm`, Secure, HttpOnly, SameSite=Strict.
- Endpoints: `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me`, `POST /api/v1/auth/change-password`, `GET /api/v1/auth/csrf`.
- CSRF: antiforgery token. Every request except GET, HEAD and OPTIONS must send the `X-CSRF-TOKEN` header.
- Brute force: lockout after 5 failed attempts for 15 minutes. The login endpoint also has a per-IP rate limit. Error responses do not reveal whether an account exists.
- Password policy: at least 12 characters, with at least one letter and one digit.
- While `must_change_password` is true, only `me`, `change-password` and `logout` are allowed. Other endpoints return 403 `auth.password_change_required`.
- Session revocation: role change, deactivation and password reset rotate `security_stamp`. The stamp and `is_active` are validated on every request.
- The last active admin cannot be deactivated or demoted (`users.last_admin`).
- Policies: `Admin` for `/api/v1/admin/*`. `Authenticated` for the rest. Hiding UI is not authorization.
- List endpoints page on the server: `page`, `pageSize` (max 100), `sort`, `search`, `isActive`.

**First admin**

- Command: `tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>` runs the `setup-admin` command of `Tgysm.Api`.
- If any user exists, the command fails with a non-zero exit code.
- The command prints a temporary password once. The password must be changed at first login.

**Admin user management (Phase 1)**

- Create a user: the temporary password is shown once in the UI.
- Edit a user, change the role, deactivate or reactivate.
- Reset password: generates a new temporary password and sets `must_change_password`.

## 5. Audit

- Events: login succeeded, login failed, lockout, password changed, password reset. User created, user updated, role changed, user deactivated or reactivated. Department, location, team and membership create/update/deactivate.
- Each event has: actor, action, entity type and id, UTC time, reason (when given), old/new values where relevant, and a correlation id.
- Audit rows are written in the same transaction as the change.
- Passwords, tokens and hashes are never written to audit rows or logs.
- Admins read audit rows. Nobody can edit them.

## 6. Frontend

- React 19, TypeScript strict, Vite, Tailwind CSS v4, Radix UI, React Router, TanStack Query, react-hook-form + zod, i18next.
- API types are generated from OpenAPI with `openapi-typescript` (`tools/gen-api-types.sh`). No hand-written DTOs.
- Folders: `src/features/{auth,admin,home,profile}`, `src/shared/{api,ui,i18n,lib}`.
- ESLint setup and chunk groups follow `~/.claude/guides/react.md`.

**Mobile-first**

- The admin area uses a top bar with a Radix drawer menu on small screens. From 1024 px, a fixed sidebar is shown.
- Lists are cards on small screens and tables on wide screens. Filters live in a drawer.
- No horizontal scroll at 360 px.
- Every screen has loading, empty, error and success states. Submit buttons lock while a request runs, to prevent double submits.
- Visible focus and keyboard access on all controls. Form errors link to their field with `aria-describedby`.

**Phase 1 screens**

- Login and forced password change.
- Role-based home page showing the user's name and the areas available to them. Employee and IT modules come in later phases; no placeholder "coming soon" cards.
- Profile: view own data and change password.
- Admin: Users (list, create, edit, deactivate/reactivate, reset password), Departments, Locations (tree), Teams and memberships, Audit (read-only).

## 7. i18n and Turkish language helpers

- Resource files: `src/shared/i18n/locales/tr/*.json`. Keys are typed through i18next `CustomTypeOptions`, so a missing key fails the TypeScript build.
- The backend exports its error codes to `error-codes.json`. A frontend test checks that every code has a Turkish translation.
- Dates and numbers use `Intl` with `tr-TR`.
- `src/shared/lib/turkish.ts`:
  - `trUpper` and `trLower` for correct I/İ/ı/i handling.
  - `withSuffix(word, kind)` supports two-way and four-way vowel harmony, buffer consonants (`y`, `n`, `s`) and `d/t` assimilation. An exception dictionary handles abbreviations and foreign names.
  - Test examples: `Ankara'ya`, `İzmir'e`, `tabletler`, `telefonlar`.
- Where a suffix could be wrong, the UI uses a safe label form instead (for example `Atanan kişi: …`).

## 8. Testing

- **Domain and Application unit tests:** xUnit.
- **Integration tests:** WebApplicationFactory with local PostgreSQL 17.
  - Each run creates a `tgysm_test_<random>` database and applies migrations.
  - Respawn resets data between tests. The database is dropped at the end.
  - Coverage: success criteria 1–4, lockout, CSRF rejection, last-admin guard, 409 on concurrency conflict, audit UPDATE rejected by the trigger, `setup-admin` refusal.
- **Architecture tests:** NetArchTest checks the layer dependency rules.
- **Frontend tests:** Vitest + React Testing Library for form validation, role-based routing, error-code mapping and the Turkish helpers.
- **E2E:** Playwright with two viewports, 360×800 and 1280×800.
  - Flow: first admin → login → forced password change → create user → new user logs in → admin page denied → admin deactivates the user → the user's session ends.
- **Coverage target:** 80% line coverage for Domain, Application and frontend `src`. Measured with coverlet and v8.
- Each security guard is shown to fail without its fix at least once (remove guard, see red, restore).

## 9. Tools

All repeatable operations run through `tools/` scripts (`#!/usr/bin/env bash`, `set -euo pipefail`).

| Script | Purpose |
|---|---|
| `db-up.sh` | Check PostgreSQL 17 is running; create the dev role and database if missing |
| `migrate.sh` | Apply EF Core migrations to the dev database |
| `setup-admin.sh` | Create the first admin through the CLI command |
| `gen-api-types.sh` | Export OpenAPI and generate frontend types |
| `lint.sh` | `dotnet format --verify-no-changes` + `eslint --max-warnings=0` |
| `test.sh` | Backend and frontend tests |
| `e2e.sh` | Playwright E2E |
| `build.sh` | Lint + tests, then build both parts; print chunk sizes |
| `run.sh` | Start API and Vite together |

Secrets: the connection string lives in `dotnet user-secrets` or environment variables. Only `.env.example` is in the repository.

## 10. Phase 0 ADRs

Template `~/.claude/templates/adr-template.md`. Status `accepted`, Deciders `Tugay İLTUŞ`.

1. `0001-modular-monolith-clean-architecture`
2. `0002-postgresql-ef-core`
3. `0003-single-spa-with-admin-area`
4. `0004-local-identity-cookie-csrf`
5. `0005-fixed-roles-single-role-per-user`
6. `0006-english-code-turkish-ui-i18n`
7. `0007-cli-first-admin-setup`
8. `0008-frontend-stack`

## 11. Out of scope for this session

- Phase 2+ features: assets, CSV import, tickets, approvals, SLA, email, knowledge base, reports, Worker.
- Native mobile app.
- Commits and pushes. Local `git init` only.
