# Phase 0–1 Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Phase 0 (ADRs, project docs) and Phase 1 (foundation) of the TGYSM ITSM platform: layered .NET 10 backend on PostgreSQL, first-admin CLI, local login with forced password change, `/admin` management of users, departments, locations, teams and audit, and a mobile-first Turkish React UI.

**Architecture:** Modular monolith with Clean Architecture layers (`Tgysm.Domain` → `Tgysm.Application` → `Tgysm.Infrastructure` → `Tgysm.Api`). Minimal API endpoints call plain Application handler classes; EF Core + Npgsql persist to PostgreSQL with snake_case names and `xmin` concurrency. One React SPA (Vite) serves both the user area and `/admin`, same-origin with the API, cookie session + antiforgery header.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10 + Npgsql 10, EFCore.NamingConventions, FluentValidation 12, `PasswordHasher<T>` from ASP.NET Core Identity, xUnit v3, NetArchTest, PostgreSQL 17 (local Homebrew). React 19, TypeScript ~6.0, Vite 8, Tailwind CSS v4, Radix UI (`radix-ui`), React Router, TanStack Query 5, react-hook-form + zod 4, i18next, openapi-typescript + openapi-fetch, Vitest + React Testing Library, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-26-phase-0-1-foundation-design.md` (requirements source: `tgysm.md` v3.0).

## Global Constraints

- Code, identifiers, file names, database tables/columns, API endpoints and URL/route paths are **English**. Only user-visible UI text is **Turkish**, and it comes from i18n resource files, never hard-coded in components.
- API routes live under `/api/v1/...`. SPA routes: `/login`, `/change-password`, `/profile`, `/admin/users`, `/admin/departments`, `/admin/locations`, `/admin/teams`, `/admin/audit`.
- Database: PostgreSQL 17 (local Homebrew, no Docker). snake_case names, `uuid` v7 keys, `timestamptz` UTC, `xmin` concurrency token → 409 `concurrency.conflict`.
- `TreatWarningsAsErrors=true`, `Nullable=enable` for every C# project (`backend/Directory.Build.props`). No `NoWarn`, no `#pragma warning disable`.
- ESLint: config per `~/.claude/guides/react.md`, `--max-warnings=0`, no `eslint-disable` comments. Pin `eslint@^9`, `@eslint/js@^9`, `typescript@~6.0.3` (typescript-eslint 8.70 supports `<6.1.0`).
- No magic numbers or strings: every non-obvious literal is a named constant.
- Errors: RFC 7807 ProblemDetails with a stable `code` extension and optional `errors` map (field → list of codes). The frontend translates codes via `errors.json`.
- Password policy: 12–128 characters, at least one letter and one digit. Lockout: 5 failed attempts → 15 minutes. Login error never reveals whether an account exists or is locked.
- Session cookie `__Host-tgysm` (Secure, HttpOnly, SameSite=Strict). Antiforgery header `X-CSRF-TOKEN` on every non-GET/HEAD/OPTIONS/TRACE `/api` request.
- All repeatable operations run through `tools/*.sh` (`#!/usr/bin/env bash`, `set -euo pipefail`, `cd` to repo root, `chmod +x`).
- **No commits and no pushes.** The repository is `git init`-ed only. Every task ends with a checkpoint (`git status --short`), not a commit. The owner approves commits after all tests pass.
- Owner and ADR decider: Tugay İLTUŞ. No AI attribution in any file.
- Scope: Phase 0 + Phase 1 only. No assets, tickets, SLA, email, knowledge base, reports, Worker project, or mobile app.

## Review Focus

Inputs the spec implies but no feature test would naturally hit. Each has a pinned test in the owning task.

1. **Turkish-cased duplicates and search** — `Ali@Example.com` vs `ali@example.com` is one account; "İlker IŞIK" is found by `ilker ışık` and `IŞIK`; department "İnsan Kaynakları" vs "insan kaynakları" is a duplicate. Tests: Task 9 `Create_with_email_differing_only_in_case_returns_email_taken`, `Search_is_turkish_case_insensitive`; Task 10 `Department_name_is_unique_ignoring_turkish_case`.
2. **Admin demotes or deactivates themselves as the last admin** — must return 409 `users.last_admin`, never leave the system without an admin. Test: Task 9 `Last_admin_cannot_demote_or_deactivate_self`.
3. **Double-submitted create (button spam, retried request)** — exactly one user is created; the second request gets 409 `users.email_taken`. Test: Task 9 `Parallel_duplicate_create_creates_one_user`.
4. **Two admins edit the same record** — the stale write gets 409 `concurrency.conflict` and the UI shows a Turkish message. Tests: Task 9 `Update_with_stale_version_returns_conflict`; Task 14 `shows conflict message when the user was changed elsewhere`.
5. **A deactivated department/location is chosen for a user** — 409 `common.reference_inactive`, the user is not created. Test: Task 9 `Create_with_inactive_department_returns_reference_inactive`.

---

## File Map

```
TGYSM/
  .gitignore
  CLAUDE.md  README.md  capabilities.md  SECURITY.md
  docs/adr/README.md, 0001…0008-*.md
  docs/user-guides/README.md, getting-started.md, troubleshooting.md, PROGRESS.md
  tools/  lint.sh test.sh build.sh db-up.sh migrate.sh setup-admin.sh gen-api-types.sh run.sh e2e.sh
  backend/
    Tgysm.slnx  Directory.Build.props  Directory.Packages.props  dotnet-tools.json
    src/Tgysm.Domain/          AssemblyMarker.cs
      Common/                  Entity.cs DomainException.cs ErrorCodes.cs TurkishText.cs
      Users/                   User.cs UserRole.cs UserRoleNames.cs
      Organization/            Department.cs Location.cs Team.cs TeamMembership.cs
      Audit/                   AuditEvent.cs AuditActions.cs AuditEntityTypes.cs
    src/Tgysm.Application/     AssemblyMarker.cs DependencyInjection.cs
      Abstractions/            IAppDbContext.cs IPasswordService.cs ITemporaryPasswordGenerator.cs ICurrentActor.cs IAuditLog.cs
      Common/                  ConcurrencyException.cs NotFoundException.cs AuthenticationFailedException.cs SaveChangesExtensions.cs
      Common/Paging/           PageRequest.cs PagedResult.cs QueryablePagingExtensions.cs
      Security/                PasswordRules.cs TemporaryPasswordGenerator.cs
      Audit/                   AuditLog.cs
      Features/Setup/          SetupFirstAdmin.cs
      Features/Auth/           SessionUser.cs Login.cs GetSession.cs GetMe.cs ChangePassword.cs
      Features/Users/          UserRules.cs UserDtos.cs UserMapping.cs LastAdminGuard.cs ListUsers.cs GetUser.cs CreateUser.cs UpdateUser.cs SetUserActive.cs ResetUserPassword.cs
      Features/Organization/   OrganizationReferences.cs
      Features/Organization/Departments/  DepartmentItem.cs DepartmentQueries.cs ListDepartments.cs CreateDepartment.cs UpdateDepartment.cs SetDepartmentActive.cs
      Features/Organization/Locations/    LocationItem.cs ListLocations.cs CreateLocation.cs UpdateLocation.cs SetLocationActive.cs
      Features/Organization/Teams/        TeamDtos.cs TeamQueries.cs ListTeams.cs GetTeam.cs CreateTeam.cs UpdateTeam.cs SetTeamActive.cs AddTeamMember.cs RemoveTeamMember.cs
      Features/Audit/          ListAuditEvents.cs
    src/Tgysm.Infrastructure/  AssemblyMarker.cs DependencyInjection.cs
      Persistence/             AppDbContext.cs AppDbContextFactory.cs
      Persistence/Configurations/  EntityConfigurationExtensions.cs UserConfiguration.cs DepartmentConfiguration.cs LocationConfiguration.cs TeamConfiguration.cs TeamMembershipConfiguration.cs AuditEventConfiguration.cs
      Persistence/Converters/  UserRoleConversion.cs
      Persistence/Migrations/  (generated) + AuditAppendOnly edit
      Security/                PasswordService.cs
    src/Tgysm.Api/             Program.cs AssemblyMarker.cs ApiServiceCollectionExtensions.cs ApiApplicationExtensions.cs appsettings.json appsettings.Development.json Properties/launchSettings.json
      Cli/                     SetupAdminCommand.cs
      Http/                    ApiRoutes.cs ApiProblem.cs ApiExceptionHandler.cs ValidationErrorMapper.cs CorrelationIdMiddleware.cs HttpCurrentActor.cs QueryParsing.cs
      Security/                SecurityOptions.cs SessionClaims.cs SessionValidator.cs Policies.cs PasswordChangedRequirement.cs ApiAuthorizationResultHandler.cs CsrfValidationMiddleware.cs RateLimitPolicies.cs
      Contracts/               AuthContracts.cs AdminContracts.cs
      Endpoints/               AuthEndpoints.cs
      Endpoints/Admin/         AdminEndpoints.cs UserEndpoints.cs DepartmentEndpoints.cs LocationEndpoints.cs TeamEndpoints.cs AuditEndpoints.cs
    tests/Tgysm.Domain.Tests/          TurkishTextTests.cs UserTests.cs OrganizationTests.cs
    tests/Tgysm.Application.Tests/     PageRequestTests.cs PasswordRulesTests.cs TemporaryPasswordGeneratorTests.cs TranslationCoverageTests.cs RepositoryPaths.cs
    tests/Tgysm.ArchitectureTests/     LayerDependencyTests.cs
    tests/Tgysm.Api.IntegrationTests/
      Infrastructure/          PostgresDatabase.cs DatabaseCollection.cs ApiFactory.cs ApiCollection.cs ApiClient.cs TestUsers.cs
      Persistence/PersistenceTests.cs  Security/PasswordServiceTests.cs  Setup/SetupFirstAdminTests.cs
      Auth/LoginTests.cs RateLimitTests.cs ChangePasswordTests.cs SessionRevocationTests.cs
      Admin/UserAdminTests.cs DepartmentAdminTests.cs LocationAdminTests.cs TeamAdminTests.cs AuditAdminTests.cs
      DependencyInjectionTests.cs
  frontend/
    package.json vite.config.ts tsconfig.json tsconfig.app.json tsconfig.node.json eslint.config.js index.html playwright.config.ts
    openapi/Tgysm.Api.json (generated)
    e2e/admin-onboarding.spec.ts e2e/helpers.ts
    src/main.tsx index.css vite-env.d.ts
    src/app/App.tsx router.tsx queryClient.ts
    src/test/setup.ts mockApi.ts renderRoute.tsx
    src/shared/routes.ts
    src/shared/i18n/index.ts i18next.d.ts locales/tr/{common,auth,admin,errors}.json
    src/shared/lib/turkish.ts format.ts useDebouncedValue.ts
    src/shared/api/schema.d.ts (generated) client.ts errors.ts errorMessage.ts
    src/shared/ui/Button.tsx TextField.tsx SelectField.tsx FormAlert.tsx Dialog.tsx Sheet.tsx ConfirmDialog.tsx States.tsx StatusBadge.tsx Pagination.tsx ResponsiveList.tsx TemporaryPasswordDialog.tsx AppShell.tsx NotFoundPage.tsx
    src/features/auth/api.ts session.ts guards.tsx passwordSchema.ts LoginPage.tsx ChangePasswordPage.tsx
    src/features/home/HomePage.tsx
    src/features/profile/ProfilePage.tsx
    src/features/admin/AdminLayout.tsx
    src/features/admin/shared/referenceOptions.ts
    src/features/admin/users/api.ts userSchema.ts UsersPage.tsx UserFormDialog.tsx
    src/features/admin/departments/api.ts DepartmentsPage.tsx DepartmentFormDialog.tsx
    src/features/admin/locations/api.ts locationTree.ts LocationsPage.tsx LocationFormDialog.tsx
    src/features/admin/teams/api.ts TeamsPage.tsx TeamFormDialog.tsx TeamMembersDialog.tsx
    src/features/admin/audit/api.ts AuditPage.tsx
```

Task order and dependencies: 1 (docs) → 2 (scaffold) → 3 (domain) → 4 (persistence) → 5 (application core) → 6 (first admin CLI) → 7 (HTTP auth) → 8 (password change, revocation) → 9 (users API) → 10 (departments, locations API) → 11 (teams, audit API) → 12 (frontend scaffold, i18n, OpenAPI types) → 13 (auth UI) → 14 (admin shell + users UI) → 15 (org + audit UI) → 16 (E2E, final gates).

---

### Task 1: Phase 0 — ADRs and project documents

**Files:**
- Create: `.gitignore`
- Create: `docs/adr/README.md`, `docs/adr/0001-modular-monolith-clean-architecture.md`, `docs/adr/0002-postgresql-ef-core.md`, `docs/adr/0003-single-spa-with-admin-area.md`, `docs/adr/0004-local-identity-cookie-csrf.md`, `docs/adr/0005-fixed-roles-single-role-per-user.md`, `docs/adr/0006-english-code-turkish-ui-i18n.md`, `docs/adr/0007-cli-first-admin-setup.md`, `docs/adr/0008-frontend-stack.md`
- Create: `CLAUDE.md`, `README.md`, `capabilities.md`, `SECURITY.md`
- Create: `docs/user-guides/README.md`, `docs/user-guides/getting-started.md`, `docs/user-guides/troubleshooting.md`, `docs/user-guides/PROGRESS.md`

**Interfaces:**
- Consumes: spec §1–§11.
- Produces: ADR numbers 0001–0008 that later code comments and docs cite as `(ADR-NNNN)`.

- [ ] **Step 1: Create `.gitignore`**

```gitignore
# .NET
bin/
obj/
TestResults/
*.user
.vs/

# Node / frontend
node_modules/
dist/
coverage/
playwright-report/
test-results/

# Local environment and tool data
.env
.env.*
!.env.example
.idea/
.DS_Store
.memsearch/
```

- [ ] **Step 2: Create `docs/adr/0001-modular-monolith-clean-architecture.md`**

```markdown
# ADR-0001: Modular Monolith with Clean Architecture Layers

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

TGYSM is an ITSM platform for one company: incident and request management, IT inventory and asset management, SLA, email notifications, knowledge base and reporting (`tgysm.md` §1). The team size and hosting target are unknown. Business rules (approval, single active asset assignment, SLA pause rules) must stay testable without HTTP or a database, and a background worker (Phase 4) must reuse the same rules.

## Decision

Build one deployable backend as a modular monolith on .NET 10 with four layers:

- `Tgysm.Domain` — entities, business rules and state transitions. No EF Core, ASP.NET Core or HTTP dependency.
- `Tgysm.Application` — use-case handlers (one class per use case, plain `HandleAsync` method) with FluentValidation, ports (`IAppDbContext`, `IPasswordService`, `IAuditLog`, `ICurrentActor`) and access-scope checks. It may use EF Core query APIs through `IAppDbContext`, but never Npgsql or ASP.NET Core.
- `Tgysm.Infrastructure` — EF Core + Npgsql, migrations, password hashing adapter.
- `Tgysm.Api` — Minimal API endpoints, authentication, authorization policies, error mapping, composition root and CLI commands.

Endpoints never touch `DbContext` for business data; they call handlers. `Tgysm.ArchitectureTests` enforces the dependency direction. The Worker project is added in Phase 4.

## Alternatives Considered

### Alternative 1: Microservices
- **Pros**: independent deployment and scaling per module.
- **Cons**: distributed transactions for assignment + audit + outbox, more infrastructure, more operations work.
- **Why not**: one company, unknown load, small team. `tgysm.md` §15 states microservices are not required.

### Alternative 2: MediatR request pipeline
- **Pros**: uniform pipeline behaviors (validation, logging).
- **Cons**: commercial license for newer versions, indirection when reading code.
- **Why not**: plain handler classes resolved from DI give the same testability with less indirection.

### Alternative 3: Repository per aggregate instead of `IAppDbContext`
- **Pros**: Application layer free of EF Core types.
- **Cons**: many thin wrappers, lost query composition, duplicated paging/filter code.
- **Why not**: EF Core already is a unit of work + repository; the architecture tests keep Npgsql and ASP.NET Core out of Application.

## Consequences

### Positive
- Business rules are unit-testable; integration tests cover handlers against real PostgreSQL.
- One deployment unit, one transaction boundary for record + audit (+ outbox later).

### Negative
- Application depends on `Microsoft.EntityFrameworkCore` abstractions.
- All modules share one database and release cycle.

### Risks
- Module boundaries can erode inside one assembly. Mitigation: feature folders per module and architecture tests.
```

- [ ] **Step 3: Create `docs/adr/0002-postgresql-ef-core.md`**

```markdown
# ADR-0002: PostgreSQL with EF Core and Npgsql

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The owner selected PostgreSQL (`tgysm.md` §1). Business rules need relational constraints (unique email, unique asset number, one active assignment per asset), transactions and optimistic concurrency. Local development has no Docker; PostgreSQL 17 runs from Homebrew.

## Decision

- EF Core 10 with `Npgsql.EntityFrameworkCore.PostgreSQL` 10 and `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`).
- Primary keys are `uuid` v7 generated in the domain (`Guid.CreateVersion7()`), so ids exist before insert and sort by time.
- Timestamps are `timestamptz` in UTC (`DateTimeOffset`).
- Optimistic concurrency uses PostgreSQL `xmin` (`uint Version` mapped with `IsRowVersion()`). A conflict surfaces as HTTP 409 `concurrency.conflict`.
- Business uniqueness is enforced by unique indexes. Names compare case-insensitively via a `normalized_name` column written with the `tr-TR` culture.
- `audit_events` is append-only: a trigger rejects `UPDATE` and `DELETE`.
- Migrations are versioned in `Tgysm.Infrastructure/Persistence/Migrations` and applied with `tools/migrate.sh`.
- Integration tests run against a real local PostgreSQL 17: each test run creates and drops a `tgysm_test_<random>` database.

## Alternatives Considered

### Alternative 1: Dapper with hand-written SQL
- **Pros**: full SQL control, no ORM overhead.
- **Cons**: manual mapping, manual migrations, manual change tracking and concurrency.
- **Why not**: CRUD-heavy admin areas and migrations are cheaper with EF Core.

### Alternative 2: Testcontainers for integration tests
- **Pros**: isolated, reproducible database per run.
- **Cons**: requires Docker, which is not installed on the development machine.
- **Why not**: the owner chose local PostgreSQL 17. The test fixture reads `TGYSM_TEST_ADMIN_CONNECTION`, so a container can be plugged in later without code changes.

## Consequences

### Positive
- Constraints and concurrency are enforced by the database, not only by code.
- Tests exercise real PostgreSQL behavior (triggers, unique indexes, `xmin`).

### Negative
- Tests need a running local PostgreSQL.

### Risks
- Turkish sort order depends on the database collation. Revisit with an ICU collation when list sorting matters to users.
```

- [ ] **Step 4: Create `docs/adr/0003-single-spa-with-admin-area.md`**

```markdown
# ADR-0003: One SPA with a Separate `/admin` Area, Same-Origin with the API

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Employees, IT staff and admins use one product (`tgysm.md` §4). `/admin` needs its own navigation and layout but not a separate deployment. The UI must be mobile-first. A native mobile app will come later and will reuse the API.

## Decision

- One React SPA. `/admin/*` routes use `AdminLayout` (drawer menu on small screens, sidebar from 1024 px). Other routes use `AppShell`.
- The API serves under `/api/v1`. In development, Vite proxies `/api` to the API (`http://localhost:5080`). In production the API serves the built SPA from `wwwroot` with a fallback to `index.html`, so browser and API share one origin.
- URL paths are English (`/admin/users`); only visible text is Turkish (ADR-0006).
- Hiding a screen is not authorization: every admin endpoint requires the `admin` policy on the server.
- The API contract is described by OpenAPI; frontend types are generated from it. The future mobile app uses the same contract.

## Alternatives Considered

### Alternative 1: Separate admin SPA
- **Pros**: smaller bundles per audience.
- **Cons**: two builds, duplicated shared UI, two deployments.
- **Why not**: `tgysm.md` §4 requires one application; lazy-loaded admin routes keep the user bundle small.

### Alternative 2: Separate origins (API and SPA on different hosts)
- **Pros**: independent hosting.
- **Cons**: CORS, `SameSite=None` cookies, larger CSRF surface.
- **Why not**: same-origin lets the session cookie stay `SameSite=Strict`. If hosting ever needs two origins, write a new ADR.

## Consequences

### Positive
- One deployment, one cookie domain, simple CSRF model.

### Negative
- Admin and user areas release together.

### Risks
- Development relies on browsers accepting `Secure` cookies on `http://localhost` (Chrome and Firefox do; Safari may not).
```

- [ ] **Step 5: Create `docs/adr/0004-local-identity-cookie-csrf.md`**

```markdown
# ADR-0004: Local Accounts, Cookie Session and Antiforgery Header

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Accounts are local (no SSO, no LDAP in the first release; `tgysm.md` §2). Only admins create users. The first login must force a password change, and role change or deactivation must end active sessions (`tgysm.md` §3.1). The domain `User` must not depend on ASP.NET Core Identity types (ADR-0001).

## Decision

- Password hashing uses ASP.NET Core Identity's `PasswordHasher<T>` (PBKDF2) behind `IPasswordService` in Infrastructure. The full Identity user store is not used; `User` is a plain domain entity.
- Session: ASP.NET Core cookie authentication. Cookie `__Host-tgysm`, `Secure`, `HttpOnly`, `SameSite=Strict`, 8-hour sliding lifetime (configurable `Security:SessionHours`).
- Every request re-reads the user (`OnValidatePrincipal`): an inactive user or a changed `security_stamp` rejects the session immediately. Fresh role and `must_change_password` claims replace the cookie claims on each request.
- `security_stamp` rotates on role change, deactivation, reactivation, password change and admin password reset.
- CSRF: antiforgery token from `GET /api/v1/auth/csrf`, sent back in the `X-CSRF-TOKEN` header on every unsafe `/api` request; checked by `CsrfValidationMiddleware`.
- Brute force: 5 failed logins lock the account for 15 minutes; `POST /api/v1/auth/login` also has a per-IP fixed-window rate limit (`Security:LoginPermitLimitPerMinute`, default 10). All login failures return the same `auth.invalid_credentials` code.
- Password policy: 12–128 characters with at least one letter and one digit.
- While `must_change_password` is true, only `me`, `change-password` and `logout` are allowed (403 `auth.password_change_required` elsewhere).

## Alternatives Considered

### Alternative 1: Full ASP.NET Core Identity (`AddIdentityCore` + EF stores)
- **Pros**: lockout, stamps and hashing built in.
- **Cons**: the domain `User` must inherit `IdentityUser`, Identity tables and conventions leak into the schema and the domain.
- **Why not**: the domain stays framework-free; the few Identity features needed are small and tested.

### Alternative 2: JWT bearer tokens in local storage
- **Pros**: stateless, easy for a future mobile app.
- **Cons**: XSS can steal tokens, revocation needs extra state.
- **Why not**: same-origin cookies are safer for the web app. The mobile app can get its own token flow in a later ADR.

## Consequences

### Positive
- Deactivation and role change take effect on the next request.
- No token in JavaScript-reachable storage.

### Negative
- One database read per authenticated request.

### Risks
- Data Protection keys must be persisted in production, or all sessions end on restart. Address with the hosting decision (Phase 6).
```

- [ ] **Step 6: Create `docs/adr/0005-fixed-roles-single-role-per-user.md`**

```markdown
# ADR-0005: Fixed Roles, One Role per User

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

`tgysm.md` §4 defines three fixed role packages (employee, IT staff, admin) and excludes direct user permissions and custom roles from the first release.

## Decision

- `users.role` is a text column with a CHECK constraint: `employee`, `it_staff`, `admin`. Domain enum `UserRole`; canonical names in `UserRoleNames`.
- The API uses the same snake_case names in JSON and in the role claim.
- Authorization policies map roles to endpoints: `admin` policy for `/api/v1/admin/*`. Team scope for IT staff arrives with tickets (Phase 3).
- The last active admin can be neither demoted nor deactivated (`users.last_admin`).
- A department approver must be an active admin (`departments.approver_invalid`).
- Only IT staff and admins can be team members (`teams.member_role_invalid`).

## Alternatives Considered

### Alternative 1: Identity role tables (many-to-many)
- **Pros**: flexible, standard.
- **Cons**: allows multiple roles per user, which the product does not want; more joins.
- **Why not**: one column with a CHECK constraint expresses the rule exactly.

## Consequences

### Positive
- Simple queries and claims; role rules are explicit.

### Negative
- Adding a role needs a migration and code change.

### Risks
- If the business later needs combined roles, write a superseding ADR.
```

- [ ] **Step 7: Create `docs/adr/0006-english-code-turkish-ui-i18n.md`**

```markdown
# ADR-0006: English Code and URLs, Turkish UI through i18n

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The owner requires English for code, identifiers, database names, API endpoints and URL paths, and Turkish only for the user interface. `tgysm.md` §14 requires i18n keys, `tr-TR` formatting, correct I/İ/ı/i handling and Turkish suffix helpers.

## Decision

- All identifiers, tables, columns, routes and endpoints are English.
- UI text lives in `frontend/src/shared/i18n/locales/tr/*.json` (namespaces `common`, `auth`, `admin`, `errors`). Keys are typed through i18next `CustomTypeOptions`, so a missing key fails the TypeScript build.
- The API returns stable error codes (`ErrorCodes` in `Tgysm.Domain`). `TranslationCoverageTests` fails when any code or audit action lacks a Turkish text, or when `errors.json` holds a stale key.
- Dates and numbers use `Intl` with `tr-TR`; display time zone `Europe/Istanbul`.
- Case-insensitive matching uses the `tr-TR` culture on the backend (`TurkishText.ToSearchKey`) and `toLocaleLowerCase('tr-TR')` on the frontend. Emails use invariant lower case.
- `withSuffix` in `shared/lib/turkish.ts` supports two/four-way vowel harmony, buffer letters and `d/t` assimilation, with a pronunciation dictionary. It returns `null` when pronunciation is unknown (e.g. an unknown abbreviation); callers then use a suffix-free label such as `Atanan kişi: …`.

## Alternatives Considered

### Alternative 1: Turkish URLs (`/kayitlar`)
- **Pros**: readable for Turkish users.
- **Cons**: mixes languages between API and SPA; harder for the future mobile app and integrations.
- **Why not**: the owner chose English URLs.

## Consequences

### Positive
- Adding a second language later only needs new resource files.

### Negative
- Every visible string needs a key.

### Risks
- Suffix rules have exceptions; the dictionary must grow as real names appear.
```

- [ ] **Step 8: Create `docs/adr/0007-cli-first-admin-setup.md`**

```markdown
# ADR-0007: First Admin Created by a CLI Command

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

`tgysm.md` §3.1: the first admin is created once, by a secure one-time setup; no default password and no permanently open setup endpoint.

## Decision

- `tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>` applies migrations and runs `Tgysm.Api setup-admin ...`.
- The command refuses (exit code 1, `setup.already_completed`) when any user exists.
- It generates a 16-character temporary password, prints it once, and sets `must_change_password`.
- No HTTP setup path exists.

## Alternatives Considered

### Alternative 1: Web setup page protected by a one-time token printed at startup
- **Pros**: no shell access needed after deploy.
- **Cons**: an HTTP surface that must be proven closed after use; token handling in logs.
- **Why not**: server shell access is available at install time; no HTTP surface is the safest option.

## Consequences

### Positive
- Nothing on the network can create the first account.

### Negative
- Installation needs shell access to the host.

### Risks
- Two parallel runs on an empty database could both pass the check. Setup is an operator action run once; add an advisory lock if this ever runs automatically.
```

- [ ] **Step 9: Create `docs/adr/0008-frontend-stack.md`**

```markdown
# ADR-0008: Frontend Stack

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The web app must be mobile-first, accessible (keyboard, visible focus, form error descriptions, 360 px without horizontal scroll) and typed end-to-end with the API (`tgysm.md` §13, §15).

## Decision

- React 19 + TypeScript (strict) + Vite 8.
- Tailwind CSS v4 for styling; Radix UI primitives (`radix-ui`) for accessible dialogs and drawers.
- React Router for routes; admin pages are lazy-loaded.
- TanStack Query for server state; react-hook-form + zod for forms.
- i18next + react-i18next with typed keys (ADR-0006).
- OpenAPI document generated at build time from the API; `openapi-typescript` generates `schema.d.ts`; `openapi-fetch` is the typed client with an antiforgery middleware.
- Tests: Vitest + React Testing Library; Playwright E2E at 360×800 and 1280×800.
- ESLint flat config per the owner's React rules (render-loop guards as errors, `--max-warnings=0`, no disable comments). ESLint pinned to v9 and TypeScript to ~6.0 until plugins support newer majors.

## Alternatives Considered

### Alternative 1: MUI
- **Pros**: complete component set.
- **Cons**: heavy bundle, Material look, harder custom mobile layouts.
- **Why not**: Tailwind + Radix is lighter and gives full control.

### Alternative 2: Mantine
- **Pros**: rich components including forms and dates.
- **Cons**: medium weight, own styling system.
- **Why not**: same reason; Radix covers the accessibility-critical primitives.

## Consequences

### Positive
- Small bundles, accessible primitives, typed API calls.

### Negative
- More hand-built UI components.

### Risks
- Vite 8 (Rolldown) chunk options differ from Rollup; check current docs when tuning chunks.
```

- [ ] **Step 10: Create `docs/adr/README.md`**

```markdown
# Architecture Decision Records

Template: `~/.claude/templates/adr-template.md`. File names: `NNNN-kebab-case-title.md`. Accepted ADRs are immutable; a change gets a new ADR that supersedes the old one.

| # | Title | Status | Date |
|---|---|---|---|
| [0001](0001-modular-monolith-clean-architecture.md) | Modular monolith with Clean Architecture layers | accepted | 2026-09-26 |
| [0002](0002-postgresql-ef-core.md) | PostgreSQL with EF Core and Npgsql | accepted | 2026-09-26 |
| [0003](0003-single-spa-with-admin-area.md) | One SPA with a separate `/admin` area, same-origin with the API | accepted | 2026-09-26 |
| [0004](0004-local-identity-cookie-csrf.md) | Local accounts, cookie session and antiforgery header | accepted | 2026-09-26 |
| [0005](0005-fixed-roles-single-role-per-user.md) | Fixed roles, one role per user | accepted | 2026-09-26 |
| [0006](0006-english-code-turkish-ui-i18n.md) | English code and URLs, Turkish UI through i18n | accepted | 2026-09-26 |
| [0007](0007-cli-first-admin-setup.md) | First admin created by a CLI command | accepted | 2026-09-26 |
| [0008](0008-frontend-stack.md) | Frontend stack | accepted | 2026-09-26 |
```

- [ ] **Step 11: Create `CLAUDE.md`**

```markdown
# TGYSM — ITSM web platform

Single-company ITSM: incident and request management, IT inventory and asset management, SLA, email, knowledge base and reports. Current state: Phase 1 foundation (auth, `/admin` users/departments/locations/teams/audit). Requirements: `tgysm.md`.

## Stack & Architecture
- **Backend:** .NET 10 modular monolith (ADR-0001) — `backend/src/Tgysm.Domain` (rules, no framework deps) → `Tgysm.Application` (handlers + FluentValidation, ports) → `Tgysm.Infrastructure` (EF Core + Npgsql, migrations) → `Tgysm.Api` (Minimal APIs, auth, CLI). Solution: `backend/Tgysm.slnx`.
- **Database:** PostgreSQL 17, local Homebrew (ADR-0002). snake_case, `uuid` v7, `xmin` concurrency.
- **Frontend:** React 19 + Vite + TypeScript in `frontend/` (ADR-0008). Server state via TanStack Query; typed client `src/shared/api/client.ts` generated from OpenAPI.
- Out of scope unless asked: Worker, tickets, assets, SLA, email, reports, mobile app.

## Commands
Run through `tools/` scripts.
- `tools/db-up.sh` — ensure local PostgreSQL role/database + API user-secret connection string.
- `tools/migrate.sh` — apply EF Core migrations.
- `tools/setup-admin.sh --email … --first-name … --last-name …` — create first admin (once).
- `tools/run.sh` — API on `http://localhost:5080` + Vite on `http://localhost:5173`.
- `tools/lint.sh` — `dotnet format --verify-no-changes` + ESLint + TypeScript.
- `tools/test.sh` — backend tests with coverage gate + frontend tests with coverage gate.
- `tools/e2e.sh` — Playwright E2E on a fresh `tgysm_e2e` database.
- `tools/gen-api-types.sh` — regenerate OpenAPI JSON and `schema.d.ts` after API contract changes.
- `tools/build.sh` — lint + tests, then Release build + frontend build with chunk sizes.

## Quality gates (must pass before commit)
- **C#:** `TreatWarningsAsErrors=true`, `Nullable=enable` — zero warnings.
- **C# tests gate builds:** `tools/build.sh` runs `tools/test.sh` first; Domain + Application line coverage ≥ 80%.
- **TS/React:** ESLint errors only, `--max-warnings=0`, no `eslint-disable`; frontend line coverage ≥ 80%.
- **Contracts:** after changing an endpoint, run `tools/gen-api-types.sh`; `TranslationCoverageTests` must pass (every error code and audit action has Turkish text).
- No magic numbers/strings — named constants.

## Conventions
- English code, identifiers, DB names, endpoints, URL paths; Turkish only in `frontend/src/shared/i18n/locales/tr/*.json` (ADR-0006).
- New API error → add a constant to `ErrorCodes` and a Turkish text to `errors.json`.
- Handlers: one class per use case in `Tgysm.Application/Features/<Area>/`, `HandleAsync(command, ct)`; validators `internal sealed`.
- Git: stage named paths only — never `git add -A`/`.`; no dotfiles/dot-dirs unless told. No commit/push without owner approval.
- Owner: Tugay İLTUŞ. Never add AI attribution to any file, commit, or PR.

## Architectural decisions
- Recorded as ADRs in `docs/adr/`, template `~/.claude/templates/adr-template.md`, index `docs/adr/README.md`.
- Significant architectural decision: add an ADR in the same change.

## Docs
- `README.md` — overview, architecture, commands, quality gates.
- Docs change only on request (`~/.claude/guides/docs-updates.md`); after a code change, name the doc that needs a line and stop.
- `capabilities.md` — authoritative feature + tech-stack reference; ends with the `DOC-BASELINE` marker.
- `SECURITY.md` — security policy and posture.
- User guide in `docs/user-guides/`. When asked to edit: update `PROGRESS.md`, regenerate `combined.md` / `combined.pdf` via `tools/build-user-guide.sh` (PDF only if a guide page changed; always A4). The script is created with the first real guide page.

## Session continuity & memory
- **Plans/specs:** `docs/superpowers/specs/`, `docs/superpowers/plans/`. Read the latest on resume.
- **Memory index:** `~/.claude/projects/-Users-lektuel-claude-TGYSM/memory/MEMORY.md` — one fact per `name.md` file with a one-line pointer in the index.
- When updating any index, create a dedicated `.md` file then add the reference line — never inline content into the index; no orphan files.
```

- [ ] **Step 12: Create `README.md`**

```markdown
# TGYSM

TGYSM is a web-based ITSM platform for one company with several departments and locations. Employees report faults and request services; IT staff work team queues and manage IT assets; admins manage users, organization, approvals, SLA rules and reports. The UI is Turkish and mobile-first; code and URLs are English. Current state: Phase 1 foundation — local login with forced password change and the `/admin` area for users, departments, locations, teams and audit.

## Architecture

| Layer | Project / folder | Responsibility |
|---|---|---|
| Domain | `backend/src/Tgysm.Domain` | Entities, business rules, state transitions; no framework dependency |
| Application | `backend/src/Tgysm.Application` | Use-case handlers, validation, ports, audit recording |
| Infrastructure | `backend/src/Tgysm.Infrastructure` | EF Core + Npgsql, migrations, password hashing |
| API | `backend/src/Tgysm.Api` | Minimal API endpoints under `/api/v1`, cookie auth, antiforgery, ProblemDetails, `setup-admin` CLI |
| Web | `frontend/` | React SPA: user area and `/admin` |

Solution: `backend/Tgysm.slnx`. Browser and API are same-origin; Vite proxies `/api` in development (ADR-0003).

## Stack

- Backend: .NET 10, ASP.NET Core Minimal APIs, EF Core 10 + Npgsql 10, FluentValidation 12; `TreatWarningsAsErrors=true`, `Nullable=enable`.
- Database: PostgreSQL 17 (ADR-0002).
- Frontend: React 19, TypeScript ~6.0 (strict), Vite 8, Tailwind CSS v4, Radix UI, React Router, TanStack Query 5, react-hook-form + zod 4, i18next (ADR-0008).
- Tests: xUnit v3, NetArchTest, Vitest + React Testing Library, Playwright.

## Commands

| Task | Command |
|---|---|
| Prepare local database | `tools/db-up.sh` |
| Apply migrations | `tools/migrate.sh` |
| Create first admin | `tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>` |
| Run API + web | `tools/run.sh` |
| Lint | `tools/lint.sh` |
| Tests (+ coverage gates) | `tools/test.sh` |
| E2E | `tools/e2e.sh` |
| Regenerate API types | `tools/gen-api-types.sh` |
| Build | `tools/build.sh` |

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
```

- [ ] **Step 13: Create `capabilities.md`**

```markdown
# TGYSM Capabilities

## Overview

ITSM platform for one company (multiple departments and locations). Users: employees, IT staff, admins. Served as one web application; host not yet decided. Out of scope for the first release: multi-tenancy, SSO/LDAP, native mobile app, CMDB, change/problem management (`tgysm.md` §2).

Status legend: **Planned (Phase 1)** = in the current implementation plan; **Planned** = later phase.

## Tech stack

| Component | Choice | Decision |
|---|---|---|
| Backend | .NET 10 modular monolith, Clean Architecture | ADR-0001 |
| Database / ORM | PostgreSQL 17, EF Core 10 + Npgsql 10, snake_case, `xmin` concurrency | ADR-0002 |
| Web | React 19, TypeScript, Vite 8, Tailwind v4, Radix UI, TanStack Query, i18next | ADR-0003, ADR-0008 |
| Auth | Local accounts, cookie session, antiforgery header, lockout, rate limit | ADR-0004 |
| Roles | `employee`, `it_staff`, `admin`; one per user | ADR-0005 |
| Language | English code/URLs, Turkish UI | ADR-0006 |
| First admin | `setup-admin` CLI | ADR-0007 |
| Background worker, email | Planned (Phase 4) | — |

## API (Planned (Phase 1))

- `GET /api/v1/auth/csrf`, `POST /api/v1/auth/login`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me`, `POST /api/v1/auth/change-password`
- `/api/v1/admin/users`, `/departments`, `/locations`, `/teams`, `/audit-events` (admin policy)

## Data models (Planned (Phase 1))

| Entity | Key fields |
|---|---|
| `users` | email (unique, normalized), role, department, location, is_active, must_change_password, security_stamp, lockout |
| `departments` | name (unique, Turkish case-insensitive), approver (admin), is_active |
| `locations` | name, parent (tree), is_active |
| `teams` / `team_memberships` | name, members (IT staff or admin) |
| `audit_events` | actor, action, entity, old/new JSON, correlation id; append-only |

## Auth & permissions

TODO: fill after Phase 1 lands (see ADR-0004, ADR-0005).

## UI / pages

TODO: fill after Phase 1 lands. Planned: login, forced password change, home, profile, `/admin` users/departments/locations/teams/audit.

## Features & business logic

TODO: fill after Phase 1 lands.

<!-- DOC-BASELINE: no commits yet -->
_Documentation baseline: current as of 2026-09-26 (no commits yet). Refresh with `git log --oneline <hash>..HEAD` once a baseline commit exists._
```

- [ ] **Step 14: Create `SECURITY.md`**

```markdown
# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| `main` (pre-release, Phase 1) | Yes |

## Reporting a vulnerability

Report privately to the project owner, Tugay İLTUŞ (GitHub [@tugayiltus](https://github.com/tugayiltus)), through a private GitHub security advisory. Do not open a public issue. Include affected endpoint or screen, steps to reproduce, impact, and any proof of concept. Acknowledgement target: 5 business days.

## Security posture

- **Authentication:** local accounts only; PBKDF2 password hashing (`PasswordHasher<T>`); 12–128 character passwords with letters and digits; forced change of temporary passwords; lockout after 5 failures for 15 minutes; per-IP login rate limit; uniform `auth.invalid_credentials` response (ADR-0004).
- **Sessions:** `__Host-tgysm` cookie (`Secure`, `HttpOnly`, `SameSite=Strict`); every request re-validates active status and security stamp; role change, deactivation and password reset end sessions.
- **CSRF:** antiforgery token in `X-CSRF-TOKEN` on every unsafe API request.
- **Authorization:** server-side policies; `/api/v1/admin/*` requires the admin role; hidden UI is not treated as authorization (ADR-0005).
- **Audit:** security and admin events in append-only `audit_events` (database trigger); passwords, hashes and tokens are never logged or audited.
- **Transport:** HTTPS required in production (HSTS outside Development/Testing).
- **Secrets:** no credentials, keys or connection strings in the repository. Development connection strings live in `dotnet user-secrets`; tests read `TGYSM_TEST_ADMIN_CONNECTION`.
- **Dependencies:** versions pinned centrally (`backend/Directory.Packages.props`, `frontend/package-lock.json`).

## Dependency & supply chain

- Automated update tool: TODO: choose (Dependabot or Renovate) when the GitHub repository exists.
- Each bump must pass `tools/lint.sh`, `tools/test.sh` and `tools/e2e.sh` before merge.
- Major version bumps additionally get a manual smoke test of login, forced password change and `/admin/users`.
```

- [ ] **Step 15: Create user-guide skeleton**

`docs/user-guides/README.md`:

```markdown
# TGYSM Kullanıcı Rehberi

1. [Başlarken](getting-started.md)
2. [Sorun giderme](troubleshooting.md)

Rehber sayfaları ürün geliştikçe eklenir. Durum: [PROGRESS.md](PROGRESS.md).
```

`docs/user-guides/getting-started.md`:

```markdown
# Başlarken

## Giriş yapma

1. Yöneticinizin size verdiği e-posta adresi ve geçici şifre ile **Giriş yap** ekranını açın.
2. İlk girişte sistem sizden yeni bir şifre ister. Şifre en az 12 karakter olmalı, harf ve rakam içermelidir.
3. Şifrenizi değiştirdikten sonra ana sayfa açılır.

## Şifremi unuttum

Şimdilik şifre sıfırlama işlemini yöneticiniz yapar. Yöneticiniz size yeni bir geçici şifre verir.
```

`docs/user-guides/troubleshooting.md`:

```markdown
# Sorun giderme

## "E-posta veya şifre hatalı" uyarısı

- E-posta adresinizi ve şifrenizi kontrol edin.
- Art arda 5 hatalı denemede hesabınız 15 dakika kilitlenir. 15 dakika sonra tekrar deneyin.

## Oturumum kapandı

Yöneticiniz rolünüzü değiştirdiyse, hesabınızı pasifleştirdiyse veya şifrenizi sıfırladıysa oturumunuz kapanır. Tekrar giriş yapın.
```

`docs/user-guides/PROGRESS.md`:

```markdown
# User guide progress

| Page | Status | Screenshots |
|---|---|---|
| `getting-started.md` | Draft (Phase 1 login flow) | Pending |
| `troubleshooting.md` | Draft | Not needed yet |
| Admin: users, departments, locations, teams, audit | Pending (after Phase 1 UI lands) | Pending |
| `combined.md` / `combined.pdf` | Pending — generated by `tools/build-user-guide.sh` once real pages exist | — |
```

- [ ] **Step 16: Verify ADR files match the index**

Run:

```bash
cd /Users/lektuel/claude/TGYSM && diff <(ls docs/adr | grep -E '^[0-9]{4}-' | sort) <(grep -oE '\(0[0-9]{3}-[a-z0-9-]+\.md\)' docs/adr/README.md | tr -d '()' | sort) && echo "ADR index OK"
```

Expected: `ADR index OK`.

- [ ] **Step 17: Checkpoint (no commit)**

Run: `git -C /Users/lektuel/claude/TGYSM status --short`
Expected: new `docs/`, `CLAUDE.md`, `README.md`, `capabilities.md`, `SECURITY.md`, `.gitignore`. Do not commit.

---
### Task 2: Backend scaffold, architecture tests, first tools

**Files:**
- Create: `backend/Tgysm.slnx`, `backend/Directory.Build.props`, `backend/Directory.Packages.props`, `backend/dotnet-tools.json`
- Create: `backend/src/Tgysm.{Domain,Application,Infrastructure,Api}/*.csproj` and `AssemblyMarker.cs` in each
- Create: `backend/tests/Tgysm.{Domain.Tests,Application.Tests,ArchitectureTests,Api.IntegrationTests}/*.csproj`
- Test: `backend/tests/Tgysm.ArchitectureTests/LayerDependencyTests.cs`
- Create: `tools/lint.sh`, `tools/test.sh`, `tools/build.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: projects `Tgysm.Domain`, `Tgysm.Application`, `Tgysm.Infrastructure`, `Tgysm.Api`; marker types `Tgysm.Domain.AssemblyMarker`, `Tgysm.Application.AssemblyMarker`, `Tgysm.Infrastructure.AssemblyMarker`, `Tgysm.Api.AssemblyMarker`; scripts `tools/lint.sh`, `tools/test.sh`, `tools/build.sh`.

- [ ] **Step 1: Create the solution and projects**

Run:

```bash
cd /Users/lektuel/claude/TGYSM && mkdir -p backend/src backend/tests tools
cd /Users/lektuel/claude/TGYSM/backend
dotnet new sln -n Tgysm --format slnx
dotnet new classlib -n Tgysm.Domain -o src/Tgysm.Domain
dotnet new classlib -n Tgysm.Application -o src/Tgysm.Application
dotnet new classlib -n Tgysm.Infrastructure -o src/Tgysm.Infrastructure
dotnet new web -n Tgysm.Api -o src/Tgysm.Api
rm src/Tgysm.Domain/Class1.cs src/Tgysm.Application/Class1.cs src/Tgysm.Infrastructure/Class1.cs
mkdir -p tests/Tgysm.Domain.Tests tests/Tgysm.Application.Tests tests/Tgysm.ArchitectureTests tests/Tgysm.Api.IntegrationTests
```

Expected: four project folders; `Tgysm.slnx` exists.

- [ ] **Step 2: Write `backend/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

`InvariantGlobalization=false` keeps ICU so the `tr-TR` culture works (ADR-0006).

- [ ] **Step 3: Write `backend/Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="EFCore.NamingConventions" Version="10.0.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageVersion Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageVersion Include="xunit.v3" Version="4.0.1" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.0" />
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>
</Project>
```

If `xunit.v3` 4.x changed its API compared with 3.x (fixture interfaces, `TestContext`), check the current xUnit v3 docs via context7 before writing tests; the test code below uses `IAsyncLifetime` returning `ValueTask`, `ICollectionFixture<T>`, `IClassFixture<T>` and `[Collection]`.

- [ ] **Step 4: Write the production project files**

`backend/src/Tgysm.Domain/Tgysm.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

`backend/src/Tgysm.Application/Tgysm.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Tgysm.Domain\Tgysm.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Tgysm.Application.Tests" />
    <InternalsVisibleTo Include="Tgysm.Api.IntegrationTests" />
  </ItemGroup>
</Project>
```

`backend/src/Tgysm.Infrastructure/Tgysm.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Tgysm.Application\Tgysm.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- PasswordHasher<T> (Microsoft.Extensions.Identity.Core) and configuration abstractions -->
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Tgysm.Api.IntegrationTests" />
  </ItemGroup>
</Project>
```

`backend/src/Tgysm.Api/Tgysm.Api.csproj` (replace template content):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\Tgysm.Application\Tgysm.Application.csproj" />
    <ProjectReference Include="..\Tgysm.Infrastructure\Tgysm.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Tgysm.Api.IntegrationTests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Add marker types**

`backend/src/Tgysm.Domain/AssemblyMarker.cs`:

```csharp
namespace Tgysm.Domain;

/// <summary>Anchor type for assembly scanning and architecture tests.</summary>
public sealed class AssemblyMarker;
```

Create the same file in the other three projects with namespaces `Tgysm.Application`, `Tgysm.Infrastructure`, `Tgysm.Api`.

Replace `backend/src/Tgysm.Api/Program.cs` with a placeholder host (Task 6 replaces it):

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
await app.RunAsync();

public partial class Program;
```

- [ ] **Step 6: Write the test project files**

`backend/tests/Tgysm.Domain.Tests/Tgysm.Domain.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Tgysm.Domain\Tgysm.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`backend/tests/Tgysm.Application.Tests/Tgysm.Application.Tests.csproj`: same as above, but the project reference is `..\..\src\Tgysm.Application\Tgysm.Application.csproj`.

`backend/tests/Tgysm.ArchitectureTests/Tgysm.ArchitectureTests.csproj`: same as above plus `<PackageReference Include="NetArchTest.Rules" />`, and project references to all four `src` projects.

`backend/tests/Tgysm.Api.IntegrationTests/Tgysm.Api.IntegrationTests.csproj`: same as the Domain test project, plus `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />` and a project reference to `..\..\src\Tgysm.Api\Tgysm.Api.csproj`.

Add every project to the solution:

```bash
cd /Users/lektuel/claude/TGYSM/backend
dotnet sln Tgysm.slnx add src/Tgysm.Domain src/Tgysm.Application src/Tgysm.Infrastructure src/Tgysm.Api
dotnet sln Tgysm.slnx add tests/Tgysm.Domain.Tests tests/Tgysm.Application.Tests tests/Tgysm.ArchitectureTests tests/Tgysm.Api.IntegrationTests
```

- [ ] **Step 7: Write the failing-capable architecture test**

`backend/tests/Tgysm.ArchitectureTests/LayerDependencyTests.cs`:

```csharp
using System.Reflection;
using NetArchTest.Rules;

namespace Tgysm.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private const string ApplicationNamespace = "Tgysm.Application";
    private const string InfrastructureNamespace = "Tgysm.Infrastructure";
    private const string ApiNamespace = "Tgysm.Api";
    private const string EntityFrameworkNamespace = "Microsoft.EntityFrameworkCore";
    private const string AspNetCoreNamespace = "Microsoft.AspNetCore";
    private const string NpgsqlNamespace = "Npgsql";
    private const string HttpNamespace = "System.Net.Http";

    private static readonly Assembly Domain = typeof(Tgysm.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(Tgysm.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(Tgysm.Infrastructure.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(
            ApplicationNamespace, InfrastructureNamespace, ApiNamespace,
            EntityFrameworkNamespace, AspNetCoreNamespace, NpgsqlNamespace, HttpNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_npgsql_or_aspnetcore()
    {
        var result = Types.InAssembly(Application).ShouldNot().HaveDependencyOnAny(
            InfrastructureNamespace, ApiNamespace, NpgsqlNamespace, AspNetCoreNamespace, HttpNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNamespace).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
```

- [ ] **Step 8: Prove the Domain rule can fail**

Temporarily add `backend/src/Tgysm.Domain/Probe.cs`:

```csharp
namespace Tgysm.Domain;

public sealed class Probe
{
    public System.Net.Http.HttpClient? Client { get; init; }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.ArchitectureTests --nologo`
Expected: FAIL in `Domain_depends_on_no_other_layer_or_framework` listing `Tgysm.Domain.Probe`.

Delete `backend/src/Tgysm.Domain/Probe.cs`.

- [ ] **Step 9: Create the local tool manifest**

```bash
cd /Users/lektuel/claude/TGYSM/backend
dotnet new tool-manifest
mv .config/dotnet-tools.json dotnet-tools.json && rmdir .config
dotnet tool install dotnet-ef --version 10.0.12
dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11
```

Expected: `backend/dotnet-tools.json` lists `dotnet-ef` and `dotnet-reportgenerator-globaltool` (root-level manifest avoids a dot-directory).

- [ ] **Step 10: Write the first tool scripts**

`tools/lint.sh`:

```bash
#!/usr/bin/env bash
# Lint gate: C# formatting (frontend lint is added in Task 12).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly MIGRATIONS_DIR="backend/src/Tgysm.Infrastructure/Persistence/Migrations"

dotnet format backend/Tgysm.slnx --verify-no-changes --exclude "$MIGRATIONS_DIR"
```

`tools/test.sh`:

```bash
#!/usr/bin/env bash
# Test gate: backend tests (coverage and frontend tests are added in Task 16 and Task 12).
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet test backend/Tgysm.slnx --nologo
```

`tools/build.sh`:

```bash
#!/usr/bin/env bash
# Build gate: lint and tests must pass before the Release build.
set -euo pipefail
cd "$(dirname "$0")/.."

tools/lint.sh
tools/test.sh
dotnet build backend/Tgysm.slnx -c Release --nologo
```

Run: `chmod +x /Users/lektuel/claude/TGYSM/tools/*.sh`

- [ ] **Step 11: Run the gates**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"`
Expected: 3 architecture tests pass, build succeeds with 0 warnings, `exit=0`. If `dotnet format` reports whitespace in template files, run `dotnet format backend/Tgysm.slnx` once and re-run.

- [ ] **Step 12: Checkpoint (no commit)**

Run: `git -C /Users/lektuel/claude/TGYSM status --short`

---

### Task 3: Domain model

**Files:**
- Create: `backend/src/Tgysm.Domain/Common/{Entity,DomainException,ErrorCodes,TurkishText}.cs`
- Create: `backend/src/Tgysm.Domain/Users/{UserRole,UserRoleNames,User}.cs`
- Create: `backend/src/Tgysm.Domain/Organization/{Department,Location,Team,TeamMembership}.cs`
- Create: `backend/src/Tgysm.Domain/Audit/{AuditEvent,AuditActions,AuditEntityTypes}.cs`
- Test: `backend/tests/Tgysm.Domain.Tests/{TurkishTextTests,UserTests,OrganizationTests}.cs`

**Interfaces:**
- Consumes: Task 2 projects.
- Produces:
  - `abstract class Entity { Guid Id; uint Version; }`
  - `sealed class DomainException(string code, string message, Exception? innerException = null) : Exception { string Code; }`
  - `static class ErrorCodes` (constants listed below), `static class TurkishText { CultureInfo Culture; string ToSearchKey(string) }`
  - `enum UserRole { Employee, ItStaff, Admin }`, `static class UserRoleNames { Employee, ItStaff, Admin, string ToName(UserRole), bool TryParse(string?, out UserRole) }`
  - `User.Create(firstName, lastName, email, role, departmentId, locationId, temporaryPasswordHash, now)`, `UpdateProfile(firstName, lastName, email, departmentId, locationId, now)`, `ChangeRole(role, now)`, `Deactivate(now)`, `Reactivate(now)`, `SetTemporaryPassword(hash, now)`, `ChangePassword(hash, now)`, `IsLockedOut(now)`, `CanSignIn(now)`, `bool RegisterFailedSignIn(now)`, `RegisterSuccessfulSignIn(now)`, `static string NormalizeEmail(string)`; constants `NameMaxLength=100`, `EmailMaxLength=254`, `SearchNameMaxLength`, `SecurityStampMaxLength=64`, `MaxFailedSignInAttempts=5`, `LockoutDuration=15 min`
  - `Department.Create(name, approverUserId)`, `Update(name, approverUserId)`; `Location.Create(name, parentId)`, `Update(name, parentId)`; `Team.Create(name)`, `Rename(name)`; all three: `Deactivate()`, `Reactivate()`, `NameMaxLength=150`, `NormalizedName`
  - `TeamMembership.Create(teamId, userId, now)`
  - `AuditEvent.Create(occurredAt, actorUserId, action, entityType, entityId, reason, oldValues, newValues, correlationId)`; `AuditActions`, `AuditEntityTypes` constants

- [ ] **Step 1: Write the failing domain tests**

`backend/tests/Tgysm.Domain.Tests/TurkishTextTests.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests;

public sealed class TurkishTextTests
{
    [Theory]
    [InlineData("İLKER IŞIK", "ilker ışık")]
    [InlineData("  İstanbul  ", "istanbul")]
    [InlineData("ÇAĞRI ÖZGÜR", "çağrı özgür")]
    public void ToSearchKey_lowercases_with_turkish_rules_and_trims(string input, string expected) =>
        Assert.Equal(expected, TurkishText.ToSearchKey(input));
}
```

`backend/tests/Tgysm.Domain.Tests/UserTests.cs`:

```csharp
using Tgysm.Domain.Users;

namespace Tgysm.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    private static User NewUser(UserRole role = UserRole.Employee) =>
        User.Create("İlker", "Işık", "  Ilker.Isik@Example.COM ", role, null, null, "temp-hash", Now);

    [Fact]
    public void Create_sets_temporary_password_state_and_normalized_fields()
    {
        var user = NewUser();

        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.Equal("Ilker.Isik@Example.COM", user.Email);
        Assert.Equal("ilker.isik@example.com", user.NormalizedEmail);
        Assert.Equal("ilker ışık ilker.isik@example.com", user.SearchName);
        Assert.NotEmpty(user.SecurityStamp);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Five_failed_sign_ins_lock_the_account_for_fifteen_minutes()
    {
        var user = NewUser();

        for (var attempt = 1; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            Assert.False(user.RegisterFailedSignIn(Now));
        }

        Assert.True(user.RegisterFailedSignIn(Now));
        Assert.True(user.IsLockedOut(Now));
        Assert.False(user.CanSignIn(Now.AddMinutes(14)));
        Assert.True(user.CanSignIn(Now + User.LockoutDuration));
    }

    [Fact]
    public void Successful_sign_in_resets_failed_attempts()
    {
        var user = NewUser();
        user.RegisterFailedSignIn(Now);

        user.RegisterSuccessfulSignIn(Now);

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public void ChangeRole_rotates_stamp_only_when_role_changes()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.ChangeRole(UserRole.Employee, Now);
        Assert.Equal(stamp, user.SecurityStamp);

        user.ChangeRole(UserRole.ItStaff, Now);
        Assert.Equal(UserRole.ItStaff, user.Role);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void Deactivate_rotates_stamp_and_blocks_sign_in()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.Deactivate(Now);

        Assert.False(user.IsActive);
        Assert.False(user.CanSignIn(Now));
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void ChangePassword_clears_must_change_flag_and_rotates_stamp()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.ChangePassword("new-hash", Now);

        Assert.False(user.MustChangePassword);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.NotEqual(stamp, user.SecurityStamp);
    }

    [Fact]
    public void SetTemporaryPassword_requires_change_and_clears_lockout()
    {
        var user = NewUser();
        user.ChangePassword("new-hash", Now);
        for (var attempt = 0; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            user.RegisterFailedSignIn(Now);
        }

        user.SetTemporaryPassword("temp-2", Now);

        Assert.True(user.MustChangePassword);
        Assert.False(user.IsLockedOut(Now));
    }

    [Theory]
    [InlineData(UserRole.Employee, "employee")]
    [InlineData(UserRole.ItStaff, "it_staff")]
    [InlineData(UserRole.Admin, "admin")]
    public void Role_names_round_trip(UserRole role, string name)
    {
        Assert.Equal(name, UserRoleNames.ToName(role));
        Assert.True(UserRoleNames.TryParse(name, out var parsed));
        Assert.Equal(role, parsed);
    }

    [Fact]
    public void Unknown_role_name_does_not_parse() =>
        Assert.False(UserRoleNames.TryParse("superuser", out _));
}
```

`backend/tests/Tgysm.Domain.Tests/OrganizationTests.cs`:

```csharp
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Domain.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void Department_normalizes_name_with_turkish_rules()
    {
        var department = Department.Create("  İnsan Kaynakları ", null);

        Assert.Equal("İnsan Kaynakları", department.Name);
        Assert.Equal("insan kaynakları", department.NormalizedName);
        Assert.True(department.IsActive);
    }

    [Fact]
    public void Location_cannot_be_its_own_parent()
    {
        var location = Location.Create("Merkez Kampüs", null);

        var error = Assert.Throws<DomainException>(() => location.Update("Merkez Kampüs", location.Id));

        Assert.Equal(ErrorCodes.LocationCycle, error.Code);
    }

    [Fact]
    public void Team_deactivate_and_reactivate_toggle_state()
    {
        var team = Team.Create("Saha Destek");

        team.Deactivate();
        Assert.False(team.IsActive);

        team.Reactivate();
        Assert.True(team.IsActive);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Domain.Tests --nologo`
Expected: build FAIL — `TurkishText`, `User`, `Department` not found.

- [ ] **Step 3: Implement `Common`**

`backend/src/Tgysm.Domain/Common/Entity.cs`:

```csharp
namespace Tgysm.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Mapped to PostgreSQL <c>xmin</c>; optimistic concurrency token (ADR-0002).</summary>
    public uint Version { get; private set; }
}
```

`backend/src/Tgysm.Domain/Common/DomainException.cs`:

```csharp
namespace Tgysm.Domain.Common;

/// <summary>A business rule was violated. <see cref="Code"/> is a stable <see cref="ErrorCodes"/> value.</summary>
public sealed class DomainException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
```

`backend/src/Tgysm.Domain/Common/ErrorCodes.cs`:

```csharp
namespace Tgysm.Domain.Common;

/// <summary>
/// Stable error codes returned by the API. Every constant needs a Turkish text in
/// frontend/src/shared/i18n/locales/tr/errors.json (checked by TranslationCoverageTests).
/// </summary>
public static class ErrorCodes
{
    public const string NotFound = "common.not_found";
    public const string Forbidden = "common.forbidden";
    public const string Unexpected = "common.unexpected";
    public const string Duplicate = "common.duplicate";
    public const string ReferenceInactive = "common.reference_inactive";

    public const string ValidationFailed = "validation.failed";
    public const string Required = "validation.required";
    public const string TooLong = "validation.too_long";
    public const string InvalidEmail = "validation.invalid_email";
    public const string InvalidValue = "validation.invalid";

    public const string ConcurrencyConflict = "concurrency.conflict";

    public const string Unauthenticated = "auth.unauthenticated";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string PasswordChangeRequired = "auth.password_change_required";
    public const string CsrfInvalid = "auth.csrf_invalid";
    public const string CurrentPasswordInvalid = "auth.current_password_invalid";
    public const string PasswordPolicy = "auth.password_policy";
    public const string PasswordSameAsOld = "auth.password_same_as_old";
    public const string RateLimited = "auth.rate_limited";

    public const string SetupAlreadyCompleted = "setup.already_completed";

    public const string EmailTaken = "users.email_taken";
    public const string LastAdmin = "users.last_admin";

    public const string DepartmentNameTaken = "departments.name_taken";
    public const string ApproverInvalid = "departments.approver_invalid";

    public const string LocationNameTaken = "locations.name_taken";
    public const string LocationCycle = "locations.cycle";
    public const string LocationHasActiveChildren = "locations.has_active_children";

    public const string TeamNameTaken = "teams.name_taken";
    public const string MembershipExists = "teams.membership_exists";
    public const string MemberInactive = "teams.member_inactive";
    public const string MemberRoleInvalid = "teams.member_role_invalid";
}
```

`backend/src/Tgysm.Domain/Common/TurkishText.cs`:

```csharp
using System.Globalization;

namespace Tgysm.Domain.Common;

/// <summary>Turkish-aware text helpers (ADR-0006).</summary>
public static class TurkishText
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Trimmed, lower-cased with Turkish rules (I→ı, İ→i). Used for search and uniqueness keys.</summary>
    public static string ToSearchKey(string value) => value.Trim().ToLower(Culture);
}
```

- [ ] **Step 4: Implement `Users`**

`backend/src/Tgysm.Domain/Users/UserRole.cs`:

```csharp
namespace Tgysm.Domain.Users;

public enum UserRole
{
    Employee,
    ItStaff,
    Admin,
}
```

`backend/src/Tgysm.Domain/Users/UserRoleNames.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Tgysm.Domain.Users;

/// <summary>Canonical role names used in the database, JSON and claims (ADR-0005).</summary>
public static class UserRoleNames
{
    public const string Employee = "employee";
    public const string ItStaff = "it_staff";
    public const string Admin = "admin";

    public static string ToName(UserRole role) => role switch
    {
        UserRole.Employee => Employee,
        UserRole.ItStaff => ItStaff,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };

    public static bool TryParse([NotNullWhen(true)] string? name, out UserRole role)
    {
        switch (name)
        {
            case Employee: role = UserRole.Employee; return true;
            case ItStaff: role = UserRole.ItStaff; return true;
            case Admin: role = UserRole.Admin; return true;
            default: role = default; return false;
        }
    }
}
```

`backend/src/Tgysm.Domain/Users/User.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Users;

public sealed class User : Entity
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int SearchNameMaxLength = (NameMaxLength * 2) + EmailMaxLength + 2;
    public const int SecurityStampMaxLength = 64;
    public const int MaxFailedSignInAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const string SecurityStampFormat = "N";

    private User()
    {
    }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public bool IsActive { get; private set; }
    public bool MustChangePassword { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = string.Empty;
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Emails compare with invariant lower case; Turkish casing would turn "I" into "ı".</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static User Create(
        string firstName,
        string lastName,
        string email,
        UserRole role,
        Guid? departmentId,
        Guid? locationId,
        string temporaryPasswordHash,
        DateTimeOffset now)
    {
        var user = new User { Role = role, IsActive = true, CreatedAt = now };
        user.SetProfile(firstName, lastName, email, departmentId, locationId, now);
        user.SetTemporaryPassword(temporaryPasswordHash, now);
        return user;
    }

    public void UpdateProfile(string firstName, string lastName, string email, Guid? departmentId, Guid? locationId, DateTimeOffset now) =>
        SetProfile(firstName, lastName, email, departmentId, locationId, now);

    public void ChangeRole(UserRole role, DateTimeOffset now)
    {
        if (Role == role)
        {
            return;
        }

        Role = role;
        TouchAndRotateStamp(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        TouchAndRotateStamp(now);
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        TouchAndRotateStamp(now);
    }

    public void SetTemporaryPassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        MustChangePassword = true;
        ClearLockout();
        TouchAndRotateStamp(now);
    }

    public void ChangePassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
        TouchAndRotateStamp(now);
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    public bool CanSignIn(DateTimeOffset now) => IsActive && !IsLockedOut(now);

    /// <summary>Counts a failed sign-in. Returns true when this failure started a lockout.</summary>
    public bool RegisterFailedSignIn(DateTimeOffset now)
    {
        AccessFailedCount++;
        UpdatedAt = now;
        if (AccessFailedCount < MaxFailedSignInAttempts)
        {
            return false;
        }

        AccessFailedCount = 0;
        LockoutEnd = now + LockoutDuration;
        return true;
    }

    public void RegisterSuccessfulSignIn(DateTimeOffset now)
    {
        ClearLockout();
        UpdatedAt = now;
    }

    private void SetProfile(string firstName, string lastName, string email, Guid? departmentId, Guid? locationId, DateTimeOffset now)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        SearchName = TurkishText.ToSearchKey($"{FirstName} {LastName} {NormalizedEmail}");
        DepartmentId = departmentId;
        LocationId = locationId;
        UpdatedAt = now;
    }

    private void ClearLockout()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    private void TouchAndRotateStamp(DateTimeOffset now)
    {
        UpdatedAt = now;
        SecurityStamp = Guid.NewGuid().ToString(SecurityStampFormat);
    }
}
```

- [ ] **Step 5: Implement `Organization`**

`backend/src/Tgysm.Domain/Organization/Department.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

public sealed class Department : Entity
{
    public const int NameMaxLength = 150;

    private Department()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Admin who approves requests of this department (used from Phase 3).</summary>
    public Guid? ApproverUserId { get; private set; }

    public bool IsActive { get; private set; }

    public static Department Create(string name, Guid? approverUserId)
    {
        var department = new Department { IsActive = true };
        department.Update(name, approverUserId);
        return department;
    }

    public void Update(string name, Guid? approverUserId)
    {
        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
        ApproverUserId = approverUserId;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
```

`backend/src/Tgysm.Domain/Organization/Location.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

/// <summary>Location tree node: campus → building/area → optional sub-location.</summary>
public sealed class Location : Entity
{
    public const int NameMaxLength = 150;

    private Location()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; }

    public static Location Create(string name, Guid? parentId)
    {
        var location = new Location { IsActive = true };
        location.Update(name, parentId);
        return location;
    }

    public void Update(string name, Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new DomainException(ErrorCodes.LocationCycle, "A location cannot be its own parent.");
        }

        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
        ParentId = parentId;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
```

`backend/src/Tgysm.Domain/Organization/Team.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

public sealed class Team : Entity
{
    public const int NameMaxLength = 150;

    private Team()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static Team Create(string name)
    {
        var team = new Team { IsActive = true };
        team.Rename(name);
        return team;
    }

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
```

`backend/src/Tgysm.Domain/Organization/TeamMembership.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

public sealed class TeamMembership : Entity
{
    private TeamMembership()
    {
    }

    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TeamMembership Create(Guid teamId, Guid userId, DateTimeOffset now) =>
        new() { TeamId = teamId, UserId = userId, CreatedAt = now };
}
```

- [ ] **Step 6: Implement `Audit`**

`backend/src/Tgysm.Domain/Audit/AuditEvent.cs`:

```csharp
namespace Tgysm.Domain.Audit;

/// <summary>Append-only audit record (database trigger rejects UPDATE/DELETE).</summary>
public sealed class AuditEvent
{
    public const int ActionMaxLength = 100;
    public const int EntityTypeMaxLength = 50;
    public const int ReasonMaxLength = 500;
    public const int CorrelationIdMaxLength = 100;

    private AuditEvent()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string Action { get; private init; } = string.Empty;
    public string EntityType { get; private init; } = string.Empty;
    public Guid? EntityId { get; private init; }
    public string? Reason { get; private init; }

    /// <summary>JSON snapshot before the change (jsonb).</summary>
    public string? OldValues { get; private init; }

    /// <summary>JSON snapshot after the change (jsonb).</summary>
    public string? NewValues { get; private init; }

    public string CorrelationId { get; private init; } = string.Empty;

    public static AuditEvent Create(
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        string action,
        string entityType,
        Guid? entityId,
        string? reason,
        string? oldValues,
        string? newValues,
        string correlationId) => new()
    {
        OccurredAt = occurredAt,
        ActorUserId = actorUserId,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        Reason = reason,
        OldValues = oldValues,
        NewValues = newValues,
        CorrelationId = correlationId,
    };
}
```

`backend/src/Tgysm.Domain/Audit/AuditActions.cs`:

```csharp
namespace Tgysm.Domain.Audit;

/// <summary>
/// Audit action codes. Every constant needs a Turkish label under "auditActions" in
/// frontend/src/shared/i18n/locales/tr/admin.json (checked by TranslationCoverageTests).
/// </summary>
public static class AuditActions
{
    public const string FirstAdminCreated = "setup.first_admin_created";

    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string LockedOut = "auth.locked_out";
    public const string PasswordChanged = "auth.password_changed";

    public const string UserCreated = "users.created";
    public const string UserUpdated = "users.updated";
    public const string UserRoleChanged = "users.role_changed";
    public const string UserDeactivated = "users.deactivated";
    public const string UserReactivated = "users.reactivated";
    public const string UserPasswordReset = "users.password_reset";

    public const string DepartmentCreated = "departments.created";
    public const string DepartmentUpdated = "departments.updated";
    public const string DepartmentDeactivated = "departments.deactivated";
    public const string DepartmentReactivated = "departments.reactivated";

    public const string LocationCreated = "locations.created";
    public const string LocationUpdated = "locations.updated";
    public const string LocationDeactivated = "locations.deactivated";
    public const string LocationReactivated = "locations.reactivated";

    public const string TeamCreated = "teams.created";
    public const string TeamUpdated = "teams.updated";
    public const string TeamDeactivated = "teams.deactivated";
    public const string TeamReactivated = "teams.reactivated";
    public const string TeamMemberAdded = "teams.member_added";
    public const string TeamMemberRemoved = "teams.member_removed";
}
```

`backend/src/Tgysm.Domain/Audit/AuditEntityTypes.cs`:

```csharp
namespace Tgysm.Domain.Audit;

/// <summary>Entity type names used in audit rows and NotFound errors. Turkish labels live under "entityTypes" in admin.json.</summary>
public static class AuditEntityTypes
{
    public const string User = "user";
    public const string Department = "department";
    public const string Location = "location";
    public const string Team = "team";
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Domain.Tests tests/Tgysm.ArchitectureTests --nologo`
Expected: all PASS, 0 warnings.

- [ ] **Step 8: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/lint.sh && git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 4: Persistence — DbContext, mappings, migrations, database fixture

**Files:**
- Create: `backend/src/Tgysm.Application/Abstractions/IAppDbContext.cs`, `backend/src/Tgysm.Application/Common/ConcurrencyException.cs`
- Create: `backend/src/Tgysm.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/Tgysm.Infrastructure/Persistence/{AppDbContext,AppDbContextFactory}.cs`
- Create: `backend/src/Tgysm.Infrastructure/Persistence/Configurations/*.cs`, `Persistence/Converters/UserRoleConversion.cs`
- Create (generated + edited): `backend/src/Tgysm.Infrastructure/Persistence/Migrations/*`
- Create: `tools/db-up.sh`, `tools/migrate.sh`
- Create: `backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/{PostgresDatabase,DatabaseCollection}.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Persistence/PersistenceTests.cs`

**Interfaces:**
- Consumes: Task 3 domain types.
- Produces:
  - `interface IAppDbContext { DbSet<User> Users; DbSet<Department> Departments; DbSet<Location> Locations; DbSet<Team> Teams; DbSet<TeamMembership> TeamMemberships; DbSet<AuditEvent> AuditEvents; Task<int> SaveChangesAsync(CancellationToken); void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion) where TEntity : Entity; }`
  - `sealed class ConcurrencyException : Exception`
  - `AppDbContext` (public), `DependencyInjection.AddInfrastructure(this IServiceCollection)`, `DependencyInjection.ConnectionStringName = "Default"`
  - `AppDbContext.SaveChangesAsync` translates unique violations to `DomainException(ErrorCodes.Duplicate)` and concurrency failures to `ConcurrencyException`.
  - Test fixture `PostgresDatabase { string ConnectionString; AppDbContext CreateDbContext(); Task ResetAsync(); }`, collection `DatabaseCollection.Name = "database"`.
  - Tables: `users`, `departments`, `locations`, `teams`, `team_memberships`, `audit_events`.

- [ ] **Step 1: Add the Application ports used by persistence**

`backend/src/Tgysm.Application/Abstractions/IAppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Department> Departments { get; }
    DbSet<Location> Locations { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMembership> TeamMemberships { get; }
    DbSet<AuditEvent> AuditEvents { get; }

    /// <summary>
    /// Saves all tracked changes in one transaction. Throws <see cref="Common.ConcurrencyException"/> on a stale
    /// <see cref="Entity.Version"/> and <see cref="DomainException"/> with <see cref="ErrorCodes.Duplicate"/> on a unique violation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Makes the next save fail with a concurrency error unless the row still has <paramref name="expectedVersion"/>.</summary>
    void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : Entity;
}
```

`backend/src/Tgysm.Application/Common/ConcurrencyException.cs`:

```csharp
namespace Tgysm.Application.Common;

/// <summary>The row changed since the client read it (maps to 409 concurrency.conflict).</summary>
public sealed class ConcurrencyException(Exception innerException)
    : Exception("The record was changed by another request.", innerException);
```

- [ ] **Step 2: Write the database fixture**

`backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/PostgresDatabase.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>Creates a throw-away database on local PostgreSQL 17, applies migrations, drops it at the end.</summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    public const string AdminConnectionEnvironmentVariable = "TGYSM_TEST_ADMIN_CONNECTION";
    private const string DatabaseNamePrefix = "tgysm_test_";
    private const string AdminDatabase = "postgres";
    private const string LocalHost = "localhost";

    private const string TruncateAllSql =
        "TRUNCATE team_memberships, teams, audit_events, users, departments, locations RESTART IDENTITY CASCADE";

    private readonly string _databaseName = DatabaseNamePrefix + Guid.NewGuid().ToString("N");
    private string _adminConnectionString = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable(AdminConnectionEnvironmentVariable)
            ?? new NpgsqlConnectionStringBuilder { Host = LocalHost, Database = AdminDatabase, Username = Environment.UserName }.ConnectionString;

        await ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(TruncateAllSql);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/DatabaseCollection.cs`:

```csharp
namespace Tgysm.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "database";
}
```

- [ ] **Step 3: Write the failing persistence tests**

`backend/tests/Tgysm.Api.IntegrationTests/Persistence/PersistenceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class PersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string PasswordHash = "hash";
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await database.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User NewUser(string email) =>
        User.Create("Ali", "Yılmaz", email, UserRole.Employee, null, null, PasswordHash, Now);

    [Fact]
    public async Task Duplicate_email_differing_only_in_case_is_rejected()
    {
        await using (var first = database.CreateDbContext())
        {
            first.Users.Add(NewUser("Ali@Example.com"));
            await first.SaveChangesAsync(Ct);
        }

        await using var second = database.CreateDbContext();
        second.Users.Add(NewUser("ali@example.com"));

        var error = await Assert.ThrowsAsync<DomainException>(() => second.SaveChangesAsync(Ct));
        Assert.Equal(ErrorCodes.Duplicate, error.Code);
    }

    [Fact]
    public async Task Audit_rows_cannot_be_updated_or_deleted()
    {
        await using var db = database.CreateDbContext();
        db.AuditEvents.Add(AuditEvent.Create(Now, null, AuditActions.UserCreated, AuditEntityTypes.User, Guid.NewGuid(), null, null, null, "test"));
        await db.SaveChangesAsync(Ct);

        var update = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync("UPDATE audit_events SET action = 'tampered'", Ct));
        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync("DELETE FROM audit_events", Ct));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, update.SqlState);
        Assert.Equal(PostgresErrorCodes.RestrictViolation, delete.SqlState);
    }

    [Fact]
    public async Task Stale_write_throws_concurrency_exception()
    {
        var user = NewUser("stale@example.com");
        await using (var seed = database.CreateDbContext())
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync(Ct);
        }

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();
        var firstCopy = await first.Users.SingleAsync(u => u.Id == user.Id, Ct);
        var secondCopy = await second.Users.SingleAsync(u => u.Id == user.Id, Ct);

        firstCopy.ChangeRole(UserRole.Admin, Now);
        await first.SaveChangesAsync(Ct);
        secondCopy.ChangeRole(UserRole.ItStaff, Now);

        await Assert.ThrowsAsync<ConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Expected_version_mismatch_throws_concurrency_exception()
    {
        await using var db = database.CreateDbContext();
        var user = NewUser("expected@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);

        db.SetExpectedVersion(user, user.Version + 1);
        user.ChangeRole(UserRole.Admin, Now);

        await Assert.ThrowsAsync<ConcurrencyException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Root_locations_with_same_name_are_rejected_but_siblings_under_other_parents_are_allowed()
    {
        await using var db = database.CreateDbContext();
        var campusA = Location.Create("Kampüs A", null);
        var campusB = Location.Create("Kampüs B", null);
        db.Locations.AddRange(campusA, campusB, Location.Create("Bina 1", campusA.Id), Location.Create("Bina 1", campusB.Id));
        await db.SaveChangesAsync(Ct);

        await using var other = database.CreateDbContext();
        other.Locations.Add(Location.Create("kampüs a", null));

        var error = await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct));
        Assert.Equal(ErrorCodes.Duplicate, error.Code);
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: build FAIL — `Tgysm.Infrastructure.Persistence.AppDbContext` not found.

- [ ] **Step 4: Implement the role converter and configurations**

`backend/src/Tgysm.Infrastructure/Persistence/Converters/UserRoleConversion.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Converters;

internal static class UserRoleConversion
{
    public const int MaxLength = 20;

    public static readonly ValueConverter<UserRole, string> Converter = new(
        role => UserRoleNames.ToName(role),
        name => Parse(name));

    private static UserRole Parse(string name) =>
        UserRoleNames.TryParse(name, out var role)
            ? role
            : throw new InvalidOperationException($"Unknown role '{name}' in database.");
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/EntityConfigurationExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Common;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal static class EntityConfigurationExtensions
{
    public static void ConfigureEntityBase<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Npgsql maps a uint row version to the xmin system column (ADR-0002).
        builder.Property(e => e.Version).IsRowVersion();
    }
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/UserConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const string TableName = "users";
    private const string RoleCheckName = "ck_users_role";

    private static readonly string RoleCheckSql =
        $"role IN ('{UserRoleNames.Employee}', '{UserRoleNames.ItStaff}', '{UserRoleNames.Admin}')";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(RoleCheckName, RoleCheckSql));
        builder.ConfigureEntityBase();

        builder.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.SearchName).HasMaxLength(User.SearchNameMaxLength).IsRequired();
        builder.Property(u => u.Role).HasConversion(UserRoleConversion.Converter).HasMaxLength(UserRoleConversion.MaxLength);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.SecurityStamp).HasMaxLength(User.SecurityStampMaxLength).IsRequired();

        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.HasOne<Department>().WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(u => u.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/DepartmentConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.ConfigureEntityBase();

        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.NormalizedName).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.HasIndex(d => d.NormalizedName).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/LocationConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.ConfigureEntityBase();

        builder.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).IsRequired();
        builder.Property(l => l.NormalizedName).HasMaxLength(Location.NameMaxLength).IsRequired();

        // Two root locations (parent_id NULL) with the same name must also collide.
        builder.HasIndex(l => new { l.ParentId, l.NormalizedName }).IsUnique().AreNullsDistinct(false);

        builder.HasOne<Location>().WithMany().HasForeignKey(l => l.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/TeamConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.ConfigureEntityBase();

        builder.Property(t => t.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(t => t.NormalizedName).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.HasIndex(t => t.NormalizedName).IsUnique();
    }
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/TeamMembershipConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships");
        builder.ConfigureEntityBase();

        builder.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();
        builder.HasOne<Team>().WithMany().HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

`backend/src/Tgysm.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Audit;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    private const string JsonColumnType = "jsonb";

    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Action).HasMaxLength(AuditEvent.ActionMaxLength).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(AuditEvent.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(AuditEvent.ReasonMaxLength);
        builder.Property(a => a.CorrelationId).HasMaxLength(AuditEvent.CorrelationIdMaxLength).IsRequired();
        builder.Property(a => a.OldValues).HasColumnType(JsonColumnType);
        builder.Property(a => a.NewValues).HasColumnType(JsonColumnType);

        // No FK on actor_user_id: audit rows must survive any change to users.
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
```

- [ ] **Step 5: Implement `AppDbContext`, the design-time factory and DI**

`backend/src/Tgysm.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException(ErrorCodes.Duplicate, "A unique constraint was violated.", exception);
        }
    }

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : Entity =>
        Entry(entity).Property(e => e.Version).OriginalValue = expectedVersion;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

`backend/src/Tgysm.Infrastructure/Persistence/AppDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>Used only by dotnet-ef (migrations add / database update).</summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConnectionEnvironmentVariable = "TGYSM_CONNECTION_STRING";

    // "migrations add" never connects; this value only satisfies the provider.
    private const string DesignTimeFallbackConnection = "Host=localhost;Database=tgysm_design;Username=tgysm";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable) ?? DesignTimeFallbackConnection;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
```

`backend/src/Tgysm.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The connection string is read when the first DbContext is created, so hosts that never
        // touch the database (OpenAPI generation) start without one.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured. Run tools/db-up.sh.");
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        return services;
    }
}
```

- [ ] **Step 6: Generate the initial migration**

Run:

```bash
cd /Users/lektuel/claude/TGYSM/backend
dotnet tool run dotnet-ef migrations add Initial \
  --project src/Tgysm.Infrastructure --startup-project src/Tgysm.Infrastructure \
  --output-dir Persistence/Migrations
```

Expected: `Persistence/Migrations/<timestamp>_Initial.cs` creates the six tables with snake_case columns, `ck_users_role`, unique indexes on `users.normalized_email`, `departments.normalized_name`, `teams.normalized_name`, `(team_id, user_id)` and `(parent_id, normalized_name)` with `NULLS NOT DISTINCT`. Open the file and confirm these by reading it.

Run the tests: `dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: `Audit_rows_cannot_be_updated_or_deleted` FAILS (no trigger yet: `Assert.Throws() Failure: No exception was thrown`); the other four PASS.

- [ ] **Step 7: Add the append-only trigger migration**

Run:

```bash
cd /Users/lektuel/claude/TGYSM/backend
dotnet tool run dotnet-ef migrations add AuditAppendOnly \
  --project src/Tgysm.Infrastructure --startup-project src/Tgysm.Infrastructure \
  --output-dir Persistence/Migrations
```

Replace the body of the generated `<timestamp>_AuditAppendOnly.cs` class with:

```csharp
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION audit_events_reject_change() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'audit_events is append-only' USING ERRCODE = 'restrict_violation';
            END;
            $$;

            CREATE TRIGGER audit_events_append_only
                BEFORE UPDATE OR DELETE ON audit_events
                FOR EACH ROW EXECUTE FUNCTION audit_events_reject_change();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS audit_events_append_only ON audit_events;
            DROP FUNCTION IF EXISTS audit_events_reject_change();
            """);
    }
```

Run: `dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: 5 PASS.

- [ ] **Step 8: Write `tools/db-up.sh` and `tools/migrate.sh`**

`tools/db-up.sh`:

```bash
#!/usr/bin/env bash
# Ensure the local PostgreSQL 17 development role and database exist.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly ADMIN_DB="postgres"

if ! pg_isready -h "$DB_HOST" >/dev/null; then
  echo "PostgreSQL is not running on $DB_HOST. Start it: brew services start postgresql@17" >&2
  exit 1
fi

if [[ "$(psql -h "$DB_HOST" -d "$ADMIN_DB" -tAc "SELECT 1 FROM pg_roles WHERE rolname = '$DB_USER'")" != "1" ]]; then
  psql -h "$DB_HOST" -d "$ADMIN_DB" -c "CREATE ROLE \"$DB_USER\" LOGIN"
fi

if [[ "$(psql -h "$DB_HOST" -d "$ADMIN_DB" -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'")" != "1" ]]; then
  psql -h "$DB_HOST" -d "$ADMIN_DB" -c "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\""
fi

echo "Database $DB_NAME is ready on $DB_HOST (role $DB_USER)."
```

`tools/migrate.sh`:

```bash
#!/usr/bin/env bash
# Apply EF Core migrations to the database named by TGYSM_DB_* (default: local tgysm_dev).
set -euo pipefail
cd "$(dirname "$0")/../backend"

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"

export TGYSM_CONNECTION_STRING="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet tool run dotnet-ef database update \
  --project src/Tgysm.Infrastructure --startup-project src/Tgysm.Infrastructure
```

Run:

```bash
chmod +x /Users/lektuel/claude/TGYSM/tools/*.sh
/Users/lektuel/claude/TGYSM/tools/db-up.sh && /Users/lektuel/claude/TGYSM/tools/migrate.sh; echo "exit=$?"
psql -h localhost -d tgysm_dev -c '\dt'
```

Expected: `exit=0`; `\dt` lists the six tables plus `__EFMigrationsHistory`.

- [ ] **Step 9: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`
Expected: `exit=0`.

---

### Task 5: Application core — ports, errors, paging, password rules, audit log, DI

**Files:**
- Create: `backend/src/Tgysm.Application/Abstractions/{IPasswordService,ITemporaryPasswordGenerator,ICurrentActor,IAuditLog}.cs`
- Create: `backend/src/Tgysm.Application/Common/{NotFoundException,AuthenticationFailedException,SaveChangesExtensions}.cs`
- Create: `backend/src/Tgysm.Application/Common/Paging/{PageRequest,PagedResult,QueryablePagingExtensions}.cs`
- Create: `backend/src/Tgysm.Application/Security/{PasswordRules,TemporaryPasswordGenerator}.cs`
- Create: `backend/src/Tgysm.Application/Audit/AuditLog.cs`, `backend/src/Tgysm.Application/DependencyInjection.cs`
- Create: `backend/src/Tgysm.Infrastructure/Security/PasswordService.cs`; Modify: `backend/src/Tgysm.Infrastructure/DependencyInjection.cs`
- Test: `backend/tests/Tgysm.Application.Tests/{PageRequestTests,PasswordRulesTests,TemporaryPasswordGeneratorTests}.cs`, `backend/tests/Tgysm.Api.IntegrationTests/Security/PasswordServiceTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext`, domain types.
- Produces:
  - `interface IPasswordService { string Hash(string password); bool Verify(string passwordHash, string password); string DummyHash { get; } }`
  - `interface ITemporaryPasswordGenerator { string Generate(); }`
  - `interface ICurrentActor { Guid? UserId { get; } string CorrelationId { get; } }`
  - `interface IAuditLog { void Record(string action, string entityType, Guid? entityId, object? oldValues = null, object? newValues = null, string? reason = null, Guid? actorUserId = null); }`
  - `NotFoundException(string entityType)` with `Code = ErrorCodes.NotFound`; `AuthenticationFailedException()`
  - `SaveChangesExtensions.SaveTranslatingDuplicateAsync(this IAppDbContext, string code, string message, CancellationToken)`
  - `record PageRequest(int Page, int PageSize)` with `FirstPage=1`, `DefaultPageSize=20`, `MaxPageSize=100`, `NormalizedPage`, `NormalizedPageSize`; `record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`; `IQueryable<T>.ToPagedResultAsync(PageRequest, CancellationToken)`
  - `PasswordRules.MinLength=12`, `MaxLength=128`, `MustBeStrongPassword<T>()` (error code `auth.password_policy`)
  - `TemporaryPasswordGenerator.Length = 16` (implements `ITemporaryPasswordGenerator`)
  - `DependencyInjection.AddApplication(this IServiceCollection)` registers every non-abstract class whose name ends in `Handler` as scoped, all validators, `IAuditLog`, `ITemporaryPasswordGenerator`, `TimeProvider.System`.

- [ ] **Step 1: Write the failing unit tests**

`backend/tests/Tgysm.Application.Tests/PageRequestTests.cs`:

```csharp
using Tgysm.Application.Common.Paging;

namespace Tgysm.Application.Tests;

public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 0, PageRequest.FirstPage, 1)]
    [InlineData(-5, 20, PageRequest.FirstPage, 20)]
    [InlineData(3, 1000, 3, PageRequest.MaxPageSize)]
    public void Normalizes_page_and_caps_page_size(int page, int size, int expectedPage, int expectedSize)
    {
        var request = new PageRequest(page, size);

        Assert.Equal(expectedPage, request.NormalizedPage);
        Assert.Equal(expectedSize, request.NormalizedPageSize);
    }
}
```

`backend/tests/Tgysm.Application.Tests/PasswordRulesTests.cs`:

```csharp
using FluentValidation;
using Tgysm.Application.Security;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Tests;

public sealed class PasswordRulesTests
{
    private sealed record Input(string Password);

    private sealed class InputValidator : AbstractValidator<Input>
    {
        public InputValidator() => RuleFor(i => i.Password).MustBeStrongPassword();
    }

    private readonly InputValidator _validator = new();

    [Theory]
    [InlineData("short1a")]
    [InlineData("onlyletterslong")]
    [InlineData("123456789012")]
    public void Rejects_weak_passwords(string password)
    {
        var result = _validator.Validate(new Input(password));

        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.PasswordPolicy);
    }

    [Fact]
    public void Rejects_password_longer_than_maximum() =>
        Assert.False(_validator.Validate(new Input(new string('a', PasswordRules.MaxLength) + "1")).IsValid);

    [Fact]
    public void Accepts_twelve_characters_with_letter_and_digit() =>
        Assert.True(_validator.Validate(new Input("abcdefghijk1")).IsValid);
}
```

`backend/tests/Tgysm.Application.Tests/TemporaryPasswordGeneratorTests.cs`:

```csharp
using Tgysm.Application.Security;

namespace Tgysm.Application.Tests;

public sealed class TemporaryPasswordGeneratorTests
{
    private const int SampleCount = 200;

    [Fact]
    public void Generates_passwords_that_satisfy_the_policy_and_differ()
    {
        var generator = new TemporaryPasswordGenerator();

        var passwords = Enumerable.Range(0, SampleCount).Select(_ => generator.Generate()).ToList();

        Assert.All(passwords, p =>
        {
            Assert.Equal(TemporaryPasswordGenerator.Length, p.Length);
            Assert.True(p.Length >= PasswordRules.MinLength);
            Assert.Contains(p, char.IsLetter);
            Assert.Contains(p, char.IsDigit);
        });
        Assert.Equal(SampleCount, passwords.Distinct().Count());
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Security/PasswordServiceTests.cs`:

```csharp
using Tgysm.Infrastructure.Security;

namespace Tgysm.Api.IntegrationTests.Security;

public sealed class PasswordServiceTests
{
    private const string Password = "Correct-horse-1";

    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_verifies_only_the_original_password()
    {
        var hash = _service.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.True(_service.Verify(hash, Password));
        Assert.False(_service.Verify(hash, Password + "x"));
    }

    [Fact]
    public void Dummy_hash_rejects_any_password() =>
        Assert.False(_service.Verify(_service.DummyHash, Password));
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test --nologo`
Expected: build FAIL — `PageRequest`, `PasswordRules`, `TemporaryPasswordGenerator`, `PasswordService` not found.

- [ ] **Step 2: Implement the ports**

`backend/src/Tgysm.Application/Abstractions/IPasswordService.cs`:

```csharp
namespace Tgysm.Application.Abstractions;

public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    /// <summary>Hash of a random secret; verifying against it costs the same time as a real check (login timing).</summary>
    string DummyHash { get; }
}
```

`backend/src/Tgysm.Application/Abstractions/ITemporaryPasswordGenerator.cs`:

```csharp
namespace Tgysm.Application.Abstractions;

public interface ITemporaryPasswordGenerator
{
    string Generate();
}
```

`backend/src/Tgysm.Application/Abstractions/ICurrentActor.cs`:

```csharp
namespace Tgysm.Application.Abstractions;

/// <summary>Who performs the current operation (HTTP user or CLI) and the correlation id for audit.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }

    string CorrelationId { get; }
}
```

`backend/src/Tgysm.Application/Abstractions/IAuditLog.cs`:

```csharp
namespace Tgysm.Application.Abstractions;

public interface IAuditLog
{
    /// <summary>
    /// Adds an audit row to the current unit of work; it is saved in the same transaction as the change.
    /// Never pass passwords, hashes or tokens in <paramref name="oldValues"/> / <paramref name="newValues"/>.
    /// </summary>
    void Record(
        string action,
        string entityType,
        Guid? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? actorUserId = null);
}
```

- [ ] **Step 3: Implement common errors and helpers**

`backend/src/Tgysm.Application/Common/NotFoundException.cs`:

```csharp
using Tgysm.Domain.Common;

namespace Tgysm.Application.Common;

public sealed class NotFoundException(string entityType) : Exception($"{entityType} was not found.")
{
    public string Code { get; } = ErrorCodes.NotFound;

    public string EntityType { get; } = entityType;
}
```

`backend/src/Tgysm.Application/Common/AuthenticationFailedException.cs`:

```csharp
namespace Tgysm.Application.Common;

/// <summary>Login or session failure. Always surfaces as 401 auth.invalid_credentials (no account enumeration).</summary>
public sealed class AuthenticationFailedException() : Exception("Authentication failed.");
```

`backend/src/Tgysm.Application/Common/SaveChangesExtensions.cs`:

```csharp
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Common;

internal static class SaveChangesExtensions
{
    /// <summary>Saves and turns a generic unique violation (a lost race with a parallel request) into a specific code.</summary>
    public static async Task SaveTranslatingDuplicateAsync(this IAppDbContext db, string code, string message, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception) when (exception.Code == ErrorCodes.Duplicate)
        {
            throw new DomainException(code, message, exception);
        }
    }
}
```

`backend/src/Tgysm.Application/Common/Paging/PageRequest.cs`:

```csharp
namespace Tgysm.Application.Common.Paging;

public sealed record PageRequest(int Page, int PageSize)
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    private const int MinPageSize = 1;

    public int NormalizedPage => Math.Max(FirstPage, Page);

    public int NormalizedPageSize => Math.Clamp(PageSize, MinPageSize, MaxPageSize);
}
```

`backend/src/Tgysm.Application/Common/Paging/PagedResult.cs`:

```csharp
namespace Tgysm.Application.Common.Paging;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

`backend/src/Tgysm.Application/Common/Paging/QueryablePagingExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Tgysm.Application.Common.Paging;

public static class QueryablePagingExtensions
{
    /// <summary>The query must already be ordered; paging an unordered query returns unstable pages.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken)
    {
        var page = request.NormalizedPage;
        var size = request.NormalizedPageSize;
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - PageRequest.FirstPage) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page, size, total);
    }
}
```

- [ ] **Step 4: Implement password rules and the generator**

`backend/src/Tgysm.Application/Security/PasswordRules.cs`:

```csharp
using FluentValidation;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Security;

/// <summary>Password policy (ADR-0004): 12–128 characters, at least one letter and one digit.</summary>
public static class PasswordRules
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsStrong)
            .WithErrorCode(ErrorCodes.PasswordPolicy)
            .WithMessage($"Password must be {MinLength}-{MaxLength} characters and contain a letter and a digit.");

    private static bool IsStrong(string? password) =>
        password is { Length: >= MinLength and <= MaxLength }
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
```

`backend/src/Tgysm.Application/Security/TemporaryPasswordGenerator.cs`:

```csharp
using System.Security.Cryptography;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Security;

/// <summary>Cryptographically random temporary passwords that satisfy <see cref="PasswordRules"/>.</summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    public const int Length = 16;

    // Ambiguous characters (0/O, 1/l/I) are left out; admins read these passwords to users.
    private const string Letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Alphabet = Letters + Digits;
    private const int GuaranteedCharacters = 2;

    public string Generate()
    {
        var characters = new char[Length];
        characters[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        characters[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        RandomNumberGenerator.GetItems(Alphabet.AsSpan(), characters.AsSpan(GuaranteedCharacters));
        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }
}
```

- [ ] **Step 5: Implement the audit log and Application DI**

`backend/src/Tgysm.Application/Audit/AuditLog.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Audit;

internal sealed class AuditLog(IAppDbContext db, ICurrentActor actor, TimeProvider clock) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public void Record(
        string action,
        string entityType,
        Guid? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? actorUserId = null)
    {
        db.AuditEvents.Add(AuditEvent.Create(
            clock.GetUtcNow(),
            actorUserId ?? actor.UserId,
            action,
            entityType,
            entityId,
            reason,
            Serialize(oldValues),
            Serialize(newValues),
            actor.CorrelationId));
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
}
```

`backend/src/Tgysm.Application/DependencyInjection.cs`:

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Audit;
using Tgysm.Application.Security;

namespace Tgysm.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        var handlerTypes = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true }
                && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal));
        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
```

- [ ] **Step 6: Implement the password service**

`backend/src/Tgysm.Infrastructure/Security/PasswordService.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Security;

/// <summary>PBKDF2 hashing via ASP.NET Core Identity's PasswordHasher (ADR-0004).</summary>
public sealed class PasswordService : IPasswordService
{
    // PasswordHasher<T> ignores the user argument in its default implementation.
    private static readonly object HashSubject = new();

    private readonly PasswordHasher<object> _hasher = new();
    private readonly Lazy<string> _dummyHash;

    public PasswordService() =>
        _dummyHash = new Lazy<string>(() => _hasher.HashPassword(HashSubject, Guid.NewGuid().ToString()));

    public string DummyHash => _dummyHash.Value;

    public string Hash(string password) => _hasher.HashPassword(HashSubject, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(HashSubject, passwordHash, password) != PasswordVerificationResult.Failed;
}
```

In `backend/src/Tgysm.Infrastructure/DependencyInjection.cs`, add `using Tgysm.Infrastructure.Security;` and, before `return services;`:

```csharp
        services.AddSingleton<IPasswordService, PasswordService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test --nologo`
Expected: all PASS (Domain, Application, Architecture, Integration), 0 warnings.

- [ ] **Step 8: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/lint.sh && git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 6: API host skeleton and first-admin CLI

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Users/UserRules.cs`
- Create: `backend/src/Tgysm.Application/Features/Setup/SetupFirstAdmin.cs`
- Create: `backend/src/Tgysm.Api/Http/HttpCurrentActor.cs`, `backend/src/Tgysm.Api/Cli/SetupAdminCommand.cs`
- Modify: `backend/src/Tgysm.Api/Program.cs`
- Create: `tools/setup-admin.sh`
- Create: `backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/{ApiFactory,ApiCollection,TestUsers}.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Setup/SetupFirstAdminTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext`, `IPasswordService`, `ITemporaryPasswordGenerator`, `IAuditLog`, `AddApplication()`, `AddInfrastructure()`.
- Produces:
  - `UserRules.ValidPersonName<T>()`, `UserRules.ValidEmail<T>()` (FluentValidation extensions, internal)
  - `record SetupFirstAdminCommand(string Email, string FirstName, string LastName)`, `record SetupFirstAdminResult(Guid UserId, string TemporaryPassword)`, `SetupFirstAdminHandler.HandleAsync(command, ct)`
  - `SetupAdminCommand.Name = "setup-admin"`, `SetupAdminCommand.RunAsync(IServiceProvider, IReadOnlyList<string> args, TextWriter output, TextWriter error) : Task<int>` (0 success, 1 failure)
  - Test infrastructure: `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` with `Database`, `ResetAsync()`, virtual `LoginPermitLimitPerMinute`; `ApiCollection.Name = "api"`; `TestUsers.DefaultPassword`, `TestUsers.CreateAsync(ApiFactory, UserRole, string email, bool mustChangePassword = false, Guid? departmentId = null, Guid? locationId = null, string firstName = "Test", string lastName = "User")`

- [ ] **Step 1: Write the test infrastructure**

`backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/ApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Tgysm.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>API host on a throw-away PostgreSQL database.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestingEnvironment = "Testing";
    private const int GenerousLoginLimit = 10_000;

    public PostgresDatabase Database { get; } = new();

    /// <summary>High by default so tests that log in many times never hit the rate limit.</summary>
    protected virtual int LoginPermitLimitPerMinute => GenerousLoginLimit;

    public async ValueTask InitializeAsync() => await Database.InitializeAsync();

    public Task ResetAsync() => Database.ResetAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", Database.ConnectionString);
        builder.UseSetting("Security:LoginPermitLimitPerMinute", LoginPermitLimitPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/ApiCollection.cs`:

```csharp
namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>All API tests share one host and database and run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/TestUsers.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

public static class TestUsers
{
    public const string DefaultPassword = "Test-password-2026";

    public static async Task<User> CreateAsync(
        ApiFactory factory,
        UserRole role,
        string email,
        bool mustChangePassword = false,
        Guid? departmentId = null,
        Guid? locationId = null,
        string firstName = "Test",
        string lastName = "User")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var now = DateTimeOffset.UtcNow;

        var user = User.Create(firstName, lastName, email, role, departmentId, locationId, passwords.Hash(DefaultPassword), now);
        if (!mustChangePassword)
        {
            user.ChangePassword(passwords.Hash(DefaultPassword), now);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }
}
```

- [ ] **Step 2: Write the failing setup tests**

`backend/tests/Tgysm.Api.IntegrationTests/Setup/SetupFirstAdminTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.Cli;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Setup;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Setup;

[Collection(ApiCollection.Name)]
public sealed class SetupFirstAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CliSuccess = 0;
    private const int CliFailure = 1;
    private static readonly SetupFirstAdminCommand Command = new("admin@example.com", "Tugay", "İltuş");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creates_admin_with_temporary_password_when_no_user_exists()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetupFirstAdminHandler>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var result = await handler.HandleAsync(Command, Ct);

        var admin = await db.Users.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(result.UserId, admin.Id);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
        Assert.Equal(TemporaryPasswordGenerator.Length, result.TemporaryPassword.Length);
        Assert.True(passwords.Verify(admin.PasswordHash, result.TemporaryPassword));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.FirstAdminCreated && a.EntityId == admin.Id, Ct));
    }

    [Fact]
    public async Task Refuses_when_any_user_exists()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, "existing@example.com");
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetupFirstAdminHandler>();

        var error = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(Command, Ct));

        Assert.Equal(ErrorCodes.SetupAlreadyCompleted, error.Code);
    }

    [Fact]
    public async Task Cli_prints_temporary_password_once_and_then_refuses()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] args = ["--email", "admin@example.com", "--first-name", "Tugay", "--last-name", "İltuş"];

        var first = await SetupAdminCommand.RunAsync(factory.Services, args, output, error);
        var second = await SetupAdminCommand.RunAsync(factory.Services, args, output, error);

        Assert.Equal(CliSuccess, first);
        Assert.Contains("Temporary password", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(CliFailure, second);
        Assert.Contains("already exists", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_prints_usage_when_options_are_missing()
    {
        var error = new StringWriter();

        var exitCode = await SetupAdminCommand.RunAsync(factory.Services, ["--email", "admin@example.com"], TextWriter.Null, error);

        Assert.Equal(CliFailure, exitCode);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: build FAIL — `SetupFirstAdminHandler`, `SetupAdminCommand` not found.

- [ ] **Step 3: Implement shared user validation rules**

`backend/src/Tgysm.Application/Features/Users/UserRules.cs`:

```csharp
using FluentValidation;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

internal static class UserRules
{
    public static IRuleBuilderOptions<T, string> ValidPersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();
}
```

- [ ] **Step 4: Implement the setup handler**

`backend/src/Tgysm.Application/Features/Setup/SetupFirstAdmin.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Users;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Setup;

public sealed record SetupFirstAdminCommand(string Email, string FirstName, string LastName);

public sealed record SetupFirstAdminResult(Guid UserId, string TemporaryPassword);

internal sealed class SetupFirstAdminValidator : AbstractValidator<SetupFirstAdminCommand>
{
    public SetupFirstAdminValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
    }
}

/// <summary>Creates the first admin once (ADR-0007). Refuses when any account exists.</summary>
public sealed class SetupFirstAdminHandler(
    IAppDbContext db,
    IValidator<SetupFirstAdminCommand> validator,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<SetupFirstAdminResult> HandleAsync(SetupFirstAdminCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            throw new DomainException(ErrorCodes.SetupAlreadyCompleted, "An account already exists; setup is closed.");
        }

        var temporaryPassword = temporaryPasswords.Generate();
        var admin = User.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            UserRole.Admin,
            departmentId: null,
            locationId: null,
            passwords.Hash(temporaryPassword),
            clock.GetUtcNow());

        db.Users.Add(admin);
        audit.Record(
            AuditActions.FirstAdminCreated,
            AuditEntityTypes.User,
            admin.Id,
            newValues: new { admin.Email, admin.Role },
            actorUserId: admin.Id);
        await db.SaveChangesAsync(cancellationToken);

        return new SetupFirstAdminResult(admin.Id, temporaryPassword);
    }
}
```

- [ ] **Step 5: Implement the current actor and the CLI command**

`backend/src/Tgysm.Api/Http/HttpCurrentActor.cs`:

```csharp
using System.Security.Claims;
using Tgysm.Application.Abstractions;

namespace Tgysm.Api.Http;

/// <summary>Actor from the HTTP user; outside a request (CLI) the actor is anonymous with a generated correlation id.</summary>
internal sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private const string CliCorrelationPrefix = "cli-";
    private readonly string _fallbackCorrelationId = CliCorrelationPrefix + Guid.NewGuid().ToString("N");

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string CorrelationId => accessor.HttpContext?.TraceIdentifier ?? _fallbackCorrelationId;
}
```

`backend/src/Tgysm.Api/Cli/SetupAdminCommand.cs`:

```csharp
using FluentValidation;
using Tgysm.Application.Features.Setup;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Cli;

/// <summary><c>Tgysm.Api setup-admin --email … --first-name … --last-name …</c> (ADR-0007).</summary>
internal static class SetupAdminCommand
{
    public const string Name = "setup-admin";

    private const string EmailOption = "--email";
    private const string FirstNameOption = "--first-name";
    private const string LastNameOption = "--last-name";
    private const int Success = 0;
    private const int Failure = 1;
    private const int OptionPairSize = 2;

    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var options = ParseOptions(args);
        if (!options.TryGetValue(EmailOption, out var email)
            || !options.TryGetValue(FirstNameOption, out var firstName)
            || !options.TryGetValue(LastNameOption, out var lastName))
        {
            await error.WriteLineAsync($"Usage: {Name} {EmailOption} <email> {FirstNameOption} <name> {LastNameOption} <name>");
            return Failure;
        }

        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SetupFirstAdminHandler>();
        try
        {
            var result = await handler.HandleAsync(new SetupFirstAdminCommand(email, firstName, lastName), CancellationToken.None);
            await output.WriteLineAsync($"First admin created: {email}");
            await output.WriteLineAsync($"Temporary password (shown once): {result.TemporaryPassword}");
            return Success;
        }
        catch (DomainException exception) when (exception.Code == ErrorCodes.SetupAlreadyCompleted)
        {
            await error.WriteLineAsync("Setup refused: an account already exists.");
            return Failure;
        }
        catch (ValidationException exception)
        {
            var details = string.Join("; ", exception.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            await error.WriteLineAsync($"Invalid input: {details}");
            return Failure;
        }
    }

    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < args.Count; index += OptionPairSize)
        {
            options[args[index]] = args[index + 1];
        }

        return options;
    }
}
```

`backend/src/Tgysm.Api/Program.cs`:

```csharp
using Tgysm.Api.Cli;
using Tgysm.Api.Http;
using Tgysm.Application;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();

var app = builder.Build();

if (args is [SetupAdminCommand.Name, .. var commandArgs])
{
    return await SetupAdminCommand.RunAsync(app.Services, commandArgs, Console.Out, Console.Error);
}

await app.RunAsync();
return 0;

public partial class Program;
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: all PASS.

- [ ] **Step 7: Write `tools/setup-admin.sh`**

```bash
#!/usr/bin/env bash
# Create the first admin (once). Usage: tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"

tools/migrate.sh
export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet run --project backend/src/Tgysm.Api --no-launch-profile -- setup-admin "$@"
```

Run:

```bash
chmod +x /Users/lektuel/claude/TGYSM/tools/setup-admin.sh
/Users/lektuel/claude/TGYSM/tools/setup-admin.sh --email admin@example.com --first-name Tugay --last-name İltuş; echo "exit=$?"
/Users/lektuel/claude/TGYSM/tools/setup-admin.sh --email admin@example.com --first-name Tugay --last-name İltuş; echo "exit=$?"
```

Expected: first run prints `Temporary password (shown once): …` and `exit=0`; second prints `Setup refused: an account already exists.` and `exit=1`. Note the temporary password for manual testing in later tasks.

- [ ] **Step 8: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---

### Task 7: HTTP pipeline, cookie session, CSRF, rate limit, login/logout/me

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Auth/{SessionUser,Login,GetSession,GetMe}.cs`
- Create: `backend/src/Tgysm.Api/Http/{ApiRoutes,ApiProblem,ApiExceptionHandler,ValidationErrorMapper,CorrelationIdMiddleware}.cs`
- Create: `backend/src/Tgysm.Api/Security/{SecurityOptions,SessionClaims,SessionValidator,Policies,PasswordChangedRequirement,ApiAuthorizationResultHandler,CsrfValidationMiddleware,RateLimitPolicies}.cs`
- Create: `backend/src/Tgysm.Api/Contracts/AuthContracts.cs`, `backend/src/Tgysm.Api/Endpoints/AuthEndpoints.cs`
- Create: `backend/src/Tgysm.Api/{ApiServiceCollectionExtensions,ApiApplicationExtensions}.cs`
- Modify: `backend/src/Tgysm.Api/Tgysm.Api.csproj` (add `Microsoft.AspNetCore.OpenApi`), `backend/src/Tgysm.Api/Program.cs`, `backend/src/Tgysm.Api/appsettings.json`, `backend/src/Tgysm.Api/Properties/launchSettings.json`
- Modify: `tools/db-up.sh` (store the API connection string in user-secrets)
- Create: `backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/ApiClient.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Auth/{LoginTests,RateLimitTests}.cs`, `backend/tests/Tgysm.Api.IntegrationTests/DependencyInjectionTests.cs`

**Interfaces:**
- Consumes: Task 5 ports, Task 6 host.
- Produces:
  - `record SessionUser(Guid UserId, UserRole Role, string SecurityStamp, bool MustChangePassword)` with `static SessionUser From(User)`
  - `record LoginCommand(string Email, string Password)`, `LoginHandler.HandleAsync(LoginCommand, ct) : Task<SessionUser>`
  - `record GetSessionQuery(Guid UserId)`, `GetSessionHandler.HandleAsync(query, ct) : Task<SessionUser?>`
  - `record MeDto(Guid Id, string FirstName, string LastName, string Email, UserRole Role, bool MustChangePassword, string? DepartmentName, string? LocationName)`, `GetMeHandler.HandleAsync(ct) : Task<MeDto>`
  - `ApiRoutes.ApiPrefix = "/api"`, `ApiRoutes.V1 = "/api/v1"`, `ApiRoutes.Admin = "/api/v1/admin"`
  - `ApiProblem.WriteAsync(HttpContext, int status, string code, IDictionary<string, string[]>? errors = null)`; JSON body `{ status, title, code, errors? }`
  - `Policies.Admin = "admin"`, `Policies.PasswordChangePending = "password-change-pending"`; fallback policy = authenticated + password changed
  - `SessionClaims.CreatePrincipal(SessionUser)`; claim types `SessionClaims.SecurityStamp`, `SessionClaims.MustChangePassword`
  - `CsrfValidationMiddleware.HeaderName = "X-CSRF-TOKEN"`; `CorrelationIdMiddleware.HeaderName = "X-Correlation-Id"`
  - `SecurityOptions { LoginPermitLimitPerMinute = 10; SessionHours = 8 }` (section `Security`)
  - Endpoints: `GET /api/v1/auth/csrf` → `{ token }`; `POST /api/v1/auth/login` `{ email, password }` → `{ mustChangePassword }`; `POST /api/v1/auth/logout` → 204; `GET /api/v1/auth/me` → `MeDto`
  - `AddApiServices(this IServiceCollection, IConfiguration)`, `UseApiPipeline(this WebApplication)`, `MapApi(this WebApplication)` returning a `RouteGroupBuilder` for `/api/v1` via `ApiApplicationExtensions.MapApi`
  - Test helper `ApiClient` (`Create(ApiFactory)`, `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `SendAsync(..., withCsrf)`, `RefreshCsrfAsync`, `LoginAsync`, static `ReadCodeAsync`, static `ReadFieldCodesAsync`, `ReadJsonAsync<T>`), and `ApiClient.LoginAsNewUserAsync(ApiFactory, UserRole, string email)`

- [ ] **Step 1: Write the API test client**

`backend/tests/Tgysm.Api.IntegrationTests/Infrastructure/ApiClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>HTTPS test client with cookie jar and antiforgery handling, like the browser SPA.</summary>
public sealed class ApiClient
{
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public const string CsrfPath = "/api/v1/auth/csrf";
    public const string LoginPath = "/api/v1/auth/login";
    public const string MePath = "/api/v1/auth/me";
    public const string LogoutPath = "/api/v1/auth/logout";
    private const string CodeProperty = "code";
    private const string ErrorsProperty = "errors";
    private const string TokenProperty = "token";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly Uri HttpsBase = new("https://localhost");
    private string? _csrfToken;

    private ApiClient(HttpClient http) => Http = http;

    public HttpClient Http { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static ApiClient Create(ApiFactory factory) =>
        new(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = HttpsBase,
            HandleCookies = true,
            AllowAutoRedirect = false,
        }));

    public static async Task<(ApiClient Client, User User)> LoginAsNewUserAsync(ApiFactory factory, UserRole role, string email)
    {
        var user = await TestUsers.CreateAsync(factory, role, email);
        var client = Create(factory);
        await client.LoginAsync(email);
        return (client, user);
    }

    public async Task RefreshCsrfAsync()
    {
        var body = await Http.GetFromJsonAsync<JsonElement>(CsrfPath, Ct);
        _csrfToken = body.GetProperty(TokenProperty).GetString();
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url, Ct);

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, bool withCsrf = true) =>
        SendAsync(HttpMethod.Post, url, body, withCsrf);

    public Task<HttpResponseMessage> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body, withCsrf: true);

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, body: null, withCsrf: true);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, bool withCsrf)
    {
        if (withCsrf && _csrfToken is null)
        {
            await RefreshCsrfAsync();
        }

        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (withCsrf)
        {
            request.Headers.Add(CsrfHeader, _csrfToken);
        }

        return await Http.SendAsync(request, Ct);
    }

    /// <summary>Logs in and refreshes the antiforgery token, which is bound to the signed-in user.</summary>
    public async Task LoginAsync(string email, string password = TestUsers.DefaultPassword)
    {
        using var response = await PostAsync(LoginPath, new { email, password });
        response.EnsureSuccessStatusCode();
        await RefreshCsrfAsync();
    }

    public async Task<T> ReadJsonAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.TryGetProperty(CodeProperty, out var code) ? code.GetString() : null;
    }

    public static async Task<IReadOnlyDictionary<string, string[]>> ReadFieldCodesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.TryGetProperty(ErrorsProperty, out var errors)
            ? errors.Deserialize<Dictionary<string, string[]>>(Json)!
            : new Dictionary<string, string[]>();
    }
}
```

- [ ] **Step 2: Write the failing auth tests**

`backend/tests/Tgysm.Api.IntegrationTests/Auth/LoginTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class LoginTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Email = "employee@example.com";
    private const string CorrelationHeader = "X-Correlation-Id";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Valid_credentials_start_a_session_and_me_returns_the_user()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email, firstName: "Ayşe", lastName: "Kaya");
        var client = ApiClient.Create(factory);

        await client.LoginAsync(Email);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await client.ReadJsonAsync<JsonElement>(me);
        Assert.Equal("Ayşe", body.GetProperty("firstName").GetString());
        Assert.Equal(UserRoleNames.Employee, body.GetProperty("role").GetString());
        Assert.False(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_error()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        var client = ApiClient.Create(factory);

        using var wrongPassword = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = "Wrong-password-1" });
        using var unknownEmail = await client.PostAsync(ApiClient.LoginPath, new { email = "nobody@example.com", password = "Wrong-password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(wrongPassword));
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(unknownEmail));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        var client = ApiClient.Create(factory);

        for (var attempt = 0; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            using var failed = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = "Wrong-password-1" });
        }

        using var correct = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(correct));
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.LockedOut && a.EntityId == user.Id, Ct));
    }

    [Fact]
    public async Task Inactive_user_cannot_log_in()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        await using (var db = factory.Database.CreateDbContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id, Ct);
            tracked.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        using var response = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_csrf_token_is_rejected()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email);

        using var response = await ApiClient.Create(factory).PostAsync(
            ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword }, withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.CsrfInvalid, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Empty_credentials_return_field_errors()
    {
        using var response = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.Required, fields["email"]);
        Assert.Contains(ErrorCodes.Required, fields["password"]);
    }

    [Fact]
    public async Task Me_without_session_returns_unauthenticated()
    {
        using var response = await ApiClient.Create(factory).GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);

        using var logout = await client.PostAsync(ApiClient.LogoutPath);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_header_is_echoed_and_stored_in_audit()
    {
        const string correlationId = "e2e-login-42";
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        var client = ApiClient.Create(factory);
        await client.RefreshCsrfAsync();
        client.Http.DefaultRequestHeaders.Add(CorrelationHeader, correlationId);

        using var response = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });

        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationHeader).Single());
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.LoginSucceeded && a.CorrelationId == correlationId, Ct));
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Auth/RateLimitTests.cs`:

```csharp
using System.Net;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Auth;

public sealed class LowRateLimitApiFactory : ApiFactory
{
    public const int Limit = 2;

    protected override int LoginPermitLimitPerMinute => Limit;
}

public sealed class RateLimitTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Login_attempts_over_the_limit_get_429()
    {
        var client = ApiClient.Create(factory);
        var credentials = new { email = "nobody@example.com", password = "Wrong-password-1" };

        for (var attempt = 0; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var allowed = await client.PostAsync(ApiClient.LoginPath, credentials);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var limited = await client.PostAsync(ApiClient.LoginPath, credentials);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await ApiClient.ReadCodeAsync(limited));
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/DependencyInjectionTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;

namespace Tgysm.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DependencyInjectionTests(ApiFactory factory)
{
    private const string HandlerSuffix = "Handler";

    [Fact]
    public async Task Every_application_handler_resolves()
    {
        var handlerTypes = typeof(Tgysm.Application.AssemblyMarker).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && t.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal))
            .ToList();
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.NotEmpty(handlerTypes);
        Assert.All(handlerTypes, type => Assert.NotNull(scope.ServiceProvider.GetRequiredService(type)));
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: `LoginTests` and `RateLimitTests` FAIL with 404 (no endpoints); `DependencyInjectionTests` passes.

- [ ] **Step 3: Implement the auth handlers**

`backend/src/Tgysm.Application/Features/Auth/SessionUser.cs`:

```csharp
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

/// <summary>What the session cookie needs about a user; re-read on every request (ADR-0004).</summary>
public sealed record SessionUser(Guid UserId, UserRole Role, string SecurityStamp, bool MustChangePassword)
{
    public static SessionUser From(User user) => new(user.Id, user.Role, user.SecurityStamp, user.MustChangePassword);
}
```

`backend/src/Tgysm.Application/Features/Auth/Login.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password);

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

/// <summary>
/// Verifies credentials. Every failure throws <see cref="AuthenticationFailedException"/> so callers cannot tell
/// unknown, inactive, locked and wrong-password accounts apart.
/// </summary>
public sealed class LoginHandler(
    IAppDbContext db,
    IValidator<LoginCommand> validator,
    IPasswordService passwords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<SessionUser> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        var normalizedEmail = User.NormalizeEmail(command.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            // Same hashing cost as a real check, so response time does not reveal unknown emails.
            passwords.Verify(passwords.DummyHash, command.Password);
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, entityId: null, newValues: new { Email = normalizedEmail });
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        if (!user.CanSignIn(now))
        {
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, user.Id);
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        if (!passwords.Verify(user.PasswordHash, command.Password))
        {
            var lockedNow = user.RegisterFailedSignIn(now);
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, user.Id);
            if (lockedNow)
            {
                audit.Record(AuditActions.LockedOut, AuditEntityTypes.User, user.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        user.RegisterSuccessfulSignIn(now);
        audit.Record(AuditActions.LoginSucceeded, AuditEntityTypes.User, user.Id, actorUserId: user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return SessionUser.From(user);
    }
}
```

`backend/src/Tgysm.Application/Features/Auth/GetSession.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Auth;

public sealed record GetSessionQuery(Guid UserId);

/// <summary>Returns the session data of an active user, or null when the user is missing or inactive.</summary>
public sealed class GetSessionHandler(IAppDbContext db)
{
    public Task<SessionUser?> HandleAsync(GetSessionQuery query, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == query.UserId && u.IsActive)
            .Select(u => new SessionUser(u.Id, u.Role, u.SecurityStamp, u.MustChangePassword))
            .SingleOrDefaultAsync(cancellationToken);
}
```

`backend/src/Tgysm.Application/Features/Auth/GetMe.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

public sealed record MeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool MustChangePassword,
    string? DepartmentName,
    string? LocationName);

public sealed class GetMeHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<MeDto> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();

        var me = await (
            from user in db.Users.AsNoTracking()
            where user.Id == userId
            join department in db.Departments on user.DepartmentId equals (Guid?)department.Id into departments
            from department in departments.DefaultIfEmpty()
            join location in db.Locations on user.LocationId equals (Guid?)location.Id into locations
            from location in locations.DefaultIfEmpty()
            select new MeDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role,
                user.MustChangePassword,
                department == null ? null : department.Name,
                location == null ? null : location.Name))
            .SingleOrDefaultAsync(cancellationToken);

        return me ?? throw new NotFoundException(AuditEntityTypes.User);
    }
}
```

- [ ] **Step 4: Implement HTTP helpers**

`backend/src/Tgysm.Api/Http/ApiRoutes.cs`:

```csharp
namespace Tgysm.Api.Http;

internal static class ApiRoutes
{
    public const string ApiPrefix = "/api";
    public const string V1 = ApiPrefix + "/v1";
    public const string Auth = "/auth";
    public const string Admin = V1 + "/admin";
}
```

`backend/src/Tgysm.Api/Http/ApiProblem.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace Tgysm.Api.Http;

/// <summary>RFC 7807 body with a stable <c>code</c> and optional per-field <c>errors</c> (field → error codes).</summary>
internal static class ApiProblem
{
    public const string ContentType = "application/problem+json";
    public const string CodeKey = "code";
    public const string ErrorsKey = "errors";

    public static ProblemDetails Create(int status, string code, IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = code };
        problem.Extensions[CodeKey] = code;
        if (errors is not null)
        {
            problem.Extensions[ErrorsKey] = errors;
        }

        return problem;
    }

    public static Task WriteAsync(HttpContext context, int status, string code, IDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Create(status, code, errors), options: null, contentType: ContentType, context.RequestAborted);
    }
}
```

`backend/src/Tgysm.Api/Http/ValidationErrorMapper.cs`:

```csharp
using System.Text.Json;
using FluentValidation.Results;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Http;

/// <summary>Maps FluentValidation failures to camelCase field names and stable error codes.</summary>
internal static class ValidationErrorMapper
{
    private const char CodeSeparator = '.';

    private static readonly Dictionary<string, string> BuiltInValidatorCodes = new(StringComparer.Ordinal)
    {
        ["NotEmptyValidator"] = ErrorCodes.Required,
        ["NotNullValidator"] = ErrorCodes.Required,
        ["MaximumLengthValidator"] = ErrorCodes.TooLong,
        ["LengthValidator"] = ErrorCodes.TooLong,
        ["EmailValidator"] = ErrorCodes.InvalidEmail,
    };

    public static Dictionary<string, string[]> ToFieldCodes(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(MapCode).Distinct().ToArray());

    private static string MapCode(ValidationFailure failure) =>
        BuiltInValidatorCodes.TryGetValue(failure.ErrorCode, out var code) ? code
        : failure.ErrorCode.Contains(CodeSeparator) ? failure.ErrorCode
        : ErrorCodes.InvalidValue;
}
```

`backend/src/Tgysm.Api/Http/ApiExceptionHandler.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Tgysm.Application.Common;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Http;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validation:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, ValidationErrorMapper.ToFieldCodes(validation.Errors));
                break;
            case BadHttpRequestException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed);
                break;
            case AuthenticationFailedException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);
                break;
            case NotFoundException notFound:
                await ApiProblem.WriteAsync(context, StatusCodes.Status404NotFound, notFound.Code);
                break;
            case ConcurrencyException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
                break;
            case DomainException domain:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, domain.Code);
                break;
            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                await ApiProblem.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.Unexpected);
                break;
        }

        return true;
    }
}
```

`backend/src/Tgysm.Api/Http/CorrelationIdMiddleware.cs`:

```csharp
using Tgysm.Domain.Audit;

namespace Tgysm.Api.Http;

/// <summary>Accepts a safe <c>X-Correlation-Id</c> from the client or keeps the generated trace id; echoes it back.</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        if (IsValid(incoming))
        {
            context.TraceIdentifier = incoming;
        }

        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        return next(context);
    }

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= AuditEvent.CorrelationIdMaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
```

- [ ] **Step 5: Implement security components**

`backend/src/Tgysm.Api/Security/SecurityOptions.cs`:

```csharp
namespace Tgysm.Api.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public const int DefaultLoginPermitLimitPerMinute = 10;
    public const int DefaultSessionHours = 8;

    public int LoginPermitLimitPerMinute { get; init; } = DefaultLoginPermitLimitPerMinute;

    public int SessionHours { get; init; } = DefaultSessionHours;
}
```

`backend/src/Tgysm.Api/Security/SessionClaims.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tgysm.Application.Features.Auth;
using Tgysm.Domain.Users;

namespace Tgysm.Api.Security;

internal static class SessionClaims
{
    public const string SecurityStamp = "tgysm:security_stamp";
    public const string MustChangePassword = "tgysm:must_change_password";

    public static ClaimsPrincipal CreatePrincipal(SessionUser session)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Role, UserRoleNames.ToName(session.Role)),
            new(SecurityStamp, session.SecurityStamp),
            new(MustChangePassword, session.MustChangePassword ? bool.TrueString : bool.FalseString),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
```

`backend/src/Tgysm.Api/Security/SessionValidator.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tgysm.Application.Features.Auth;

namespace Tgysm.Api.Security;

/// <summary>
/// Runs on every request: rejects the cookie when the user is inactive or the security stamp changed,
/// otherwise refreshes role and must-change-password claims from the database (ADR-0004).
/// </summary>
internal static class SessionValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var stamp = principal?.FindFirstValue(SessionClaims.SecurityStamp);
        if (stamp is null || !Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var handler = context.HttpContext.RequestServices.GetRequiredService<GetSessionHandler>();
        var session = await handler.HandleAsync(new GetSessionQuery(userId), context.HttpContext.RequestAborted);
        if (session is null || !string.Equals(session.SecurityStamp, stamp, StringComparison.Ordinal))
        {
            await RejectAsync(context);
            return;
        }

        context.ReplacePrincipal(SessionClaims.CreatePrincipal(session));
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
```

`backend/src/Tgysm.Api/Security/Policies.cs`:

```csharp
namespace Tgysm.Api.Security;

internal static class Policies
{
    /// <summary>Authenticated admin whose password is not pending change.</summary>
    public const string Admin = "admin";

    /// <summary>Authenticated user, even with a pending password change (me, change-password, logout only).</summary>
    public const string PasswordChangePending = "password-change-pending";
}
```

`backend/src/Tgysm.Api/Security/PasswordChangedRequirement.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Security;

internal sealed class PasswordChangedRequirement : IAuthorizationRequirement;

internal sealed class PasswordChangedHandler : AuthorizationHandler<PasswordChangedRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PasswordChangedRequirement requirement)
    {
        if (context.User.FindFirstValue(SessionClaims.MustChangePassword) == bool.FalseString)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, ErrorCodes.PasswordChangeRequired));
        }

        return Task.CompletedTask;
    }
}
```

`backend/src/Tgysm.Api/Security/ApiAuthorizationResultHandler.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Tgysm.Api.Http;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Security;

/// <summary>Writes 401/403 as ProblemDetails with a code instead of empty responses or redirects.</summary>
internal sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            var passwordChangePending = authorizeResult.AuthorizationFailure?.FailureReasons
                .Any(reason => reason.Message == ErrorCodes.PasswordChangeRequired) == true;
            await ApiProblem.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                passwordChangePending ? ErrorCodes.PasswordChangeRequired : ErrorCodes.Forbidden);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
```

`backend/src/Tgysm.Api/Security/CsrfValidationMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Antiforgery;
using Tgysm.Api.Http;
using Tgysm.Domain.Common;

namespace Tgysm.Api.Security;

/// <summary>Requires a valid antiforgery token in <see cref="HeaderName"/> on every unsafe /api request (ADR-0004).</summary>
internal sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context.Request))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.CsrfInvalid);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiRoutes.ApiPrefix)
        && !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method));
}
```

`backend/src/Tgysm.Api/Security/RateLimitPolicies.cs`:

```csharp
namespace Tgysm.Api.Security;

internal static class RateLimitPolicies
{
    public const string Login = "login";
}
```

- [ ] **Step 6: Implement contracts and endpoints**

`backend/src/Tgysm.Api/Contracts/AuthContracts.cs`:

```csharp
namespace Tgysm.Api.Contracts;

public sealed record CsrfTokenResponse(string Token);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(bool MustChangePassword);
```

`backend/src/Tgysm.Api/Endpoints/AuthEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Auth;

namespace Tgysm.Api.Endpoints;

internal static class AuthEndpoints
{
    private const string Tag = "Auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup(ApiRoutes.Auth).WithTags(Tag);

        auth.MapGet("/csrf", GetCsrfToken).AllowAnonymous();
        auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization(Policies.PasswordChangePending);
        auth.MapGet("/me", GetMeAsync).RequireAuthorization(Policies.PasswordChangePending)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }

    private static Ok<CsrfTokenResponse> GetCsrfToken(HttpContext context, IAntiforgery antiforgery) =>
        TypedResults.Ok(new CsrfTokenResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));

    private static async Task<Ok<LoginResponse>> LoginAsync(LoginRequest request, LoginHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionClaims.CreatePrincipal(session));
        return TypedResults.Ok(new LoginResponse(session.MustChangePassword));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<MeDto>> GetMeAsync(GetMeHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
```

- [ ] **Step 7: Wire services and pipeline**

Add to the `<ItemGroup>` with package references in `backend/src/Tgysm.Api/Tgysm.Api.csproj` (create the group if the template has none):

```xml
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
```

`backend/src/Tgysm.Api/ApiServiceCollectionExtensions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api;

internal static class ApiServiceCollectionExtensions
{
    private const string SessionCookieName = "__Host-tgysm";
    private const string AntiforgeryCookieName = "__Host-tgysm-af";
    private const string UnknownClient = "unknown";
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .Validate(o => o.LoginPermitLimitPerMinute > 0 && o.SessionHours > 0, "Security limits must be positive.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;
                options.Events.OnRedirectToLogin = context => ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
                options.Events.OnRedirectToAccessDenied = context => ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);
            });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<SecurityOptions>>((cookie, security) => cookie.ExpireTimeSpan = TimeSpan.FromHours(security.Value.SessionHours));

        var passwordChanged = new PasswordChangedRequirement();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(passwordChanged).Build())
            .AddPolicy(Policies.PasswordChangePending, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(passwordChanged).RequireRole(UserRoleNames.Admin));
        services.AddSingleton<IAuthorizationHandler, PasswordChangedHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfValidationMiddleware.HeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(
                ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited));
            options.AddPolicy(RateLimitPolicies.Login, context =>
            {
                var limit = context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value.LoginPermitLimitPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = LoginWindow, QueueLimit = 0 });
            });
        });

        return services;
    }
}
```

If `using Microsoft.AspNetCore.Http.HttpResults;` is unused after writing, remove it (warnings are errors).

`backend/src/Tgysm.Api/ApiApplicationExtensions.cs`:

```csharp
using Tgysm.Api.Endpoints;
using Tgysm.Api.Http;
using Tgysm.Api.Security;

namespace Tgysm.Api;

internal static class ApiApplicationExtensions
{
    private const string SpaIndexFile = "index.html";
    private const string TestingEnvironment = "Testing";

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment(TestingEnvironment))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseAuthorization();
        return app;
    }

    public static RouteGroupBuilder MapApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        var api = app.MapGroup(ApiRoutes.V1);
        api.MapAuthEndpoints();

        // Production serves the built SPA from wwwroot (ADR-0003).
        app.MapFallbackToFile(SpaIndexFile).AllowAnonymous();
        return api;
    }
}
```

Replace `backend/src/Tgysm.Api/Program.cs`:

```csharp
using Tgysm.Api;
using Tgysm.Api.Cli;
using Tgysm.Application;
using Tgysm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

if (args is [SetupAdminCommand.Name, .. var commandArgs])
{
    return await SetupAdminCommand.RunAsync(app.Services, commandArgs, Console.Out, Console.Error);
}

app.UseApiPipeline();
app.MapApi();
await app.RunAsync();
return 0;

public partial class Program;
```

Remove the now-duplicated `AddHttpContextAccessor`/`HttpCurrentActor` lines from Task 6 (they moved into `AddApiServices`).

`backend/src/Tgysm.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Security": {
    "LoginPermitLimitPerMinute": 10,
    "SessionHours": 8
  }
}
```

`backend/src/Tgysm.Api/Properties/launchSettings.json`:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 8: Run the tests**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: all PASS.

- [ ] **Step 9: Prove the CSRF test guards the middleware**

Comment out `app.UseMiddleware<CsrfValidationMiddleware>();` in `ApiApplicationExtensions.cs`, run `dotnet test tests/Tgysm.Api.IntegrationTests --nologo --filter Login_without_csrf_token_is_rejected`.
Expected: FAIL (status 200 instead of 403). Restore the line and re-run: PASS.

- [ ] **Step 10: Store the dev connection string in user-secrets**

Run once: `cd /Users/lektuel/claude/TGYSM/backend && dotnet user-secrets init --project src/Tgysm.Api`

Append to `tools/db-up.sh` before the final `echo`:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER" \
  --project backend/src/Tgysm.Api >/dev/null
```

Run: `/Users/lektuel/claude/TGYSM/tools/db-up.sh; echo "exit=$?"`
Expected: `exit=0`.

Manual smoke check:

```bash
cd /Users/lektuel/claude/TGYSM/backend && dotnet run --project src/Tgysm.Api --launch-profile http &
until curl -sf http://localhost:5080/api/v1/auth/csrf >/dev/null; do sleep 1; done
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5080/api/v1/auth/me
kill %1
```

Expected: `401`.

- [ ] **Step 11: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 8: Password change, pending-change gate, session revocation

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Auth/ChangePassword.cs`
- Modify: `backend/src/Tgysm.Api/Endpoints/AuthEndpoints.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Auth/{ChangePasswordTests,SessionRevocationTests}.cs`

**Interfaces:**
- Consumes: `SessionUser`, `SessionClaims.CreatePrincipal`, `PasswordRules`, `ICurrentActor`.
- Produces: `record ChangePasswordCommand(string CurrentPassword, string NewPassword)`, `ChangePasswordHandler.HandleAsync(command, ct) : Task<SessionUser>`; endpoint `POST /api/v1/auth/change-password` → 204 and a re-issued session cookie.

- [ ] **Step 1: Write the failing tests**

`backend/tests/Tgysm.Api.IntegrationTests/Auth/ChangePasswordTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class ChangePasswordTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ChangePasswordPath = "/api/v1/auth/change-password";
    private const string Email = "pending@example.com";
    private const string NewPassword = "Brand-new-pass-7";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Pending_user_changes_password_and_old_password_stops_working()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email, mustChangePassword: true);
        var client = ApiClient.Create(factory);
        await client.LoginAsync(Email);

        using var before = await client.GetAsync(ApiClient.MePath);
        Assert.True((await client.ReadJsonAsync<JsonElement>(before)).GetProperty("mustChangePassword").GetBoolean());

        using var change = await client.PostAsync(ChangePasswordPath, new { currentPassword = TestUsers.DefaultPassword, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        using var after = await client.GetAsync(ApiClient.MePath);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.False((await client.ReadJsonAsync<JsonElement>(after)).GetProperty("mustChangePassword").GetBoolean());

        using var oldLogin = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await ApiClient.Create(factory).LoginAsync(Email, NewPassword);
    }

    [Fact]
    public async Task Wrong_current_password_is_a_field_error()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);

        using var response = await client.PostAsync(ChangePasswordPath, new { currentPassword = "Wrong-password-1", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.CurrentPasswordInvalid, (await ApiClient.ReadFieldCodesAsync(response))["currentPassword"]);
    }

    [Theory]
    [InlineData("short1", ErrorCodes.PasswordPolicy)]
    [InlineData(TestUsers.DefaultPassword, ErrorCodes.PasswordSameAsOld)]
    public async Task Invalid_new_password_is_a_field_error(string newPassword, string expectedCode)
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);

        using var response = await client.PostAsync(ChangePasswordPath, new { currentPassword = TestUsers.DefaultPassword, newPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, (await ApiClient.ReadFieldCodesAsync(response))["newPassword"]);
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions_but_keeps_the_current_one()
    {
        var (current, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);
        var other = ApiClient.Create(factory);
        await other.LoginAsync(Email);

        using var change = await current.PostAsync(ChangePasswordPath, new { currentPassword = TestUsers.DefaultPassword, newPassword = NewPassword });
        using var currentMe = await current.GetAsync(ApiClient.MePath);
        using var otherMe = await other.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherMe.StatusCode);
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Auth/SessionRevocationTests.cs`:

```csharp
using System.Net;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class SessionRevocationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Email = "revoked@example.com";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Deactivation_ends_the_session_on_the_next_request()
    {
        var (client, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);
        await ChangeUserAsync(user.Id, u => u.Deactivate(DateTimeOffset.UtcNow));

        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Role_change_ends_the_session_on_the_next_request()
    {
        var (client, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);
        await ChangeUserAsync(user.Id, u => u.ChangeRole(UserRole.ItStaff, DateTimeOffset.UtcNow));

        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private async Task ChangeUserAsync(Guid userId, Action<User> change)
    {
        await using var db = factory.Database.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Id == userId, Ct);
        change(user);
        await db.SaveChangesAsync(Ct);
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: `ChangePasswordTests` FAIL (404); `SessionRevocationTests` PASS already (Task 7 validator). Prove the revocation tests guard the validator: temporarily replace the body of `SessionValidator.ValidateAsync` with `return;` (keep the signature, return `Task.CompletedTask` if needed), run `--filter SessionRevocationTests` → FAIL (200), restore → PASS.

- [ ] **Step 2: Implement the handler**

`backend/src/Tgysm.Application/Features/Auth/ChangePassword.cs`:

```csharp
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword)
            .MustBeStrongPassword()
            .NotEqual(c => c.CurrentPassword).WithErrorCode(ErrorCodes.PasswordSameAsOld);
    }
}

/// <summary>Changes the signed-in user's password; rotates the security stamp so other sessions end.</summary>
public sealed class ChangePasswordHandler(
    IAppDbContext db,
    IValidator<ChangePasswordCommand> validator,
    IPasswordService passwords,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<SessionUser> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new AuthenticationFailedException();

        if (!passwords.Verify(user.PasswordHash, command.CurrentPassword))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(ChangePasswordCommand.CurrentPassword), "Current password is invalid.")
                {
                    ErrorCode = ErrorCodes.CurrentPasswordInvalid,
                },
            ]);
        }

        user.ChangePassword(passwords.Hash(command.NewPassword), clock.GetUtcNow());
        audit.Record(AuditActions.PasswordChanged, AuditEntityTypes.User, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return SessionUser.From(user);
    }
}
```

- [ ] **Step 3: Add the endpoint**

In `AuthEndpoints.MapAuthEndpoints`, after the `/me` mapping:

```csharp
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization(Policies.PasswordChangePending)
            .ProducesProblem(StatusCodes.Status400BadRequest);
```

Add the method (and `using Tgysm.Application.Features.Auth;` is already present):

```csharp
    /// <summary>Re-issues the cookie with the new security stamp so this session survives the rotation.</summary>
    private static async Task<NoContent> ChangePasswordAsync(
        ChangePasswordCommand command, ChangePasswordHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(command, cancellationToken);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionClaims.CreatePrincipal(session));
        return TypedResults.NoContent();
    }
```

- [ ] **Step 4: Run tests**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo`
Expected: all PASS.

- [ ] **Step 5: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---

### Task 9: Admin users API

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Organization/OrganizationReferences.cs`
- Create: `backend/src/Tgysm.Application/Features/Users/{UserDtos,UserMapping,LastAdminGuard,ListUsers,GetUser,CreateUser,UpdateUser,SetUserActive,ResetUserPassword}.cs`
- Modify: `backend/src/Tgysm.Application/Features/Users/UserRules.cs`, `backend/src/Tgysm.Application/DependencyInjection.cs`
- Create: `backend/src/Tgysm.Api/Contracts/AdminContracts.cs`, `backend/src/Tgysm.Api/Http/QueryParsing.cs`
- Create: `backend/src/Tgysm.Api/Endpoints/Admin/{AdminEndpoints,UserEndpoints}.cs`
- Modify: `backend/src/Tgysm.Api/Http/ApiRoutes.cs`, `backend/src/Tgysm.Api/ApiApplicationExtensions.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Admin/UserAdminTests.cs`

**Interfaces:**
- Consumes: Task 5–8 types.
- Produces:
  - `OrganizationReferences.EnsureActiveAsync(Guid? departmentId, Guid? locationId, CancellationToken)` (throws `common.reference_inactive`)
  - `LastAdminGuard.EnsureAnotherActiveAdminAsync(Guid userId, CancellationToken)` (throws `users.last_admin`)
  - DTOs: `UserListItem(Guid Id, string FirstName, string LastName, string Email, UserRole Role, bool IsActive, Guid? DepartmentId, string? DepartmentName, Guid? LocationId, string? LocationName, uint Version)`, `UserDetail(Guid Id, string FirstName, string LastName, string Email, UserRole Role, bool IsActive, bool MustChangePassword, Guid? DepartmentId, Guid? LocationId, DateTimeOffset CreatedAt, uint Version)`, `TemporaryPasswordResult(Guid UserId, string TemporaryPassword)`
  - Commands/queries: `ListUsersQuery(string? Search, bool? IsActive, UserRole? Role, string? Sort, int Page, int PageSize)`, `UserSort` constants (`name`, `-name`, `email`, `-email`, `createdAt`, `-createdAt`), `GetUserQuery(Guid Id)`, `CreateUserCommand(string FirstName, string LastName, string Email, UserRole Role, Guid? DepartmentId, Guid? LocationId)`, `UpdateUserCommand(Guid Id, uint Version, …same fields…)`, `SetUserActiveCommand(Guid Id, uint Version, bool IsActive)`, `ResetUserPasswordCommand(Guid Id)`
  - API contracts: `VersionRequest(uint Version)`, `UpdateUserRequest(uint Version, string FirstName, string LastName, string Email, UserRole Role, Guid? DepartmentId, Guid? LocationId)`, `UpdateDepartmentRequest(uint Version, string Name, Guid? ApproverUserId)`, `UpdateLocationRequest(uint Version, string Name, Guid? ParentId)`, `UpdateTeamRequest(uint Version, string Name)`, `AddTeamMemberRequest(Guid UserId)`
  - `QueryParsing.ParseRole(string?) : UserRole?`
  - Endpoints under `/api/v1/admin/users`: `GET ""` (query `search,isActive,role,sort,page,pageSize`), `GET /{id}`, `POST ""` → 201 `TemporaryPasswordResult`, `PUT /{id}` → `UserDetail`, `POST /{id}/deactivate`, `POST /{id}/reactivate` (body `VersionRequest`) → `UserDetail`, `POST /{id}/reset-password` → `TemporaryPasswordResult`
  - `AdminEndpoints.MapAdminEndpoints(this RouteGroupBuilder api)` returns the `/admin` group (policy `admin`); later tasks add `Map…Endpoints(admin)` calls there.

- [ ] **Step 1: Write the failing tests**

`backend/tests/Tgysm.Api.IntegrationTests/Admin/UserAdminTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class UserAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string UsersPath = "/api/v1/admin/users";
    private const string AdminEmail = "admin@example.com";
    private const int MaxPageSize = 100;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<(ApiClient Client, User User)> LoginAsAdminAsync() =>
        ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, AdminEmail);

    private static object NewUserBody(string email, string role = UserRoleNames.Employee, Guid? departmentId = null) =>
        new { firstName = "Deniz", lastName = "Aydın", email, role, departmentId, locationId = (Guid?)null };

    private static async Task<uint> GetVersionAsync(ApiClient admin, Guid userId)
    {
        using var response = await admin.GetAsync($"{UsersPath}/{userId}");
        response.EnsureSuccessStatusCode();
        return (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("version").GetUInt32();
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.ItStaff)]
    public async Task Non_admin_roles_are_forbidden_including_direct_id_access(UserRole role)
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, role, "user@example.com");
        var other = await TestUsers.CreateAsync(factory, UserRole.Employee, "other@example.com");

        using var list = await client.GetAsync(UsersPath);
        using var byId = await client.GetAsync($"{UsersPath}/{other.Id}");
        using var create = await client.PostAsync(UsersPath, NewUserBody("new@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await ApiClient.ReadCodeAsync(byId));
    }

    [Fact]
    public async Task Anonymous_request_is_unauthenticated()
    {
        using var response = await ApiClient.Create(factory).GetAsync(UsersPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Admin_with_pending_password_change_gets_password_change_required()
    {
        await TestUsers.CreateAsync(factory, UserRole.Admin, AdminEmail, mustChangePassword: true);
        var client = ApiClient.Create(factory);
        await client.LoginAsync(AdminEmail);

        using var response = await client.GetAsync(UsersPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.PasswordChangeRequired, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_returns_temporary_password_and_new_user_must_change_it()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.PostAsync(UsersPath, NewUserBody("deniz@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var temporaryPassword = (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("temporaryPassword").GetString()!;
        var newUser = ApiClient.Create(factory);
        await newUser.LoginAsync("deniz@example.com", temporaryPassword);
        using var me = await newUser.GetAsync(ApiClient.MePath);
        Assert.True((await newUser.ReadJsonAsync<JsonElement>(me)).GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Create_with_email_differing_only_in_case_returns_email_taken()
    {
        var (admin, _) = await LoginAsAdminAsync();
        await TestUsers.CreateAsync(factory, UserRole.Employee, "ali@example.com");

        using var response = await admin.PostAsync(UsersPath, NewUserBody("ALI@Example.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Parallel_duplicate_create_creates_one_user()
    {
        const string email = "twice@example.com";
        var (admin, _) = await LoginAsAdminAsync();
        await admin.RefreshCsrfAsync();

        var responses = await Task.WhenAll(admin.PostAsync(UsersPath, NewUserBody(email)), admin.PostAsync(UsersPath, NewUserBody(email)));
        try
        {
            var statuses = responses.Select(r => r.StatusCode).Order().ToArray();
            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statuses);
            var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(ErrorCodes.EmailTaken, await ApiClient.ReadCodeAsync(conflict));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == email, Ct));
    }

    [Fact]
    public async Task Create_with_inactive_department_returns_reference_inactive()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var department = Department.Create("Muhasebe", null);
        department.Deactivate();
        await using (var db = factory.Database.CreateDbContext())
        {
            db.Departments.Add(department);
            await db.SaveChangesAsync(Ct);
        }

        using var response = await admin.PostAsync(UsersPath, NewUserBody("x@example.com", departmentId: department.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Validation_errors_are_reported_per_field()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.PostAsync(UsersPath, new
        {
            firstName = "",
            lastName = new string('a', User.NameMaxLength + 1),
            email = "not-an-email",
            role = UserRoleNames.Employee,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.Required, fields["firstName"]);
        Assert.Contains(ErrorCodes.TooLong, fields["lastName"]);
        Assert.Contains(ErrorCodes.InvalidEmail, fields["email"]);
    }

    [Fact]
    public async Task Update_with_stale_version_returns_conflict()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, "stale@example.com");
        var version = await GetVersionAsync(admin, user.Id);
        object Body(string firstName) => new { version, firstName, lastName = "User", email = "stale@example.com", role = UserRoleNames.Employee };

        using var first = await admin.PutAsync($"{UsersPath}/{user.Id}", Body("Birinci"));
        using var second = await admin.PutAsync($"{UsersPath}/{user.Id}", Body("İkinci"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Last_admin_cannot_demote_or_deactivate_self()
    {
        var (admin, self) = await LoginAsAdminAsync();
        var version = await GetVersionAsync(admin, self.Id);

        using var demote = await admin.PutAsync($"{UsersPath}/{self.Id}", new
        {
            version, firstName = "Test", lastName = "User", email = AdminEmail, role = UserRoleNames.Employee,
        });
        using var deactivate = await admin.PostAsync($"{UsersPath}/{self.Id}/deactivate", new { version });

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(ErrorCodes.LastAdmin, await ApiClient.ReadCodeAsync(demote));
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(ErrorCodes.LastAdmin, await ApiClient.ReadCodeAsync(deactivate));

        await TestUsers.CreateAsync(factory, UserRole.Admin, "second-admin@example.com");
        using var allowed = await admin.PutAsync($"{UsersPath}/{self.Id}", new
        {
            version, firstName = "Test", lastName = "User", email = AdminEmail, role = UserRoleNames.ItStaff,
        });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_loses_session_and_reactivated_user_can_log_in_again()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var (userClient, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "worker@example.com");

        using var deactivate = await admin.PostAsync($"{UsersPath}/{user.Id}/deactivate", new { version = await GetVersionAsync(admin, user.Id) });
        using var me = await userClient.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        using var reactivate = await admin.PostAsync($"{UsersPath}/{user.Id}/reactivate", new { version = await GetVersionAsync(admin, user.Id) });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        await ApiClient.Create(factory).LoginAsync("worker@example.com");
    }

    [Fact]
    public async Task Reset_password_ends_session_and_requires_change()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var (userClient, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "forgetful@example.com");

        using var reset = await admin.PostAsync($"{UsersPath}/{user.Id}/reset-password");
        var temporaryPassword = (await admin.ReadJsonAsync<JsonElement>(reset)).GetProperty("temporaryPassword").GetString()!;
        using var oldSession = await userClient.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        var fresh = ApiClient.Create(factory);
        await fresh.LoginAsync("forgetful@example.com", temporaryPassword);
        using var me = await fresh.GetAsync(ApiClient.MePath);
        Assert.True((await fresh.ReadJsonAsync<JsonElement>(me)).GetProperty("mustChangePassword").GetBoolean());
    }

    [Theory]
    [InlineData("ilker ışık")]
    [InlineData("IŞIK")]
    [InlineData("İLKER")]
    public async Task Search_is_turkish_case_insensitive(string search)
    {
        var (admin, _) = await LoginAsAdminAsync();
        await TestUsers.CreateAsync(factory, UserRole.Employee, "ilker@example.com", firstName: "İlker", lastName: "Işık");
        await TestUsers.CreateAsync(factory, UserRole.Employee, "baska@example.com", firstName: "Başka", lastName: "Kişi");

        using var response = await admin.GetAsync($"{UsersPath}?search={Uri.EscapeDataString(search)}");

        var items = (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("ilker@example.com", items[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.GetAsync($"{UsersPath}?pageSize=1000");

        Assert.Equal(MaxPageSize, (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Unknown_role_filter_is_a_validation_error()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.GetAsync($"{UsersPath}?role=superuser");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.InvalidValue, (await ApiClient.ReadFieldCodesAsync(response))["role"]);
    }

    [Fact]
    public async Task Create_update_and_role_change_are_audited_with_the_admin_as_actor()
    {
        var (admin, self) = await LoginAsAdminAsync();
        using var created = await admin.PostAsync(UsersPath, NewUserBody("audited@example.com"));
        var userId = (await admin.ReadJsonAsync<JsonElement>(created)).GetProperty("userId").GetGuid();

        using var updated = await admin.PutAsync($"{UsersPath}/{userId}", new
        {
            version = await GetVersionAsync(admin, userId), firstName = "Deniz", lastName = "Aydın", email = "audited@example.com", role = UserRoleNames.ItStaff,
        });

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(a => a.EntityId == userId).Select(a => new { a.Action, a.ActorUserId }).ToListAsync(Ct);
        Assert.Contains(actions, a => a.Action == AuditActions.UserCreated && a.ActorUserId == self.Id);
        Assert.Contains(actions, a => a.Action == AuditActions.UserUpdated && a.ActorUserId == self.Id);
        Assert.Contains(actions, a => a.Action == AuditActions.UserRoleChanged && a.ActorUserId == self.Id);
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo --filter UserAdminTests`
Expected: FAIL — 404 on all admin routes (anonymous test: fallback returns 401 already; the rest fail).

- [ ] **Step 2: Implement shared guards**

`backend/src/Tgysm.Application/Features/Organization/OrganizationReferences.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization;

/// <summary>Checks that referenced departments and locations exist and are active.</summary>
public sealed class OrganizationReferences(IAppDbContext db)
{
    /// <summary>Pass null for a reference that did not change; only new references are checked.</summary>
    public async Task EnsureActiveAsync(Guid? departmentId, Guid? locationId, CancellationToken cancellationToken)
    {
        if (departmentId is { } department && !await db.Departments.AnyAsync(d => d.Id == department && d.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Department is missing or inactive.");
        }

        if (locationId is { } location && !await db.Locations.AnyAsync(l => l.Id == location && l.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Location is missing or inactive.");
        }
    }
}
```

`backend/src/Tgysm.Application/Features/Users/LastAdminGuard.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

/// <summary>The system must always keep at least one active admin (ADR-0005).</summary>
public sealed class LastAdminGuard(IAppDbContext db)
{
    public async Task EnsureAnotherActiveAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var anotherAdminExists = await db.Users.AnyAsync(
            u => u.Id != userId && u.IsActive && u.Role == UserRole.Admin, cancellationToken);
        if (!anotherAdminExists)
        {
            throw new DomainException(ErrorCodes.LastAdmin, "The last active admin cannot be demoted or deactivated.");
        }
    }
}
```

In `backend/src/Tgysm.Application/DependencyInjection.cs`, add usings `Tgysm.Application.Features.Organization` and `Tgysm.Application.Features.Users`, and before `return services;`:

```csharp
        services.AddScoped<OrganizationReferences>();
        services.AddScoped<LastAdminGuard>();
```

Append to `backend/src/Tgysm.Application/Features/Users/UserRules.cs` (same file, new class):

```csharp
internal static class UserMessages
{
    public const string EmailTaken = "Another account uses this email.";
}
```

- [ ] **Step 3: Implement DTOs and mapping**

`backend/src/Tgysm.Application/Features/Users/UserDtos.cs`:

```csharp
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record UserListItem(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? LocationId,
    string? LocationName,
    uint Version);

public sealed record UserDetail(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    Guid? DepartmentId,
    Guid? LocationId,
    DateTimeOffset CreatedAt,
    uint Version);

/// <summary>Shown to the admin once; never stored in plain text.</summary>
public sealed record TemporaryPasswordResult(Guid UserId, string TemporaryPassword);

/// <summary>Audit snapshot; deliberately excludes password hash and security stamp.</summary>
internal sealed record UserAuditSnapshot(
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId,
    bool IsActive)
{
    public static UserAuditSnapshot From(User user) =>
        new(user.FirstName, user.LastName, user.Email, user.Role, user.DepartmentId, user.LocationId, user.IsActive);
}
```

`backend/src/Tgysm.Application/Features/Users/UserMapping.cs`:

```csharp
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

internal static class UserMapping
{
    public static UserDetail ToDetail(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role,
        user.IsActive,
        user.MustChangePassword,
        user.DepartmentId,
        user.LocationId,
        user.CreatedAt,
        user.Version);
}
```

- [ ] **Step 4: Implement queries**

`backend/src/Tgysm.Application/Features/Users/ListUsers.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record ListUsersQuery(string? Search, bool? IsActive, UserRole? Role, string? Sort, int Page, int PageSize);

public static class UserSort
{
    public const string Name = "name";
    public const string NameDescending = "-name";
    public const string Email = "email";
    public const string EmailDescending = "-email";
    public const string CreatedAt = "createdAt";
    public const string CreatedAtDescending = "-createdAt";
}

public sealed class ListUsersHandler(IAppDbContext db)
{
    public Task<PagedResult<UserListItem>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            users = users.Where(u => u.SearchName.Contains(key));
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        var rows =
            from user in users
            join department in db.Departments on user.DepartmentId equals (Guid?)department.Id into departments
            from department in departments.DefaultIfEmpty()
            join location in db.Locations on user.LocationId equals (Guid?)location.Id into locations
            from location in locations.DefaultIfEmpty()
            select new
            {
                User = user,
                DepartmentName = department == null ? null : department.Name,
                LocationName = location == null ? null : location.Name,
            };

        rows = query.Sort switch
        {
            UserSort.NameDescending => rows.OrderByDescending(r => r.User.LastName).ThenByDescending(r => r.User.FirstName).ThenBy(r => r.User.Id),
            UserSort.Email => rows.OrderBy(r => r.User.NormalizedEmail).ThenBy(r => r.User.Id),
            UserSort.EmailDescending => rows.OrderByDescending(r => r.User.NormalizedEmail).ThenBy(r => r.User.Id),
            UserSort.CreatedAt => rows.OrderBy(r => r.User.CreatedAt).ThenBy(r => r.User.Id),
            UserSort.CreatedAtDescending => rows.OrderByDescending(r => r.User.CreatedAt).ThenBy(r => r.User.Id),
            _ => rows.OrderBy(r => r.User.LastName).ThenBy(r => r.User.FirstName).ThenBy(r => r.User.Id),
        };

        return rows
            .Select(r => new UserListItem(
                r.User.Id,
                r.User.FirstName,
                r.User.LastName,
                r.User.Email,
                r.User.Role,
                r.User.IsActive,
                r.User.DepartmentId,
                r.DepartmentName,
                r.User.LocationId,
                r.LocationName,
                r.User.Version))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Users/GetUser.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Users;

public sealed record GetUserQuery(Guid Id);

public sealed class GetUserHandler(IAppDbContext db)
{
    public async Task<UserDetail> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        return UserMapping.ToDetail(user);
    }
}
```

- [ ] **Step 5: Implement commands**

`backend/src/Tgysm.Application/Features/Users/CreateUser.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record CreateUserCommand(string FirstName, string LastName, string Email, UserRole Role, Guid? DepartmentId, Guid? LocationId);

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Role).IsInEnum();
    }
}

public sealed class CreateUserHandler(
    IAppDbContext db,
    IValidator<CreateUserCommand> validator,
    OrganizationReferences references,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<TemporaryPasswordResult> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = User.NormalizeEmail(command.Email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new DomainException(ErrorCodes.EmailTaken, UserMessages.EmailTaken);
        }

        await references.EnsureActiveAsync(command.DepartmentId, command.LocationId, cancellationToken);

        var temporaryPassword = temporaryPasswords.Generate();
        var user = User.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Role,
            command.DepartmentId,
            command.LocationId,
            passwords.Hash(temporaryPassword),
            clock.GetUtcNow());

        db.Users.Add(user);
        audit.Record(AuditActions.UserCreated, AuditEntityTypes.User, user.Id, newValues: UserAuditSnapshot.From(user));

        // A parallel request with the same email loses on the unique index (double submit).
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.EmailTaken, UserMessages.EmailTaken, cancellationToken);
        return new TemporaryPasswordResult(user.Id, temporaryPassword);
    }
}
```

`backend/src/Tgysm.Application/Features/Users/UpdateUser.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record UpdateUserCommand(
    Guid Id,
    uint Version,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId);

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Role).IsInEnum();
    }
}

public sealed class UpdateUserHandler(
    IAppDbContext db,
    IValidator<UpdateUserCommand> validator,
    OrganizationReferences references,
    LastAdminGuard lastAdmin,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<UserDetail> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        db.SetExpectedVersion(user, command.Version);

        var normalizedEmail = User.NormalizeEmail(command.Email);
        if (normalizedEmail != user.NormalizedEmail
            && await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != user.Id, cancellationToken))
        {
            throw new DomainException(ErrorCodes.EmailTaken, UserMessages.EmailTaken);
        }

        await references.EnsureActiveAsync(
            command.DepartmentId == user.DepartmentId ? null : command.DepartmentId,
            command.LocationId == user.LocationId ? null : command.LocationId,
            cancellationToken);

        if (user is { IsActive: true, Role: UserRole.Admin } && command.Role != UserRole.Admin)
        {
            await lastAdmin.EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
        }

        var before = UserAuditSnapshot.From(user);
        var now = clock.GetUtcNow();
        user.UpdateProfile(command.FirstName, command.LastName, command.Email, command.DepartmentId, command.LocationId, now);
        user.ChangeRole(command.Role, now);
        var after = UserAuditSnapshot.From(user);

        audit.Record(AuditActions.UserUpdated, AuditEntityTypes.User, user.Id, before, after);
        if (before.Role != after.Role)
        {
            audit.Record(AuditActions.UserRoleChanged, AuditEntityTypes.User, user.Id, new { before.Role }, new { after.Role });
        }

        await db.SaveTranslatingDuplicateAsync(ErrorCodes.EmailTaken, UserMessages.EmailTaken, cancellationToken);
        return UserMapping.ToDetail(user);
    }
}
```

`backend/src/Tgysm.Application/Features/Users/SetUserActive.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record SetUserActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetUserActiveHandler(IAppDbContext db, LastAdminGuard lastAdmin, IAuditLog audit, TimeProvider clock)
{
    public async Task<UserDetail> HandleAsync(SetUserActiveCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        if (user.IsActive == command.IsActive)
        {
            return UserMapping.ToDetail(user);
        }

        db.SetExpectedVersion(user, command.Version);
        var now = clock.GetUtcNow();
        if (command.IsActive)
        {
            user.Reactivate(now);
        }
        else
        {
            if (user.Role == UserRole.Admin)
            {
                await lastAdmin.EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
            }

            user.Deactivate(now);
        }

        audit.Record(command.IsActive ? AuditActions.UserReactivated : AuditActions.UserDeactivated, AuditEntityTypes.User, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return UserMapping.ToDetail(user);
    }
}
```

`backend/src/Tgysm.Application/Features/Users/ResetUserPassword.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Users;

public sealed record ResetUserPasswordCommand(Guid Id);

/// <summary>Admin-issued temporary password (replaces "forgot password" until email arrives in Phase 4).</summary>
public sealed class ResetUserPasswordHandler(
    IAppDbContext db,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<TemporaryPasswordResult> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);

        var temporaryPassword = temporaryPasswords.Generate();
        user.SetTemporaryPassword(passwords.Hash(temporaryPassword), clock.GetUtcNow());
        audit.Record(AuditActions.UserPasswordReset, AuditEntityTypes.User, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new TemporaryPasswordResult(user.Id, temporaryPassword);
    }
}
```

- [ ] **Step 6: Implement API contracts, parsing and endpoints**

`backend/src/Tgysm.Api/Contracts/AdminContracts.cs`:

```csharp
using Tgysm.Domain.Users;

namespace Tgysm.Api.Contracts;

public sealed record VersionRequest(uint Version);

public sealed record UpdateUserRequest(
    uint Version,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId);

public sealed record UpdateDepartmentRequest(uint Version, string Name, Guid? ApproverUserId);

public sealed record UpdateLocationRequest(uint Version, string Name, Guid? ParentId);

public sealed record UpdateTeamRequest(uint Version, string Name);

public sealed record AddTeamMemberRequest(Guid UserId);
```

`backend/src/Tgysm.Api/Http/QueryParsing.cs`:

```csharp
using FluentValidation;
using FluentValidation.Results;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.Http;

internal static class QueryParsing
{
    private const string RoleParameter = "role";

    /// <summary>Parses the snake_case role name used in URLs ("it_staff"); unknown names are a 400.</summary>
    public static UserRole? ParseRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return UserRoleNames.TryParse(value, out var role)
            ? role
            : throw new ValidationException([new ValidationFailure(RoleParameter, "Unknown role.") { ErrorCode = ErrorCodes.InvalidValue }]);
    }
}
```

Replace `backend/src/Tgysm.Api/Http/ApiRoutes.cs`:

```csharp
namespace Tgysm.Api.Http;

internal static class ApiRoutes
{
    public const string ApiPrefix = "/api";
    public const string V1 = ApiPrefix + "/v1";
    public const string Auth = "/auth";
    public const string AdminGroup = "/admin";
    public const string Admin = V1 + AdminGroup;
    public const string Users = "/users";
    public const string Departments = "/departments";
    public const string Locations = "/locations";
    public const string Teams = "/teams";
    public const string AuditEvents = "/audit-events";
}
```

`backend/src/Tgysm.Api/Endpoints/Admin/AdminEndpoints.cs`:

```csharp
using Tgysm.Api.Http;
using Tgysm.Api.Security;

namespace Tgysm.Api.Endpoints.Admin;

internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup(ApiRoutes.AdminGroup).RequireAuthorization(Policies.Admin);
        admin.MapUserEndpoints();
        return admin;
    }
}
```

`backend/src/Tgysm.Api/Endpoints/Admin/UserEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Users;

namespace Tgysm.Api.Endpoints.Admin;

internal static class UserEndpoints
{
    private const string Tag = "Admin: Users";

    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder admin)
    {
        var users = admin.MapGroup(ApiRoutes.Users).WithTags(Tag);

        users.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        users.MapGet("/{id:guid}", GetAsync).ProducesProblem(StatusCodes.Status404NotFound);
        users.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/reset-password", ResetPasswordAsync).ProducesProblem(StatusCodes.Status404NotFound);

        return admin;
    }

    private static async Task<Ok<PagedResult<UserListItem>>> ListAsync(
        string? search, bool? isActive, string? role, string? sort, int? page, int? pageSize,
        ListUsersHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListUsersQuery(
            search, isActive, QueryParsing.ParseRole(role), sort,
            page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static async Task<Ok<UserDetail>> GetAsync(Guid id, GetUserHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetUserQuery(id), cancellationToken));

    private static async Task<Created<TemporaryPasswordResult>> CreateAsync(
        CreateUserCommand command, CreateUserHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Users}/{result.UserId}", result);
    }

    private static async Task<Ok<UserDetail>> UpdateAsync(
        Guid id, UpdateUserRequest request, UpdateUserHandler handler, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id, request.Version, request.FirstName, request.LastName, request.Email, request.Role, request.DepartmentId, request.LocationId);
        return TypedResults.Ok(await handler.HandleAsync(command, cancellationToken));
    }

    private static async Task<Ok<UserDetail>> DeactivateAsync(
        Guid id, VersionRequest request, SetUserActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetUserActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<UserDetail>> ReactivateAsync(
        Guid id, VersionRequest request, SetUserActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetUserActiveCommand(id, request.Version, IsActive: true), cancellationToken));

    private static async Task<Ok<TemporaryPasswordResult>> ResetPasswordAsync(
        Guid id, ResetUserPasswordHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ResetUserPasswordCommand(id), cancellationToken));
}
```

In `ApiApplicationExtensions.MapApi`, add `using Tgysm.Api.Endpoints.Admin;` and after `api.MapAuthEndpoints();`:

```csharp
        api.MapAdminEndpoints();
```

- [ ] **Step 7: Run tests**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test --nologo`
Expected: all PASS.

- [ ] **Step 8: Prove the last-admin test guards the rule**

Comment out the `await lastAdmin.EnsureAnotherActiveAdminAsync(...)` call in `UpdateUserHandler`; run `dotnet test tests/Tgysm.Api.IntegrationTests --nologo --filter Last_admin_cannot_demote_or_deactivate_self` → FAIL (200 on demote). Restore → PASS.

- [ ] **Step 9: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 10: Admin departments and locations API

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Organization/Departments/{DepartmentItem,DepartmentQueries,ListDepartments,CreateDepartment,UpdateDepartment,SetDepartmentActive}.cs`
- Create: `backend/src/Tgysm.Application/Features/Organization/Locations/{LocationItem,LocationRules,ListLocations,CreateLocation,UpdateLocation,SetLocationActive}.cs`
- Create: `backend/src/Tgysm.Api/Endpoints/Admin/{DepartmentEndpoints,LocationEndpoints}.cs`
- Modify: `backend/src/Tgysm.Api/Endpoints/Admin/AdminEndpoints.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Admin/{DepartmentAdminTests,LocationAdminTests}.cs`

**Interfaces:**
- Consumes: `IAppDbContext`, `IAuditLog`, `SaveTranslatingDuplicateAsync`, `PageRequest`, `VersionRequest`, `UpdateDepartmentRequest`, `UpdateLocationRequest`.
- Produces:
  - `DepartmentItem(Guid Id, string Name, bool IsActive, Guid? ApproverUserId, string? ApproverName, uint Version)`
  - `ListDepartmentsQuery(string? Search, bool? IsActive, int Page, int PageSize)`, `CreateDepartmentCommand(string Name, Guid? ApproverUserId)`, `UpdateDepartmentCommand(Guid Id, uint Version, string Name, Guid? ApproverUserId)`, `SetDepartmentActiveCommand(Guid Id, uint Version, bool IsActive)`; handlers return `DepartmentItem` (list: `PagedResult<DepartmentItem>`)
  - `LocationItem(Guid Id, string Name, Guid? ParentId, bool IsActive, uint Version)`
  - `ListLocationsHandler.HandleAsync(ct) : Task<IReadOnlyList<LocationItem>>` (all, max `ListLocationsHandler.MaxLocations = 2000`), `CreateLocationCommand(string Name, Guid? ParentId)`, `UpdateLocationCommand(Guid Id, uint Version, string Name, Guid? ParentId)`, `SetLocationActiveCommand(Guid Id, uint Version, bool IsActive)`; handlers return `LocationItem`
  - Endpoints: `/api/v1/admin/departments` (`GET ""`, `POST ""`, `PUT /{id}`, `POST /{id}/deactivate`, `POST /{id}/reactivate`), `/api/v1/admin/locations` (same shape; `GET ""` returns a plain array)

- [ ] **Step 1: Write the failing tests**

`backend/tests/Tgysm.Api.IntegrationTests/Admin/DepartmentAdminTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class DepartmentAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string DepartmentsPath = "/api/v1/admin/departments";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreateAsync(ApiClient admin, string name, Guid? approverUserId = null)
    {
        using var response = await admin.PostAsync(DepartmentsPath, new { name, approverUserId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Create_list_and_rename()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, "Bilgi İşlem");

        using var renamed = await admin.PutAsync($"{DepartmentsPath}/{created.GetProperty("id").GetGuid()}", new
        {
            version = created.GetProperty("version").GetUInt32(), name = "Bilgi Teknolojileri", approverUserId = (Guid?)null,
        });
        using var list = await admin.GetAsync($"{DepartmentsPath}?search={Uri.EscapeDataString("BİLGİ")}");

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var items = (await admin.ReadJsonAsync<JsonElement>(list)).GetProperty("items");
        Assert.Equal("Bilgi Teknolojileri", items[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Department_name_is_unique_ignoring_turkish_case()
    {
        var admin = await AdminAsync();
        await CreateAsync(admin, "İnsan Kaynakları");

        using var duplicate = await admin.PostAsync(DepartmentsPath, new { name = "insan kaynakları" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.DepartmentNameTaken, await ApiClient.ReadCodeAsync(duplicate));
    }

    [Fact]
    public async Task Approver_must_be_an_active_admin()
    {
        var admin = await AdminAsync();
        var employee = await TestUsers.CreateAsync(factory, UserRole.Employee, "employee@example.com");
        var approver = await TestUsers.CreateAsync(factory, UserRole.Admin, "approver@example.com", firstName: "Onay", lastName: "Veren");

        using var rejected = await admin.PostAsync(DepartmentsPath, new { name = "Satış", approverUserId = employee.Id });
        var accepted = await CreateAsync(admin, "Satış", approver.Id);

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(ErrorCodes.ApproverInvalid, await ApiClient.ReadCodeAsync(rejected));
        Assert.Equal("Onay Veren", accepted.GetProperty("approverName").GetString());
    }

    [Fact]
    public async Task Stale_version_returns_conflict()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, "Hukuk");
        var path = $"{DepartmentsPath}/{created.GetProperty("id").GetGuid()}";
        var version = created.GetProperty("version").GetUInt32();

        using var first = await admin.PutAsync(path, new { version, name = "Hukuk Müşavirliği" });
        using var second = await admin.PutAsync(path, new { version, name = "Hukuk Birimi" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Deactivate_and_reactivate_are_audited()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, "Lojistik");
        var id = created.GetProperty("id").GetGuid();

        using var deactivated = await admin.PostAsync($"{DepartmentsPath}/{id}/deactivate", new { version = created.GetProperty("version").GetUInt32() });
        var afterDeactivate = await admin.ReadJsonAsync<JsonElement>(deactivated);
        using var reactivated = await admin.PostAsync($"{DepartmentsPath}/{id}/reactivate", new { version = afterDeactivate.GetProperty("version").GetUInt32() });

        Assert.False(afterDeactivate.GetProperty("isActive").GetBoolean());
        Assert.True((await admin.ReadJsonAsync<JsonElement>(reactivated)).GetProperty("isActive").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(a => a.EntityId == id).Select(a => a.Action).ToListAsync(Ct);
        Assert.Contains(AuditActions.DepartmentCreated, actions);
        Assert.Contains(AuditActions.DepartmentDeactivated, actions);
        Assert.Contains(AuditActions.DepartmentReactivated, actions);
    }

    [Fact]
    public async Task Non_admin_is_forbidden()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var response = await client.PostAsync(DepartmentsPath, new { name = "Gizli" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Admin/LocationAdminTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class LocationAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string LocationsPath = "/api/v1/admin/locations";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreateAsync(ApiClient admin, string name, Guid? parentId = null)
    {
        using var response = await admin.PostAsync(LocationsPath, new { name, parentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    private static uint Version(JsonElement item) => item.GetProperty("version").GetUInt32();

    [Fact]
    public async Task Builds_a_tree_and_lists_all_locations()
    {
        var admin = await AdminAsync();
        var campus = await CreateAsync(admin, "Merkez Kampüs");
        await CreateAsync(admin, "A Blok", Id(campus));

        using var response = await admin.GetAsync(LocationsPath);
        var items = await admin.ReadJsonAsync<JsonElement>(response);

        Assert.Equal(2, items.GetArrayLength());
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("parentId").ValueKind == JsonValueKind.Null);
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("parentId").ValueKind != JsonValueKind.Null && i.GetProperty("parentId").GetGuid() == Id(campus));
    }

    [Fact]
    public async Task Same_root_name_is_rejected_but_same_name_under_other_parents_is_allowed()
    {
        var admin = await AdminAsync();
        var first = await CreateAsync(admin, "Kampüs A");
        var second = await CreateAsync(admin, "Kampüs B");
        await CreateAsync(admin, "Bina 1", Id(first));
        await CreateAsync(admin, "Bina 1", Id(second));

        using var duplicate = await admin.PostAsync(LocationsPath, new { name = "KAMPÜS A" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.LocationNameTaken, await ApiClient.ReadCodeAsync(duplicate));
    }

    [Fact]
    public async Task Moving_a_location_under_itself_or_its_descendant_is_rejected()
    {
        var admin = await AdminAsync();
        var campus = await CreateAsync(admin, "Kampüs");
        var building = await CreateAsync(admin, "Bina", Id(campus));
        var floor = await CreateAsync(admin, "Kat 1", Id(building));

        using var underDescendant = await admin.PutAsync($"{LocationsPath}/{Id(campus)}", new { version = Version(campus), name = "Kampüs", parentId = Id(floor) });
        using var underSelf = await admin.PutAsync($"{LocationsPath}/{Id(campus)}", new { version = Version(campus), name = "Kampüs", parentId = Id(campus) });

        Assert.Equal(ErrorCodes.LocationCycle, await ApiClient.ReadCodeAsync(underDescendant));
        Assert.Equal(ErrorCodes.LocationCycle, await ApiClient.ReadCodeAsync(underSelf));
    }

    [Fact]
    public async Task Parent_with_active_children_cannot_be_deactivated_and_child_of_inactive_parent_cannot_be_reactivated()
    {
        var admin = await AdminAsync();
        var campus = await CreateAsync(admin, "Kampüs");
        var building = await CreateAsync(admin, "Bina", Id(campus));

        using var blocked = await admin.PostAsync($"{LocationsPath}/{Id(campus)}/deactivate", new { version = Version(campus) });
        Assert.Equal(ErrorCodes.LocationHasActiveChildren, await ApiClient.ReadCodeAsync(blocked));

        using var childOff = await admin.PostAsync($"{LocationsPath}/{Id(building)}/deactivate", new { version = Version(building) });
        var inactiveChild = await admin.ReadJsonAsync<JsonElement>(childOff);
        using var parentOff = await admin.PostAsync($"{LocationsPath}/{Id(campus)}/deactivate", new { version = Version(campus) });
        Assert.Equal(HttpStatusCode.OK, parentOff.StatusCode);

        using var childOn = await admin.PostAsync($"{LocationsPath}/{Id(building)}/reactivate", new { version = Version(inactiveChild) });
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(childOn));

        using var newChild = await admin.PostAsync(LocationsPath, new { name = "Yeni Bina", parentId = Id(campus) });
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(newChild));
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo --filter "DepartmentAdminTests|LocationAdminTests"`
Expected: FAIL (404).

- [ ] **Step 2: Implement departments**

`backend/src/Tgysm.Application/Features/Organization/Departments/DepartmentItem.cs`:

```csharp
namespace Tgysm.Application.Features.Organization.Departments;

public sealed record DepartmentItem(Guid Id, string Name, bool IsActive, Guid? ApproverUserId, string? ApproverName, uint Version);
```

`backend/src/Tgysm.Application/Features/Organization/Departments/DepartmentQueries.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Organization.Departments;

internal static class DepartmentQueries
{
    public const string NameTakenMessage = "Another department uses this name.";

    /// <summary>Ordered by name; ordering sits inside the query because the record projection cannot be sorted by EF.</summary>
    public static IQueryable<DepartmentItem> ToItems(this IQueryable<Department> departments, IAppDbContext db) =>
        from department in departments
        join approver in db.Users on department.ApproverUserId equals (Guid?)approver.Id into approvers
        from approver in approvers.DefaultIfEmpty()
        orderby department.Name, department.Id
        select new DepartmentItem(
            department.Id,
            department.Name,
            department.IsActive,
            department.ApproverUserId,
            approver == null ? null : approver.FirstName + " " + approver.LastName,
            department.Version);

    public static Task<DepartmentItem> LoadItemAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Departments.AsNoTracking().Where(d => d.Id == id).ToItems(db).SingleAsync(cancellationToken);

    public static async Task EnsureNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Departments.AnyAsync(d => d.NormalizedName == key && d.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.DepartmentNameTaken, NameTakenMessage);
        }
    }

    public static async Task EnsureApproverAsync(IAppDbContext db, Guid? approverUserId, CancellationToken cancellationToken)
    {
        if (approverUserId is { } id
            && !await db.Users.AnyAsync(u => u.Id == id && u.IsActive && u.Role == UserRole.Admin, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ApproverInvalid, "The approver must be an active admin.");
        }
    }

    public static IRuleBuilderOptions<T, string> ValidDepartmentName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Department.NameMaxLength);
}
```

`backend/src/Tgysm.Application/Features/Organization/Departments/ListDepartments.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Departments;

public sealed record ListDepartmentsQuery(string? Search, bool? IsActive, int Page, int PageSize);

public sealed class ListDepartmentsHandler(IAppDbContext db)
{
    public Task<PagedResult<DepartmentItem>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var departments = db.Departments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            departments = departments.Where(d => d.NormalizedName.Contains(key));
        }

        if (query.IsActive is { } isActive)
        {
            departments = departments.Where(d => d.IsActive == isActive);
        }

        return departments.ToItems(db).ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Departments/CreateDepartment.cs`:

```csharp
using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Departments;

public sealed record CreateDepartmentCommand(string Name, Guid? ApproverUserId);

internal sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator() => RuleFor(c => c.Name).ValidDepartmentName();
}

public sealed class CreateDepartmentHandler(IAppDbContext db, IValidator<CreateDepartmentCommand> validator, IAuditLog audit)
{
    public async Task<DepartmentItem> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await DepartmentQueries.EnsureNameFreeAsync(db, command.Name, exceptId: null, cancellationToken);
        await DepartmentQueries.EnsureApproverAsync(db, command.ApproverUserId, cancellationToken);

        var department = Department.Create(command.Name, command.ApproverUserId);
        db.Departments.Add(department);
        audit.Record(AuditActions.DepartmentCreated, AuditEntityTypes.Department, department.Id,
            newValues: new { department.Name, department.ApproverUserId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.DepartmentNameTaken, DepartmentQueries.NameTakenMessage, cancellationToken);
        return await DepartmentQueries.LoadItemAsync(db, department.Id, cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Departments/UpdateDepartment.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Departments;

public sealed record UpdateDepartmentCommand(Guid Id, uint Version, string Name, Guid? ApproverUserId);

internal sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator() => RuleFor(c => c.Name).ValidDepartmentName();
}

public sealed class UpdateDepartmentHandler(IAppDbContext db, IValidator<UpdateDepartmentCommand> validator, IAuditLog audit)
{
    public async Task<DepartmentItem> HandleAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Department);
        db.SetExpectedVersion(department, command.Version);

        if (TurkishText.ToSearchKey(command.Name) != department.NormalizedName)
        {
            await DepartmentQueries.EnsureNameFreeAsync(db, command.Name, department.Id, cancellationToken);
        }

        if (command.ApproverUserId != department.ApproverUserId)
        {
            await DepartmentQueries.EnsureApproverAsync(db, command.ApproverUserId, cancellationToken);
        }

        var before = new { department.Name, department.ApproverUserId };
        department.Update(command.Name, command.ApproverUserId);
        audit.Record(AuditActions.DepartmentUpdated, AuditEntityTypes.Department, department.Id, before,
            new { department.Name, department.ApproverUserId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.DepartmentNameTaken, DepartmentQueries.NameTakenMessage, cancellationToken);
        return await DepartmentQueries.LoadItemAsync(db, department.Id, cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Departments/SetDepartmentActive.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Organization.Departments;

public sealed record SetDepartmentActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetDepartmentActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<DepartmentItem> HandleAsync(SetDepartmentActiveCommand command, CancellationToken cancellationToken)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Department);
        if (department.IsActive != command.IsActive)
        {
            db.SetExpectedVersion(department, command.Version);
            if (command.IsActive)
            {
                department.Reactivate();
            }
            else
            {
                department.Deactivate();
            }

            audit.Record(
                command.IsActive ? AuditActions.DepartmentReactivated : AuditActions.DepartmentDeactivated,
                AuditEntityTypes.Department,
                department.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await DepartmentQueries.LoadItemAsync(db, department.Id, cancellationToken);
    }
}
```

- [ ] **Step 3: Implement locations**

`backend/src/Tgysm.Application/Features/Organization/Locations/LocationItem.cs`:

```csharp
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record LocationItem(Guid Id, string Name, Guid? ParentId, bool IsActive, uint Version)
{
    public static LocationItem From(Location location) =>
        new(location.Id, location.Name, location.ParentId, location.IsActive, location.Version);
}
```

`backend/src/Tgysm.Application/Features/Organization/Locations/LocationRules.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Locations;

internal static class LocationRules
{
    public const string NameTakenMessage = "Another location with this name exists under the same parent.";

    public static IRuleBuilderOptions<T, string> ValidLocationName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Location.NameMaxLength);

    public static async Task EnsureParentActiveAsync(IAppDbContext db, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is { } id && !await db.Locations.AnyAsync(l => l.Id == id && l.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Parent location is missing or inactive.");
        }
    }

    public static async Task EnsureNameFreeAsync(IAppDbContext db, Guid? parentId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Locations.AnyAsync(l => l.ParentId == parentId && l.NormalizedName == key && l.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.LocationNameTaken, NameTakenMessage);
        }
    }

    /// <summary>Walks up from the new parent; reaching <paramref name="locationId"/> means the move creates a cycle.</summary>
    public static async Task EnsureNoCycleAsync(IAppDbContext db, Guid locationId, Guid? newParentId, CancellationToken cancellationToken)
    {
        if (newParentId is null)
        {
            return;
        }

        var parents = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.ParentId, cancellationToken);
        var current = newParentId;
        for (var steps = 0; current is { } id && steps <= parents.Count; steps++)
        {
            if (id == locationId)
            {
                throw new DomainException(ErrorCodes.LocationCycle, "A location cannot be moved under itself or its descendant.");
            }

            current = parents.GetValueOrDefault(id);
        }
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Locations/ListLocations.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Organization.Locations;

/// <summary>Returns the whole location tree as a flat list; the UI builds the tree.</summary>
public sealed class ListLocationsHandler(IAppDbContext db)
{
    /// <summary>Reference data stays small; the cap protects the endpoint from unbounded responses.</summary>
    public const int MaxLocations = 2000;

    public async Task<IReadOnlyList<LocationItem>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Locations.AsNoTracking()
            .OrderBy(l => l.Name).ThenBy(l => l.Id)
            .Take(MaxLocations)
            .Select(l => new LocationItem(l.Id, l.Name, l.ParentId, l.IsActive, l.Version))
            .ToListAsync(cancellationToken);
}
```

`backend/src/Tgysm.Application/Features/Organization/Locations/CreateLocation.cs`:

```csharp
using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record CreateLocationCommand(string Name, Guid? ParentId);

internal sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator() => RuleFor(c => c.Name).ValidLocationName();
}

public sealed class CreateLocationHandler(IAppDbContext db, IValidator<CreateLocationCommand> validator, IAuditLog audit)
{
    public async Task<LocationItem> HandleAsync(CreateLocationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await LocationRules.EnsureParentActiveAsync(db, command.ParentId, cancellationToken);
        await LocationRules.EnsureNameFreeAsync(db, command.ParentId, command.Name, exceptId: null, cancellationToken);

        var location = Location.Create(command.Name, command.ParentId);
        db.Locations.Add(location);
        audit.Record(AuditActions.LocationCreated, AuditEntityTypes.Location, location.Id, newValues: new { location.Name, location.ParentId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.LocationNameTaken, LocationRules.NameTakenMessage, cancellationToken);
        return LocationItem.From(location);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Locations/UpdateLocation.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record UpdateLocationCommand(Guid Id, uint Version, string Name, Guid? ParentId);

internal sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator() => RuleFor(c => c.Name).ValidLocationName();
}

public sealed class UpdateLocationHandler(IAppDbContext db, IValidator<UpdateLocationCommand> validator, IAuditLog audit)
{
    public async Task<LocationItem> HandleAsync(UpdateLocationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var location = await db.Locations.SingleOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Location);
        db.SetExpectedVersion(location, command.Version);

        if (command.ParentId != location.ParentId)
        {
            if (command.ParentId == location.Id)
            {
                throw new DomainException(ErrorCodes.LocationCycle, "A location cannot be its own parent.");
            }

            await LocationRules.EnsureParentActiveAsync(db, command.ParentId, cancellationToken);
            await LocationRules.EnsureNoCycleAsync(db, location.Id, command.ParentId, cancellationToken);
        }

        await LocationRules.EnsureNameFreeAsync(db, command.ParentId, command.Name, location.Id, cancellationToken);

        var before = new { location.Name, location.ParentId };
        location.Update(command.Name, command.ParentId);
        audit.Record(AuditActions.LocationUpdated, AuditEntityTypes.Location, location.Id, before, new { location.Name, location.ParentId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.LocationNameTaken, LocationRules.NameTakenMessage, cancellationToken);
        return LocationItem.From(location);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Locations/SetLocationActive.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record SetLocationActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetLocationActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<LocationItem> HandleAsync(SetLocationActiveCommand command, CancellationToken cancellationToken)
    {
        var location = await db.Locations.SingleOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Location);
        if (location.IsActive == command.IsActive)
        {
            return LocationItem.From(location);
        }

        db.SetExpectedVersion(location, command.Version);
        if (command.IsActive)
        {
            await LocationRules.EnsureParentActiveAsync(db, location.ParentId, cancellationToken);
            location.Reactivate();
        }
        else
        {
            if (await db.Locations.AnyAsync(l => l.ParentId == location.Id && l.IsActive, cancellationToken))
            {
                throw new DomainException(ErrorCodes.LocationHasActiveChildren, "Deactivate child locations first.");
            }

            location.Deactivate();
        }

        audit.Record(
            command.IsActive ? AuditActions.LocationReactivated : AuditActions.LocationDeactivated,
            AuditEntityTypes.Location,
            location.Id);
        await db.SaveChangesAsync(cancellationToken);
        return LocationItem.From(location);
    }
}
```

- [ ] **Step 4: Implement endpoints**

`backend/src/Tgysm.Api/Endpoints/Admin/DepartmentEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Organization.Departments;

namespace Tgysm.Api.Endpoints.Admin;

internal static class DepartmentEndpoints
{
    private const string Tag = "Admin: Departments";

    public static RouteGroupBuilder MapDepartmentEndpoints(this RouteGroupBuilder admin)
    {
        var departments = admin.MapGroup(ApiRoutes.Departments).WithTags(Tag);

        departments.MapGet("", ListAsync);
        departments.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        departments.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        departments.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        departments.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    private static async Task<Ok<PagedResult<DepartmentItem>>> ListAsync(
        string? search, bool? isActive, int? page, int? pageSize, ListDepartmentsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListDepartmentsQuery(search, isActive, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));

    private static async Task<Created<DepartmentItem>> CreateAsync(
        CreateDepartmentCommand command, CreateDepartmentHandler handler, CancellationToken cancellationToken)
    {
        var item = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Departments}/{item.Id}", item);
    }

    private static async Task<Ok<DepartmentItem>> UpdateAsync(
        Guid id, UpdateDepartmentRequest request, UpdateDepartmentHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new UpdateDepartmentCommand(id, request.Version, request.Name, request.ApproverUserId), cancellationToken));

    private static async Task<Ok<DepartmentItem>> DeactivateAsync(
        Guid id, VersionRequest request, SetDepartmentActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetDepartmentActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<DepartmentItem>> ReactivateAsync(
        Guid id, VersionRequest request, SetDepartmentActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetDepartmentActiveCommand(id, request.Version, IsActive: true), cancellationToken));
}
```

`backend/src/Tgysm.Api/Endpoints/Admin/LocationEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Organization.Locations;

namespace Tgysm.Api.Endpoints.Admin;

internal static class LocationEndpoints
{
    private const string Tag = "Admin: Locations";

    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder admin)
    {
        var locations = admin.MapGroup(ApiRoutes.Locations).WithTags(Tag);

        locations.MapGet("", ListAsync);
        locations.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        locations.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        locations.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        locations.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    private static async Task<Ok<IReadOnlyList<LocationItem>>> ListAsync(ListLocationsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Created<LocationItem>> CreateAsync(
        CreateLocationCommand command, CreateLocationHandler handler, CancellationToken cancellationToken)
    {
        var item = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Locations}/{item.Id}", item);
    }

    private static async Task<Ok<LocationItem>> UpdateAsync(
        Guid id, UpdateLocationRequest request, UpdateLocationHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateLocationCommand(id, request.Version, request.Name, request.ParentId), cancellationToken));

    private static async Task<Ok<LocationItem>> DeactivateAsync(
        Guid id, VersionRequest request, SetLocationActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetLocationActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<LocationItem>> ReactivateAsync(
        Guid id, VersionRequest request, SetLocationActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetLocationActiveCommand(id, request.Version, IsActive: true), cancellationToken));
}
```

In `AdminEndpoints.MapAdminEndpoints`, after `admin.MapUserEndpoints();`:

```csharp
        admin.MapDepartmentEndpoints();
        admin.MapLocationEndpoints();
```

- [ ] **Step 5: Run tests**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test --nologo`
Expected: all PASS.

- [ ] **Step 6: Prove the cycle test guards the rule**

Comment out the `await LocationRules.EnsureNoCycleAsync(...)` line in `UpdateLocationHandler`; run `--filter Moving_a_location_under_itself_or_its_descendant_is_rejected` → FAIL. Restore → PASS.

- [ ] **Step 7: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---

### Task 11: Admin teams, memberships and audit API

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Organization/Teams/{TeamDtos,TeamQueries,ListTeams,GetTeam,CreateTeam,UpdateTeam,SetTeamActive,AddTeamMember,RemoveTeamMember}.cs`
- Create: `backend/src/Tgysm.Application/Features/Audit/ListAuditEvents.cs`
- Create: `backend/src/Tgysm.Api/Endpoints/Admin/{TeamEndpoints,AuditEndpoints}.cs`
- Modify: `backend/src/Tgysm.Api/Endpoints/Admin/AdminEndpoints.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Admin/{TeamAdminTests,AuditAdminTests}.cs`

**Interfaces:**
- Consumes: as Task 10, plus `UpdateTeamRequest`, `AddTeamMemberRequest`.
- Produces:
  - `TeamItem(Guid Id, string Name, bool IsActive, int MemberCount, uint Version)`, `TeamMemberItem(Guid UserId, string FirstName, string LastName, string Email, UserRole Role, bool IsActive)`, `TeamDetail(Guid Id, string Name, bool IsActive, uint Version, IReadOnlyList<TeamMemberItem> Members)`
  - `ListTeamsQuery(string? Search, bool? IsActive, int Page, int PageSize)`, `GetTeamQuery(Guid Id)`, `CreateTeamCommand(string Name)`, `UpdateTeamCommand(Guid Id, uint Version, string Name)`, `SetTeamActiveCommand(Guid Id, uint Version, bool IsActive)`, `AddTeamMemberCommand(Guid TeamId, Guid UserId)`, `RemoveTeamMemberCommand(Guid TeamId, Guid UserId)`; handlers return `TeamDetail` (list: `PagedResult<TeamItem>`)
  - `AuditEventItem(Guid Id, DateTimeOffset OccurredAt, Guid? ActorUserId, string? ActorName, string Action, string EntityType, Guid? EntityId, string? Reason, string? OldValues, string? NewValues, string CorrelationId)`, `ListAuditEventsQuery(string? Action, string? EntityType, Guid? EntityId, Guid? ActorUserId, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize)` newest first
  - Endpoints: `/api/v1/admin/teams` (`GET ""`, `GET /{id}`, `POST ""`, `PUT /{id}`, `POST /{id}/deactivate|reactivate`, `POST /{id}/members`, `DELETE /{id}/members/{userId}`), `GET /api/v1/admin/audit-events`

- [ ] **Step 1: Write the failing tests**

`backend/tests/Tgysm.Api.IntegrationTests/Admin/TeamAdminTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class TeamAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TeamsPath = "/api/v1/admin/teams";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreateTeamAsync(ApiClient admin, string name)
    {
        using var response = await admin.PostAsync(TeamsPath, new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    [Fact]
    public async Task Adds_and_removes_members_and_counts_them_in_the_list()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Saha Destek");
        var itStaff = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "tech@example.com");
        var otherAdmin = await TestUsers.CreateAsync(factory, UserRole.Admin, "lead@example.com");

        using var addFirst = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = itStaff.Id });
        using var addSecond = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = otherAdmin.Id });
        using var list = await admin.GetAsync(TeamsPath);
        Assert.Equal(2, (await admin.ReadJsonAsync<JsonElement>(list)).GetProperty("items")[0].GetProperty("memberCount").GetInt32());

        using var remove = await admin.DeleteAsync($"{TeamsPath}/{Id(team)}/members/{itStaff.Id}");
        var detail = await admin.ReadJsonAsync<JsonElement>(remove);
        Assert.Equal(1, detail.GetProperty("members").GetArrayLength());
        Assert.Equal(otherAdmin.Id, detail.GetProperty("members")[0].GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Member_rules_are_enforced()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Ağ Ekibi");
        var employee = await TestUsers.CreateAsync(factory, UserRole.Employee, "employee@example.com");
        var tech = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "tech@example.com");
        var inactiveTech = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "gone@example.com");
        await using (var db = factory.Database.CreateDbContext())
        {
            var tracked = await db.Users.FindAsync([inactiveTech.Id], TestContext.Current.CancellationToken);
            tracked!.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var employeeAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = employee.Id });
        using var inactiveAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = inactiveTech.Id });
        using var firstAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = tech.Id });
        using var duplicateAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = tech.Id });
        using var removeUnknown = await admin.DeleteAsync($"{TeamsPath}/{Id(team)}/members/{employee.Id}");

        Assert.Equal(ErrorCodes.MemberRoleInvalid, await ApiClient.ReadCodeAsync(employeeAdd));
        Assert.Equal(ErrorCodes.MemberInactive, await ApiClient.ReadCodeAsync(inactiveAdd));
        Assert.Equal(HttpStatusCode.OK, firstAdd.StatusCode);
        Assert.Equal(ErrorCodes.MembershipExists, await ApiClient.ReadCodeAsync(duplicateAdd));
        Assert.Equal(HttpStatusCode.NotFound, removeUnknown.StatusCode);
    }

    [Fact]
    public async Task Inactive_team_rejects_new_members_and_names_are_unique()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Yazılım");
        var tech = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "tech@example.com");

        using var deactivate = await admin.PostAsync($"{TeamsPath}/{Id(team)}/deactivate", new { version = team.GetProperty("version").GetUInt32() });
        using var add = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = tech.Id });
        using var duplicateName = await admin.PostAsync(TeamsPath, new { name = "YAZILIM" });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(add));
        Assert.Equal(ErrorCodes.TeamNameTaken, await ApiClient.ReadCodeAsync(duplicateName));
    }

    [Fact]
    public async Task Rename_with_stale_version_returns_conflict()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Destek");
        var version = team.GetProperty("version").GetUInt32();

        using var first = await admin.PutAsync($"{TeamsPath}/{Id(team)}", new { version, name = "Destek 1" });
        using var second = await admin.PutAsync($"{TeamsPath}/{Id(team)}", new { version, name = "Destek 2" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }
}
```

`backend/tests/Tgysm.Api.IntegrationTests/Admin/AuditAdminTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AuditAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string AuditPath = "/api/v1/admin/audit-events";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Lists_newest_first_with_actor_name_and_filters_by_entity_type()
    {
        var (admin, self) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        using var department = await admin.PostAsync("/api/v1/admin/departments", new { name = "Finans" });
        using var team = await admin.PostAsync("/api/v1/admin/teams", new { name = "Masaüstü Destek" });

        using var all = await admin.GetAsync(AuditPath);
        using var teamsOnly = await admin.GetAsync($"{AuditPath}?entityType={AuditEntityTypes.Team}");

        var allItems = (await admin.ReadJsonAsync<JsonElement>(all)).GetProperty("items");
        Assert.Equal(AuditActions.TeamCreated, allItems[0].GetProperty("action").GetString());
        Assert.Equal("Test User", allItems[0].GetProperty("actorName").GetString());
        Assert.Equal(self.Id, allItems[0].GetProperty("actorUserId").GetGuid());
        var teamItems = (await admin.ReadJsonAsync<JsonElement>(teamsOnly)).GetProperty("items");
        Assert.All(teamItems.EnumerateArray(), item => Assert.Equal(AuditEntityTypes.Team, item.GetProperty("entityType").GetString()));
    }

    [Fact]
    public async Task Future_from_date_returns_nothing()
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        using var response = await admin.GetAsync($"{AuditPath}?from={from}");

        Assert.Equal(0, (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Non_admin_is_forbidden()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var response = await client.GetAsync(AuditPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Api.IntegrationTests --nologo --filter "TeamAdminTests|AuditAdminTests"`
Expected: FAIL (404).

- [ ] **Step 2: Implement teams**

`backend/src/Tgysm.Application/Features/Organization/Teams/TeamDtos.cs`:

```csharp
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record TeamItem(Guid Id, string Name, bool IsActive, int MemberCount, uint Version);

public sealed record TeamMemberItem(Guid UserId, string FirstName, string LastName, string Email, UserRole Role, bool IsActive);

public sealed record TeamDetail(Guid Id, string Name, bool IsActive, uint Version, IReadOnlyList<TeamMemberItem> Members);
```

`backend/src/Tgysm.Application/Features/Organization/Teams/TeamQueries.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Teams;

internal static class TeamQueries
{
    public const string NameTakenMessage = "Another team uses this name.";

    public static IRuleBuilderOptions<T, string> ValidTeamName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Team.NameMaxLength);

    public static async Task<TeamDetail> LoadDetailAsync(IAppDbContext db, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);

        var members = await (
            from membership in db.TeamMemberships.AsNoTracking()
            where membership.TeamId == teamId
            join user in db.Users on membership.UserId equals user.Id
            orderby user.LastName, user.FirstName, user.Id
            select new TeamMemberItem(user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.IsActive))
            .ToListAsync(cancellationToken);

        return new TeamDetail(team.Id, team.Name, team.IsActive, team.Version, members);
    }

    public static async Task EnsureNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Teams.AnyAsync(t => t.NormalizedName == key && t.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.TeamNameTaken, NameTakenMessage);
        }
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/ListTeams.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record ListTeamsQuery(string? Search, bool? IsActive, int Page, int PageSize);

public sealed class ListTeamsHandler(IAppDbContext db)
{
    public Task<PagedResult<TeamItem>> HandleAsync(ListTeamsQuery query, CancellationToken cancellationToken)
    {
        var teams = db.Teams.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            teams = teams.Where(t => t.NormalizedName.Contains(key));
        }

        if (query.IsActive is { } isActive)
        {
            teams = teams.Where(t => t.IsActive == isActive);
        }

        return teams
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Select(t => new TeamItem(t.Id, t.Name, t.IsActive, db.TeamMemberships.Count(m => m.TeamId == t.Id), t.Version))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/GetTeam.cs`:

```csharp
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record GetTeamQuery(Guid Id);

public sealed class GetTeamHandler(IAppDbContext db)
{
    public Task<TeamDetail> HandleAsync(GetTeamQuery query, CancellationToken cancellationToken) =>
        TeamQueries.LoadDetailAsync(db, query.Id, cancellationToken);
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/CreateTeam.cs`:

```csharp
using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record CreateTeamCommand(string Name);

internal sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator() => RuleFor(c => c.Name).ValidTeamName();
}

public sealed class CreateTeamHandler(IAppDbContext db, IValidator<CreateTeamCommand> validator, IAuditLog audit)
{
    public async Task<TeamDetail> HandleAsync(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await TeamQueries.EnsureNameFreeAsync(db, command.Name, exceptId: null, cancellationToken);

        var team = Team.Create(command.Name);
        db.Teams.Add(team);
        audit.Record(AuditActions.TeamCreated, AuditEntityTypes.Team, team.Id, newValues: new { team.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.TeamNameTaken, TeamQueries.NameTakenMessage, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/UpdateTeam.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record UpdateTeamCommand(Guid Id, uint Version, string Name);

internal sealed class UpdateTeamValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamValidator() => RuleFor(c => c.Name).ValidTeamName();
}

public sealed class UpdateTeamHandler(IAppDbContext db, IValidator<UpdateTeamCommand> validator, IAuditLog audit)
{
    public async Task<TeamDetail> HandleAsync(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);
        db.SetExpectedVersion(team, command.Version);

        if (TurkishText.ToSearchKey(command.Name) != team.NormalizedName)
        {
            await TeamQueries.EnsureNameFreeAsync(db, command.Name, team.Id, cancellationToken);
        }

        var before = new { team.Name };
        team.Rename(command.Name);
        audit.Record(AuditActions.TeamUpdated, AuditEntityTypes.Team, team.Id, before, new { team.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.TeamNameTaken, TeamQueries.NameTakenMessage, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/SetTeamActive.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record SetTeamActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetTeamActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<TeamDetail> HandleAsync(SetTeamActiveCommand command, CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);
        if (team.IsActive != command.IsActive)
        {
            db.SetExpectedVersion(team, command.Version);
            if (command.IsActive)
            {
                team.Reactivate();
            }
            else
            {
                team.Deactivate();
            }

            audit.Record(command.IsActive ? AuditActions.TeamReactivated : AuditActions.TeamDeactivated, AuditEntityTypes.Team, team.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/AddTeamMember.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record AddTeamMemberCommand(Guid TeamId, Guid UserId);

/// <summary>Only active IT staff and admins can join an active team (ADR-0005).</summary>
public sealed class AddTeamMemberHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    private const string MembershipExistsMessage = "The user is already a member of this team.";

    public async Task<TeamDetail> HandleAsync(AddTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.TeamId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);
        if (!team.IsActive)
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "The team is inactive.");
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        if (user.Role == UserRole.Employee)
        {
            throw new DomainException(ErrorCodes.MemberRoleInvalid, "Only IT staff and admins can be team members.");
        }

        if (!user.IsActive)
        {
            throw new DomainException(ErrorCodes.MemberInactive, "Inactive users cannot join a team.");
        }

        if (await db.TeamMemberships.AnyAsync(m => m.TeamId == team.Id && m.UserId == user.Id, cancellationToken))
        {
            throw new DomainException(ErrorCodes.MembershipExists, MembershipExistsMessage);
        }

        db.TeamMemberships.Add(TeamMembership.Create(team.Id, user.Id, clock.GetUtcNow()));
        audit.Record(AuditActions.TeamMemberAdded, AuditEntityTypes.Team, team.Id, newValues: new { UserId = user.Id });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.MembershipExists, MembershipExistsMessage, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
```

`backend/src/Tgysm.Application/Features/Organization/Teams/RemoveTeamMember.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record RemoveTeamMemberCommand(Guid TeamId, Guid UserId);

/// <summary>Removes the membership row; the audit trail keeps the history.</summary>
public sealed class RemoveTeamMemberHandler(IAppDbContext db, IAuditLog audit)
{
    private const string MembershipEntity = "team_membership";

    public async Task<TeamDetail> HandleAsync(RemoveTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var membership = await db.TeamMemberships
            .SingleOrDefaultAsync(m => m.TeamId == command.TeamId && m.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException(MembershipEntity);

        db.TeamMemberships.Remove(membership);
        audit.Record(AuditActions.TeamMemberRemoved, AuditEntityTypes.Team, command.TeamId, oldValues: new { command.UserId });
        await db.SaveChangesAsync(cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, command.TeamId, cancellationToken);
    }
}
```

- [ ] **Step 3: Implement the audit query**

`backend/src/Tgysm.Application/Features/Audit/ListAuditEvents.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;

namespace Tgysm.Application.Features.Audit;

public sealed record AuditEventItem(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string? ActorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Reason,
    string? OldValues,
    string? NewValues,
    string CorrelationId);

public sealed record ListAuditEventsQuery(
    string? Action,
    string? EntityType,
    Guid? EntityId,
    Guid? ActorUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);

public sealed class ListAuditEventsHandler(IAppDbContext db)
{
    public Task<PagedResult<AuditEventItem>> HandleAsync(ListAuditEventsQuery query, CancellationToken cancellationToken)
    {
        var events = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            events = events.Where(e => e.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            events = events.Where(e => e.EntityType == query.EntityType);
        }

        if (query.EntityId is { } entityId)
        {
            events = events.Where(e => e.EntityId == entityId);
        }

        if (query.ActorUserId is { } actorUserId)
        {
            events = events.Where(e => e.ActorUserId == actorUserId);
        }

        if (query.From is { } from)
        {
            events = events.Where(e => e.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(e => e.OccurredAt <= to);
        }

        var items =
            from auditEvent in events
            join actor in db.Users on auditEvent.ActorUserId equals (Guid?)actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby auditEvent.OccurredAt descending, auditEvent.Id descending
            select new AuditEventItem(
                auditEvent.Id,
                auditEvent.OccurredAt,
                auditEvent.ActorUserId,
                actor == null ? null : actor.FirstName + " " + actor.LastName,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.Reason,
                auditEvent.OldValues,
                auditEvent.NewValues,
                auditEvent.CorrelationId);

        return items.ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
```

- [ ] **Step 4: Implement endpoints**

`backend/src/Tgysm.Api/Endpoints/Admin/TeamEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Organization.Teams;

namespace Tgysm.Api.Endpoints.Admin;

internal static class TeamEndpoints
{
    private const string Tag = "Admin: Teams";

    public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder admin)
    {
        var teams = admin.MapGroup(ApiRoutes.Teams).WithTags(Tag);

        teams.MapGet("", ListAsync);
        teams.MapGet("/{id:guid}", GetAsync).ProducesProblem(StatusCodes.Status404NotFound);
        teams.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPost("/{id:guid}/members", AddMemberAsync).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapDelete("/{id:guid}/members/{userId:guid}", RemoveMemberAsync).ProducesProblem(StatusCodes.Status404NotFound);

        return admin;
    }

    private static async Task<Ok<PagedResult<TeamItem>>> ListAsync(
        string? search, bool? isActive, int? page, int? pageSize, ListTeamsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListTeamsQuery(search, isActive, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize), cancellationToken));

    private static async Task<Ok<TeamDetail>> GetAsync(Guid id, GetTeamHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetTeamQuery(id), cancellationToken));

    private static async Task<Created<TeamDetail>> CreateAsync(CreateTeamCommand command, CreateTeamHandler handler, CancellationToken cancellationToken)
    {
        var detail = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Teams}/{detail.Id}", detail);
    }

    private static async Task<Ok<TeamDetail>> UpdateAsync(Guid id, UpdateTeamRequest request, UpdateTeamHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateTeamCommand(id, request.Version, request.Name), cancellationToken));

    private static async Task<Ok<TeamDetail>> DeactivateAsync(Guid id, VersionRequest request, SetTeamActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetTeamActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<TeamDetail>> ReactivateAsync(Guid id, VersionRequest request, SetTeamActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetTeamActiveCommand(id, request.Version, IsActive: true), cancellationToken));

    private static async Task<Ok<TeamDetail>> AddMemberAsync(Guid id, AddTeamMemberRequest request, AddTeamMemberHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new AddTeamMemberCommand(id, request.UserId), cancellationToken));

    private static async Task<Ok<TeamDetail>> RemoveMemberAsync(Guid id, Guid userId, RemoveTeamMemberHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new RemoveTeamMemberCommand(id, userId), cancellationToken));
}
```

`backend/src/Tgysm.Api/Endpoints/Admin/AuditEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Audit;

namespace Tgysm.Api.Endpoints.Admin;

internal static class AuditEndpoints
{
    private const string Tag = "Admin: Audit";

    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet(ApiRoutes.AuditEvents, ListAsync).WithTags(Tag);
        return admin;
    }

    private static async Task<Ok<PagedResult<AuditEventItem>>> ListAsync(
        string? action, string? entityType, Guid? entityId, Guid? actorUserId, DateTimeOffset? from, DateTimeOffset? to,
        int? page, int? pageSize, ListAuditEventsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListAuditEventsQuery(action, entityType, entityId, actorUserId, from, to,
                page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));
}
```

In `AdminEndpoints.MapAdminEndpoints`, after the location mapping:

```csharp
        admin.MapTeamEndpoints();
        admin.MapAuditEndpoints();
```

- [ ] **Step 5: Run tests**

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test --nologo`
Expected: all PASS.

- [ ] **Step 6: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 12: OpenAPI types, frontend scaffold, i18n, Turkish helpers, translation coverage

**Files:**
- Modify: `backend/src/Tgysm.Api/Tgysm.Api.csproj` (build-time OpenAPI document)
- Create: `tools/gen-api-types.sh`; Modify: `tools/lint.sh`, `tools/test.sh`
- Create: `frontend/package.json`, `frontend/index.html`, `frontend/vite.config.ts`, `frontend/tsconfig.json`, `frontend/tsconfig.app.json`, `frontend/tsconfig.node.json`, `frontend/eslint.config.js`
- Create: `frontend/src/vite-env.d.ts`, `frontend/src/index.css`, `frontend/src/test/setup.ts`
- Create: `frontend/src/shared/i18n/index.ts`, `frontend/src/shared/i18n/i18next.d.ts`, `frontend/src/shared/i18n/locales/tr/{common,auth,admin,errors}.json`
- Create: `frontend/src/shared/lib/{turkish,format}.ts`
- Create (generated): `frontend/openapi/Tgysm.Api.json`, `frontend/src/shared/api/schema.d.ts`
- Test: `frontend/src/shared/lib/{turkish,format}.test.ts`, `frontend/src/shared/i18n/i18n.test.ts`
- Test: `backend/tests/Tgysm.Application.Tests/{RepositoryPaths,TranslationCoverageTests}.cs`

**Interfaces:**
- Consumes: backend endpoints (Tasks 7–11), `ErrorCodes`, `AuditActions`, `AuditEntityTypes`.
- Produces:
  - `tools/gen-api-types.sh` → `frontend/openapi/Tgysm.Api.json` + `frontend/src/shared/api/schema.d.ts` (`paths`, `components`)
  - i18n: namespaces `common` (default), `auth`, `admin`, `errors`; `DEFAULT_LANGUAGE = 'tr'`; `import '…/shared/i18n'` initializes i18next
  - `turkish.ts`: `TURKISH_LOCALE`, `trUpper(value)`, `trLower(value)`, `type SuffixKind`, `withSuffix(word, kind, { properNoun? }) : string | null`
  - `format.ts`: `APP_TIME_ZONE = 'Europe/Istanbul'`, `formatDateTime(iso: string): string`
  - npm scripts: `dev`, `build`, `preview`, `lint`, `typecheck`, `test`, `test:coverage`, `e2e`, `gen:api`

- [ ] **Step 1: Enable build-time OpenAPI generation**

Add to `backend/src/Tgysm.Api/Tgysm.Api.csproj`:

```xml
  <PropertyGroup>
    <!-- Off for normal builds; tools/gen-api-types.sh turns it on. -->
    <OpenApiGenerateDocuments>false</OpenApiGenerateDocuments>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../../frontend/openapi</OpenApiDocumentsDirectory>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
```

`tools/gen-api-types.sh`:

```bash
#!/usr/bin/env bash
# Regenerate the OpenAPI document and the frontend API types after an API contract change.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly OPENAPI_FILE="openapi/Tgysm.Api.json"
readonly SCHEMA_FILE="src/shared/api/schema.d.ts"

mkdir -p frontend/openapi
dotnet build backend/src/Tgysm.Api --nologo -p:OpenApiGenerateDocuments=true
cd frontend
npx openapi-typescript "$OPENAPI_FILE" -o "$SCHEMA_FILE"
echo "Generated $OPENAPI_FILE and $SCHEMA_FILE"
```

- [ ] **Step 2: Create the frontend package and install dependencies**

`frontend/package.json`:

```json
{
  "name": "tgysm-frontend",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "lint": "eslint . --max-warnings=0",
    "typecheck": "tsc -b",
    "test": "vitest run",
    "test:coverage": "vitest run --coverage",
    "e2e": "playwright test",
    "gen:api": "openapi-typescript openapi/Tgysm.Api.json -o src/shared/api/schema.d.ts"
  }
}
```

Run:

```bash
cd /Users/lektuel/claude/TGYSM/frontend
npm install react react-dom react-router @tanstack/react-query react-hook-form @hookform/resolvers zod i18next react-i18next radix-ui openapi-fetch
npm install -D vite @vitejs/plugin-react typescript@~6.0.3 tailwindcss @tailwindcss/vite \
  vitest @vitest/coverage-v8 jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom \
  @playwright/test openapi-typescript \
  eslint@^9 @eslint/js@^9 typescript-eslint globals eslint-plugin-react eslint-plugin-react-hooks \
  eslint-plugin-react-refresh eslint-plugin-jsx-a11y eslint-plugin-import \
  @types/react @types/react-dom @types/node
/Users/lektuel/claude/TGYSM/tools/gen-api-types.sh
```

Expected: `package-lock.json`, `node_modules/`, `openapi/Tgysm.Api.json`, `src/shared/api/schema.d.ts`. Open `schema.d.ts` and confirm `paths["/api/v1/admin/users"]` and `components["schemas"]["MeDto"]` with `role: "employee" | "it_staff" | "admin"` exist. If enum values are missing, check that `ConfigureHttpJsonOptions` registers `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` (Task 7).

- [ ] **Step 3: Write build and TypeScript config**

`frontend/index.html`:

```html
<!doctype html>
<html lang="tr">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="theme-color" content="#1d4ed8" />
    <title>TGYSM</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`frontend/vite.config.ts` (Vite 8 uses Rolldown; confirm the `build.rolldownOptions.output.codeSplitting` option name in current Vite docs via context7 before relying on it):

```ts
/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env.TGYSM_API_URL ?? 'http://localhost:5080';
const DEV_PORT = 5173;
const PREVIEW_PORT = 4173;
const CHUNK_WARNING_LIMIT_KB = 500;
const COVERAGE_LINE_THRESHOLD = 80;
const apiProxy = { '/api': { target: API_TARGET, changeOrigin: false } };

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { dedupe: ['react', 'react-dom'] },
  server: { port: DEV_PORT, strictPort: true, proxy: apiProxy },
  preview: { port: PREVIEW_PORT, strictPort: true, proxy: apiProxy },
  build: {
    chunkSizeWarningLimit: CHUNK_WARNING_LIMIT_KB,
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            // React runtime (plus scheduler) in one group; keeps `dedupe` single-instance obvious.
            { name: 'vendor-react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/, priority: 100 },
            { name: 'vendor-router', test: /node_modules[\\/]react-router/, priority: 90 },
            { name: 'vendor-tanstack', test: /node_modules[\\/]@tanstack[\\/]/, priority: 90 },
            { name: 'vendor-radix', test: /node_modules[\\/](radix-ui|@radix-ui)[\\/]/, priority: 90 },
            { name: 'vendor-forms', test: /node_modules[\\/](react-hook-form|@hookform|zod)[\\/]/, priority: 90 },
            { name: 'vendor-i18n', test: /node_modules[\\/](i18next|react-i18next)[\\/]/, priority: 90 },
            { name: 'vendor-other', test: /node_modules/, priority: 10 },
            // App code; everything else falls to the entry chunk.
            { name: 'feature-admin', test: /[\\/]src[\\/]features[\\/]admin[\\/]/, priority: 40 },
            { name: 'app-shared', test: /[\\/]src[\\/]shared[\\/]/, priority: 30 },
          ],
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/test/**',
        'src/main.tsx',
        'src/vite-env.d.ts',
        'src/shared/api/schema.d.ts',
        'src/shared/i18n/i18next.d.ts',
      ],
      thresholds: { lines: COVERAGE_LINE_THRESHOLD },
    },
  },
});
```

`frontend/tsconfig.json`:

```json
{
  "files": [],
  "references": [{ "path": "./tsconfig.app.json" }, { "path": "./tsconfig.node.json" }]
}
```

`frontend/tsconfig.app.json`:

```json
{
  "compilerOptions": {
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.app.tsbuildinfo",
    "target": "ES2022",
    "lib": ["ES2023", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "types": ["vite/client"],
    "jsx": "react-jsx",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "resolveJsonModule": true,
    "verbatimModuleSyntax": true,
    "isolatedModules": true,
    "noEmit": true,
    "skipLibCheck": true
  },
  "include": ["src"]
}
```

`frontend/tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "tsBuildInfoFile": "./node_modules/.tmp/tsconfig.node.tsbuildinfo",
    "target": "ES2023",
    "lib": ["ES2023", "DOM"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "types": ["node"],
    "strict": true,
    "verbatimModuleSyntax": true,
    "isolatedModules": true,
    "noEmit": true,
    "skipLibCheck": true
  },
  "include": ["vite.config.ts", "playwright.config.ts", "e2e"]
}
```

`frontend/src/vite-env.d.ts`:

```ts
/// <reference types="vite/client" />
```

- [ ] **Step 4: Write the ESLint config**

`frontend/eslint.config.js` (per `~/.claude/guides/react.md`; if `reactHooks.configs.flat.recommended` is undefined in the installed v7, use the flat preset name from the plugin README — do not drop the rules):

```js
import js from '@eslint/js';
import importPlugin from 'eslint-plugin-import';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import react from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'build', 'coverage', 'node_modules', 'playwright-report', 'test-results', 'src/shared/api/schema.d.ts'] },
  js.configs.recommended,
  tseslint.configs.recommended,
  react.configs.flat.recommended,
  react.configs.flat['jsx-runtime'],
  reactHooks.configs.flat.recommended,
  jsxA11y.flatConfigs.recommended,
  {
    files: ['**/*.{ts,tsx,js}'],
    languageOptions: { globals: { ...globals.browser } },
    settings: { react: { version: 'detect' } },
    plugins: { 'react-refresh': reactRefresh, import: importPlugin },
    rules: {
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true, allowExportNames: ['routes'] }],
      'import/no-duplicates': 'error',
      'import/order': [
        'error',
        {
          groups: ['builtin', 'external', 'internal', 'parent', 'sibling', 'index'],
          'newlines-between': 'always',
          alphabetize: { order: 'asc', caseInsensitive: true },
        },
      ],
      // Render-loop / re-render guards (always errors).
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'error',
      'react-hooks/set-state-in-render': 'error',
      'react-hooks/set-state-in-effect': 'error',
      'react-hooks/static-components': 'error',
      'react-hooks/immutability': 'error',
      'react-hooks/refs': 'error',
      'react-hooks/purity': 'error',
      'react/no-unstable-nested-components': 'error',
      'react/jsx-no-constructed-context-values': 'error',
      'react/no-object-type-as-default-prop': 'error',
      // TypeScript types replace runtime prop-types.
      'react/prop-types': 'off',
    },
  },
  {
    files: ['vite.config.ts', 'playwright.config.ts', 'e2e/**/*.ts', 'eslint.config.js'],
    languageOptions: { globals: { ...globals.node } },
  },
);
```

- [ ] **Step 5: Write styles and test setup**

`frontend/src/index.css`:

```css
@import "tailwindcss";

@theme {
  --color-brand-50: #eff6ff;
  --color-brand-100: #dbeafe;
  --color-brand-300: #93c5fd;
  --color-brand-600: #1d4ed8;
  --color-brand-700: #1e40af;
  --color-brand-900: #1e3a8a;
}

html {
  -webkit-text-size-adjust: 100%;
}

body {
  background-color: var(--color-slate-50);
  color: var(--color-slate-900);
  -webkit-font-smoothing: antialiased;
}

:focus-visible {
  outline: 2px solid var(--color-brand-600);
  outline-offset: 2px;
}
```

`frontend/src/test/setup.ts`:

```ts
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

import '../shared/i18n';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
```

- [ ] **Step 6: Write the failing frontend unit tests**

`frontend/src/shared/lib/turkish.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { trLower, trUpper, withSuffix } from './turkish';

describe('trUpper / trLower', () => {
  it('uses Turkish dotted and dotless i', () => {
    expect(trUpper('istanbul ılık')).toBe('İSTANBUL ILIK');
    expect(trLower('IŞIK İLKER')).toBe('ışık ilker');
  });
});

describe('withSuffix', () => {
  it.each([
    ['Ankara', 'dative', "Ankara'ya"],
    ['İzmir', 'dative', "İzmir'e"],
    ['Kars', 'locative', "Kars'ta"],
    ['İzmir', 'locative', "İzmir'de"],
    ['Ankara', 'ablative', "Ankara'dan"],
    ['Kars', 'ablative', "Kars'tan"],
    ['Ankara', 'accusative', "Ankara'yı"],
    ['Ordu', 'accusative', "Ordu'yu"],
    ['Bolu', 'genitive', "Bolu'nun"],
    ['Ümit', 'dative', "Ümit'e"],
  ] as const)('%s + %s = %s (proper noun)', (word, kind, expected) => {
    expect(withSuffix(word, kind, { properNoun: true })).toBe(expected);
  });

  it.each([
    ['tablet', 'plural', 'tabletler'],
    ['telefon', 'plural', 'telefonlar'],
    ['saat', 'dative', 'saate'],
    ['kontrol', 'locative', 'kontrolde'],
  ] as const)('%s + %s = %s (common noun, pronunciation exceptions)', (word, kind, expected) => {
    expect(withSuffix(word, kind)).toBe(expected);
  });

  it('uses the pronunciation of known abbreviations', () => {
    expect(withSuffix('IT', 'dative')).toBe("IT'ye");
    expect(withSuffix('PDF', 'locative')).toBe("PDF'te");
  });

  it('returns null when pronunciation is unknown', () => {
    expect(withSuffix('XYZ', 'dative')).toBeNull();
    expect(withSuffix('   ', 'dative')).toBeNull();
  });
});
```

`frontend/src/shared/lib/format.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { formatDateTime } from './format';

describe('formatDateTime', () => {
  it('formats UTC timestamps in Istanbul time with Turkish month names', () => {
    const text = formatDateTime('2026-09-26T09:05:00Z');

    expect(text).toContain('Eyl');
    expect(text).toContain('12:05');
  });
});
```

`frontend/src/shared/i18n/i18n.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import i18n from '.';

describe('i18n', () => {
  it('serves Turkish texts from every namespace', () => {
    expect(i18n.language).toBe('tr');
    expect(i18n.t('common:roles.it_staff')).toBe('IT personeli');
    expect(i18n.t('auth:login.submit')).toBe('Giriş yap');
    expect(i18n.t('admin:nav.users')).toBe('Kullanıcılar');
    expect(i18n.t('errors:auth.invalid_credentials')).toBe('E-posta veya şifre hatalı.');
  });
});
```

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run`
Expected: FAIL — modules `./turkish`, `./format`, `.` not found.

- [ ] **Step 7: Implement the Turkish helpers and formatting**

`frontend/src/shared/lib/turkish.ts`:

```ts
/** Turkish text helpers (ADR-0006). */
export const TURKISH_LOCALE = 'tr-TR';

export const trUpper = (value: string): string => value.toLocaleUpperCase(TURKISH_LOCALE);
export const trLower = (value: string): string => value.toLocaleLowerCase(TURKISH_LOCALE);

export type SuffixKind = 'dative' | 'locative' | 'ablative' | 'accusative' | 'genitive' | 'plural';

export interface SuffixOptions {
  /** Proper nouns take an apostrophe before the suffix ("Ankara'ya"). */
  properNoun?: boolean;
}

const BACK_VOWELS = 'aıou';
const FRONT_VOWELS = 'eiöü';
const VOWELS = BACK_VOWELS + FRONT_VOWELS;
/** "FıSTıKÇı ŞaHaP": consonants that turn a following d into t. */
const HARD_CONSONANTS = 'fstkçşhp';
const APOSTROPHE = "'";
const ABBREVIATION_MAX_LENGTH = 5;
const LETTER_PATTERN = /\p{L}/u;

/**
 * How a word is read aloud, keyed by its Turkish lower-case form. The written form alone does not tell
 * the last vowel of these words. Note that Turkish lower-casing turns "IT" into "ıt".
 */
const PRONUNCIATION_OVERRIDES: Readonly<Record<string, string>> = {
  saat: 'saet',
  harf: 'herf',
  hal: 'hel',
  kalp: 'kelp',
  dikkat: 'dikket',
  rol: 'röl',
  gol: 'göl',
  kontrol: 'kontröl',
  alkol: 'alköl',
  ıt: 'ayti',
  pc: 'pisi',
  pdf: 'pedeef',
  csv: 'ceseve',
  vpn: 'vepeen',
  usb: 'usebe',
  sms: 'semes',
};

function isAbbreviation(word: string): boolean {
  return word.length <= ABBREVIATION_MAX_LENGTH && LETTER_PATTERN.test(word) && word === trUpper(word);
}

function pronunciationOf(word: string): string | null {
  const lower = trLower(word);
  const override = PRONUNCIATION_OVERRIDES[lower];
  if (override !== undefined) {
    return override;
  }
  return isAbbreviation(word) ? null : lower;
}

function lastVowelOf(pronunciation: string): string | null {
  for (let index = pronunciation.length - 1; index >= 0; index -= 1) {
    const character = pronunciation[index];
    if (character !== undefined && VOWELS.includes(character)) {
      return character;
    }
  }
  return null;
}

const twoWayVowel = (vowel: string): string => (BACK_VOWELS.includes(vowel) ? 'a' : 'e');

function fourWayVowel(vowel: string): string {
  switch (vowel) {
    case 'a':
    case 'ı':
      return 'ı';
    case 'e':
    case 'i':
      return 'i';
    case 'o':
    case 'u':
      return 'u';
    default:
      return 'ü';
  }
}

/**
 * Appends a case or plural suffix with vowel harmony, buffer letters (y, n) and d/t assimilation.
 * Returns null when the pronunciation is unknown; callers then use a suffix-free label instead.
 */
export function withSuffix(word: string, kind: SuffixKind, options: SuffixOptions = {}): string | null {
  const trimmed = word.trim();
  const pronunciation = pronunciationOf(trimmed);
  const vowel = pronunciation === null ? null : lastVowelOf(pronunciation);
  if (pronunciation === null || vowel === null) {
    return null;
  }

  const lastCharacter = pronunciation.at(-1) ?? '';
  const endsWithVowel = VOWELS.includes(lastCharacter);
  const a = twoWayVowel(vowel);
  const i = fourWayVowel(vowel);
  const d = HARD_CONSONANTS.includes(lastCharacter) ? 't' : 'd';

  const suffixes: Record<SuffixKind, string> = {
    dative: endsWithVowel ? `y${a}` : a,
    locative: `${d}${a}`,
    ablative: `${d}${a}n`,
    accusative: endsWithVowel ? `y${i}` : i,
    genitive: endsWithVowel ? `n${i}n` : `${i}n`,
    plural: `l${a}r`,
  };

  const separator = options.properNoun === true || isAbbreviation(trimmed) ? APOSTROPHE : '';
  return `${trimmed}${separator}${suffixes[kind]}`;
}
```

`frontend/src/shared/lib/format.ts`:

```ts
import { TURKISH_LOCALE } from './turkish';

/** Display time zone of the institution (spec assumption; stored times are UTC). */
export const APP_TIME_ZONE = 'Europe/Istanbul';

const dateTimeFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: APP_TIME_ZONE,
});

export const formatDateTime = (iso: string): string => dateTimeFormatter.format(new Date(iso));
```

- [ ] **Step 8: Write the i18n resources and setup**

`frontend/src/shared/i18n/index.ts`:

```ts
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import admin from './locales/tr/admin.json';
import auth from './locales/tr/auth.json';
import common from './locales/tr/common.json';
import errors from './locales/tr/errors.json';

export const DEFAULT_LANGUAGE = 'tr';
export const DEFAULT_NAMESPACE = 'common';

export const resources = {
  tr: { common, auth, admin, errors },
} as const;

void i18n.use(initReactI18next).init({
  lng: DEFAULT_LANGUAGE,
  fallbackLng: DEFAULT_LANGUAGE,
  defaultNS: DEFAULT_NAMESPACE,
  ns: Object.keys(resources.tr),
  resources,
  interpolation: { escapeValue: false },
  returnNull: false,
});

export default i18n;
```

`frontend/src/shared/i18n/i18next.d.ts`:

```ts
import 'i18next';

import type { DEFAULT_NAMESPACE, resources } from './index';

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: typeof DEFAULT_NAMESPACE;
    resources: (typeof resources)['tr'];
  }
}
```

`frontend/src/shared/i18n/locales/tr/common.json`:

```json
{
  "appName": "TGYSM",
  "none": "Yok",
  "network": "Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.",
  "actions": {
    "save": "Kaydet",
    "cancel": "Vazgeç",
    "create": "Oluştur",
    "edit": "Düzenle",
    "close": "Kapat",
    "retry": "Tekrar dene",
    "logout": "Çıkış yap",
    "deactivate": "Pasifleştir",
    "reactivate": "Aktifleştir",
    "confirm": "Onayla",
    "copy": "Kopyala",
    "copied": "Kopyalandı",
    "openMenu": "Menüyü aç"
  },
  "states": {
    "loading": "Yükleniyor…",
    "empty": "Kayıt bulunamadı.",
    "error": "Bir hata oluştu."
  },
  "status": {
    "all": "Tümü",
    "active": "Aktif",
    "inactive": "Pasif"
  },
  "roles": {
    "all": "Tüm roller",
    "employee": "Çalışan",
    "it_staff": "IT personeli",
    "admin": "Yönetici"
  },
  "pagination": {
    "label": "Sayfalama",
    "previous": "Önceki",
    "next": "Sonraki",
    "summary": "{{from}}–{{to}} / {{total}} kayıt"
  },
  "notFound": {
    "title": "Sayfa bulunamadı",
    "back": "Ana sayfaya dön"
  },
  "temporaryPassword": {
    "title": "Geçici şifre",
    "description": "Bu şifre yalnızca bir kez gösterilir. Kullanıcıya güvenli bir yoldan iletin; ilk girişte değiştirmesi istenir.",
    "label": "Geçici şifre"
  }
}
```

`frontend/src/shared/i18n/locales/tr/auth.json`:

```json
{
  "login": {
    "title": "Giriş yap",
    "subtitle": "IT destek platformuna hoş geldiniz.",
    "email": "E-posta",
    "password": "Şifre",
    "submit": "Giriş yap",
    "lockoutHint": "Art arda 5 hatalı denemede hesabınız 15 dakika kilitlenir."
  },
  "changePassword": {
    "title": "Şifre değiştir",
    "requiredNotice": "İlk girişte şifrenizi değiştirmeniz gerekiyor.",
    "current": "Mevcut şifre",
    "new": "Yeni şifre",
    "confirm": "Yeni şifre (tekrar)",
    "hint": "En az 12 karakter; harf ve rakam içermeli.",
    "mismatch": "Şifreler eşleşmiyor.",
    "submit": "Şifreyi değiştir"
  },
  "home": {
    "greeting": "Merhaba, {{name}}",
    "intro": "Yapmak istediğiniz işlemi seçin.",
    "profileCard": "Profilim",
    "profileCardDescription": "Bilgilerinizi görün, şifrenizi değiştirin.",
    "adminCard": "Yönetim paneli",
    "adminCardDescription": "Kullanıcılar, departmanlar, lokasyonlar ve ekipler."
  },
  "profile": {
    "title": "Profilim",
    "name": "Ad soyad",
    "email": "E-posta",
    "role": "Rol",
    "department": "Departman",
    "location": "Lokasyon",
    "notSet": "Belirtilmemiş",
    "changePassword": "Şifre değiştir"
  },
  "shell": {
    "mainNav": "Ana menü",
    "home": "Ana sayfa",
    "profile": "Profilim",
    "admin": "Yönetim paneli"
  }
}
```

`frontend/src/shared/i18n/locales/tr/admin.json`:

```json
{
  "title": "Yönetim",
  "nav": {
    "label": "Yönetim menüsü",
    "users": "Kullanıcılar",
    "departments": "Departmanlar",
    "locations": "Lokasyonlar",
    "teams": "Ekipler",
    "audit": "Denetim kayıtları",
    "backToApp": "Uygulamaya dön"
  },
  "users": {
    "title": "Kullanıcılar",
    "new": "Yeni kullanıcı",
    "edit": "Kullanıcıyı düzenle",
    "searchLabel": "Kullanıcı ara",
    "searchPlaceholder": "Ad, soyad veya e-posta",
    "statusFilter": "Durum",
    "roleFilter": "Rol",
    "listCaption": "Kullanıcı listesi",
    "resetPassword": "Şifre sıfırla",
    "columns": {
      "name": "Ad soyad",
      "email": "E-posta",
      "role": "Rol",
      "department": "Departman",
      "status": "Durum",
      "actions": "İşlemler"
    },
    "fields": {
      "firstName": "Ad",
      "lastName": "Soyad",
      "email": "E-posta",
      "role": "Rol",
      "department": "Departman",
      "location": "Lokasyon"
    },
    "confirmDeactivate": "{{name}} pasifleştirilsin mi? Kullanıcının açık oturumu hemen kapanır.",
    "confirmReactivate": "{{name}} tekrar aktifleştirilsin mi?",
    "confirmReset": "{{name}} için yeni geçici şifre oluşturulsun mu? Kullanıcının açık oturumu kapanır."
  },
  "departments": {
    "title": "Departmanlar",
    "new": "Yeni departman",
    "edit": "Departmanı düzenle",
    "searchLabel": "Departman ara",
    "listCaption": "Departman listesi",
    "fields": {
      "name": "Departman adı",
      "approver": "Onaylayıcı yönetici"
    },
    "columns": {
      "name": "Ad",
      "approver": "Onaylayıcı",
      "status": "Durum",
      "actions": "İşlemler"
    },
    "confirmDeactivate": "{{name}} pasifleştirilsin mi?",
    "confirmReactivate": "{{name}} aktifleştirilsin mi?"
  },
  "locations": {
    "title": "Lokasyonlar",
    "new": "Yeni lokasyon",
    "edit": "Lokasyonu düzenle",
    "addChild": "Alt lokasyon ekle",
    "treeLabel": "Lokasyon ağacı",
    "noParent": "Yok (en üst seviye)",
    "fields": {
      "name": "Lokasyon adı",
      "parent": "Üst lokasyon"
    },
    "confirmDeactivate": "{{name}} pasifleştirilsin mi?",
    "confirmReactivate": "{{name}} aktifleştirilsin mi?"
  },
  "teams": {
    "title": "Ekipler",
    "new": "Yeni ekip",
    "edit": "Ekibi düzenle",
    "searchLabel": "Ekip ara",
    "listCaption": "Ekip listesi",
    "memberCount": "{{count}} üye",
    "manageMembers": "Üyeleri yönet",
    "membersTitle": "{{name}} üyeleri",
    "addMember": "Üye ekle",
    "memberToAdd": "Eklenecek kişi",
    "removeMember": "Çıkar",
    "noMembers": "Bu ekipte henüz üye yok.",
    "fields": {
      "name": "Ekip adı"
    },
    "columns": {
      "name": "Ad",
      "members": "Üye sayısı",
      "status": "Durum",
      "actions": "İşlemler"
    },
    "confirmDeactivate": "{{name}} pasifleştirilsin mi?",
    "confirmReactivate": "{{name}} aktifleştirilsin mi?"
  },
  "audit": {
    "title": "Denetim kayıtları",
    "listCaption": "Denetim kayıtları",
    "entityFilter": "Kayıt türü",
    "allEntities": "Tümü",
    "system": "Sistem",
    "details": "Ayrıntılar",
    "before": "Önce",
    "after": "Sonra",
    "correlation": "İzleme kimliği",
    "columns": {
      "time": "Zaman",
      "actor": "Kişi",
      "action": "İşlem",
      "entity": "Kayıt"
    }
  },
  "entityTypes": {
    "user": "Kullanıcı",
    "department": "Departman",
    "location": "Lokasyon",
    "team": "Ekip"
  },
  "auditActions": {
    "setup": {
      "first_admin_created": "İlk yönetici oluşturuldu"
    },
    "auth": {
      "login_succeeded": "Başarılı giriş",
      "login_failed": "Başarısız giriş",
      "locked_out": "Hesap kilitlendi",
      "password_changed": "Şifre değiştirildi"
    },
    "users": {
      "created": "Kullanıcı oluşturuldu",
      "updated": "Kullanıcı güncellendi",
      "role_changed": "Rol değiştirildi",
      "deactivated": "Kullanıcı pasifleştirildi",
      "reactivated": "Kullanıcı aktifleştirildi",
      "password_reset": "Şifre sıfırlandı"
    },
    "departments": {
      "created": "Departman oluşturuldu",
      "updated": "Departman güncellendi",
      "deactivated": "Departman pasifleştirildi",
      "reactivated": "Departman aktifleştirildi"
    },
    "locations": {
      "created": "Lokasyon oluşturuldu",
      "updated": "Lokasyon güncellendi",
      "deactivated": "Lokasyon pasifleştirildi",
      "reactivated": "Lokasyon aktifleştirildi"
    },
    "teams": {
      "created": "Ekip oluşturuldu",
      "updated": "Ekip güncellendi",
      "deactivated": "Ekip pasifleştirildi",
      "reactivated": "Ekip aktifleştirildi",
      "member_added": "Ekibe üye eklendi",
      "member_removed": "Ekipten üye çıkarıldı"
    }
  }
}
```

`frontend/src/shared/i18n/locales/tr/errors.json`:

```json
{
  "common": {
    "not_found": "Kayıt bulunamadı.",
    "forbidden": "Bu işlem için yetkiniz yok.",
    "unexpected": "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.",
    "duplicate": "Bu kayıt zaten var.",
    "reference_inactive": "Seçilen kayıt pasif ya da bulunamadı."
  },
  "validation": {
    "failed": "Lütfen formdaki hataları düzeltin.",
    "required": "Bu alan zorunludur.",
    "too_long": "Bu alan çok uzun.",
    "invalid_email": "Geçerli bir e-posta adresi girin.",
    "invalid": "Geçersiz değer."
  },
  "concurrency": {
    "conflict": "Bu kayıt siz düzenlerken başka biri tarafından değiştirildi. Listeyi yenileyip tekrar deneyin."
  },
  "auth": {
    "unauthenticated": "Oturumunuz sona erdi. Lütfen tekrar giriş yapın.",
    "invalid_credentials": "E-posta veya şifre hatalı.",
    "password_change_required": "Devam etmek için şifrenizi değiştirmeniz gerekiyor.",
    "csrf_invalid": "Güvenlik doğrulaması başarısız oldu. Lütfen tekrar deneyin.",
    "current_password_invalid": "Mevcut şifre hatalı.",
    "password_policy": "Şifre en az 12 karakter olmalı, harf ve rakam içermelidir.",
    "password_same_as_old": "Yeni şifre mevcut şifreyle aynı olamaz.",
    "rate_limited": "Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar deneyin."
  },
  "setup": {
    "already_completed": "Kurulum zaten tamamlanmış."
  },
  "users": {
    "email_taken": "Bu e-posta adresi başka bir hesapta kullanılıyor.",
    "last_admin": "Sistemdeki son aktif yönetici pasifleştirilemez ve rolü değiştirilemez."
  },
  "departments": {
    "name_taken": "Bu adla bir departman zaten var.",
    "approver_invalid": "Onaylayıcı aktif bir yönetici olmalıdır."
  },
  "locations": {
    "name_taken": "Aynı üst lokasyonda bu adla bir lokasyon zaten var.",
    "cycle": "Lokasyon kendi altına taşınamaz.",
    "has_active_children": "Önce alt lokasyonları pasifleştirin."
  },
  "teams": {
    "name_taken": "Bu adla bir ekip zaten var.",
    "membership_exists": "Kullanıcı zaten bu ekipte.",
    "member_inactive": "Pasif kullanıcı ekibe eklenemez.",
    "member_role_invalid": "Ekibe yalnızca IT personeli ve yöneticiler eklenebilir."
  }
}
```

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run && npm run lint && npm run typecheck; echo "exit=$?"`
Expected: 3 test files PASS, lint and typecheck clean, `exit=0`.

- [ ] **Step 9: Write the backend translation coverage test**

`backend/tests/Tgysm.Application.Tests/RepositoryPaths.cs`:

```csharp
namespace Tgysm.Application.Tests;

internal static class RepositoryPaths
{
    private const string MarkerFile = "tgysm.md";

    public static string Root { get; } = FindRoot();

    public static string Locale(string fileName) =>
        Path.Combine(Root, "frontend", "src", "shared", "i18n", "locales", "tr", fileName);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, MarkerFile)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"Repository root ({MarkerFile}) not found.");
    }
}
```

`backend/tests/Tgysm.Application.Tests/TranslationCoverageTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Tests;

/// <summary>Every API error code and audit label must have Turkish text, and errors.json must not keep stale keys (ADR-0006).</summary>
public sealed class TranslationCoverageTests
{
    private const string ErrorsFile = "errors.json";
    private const string AdminFile = "admin.json";
    private const string AuditActionsPrefix = "auditActions.";
    private const string EntityTypesPrefix = "entityTypes.";
    private const char KeySeparator = '.';

    [Fact]
    public void Every_error_code_has_a_turkish_text_and_no_translation_is_stale()
    {
        var codes = ConstantsOf(typeof(ErrorCodes));
        var keys = LeafKeys(RepositoryPaths.Locale(ErrorsFile));

        Assert.Empty(codes.Except(keys));
        Assert.Empty(keys.Except(codes));
    }

    [Fact]
    public void Every_audit_action_and_entity_type_has_a_turkish_label()
    {
        var keys = LeafKeys(RepositoryPaths.Locale(AdminFile));

        Assert.Empty(ConstantsOf(typeof(AuditActions)).Select(a => AuditActionsPrefix + a).Except(keys));
        Assert.Empty(ConstantsOf(typeof(AuditEntityTypes)).Select(t => EntityTypesPrefix + t).Except(keys));
    }

    private static HashSet<string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> LeafKeys(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.RootElement, prefix: string.Empty, keys);
        return keys;
    }

    private static void Collect(JsonElement element, string prefix, HashSet<string> keys)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? property.Name : prefix + KeySeparator + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Collect(property.Value, key, keys);
            }
            else if (!string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                keys.Add(key);
            }
        }
    }
}
```

Add `<ProjectReference Include="..\..\src\Tgysm.Domain\Tgysm.Domain.csproj" />` to `Tgysm.Application.Tests.csproj` only if the transitive reference through Application does not resolve `ErrorCodes` (it normally does).

Run: `cd /Users/lektuel/claude/TGYSM/backend && dotnet test tests/Tgysm.Application.Tests --nologo`
Expected: PASS. Prove it can fail: delete the `"rate_limited"` line from `errors.json`, re-run → FAIL listing `auth.rate_limited`; restore → PASS.

- [ ] **Step 10: Extend lint and test scripts to the frontend**

Replace `tools/lint.sh`:

```bash
#!/usr/bin/env bash
# Lint gate: C# formatting, ESLint (zero warnings) and TypeScript.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly MIGRATIONS_DIR="backend/src/Tgysm.Infrastructure/Persistence/Migrations"

dotnet format backend/Tgysm.slnx --verify-no-changes --exclude "$MIGRATIONS_DIR"
(cd frontend && npm run lint && npm run typecheck)
```

Replace `tools/test.sh`:

```bash
#!/usr/bin/env bash
# Test gate: backend and frontend tests (coverage gates are added in Task 16).
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet test backend/Tgysm.slnx --nologo
(cd frontend && npm test)
```

Run: `/Users/lektuel/claude/TGYSM/tools/lint.sh && /Users/lektuel/claude/TGYSM/tools/test.sh; echo "exit=$?"`
Expected: `exit=0`.

- [ ] **Step 11: Checkpoint (no commit)**

Run: `git -C /Users/lektuel/claude/TGYSM status --short`
Expected: `frontend/` appears without `node_modules/` (ignored).

---

### Task 13: API client, session, guards, login, password change, home, profile

**Files:**
- Create: `frontend/src/shared/api/{errors,client,errorMessage,queryKeys}.ts`
- Create: `frontend/src/shared/routes.ts`
- Create: `frontend/src/shared/ui/{Button,TextField,FormAlert,States,AppShell,NotFoundPage}.tsx`
- Create: `frontend/src/app/{queryClient.ts,router.tsx,App.tsx}`, `frontend/src/main.tsx`
- Create: `frontend/src/features/auth/{api.ts,session.ts,guards.tsx,passwordSchema.ts,LoginPage.tsx,ChangePasswordPage.tsx}`
- Create: `frontend/src/features/home/HomePage.tsx`, `frontend/src/features/profile/ProfilePage.tsx`
- Create: `frontend/src/test/{mockApi.ts,renderRoute.tsx}`
- Modify: `tools/build.sh`
- Test: `frontend/src/shared/api/{errors,client}.test.ts`, `frontend/src/features/auth/{guards,LoginPage,ChangePasswordPage}.test.tsx`

**Interfaces:**
- Consumes: generated `paths`/`components`, i18n, `turkish.ts`.
- Produces:
  - `errors.ts`: `class ApiError(status: number, code: string, fieldErrors?: Record<string, string[]>)`, `class NetworkError extends ApiError` (status 0), `ERROR_CODES` (`unexpected`, `required`, `tooLong`, `invalidEmail`, `passwordPolicy`, `unauthenticated`, `passwordChangeRequired`, `csrfInvalid`, `concurrencyConflict`, `referenceInactive`, `emailTaken`, `departmentNameTaken`, `approverInvalid`, `locationNameTaken`, `locationCycle`, `teamNameTaken`), `toApiError(status, body)`
  - `client.ts`: `api` (openapi-fetch client with CSRF middleware), `CSRF_HEADER`, `refreshCsrfToken()`, `resetCsrfToken()`, `unwrap<T>(call)`
  - `errorMessage.ts`: `translateMessage(message)`, `translateErrorCode(code)`, `describeError(error)`, `fieldError(error?)`, `applyServerErrors(error, setError, fields, codeFields?) : boolean`, `isHandledByFields(error, fields, codeFields?) : boolean`
  - `queryKeys.ts`: `ME_QUERY_KEY`
  - `routes.ts`: `ROUTES` (`home`, `login`, `changePassword`, `profile`, `admin`, `adminUsers`, `adminDepartments`, `adminLocations`, `adminTeams`, `adminAudit`)
  - UI: `Button({ variant?, isLoading?, ...button })`, `TextField({ label, error?, hint?, ...input })`, `FormAlert({ message })`, `LoadingState()`, `EmptyState({ message? })`, `ErrorState({ error, onRetry? })`, `AppShell()`, `NotFoundPage()`
  - Auth: `type Me`, `type Role`, `useMe()`, `useLogin()`, `useLogout()`, `useChangePassword()`, `SessionContext`, `useSession()`, `RequireSession`, `RequireRole({ roles })`, `ADMIN_ONLY`
  - `routes` (router config array; admin children added in Task 14), `createQueryClient({ retry? })`
  - Test helpers: `mockApi(routes) : { calls: Request[]; set(key, handler) }` keyed `"METHOD /path"`, `renderRoute(path)`, fixtures `ADMIN_ME`, `EMPLOYEE_ME`

- [ ] **Step 1: Write test helpers**

`frontend/src/test/mockApi.ts`:

```ts
import { vi } from 'vitest';

export interface MockResponse {
  status: number;
  body?: unknown;
}

export type MockHandler = MockResponse | ((request: Request) => MockResponse | Promise<MockResponse>);

/** Keys are "METHOD /path" without the query string, e.g. "GET /api/v1/auth/me". */
export type MockRoutes = Record<string, MockHandler>;

export interface MockApi {
  calls: Request[];
  set: (key: string, handler: MockHandler) => void;
}

const HTTP_OK = 200;
const HTTP_NO_CONTENT = 204;
const HTTP_BAD_REQUEST = 400;
const HTTP_NOT_FOUND = 404;
const CSRF_ROUTE = 'GET /api/v1/auth/csrf';

export const ADMIN_ME = {
  id: '00000000-0000-7000-8000-000000000001',
  firstName: 'Tugay',
  lastName: 'İltuş',
  email: 'admin@example.com',
  role: 'admin',
  mustChangePassword: false,
  departmentName: null,
  locationName: null,
} as const;

export const EMPLOYEE_ME = {
  ...ADMIN_ME,
  id: '00000000-0000-7000-8000-000000000002',
  firstName: 'Ayşe',
  lastName: 'Kaya',
  email: 'ayse@example.com',
  role: 'employee',
} as const;

function toResponse({ status, body }: MockResponse): Response {
  if (status === HTTP_NO_CONTENT) {
    return new Response(null, { status });
  }
  const contentType = status >= HTTP_BAD_REQUEST ? 'application/problem+json' : 'application/json';
  return new Response(JSON.stringify(body ?? {}), { status, headers: { 'Content-Type': contentType } });
}

export function mockApi(routes: MockRoutes): MockApi {
  const table = new Map<string, MockHandler>([[CSRF_ROUTE, { status: HTTP_OK, body: { token: 'test-csrf' } }], ...Object.entries(routes)]);
  const calls: Request[] = [];

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = input instanceof Request ? input : new Request(new URL(String(input), window.location.origin), init);
      calls.push(request.clone());
      const handler = table.get(`${request.method} ${new URL(request.url).pathname}`);
      if (handler === undefined) {
        return toResponse({ status: HTTP_NOT_FOUND, body: { code: 'common.not_found' } });
      }
      return toResponse(typeof handler === 'function' ? await handler(request) : handler);
    }),
  );

  return { calls, set: (key, handler) => table.set(key, handler) };
}
```

`frontend/src/test/renderRoute.tsx`:

```tsx
import { QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';

import { createQueryClient } from '../app/queryClient';
import { routes } from '../app/router';
import { resetCsrfToken } from '../shared/api/client';

export function renderRoute(path: string) {
  resetCsrfToken();
  const queryClient = createQueryClient({ retry: false });
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const user = userEvent.setup();
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { router, user, queryClient };
}
```

- [ ] **Step 2: Write the failing tests**

`frontend/src/shared/api/errors.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { describeError, translateMessage } from './errorMessage';
import { ApiError, NetworkError, toApiError } from './errors';

describe('toApiError', () => {
  it('reads code and field errors from a problem body', () => {
    const error = toApiError(400, { code: 'validation.failed', errors: { email: ['validation.invalid_email'] } });

    expect(error.status).toBe(400);
    expect(error.code).toBe('validation.failed');
    expect(error.fieldErrors.email).toEqual(['validation.invalid_email']);
  });

  it('falls back to common.unexpected for bodies without a code', () => {
    expect(toApiError(500, 'oops').code).toBe('common.unexpected');
  });
});

describe('describeError', () => {
  it('translates known codes, unknown codes and network failures', () => {
    expect(describeError(new ApiError(409, 'users.email_taken'))).toBe('Bu e-posta adresi başka bir hesapta kullanılıyor.');
    expect(describeError(new ApiError(409, 'made.up'))).toBe('Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.');
    expect(describeError(new NetworkError(new Error('offline')))).toBe('Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.');
    expect(describeError('not an error')).toBe('Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.');
  });
});

describe('translateMessage', () => {
  it('treats namespaced keys as i18n keys and plain values as error codes', () => {
    expect(translateMessage('auth:changePassword.mismatch')).toBe('Şifreler eşleşmiyor.');
    expect(translateMessage('validation.required')).toBe('Bu alan zorunludur.');
  });
});
```

`frontend/src/shared/api/client.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';

import { api, CSRF_HEADER, resetCsrfToken, unwrap } from './client';
import { ApiError } from './errors';

describe('api client', () => {
  it('sends the antiforgery header on unsafe requests only', async () => {
    resetCsrfToken();
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } },
      'POST /api/v1/auth/logout': { status: 204 },
    });

    await expect(unwrap(api.GET('/api/v1/auth/me'))).rejects.toBeInstanceOf(ApiError);
    await unwrap(api.POST('/api/v1/auth/logout'));

    const get = mock.calls.find((r) => r.method === 'GET' && r.url.endsWith('/auth/me'));
    const post = mock.calls.find((r) => r.method === 'POST');
    expect(get?.headers.get(CSRF_HEADER)).toBeNull();
    expect(post?.headers.get(CSRF_HEADER)).toBe('test-csrf');
  });

  it('forgets the token after a csrf rejection so the next call fetches a new one', async () => {
    resetCsrfToken();
    let csrfCalls = 0;
    const mock = mockApi({ 'POST /api/v1/auth/logout': { status: 403, body: { code: 'auth.csrf_invalid' } } });
    mock.set('GET /api/v1/auth/csrf', () => {
      csrfCalls += 1;
      return { status: 200, body: { token: `token-${csrfCalls}` } };
    });

    await expect(unwrap(api.POST('/api/v1/auth/logout'))).rejects.toMatchObject({ code: 'auth.csrf_invalid' });
    await expect(unwrap(api.POST('/api/v1/auth/logout'))).rejects.toBeInstanceOf(ApiError);

    expect(csrfCalls).toBe(2);
  });
});
```

`frontend/src/features/auth/guards.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('route guards', () => {
  it('sends visitors without a session to the login page', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } });

    const { router } = renderRoute('/profile');

    expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it('forces a pending password change before anything else', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: { ...EMPLOYEE_ME, mustChangePassword: true } } });

    const { router } = renderRoute('/');

    expect(await screen.findByText('İlk girişte şifrenizi değiştirmeniz gerekiyor.')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/change-password');
  });

  it('greets a signed-in user on the home page without an admin card for employees', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });

    renderRoute('/');

    expect(await screen.findByRole('heading', { name: 'Merhaba, Ayşe' })).toBeInTheDocument();
    expect(screen.queryByText('Yönetim paneli')).not.toBeInTheDocument();
  });
});
```

`frontend/src/features/auth/LoginPage.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('LoginPage', () => {
  it('shows Turkish field errors when submitted empty', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } });
    const { user } = renderRoute('/login');

    await user.click(await screen.findByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findAllByText('Bu alan zorunludur.')).toHaveLength(2);
  });

  it('shows the uniform error for wrong credentials', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } },
      'POST /api/v1/auth/login': { status: 401, body: { code: 'auth.invalid_credentials' } },
    });
    const { user } = renderRoute('/login');

    await user.type(await screen.findByLabelText('E-posta'), 'ayse@example.com');
    await user.type(screen.getByLabelText('Şifre'), 'wrong-password-1');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya şifre hatalı.');
  });

  it('signs in and goes to the page the user asked for', async () => {
    let signedIn = false;
    const mock = mockApi({ 'POST /api/v1/auth/login': () => { signedIn = true; return { status: 200, body: { mustChangePassword: false } }; } });
    mock.set('GET /api/v1/auth/me', () => (signedIn ? { status: 200, body: EMPLOYEE_ME } : { status: 401, body: { code: 'auth.unauthenticated' } }));
    const { user, router } = renderRoute('/profile');

    await user.type(await screen.findByLabelText('E-posta'), 'ayse@example.com');
    await user.type(screen.getByLabelText('Şifre'), 'Correct-pass-1');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('heading', { name: 'Profilim' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/profile');
  });
});
```

`frontend/src/features/auth/ChangePasswordPage.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const PENDING_ME = { ...EMPLOYEE_ME, mustChangePassword: true };

describe('ChangePasswordPage', () => {
  it('checks that the new passwords match before calling the server', async () => {
    const mock = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: PENDING_ME } });
    const { user } = renderRoute('/change-password');

    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Temp-pass-1234');
    await user.type(screen.getByLabelText('Yeni şifre'), 'Brand-new-pass-7');
    await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'Different-pass-7');
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));

    expect(await screen.findByText('Şifreler eşleşmiyor.')).toBeInTheDocument();
    expect(mock.calls.some((r) => r.url.endsWith('/change-password'))).toBe(false);
  });

  it('shows a wrong current password under that field', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: PENDING_ME },
      'POST /api/v1/auth/change-password': {
        status: 400,
        body: { code: 'validation.failed', errors: { currentPassword: ['auth.current_password_invalid'] } },
      },
    });
    const { user } = renderRoute('/change-password');

    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Wrong-pass-1234');
    await user.type(screen.getByLabelText('Yeni şifre'), 'Brand-new-pass-7');
    await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'Brand-new-pass-7');
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));

    expect(await screen.findByText('Mevcut şifre hatalı.')).toBeInTheDocument();
    expect(screen.getByLabelText('Mevcut şifre')).toHaveAttribute('aria-invalid', 'true');
  });

  it('goes home after a successful change', async () => {
    let changed = false;
    const mock = mockApi({ 'POST /api/v1/auth/change-password': () => { changed = true; return { status: 204 }; } });
    mock.set('GET /api/v1/auth/me', () => ({ status: 200, body: changed ? EMPLOYEE_ME : PENDING_ME }));
    const { user, router } = renderRoute('/change-password');

    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Temp-pass-1234');
    await user.type(screen.getByLabelText('Yeni şifre'), 'Brand-new-pass-7');
    await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'Brand-new-pass-7');
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));

    expect(await screen.findByRole('heading', { name: 'Merhaba, Ayşe' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });
});
```

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run`
Expected: FAIL — modules not found.

- [ ] **Step 3: Implement the API layer**

`frontend/src/shared/api/errors.ts`:

```ts
export const ERROR_CODES = {
  unexpected: 'common.unexpected',
  required: 'validation.required',
  tooLong: 'validation.too_long',
  invalidEmail: 'validation.invalid_email',
  passwordPolicy: 'auth.password_policy',
  unauthenticated: 'auth.unauthenticated',
  passwordChangeRequired: 'auth.password_change_required',
  csrfInvalid: 'auth.csrf_invalid',
  concurrencyConflict: 'concurrency.conflict',
  referenceInactive: 'common.reference_inactive',
  emailTaken: 'users.email_taken',
  departmentNameTaken: 'departments.name_taken',
  approverInvalid: 'departments.approver_invalid',
  locationNameTaken: 'locations.name_taken',
  locationCycle: 'locations.cycle',
  teamNameTaken: 'teams.name_taken',
} as const;

const NETWORK_STATUS = 0;
const NETWORK_CODE = 'network';

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Readonly<Record<string, string[]>>;

  constructor(status: number, code: string, fieldErrors: Record<string, string[]> = {}) {
    super(code);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }
}

/** The request never reached the server (offline, DNS, CORS). */
export class NetworkError extends ApiError {
  constructor(cause: unknown) {
    super(NETWORK_STATUS, NETWORK_CODE);
    this.name = 'NetworkError';
    this.cause = cause;
  }
}

interface ProblemBody {
  code?: unknown;
  errors?: unknown;
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null;

function readFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }
  return Object.fromEntries(
    Object.entries(value).map(([field, codes]) => [field, Array.isArray(codes) ? codes.filter((c): c is string => typeof c === 'string') : []]),
  );
}

export function toApiError(status: number, body: unknown): ApiError {
  const problem: ProblemBody = isRecord(body) ? body : {};
  const code = typeof problem.code === 'string' ? problem.code : ERROR_CODES.unexpected;
  return new ApiError(status, code, readFieldErrors(problem.errors));
}
```

`frontend/src/shared/api/client.ts`:

```ts
import createClient, { type Middleware } from 'openapi-fetch';

import { ApiError, ERROR_CODES, NetworkError, toApiError } from './errors';
import type { paths } from './schema';

export const CSRF_HEADER = 'X-CSRF-TOKEN';
const CSRF_PATH = '/api/v1/auth/csrf';
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

let csrfToken: string | null = null;

/** Fetches a new antiforgery token; call after login and logout because the token is bound to the user. */
export async function refreshCsrfToken(): Promise<void> {
  const response = await fetch(`${window.location.origin}${CSRF_PATH}`, { credentials: 'same-origin' });
  if (!response.ok) {
    throw new ApiError(response.status, ERROR_CODES.unexpected);
  }
  const body = (await response.json()) as { token: string };
  csrfToken = body.token;
}

export function resetCsrfToken(): void {
  csrfToken = null;
}

const csrfMiddleware: Middleware = {
  async onRequest({ request }) {
    if (SAFE_METHODS.has(request.method)) {
      return request;
    }
    if (csrfToken === null) {
      await refreshCsrfToken();
    }
    request.headers.set(CSRF_HEADER, csrfToken ?? '');
    return request;
  },
};

export const api = createClient<paths>({
  baseUrl: window.location.origin,
  credentials: 'same-origin',
  // Resolve fetch at call time so tests can stub the global.
  fetch: (request: Request) => globalThis.fetch(request),
});
api.use(csrfMiddleware);

interface ApiResult<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

/** Returns the response data or throws ApiError / NetworkError. */
export async function unwrap<T>(call: Promise<ApiResult<T>>): Promise<T> {
  let result: ApiResult<T>;
  try {
    result = await call;
  } catch (cause) {
    if (cause instanceof ApiError) {
      throw cause;
    }
    throw new NetworkError(cause);
  }

  if (!result.response.ok) {
    const error = toApiError(result.response.status, result.error);
    if (error.code === ERROR_CODES.csrfInvalid) {
      resetCsrfToken();
    }
    throw error;
  }
  return result.data as T;
}
```

`frontend/src/shared/api/errorMessage.ts`:

```ts
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import i18n from '../i18n';

import { ApiError, ERROR_CODES, NetworkError } from './errors';

const ERRORS_NAMESPACE = 'errors';
const NAMESPACE_SEPARATOR = ':';
const NETWORK_MESSAGE_KEY = 'common:network';

// Error codes arrive at runtime, so they cannot use the typed key overloads.
const translate = i18n.t.bind(i18n) as unknown as (key: string) => string;

export function translateErrorCode(code: string): string {
  const key = `${ERRORS_NAMESPACE}${NAMESPACE_SEPARATOR}${code}`;
  return i18n.exists(key) ? translate(key) : translate(`${ERRORS_NAMESPACE}${NAMESPACE_SEPARATOR}${ERROR_CODES.unexpected}`);
}

/** Form messages are either API error codes ("validation.required") or full i18n keys ("auth:changePassword.mismatch"). */
export function translateMessage(message: string): string {
  return message.includes(NAMESPACE_SEPARATOR) ? translate(message) : translateErrorCode(message);
}

export function fieldError(error?: { message?: string }): string | undefined {
  return error?.message ? translateMessage(error.message) : undefined;
}

export function describeError(error: unknown): string {
  if (error instanceof NetworkError) {
    return translate(NETWORK_MESSAGE_KEY);
  }
  return translateErrorCode(error instanceof ApiError ? error.code : ERROR_CODES.unexpected);
}

type CodeFields<T extends FieldValues> = Readonly<Partial<Record<string, Path<T>>>>;

/** True when the error is shown next to a form field instead of in the form alert. */
export function isHandledByFields<T extends FieldValues>(error: unknown, fields: readonly Path<T>[], codeFields: CodeFields<T> = {}): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  return codeFields[error.code] !== undefined || fields.some((field) => (error.fieldErrors[field]?.length ?? 0) > 0);
}

/** Puts server errors on form fields: per-field validation codes, plus whole-request codes mapped via codeFields. */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
  codeFields: CodeFields<T> = {},
): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  let applied = false;
  for (const field of fields) {
    const code = error.fieldErrors[field]?.[0];
    if (code !== undefined) {
      setError(field, { type: 'server', message: code });
      applied = true;
    }
  }
  const mappedField = codeFields[error.code];
  if (mappedField !== undefined) {
    setError(mappedField, { type: 'server', message: error.code });
    applied = true;
  }
  return applied;
}
```

`frontend/src/shared/api/queryKeys.ts`:

```ts
export const ME_QUERY_KEY = ['auth', 'me'] as const;
```

`frontend/src/shared/routes.ts`:

```ts
/** URL paths are English (ADR-0006). */
export const ROUTES = {
  home: '/',
  login: '/login',
  changePassword: '/change-password',
  profile: '/profile',
  admin: '/admin',
  adminUsers: '/admin/users',
  adminDepartments: '/admin/departments',
  adminLocations: '/admin/locations',
  adminTeams: '/admin/teams',
  adminAudit: '/admin/audit',
} as const;
```

- [ ] **Step 4: Implement base UI components**

`frontend/src/shared/ui/Button.tsx`:

```tsx
import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const VARIANT_CLASSES: Record<Variant, string> = {
  primary: 'bg-brand-600 text-white hover:bg-brand-700 disabled:bg-brand-300',
  secondary: 'border border-slate-300 bg-white text-slate-900 hover:bg-slate-50 disabled:text-slate-400',
  danger: 'bg-red-600 text-white hover:bg-red-700 disabled:bg-red-300',
  ghost: 'text-slate-700 hover:bg-slate-100 disabled:text-slate-400',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  isLoading?: boolean;
}

/** 44 px minimum touch target; disabled while loading so a double tap cannot submit twice. */
export function Button({ variant = 'primary', isLoading = false, disabled, className = '', type = 'button', children, ...rest }: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled === true || isLoading}
      aria-busy={isLoading || undefined}
      className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-4 text-sm font-medium transition-colors disabled:cursor-not-allowed ${VARIANT_CLASSES[variant]} ${className}`}
      {...rest}
    >
      {children}
    </button>
  );
}
```

`frontend/src/shared/ui/TextField.tsx`:

```tsx
import { useId, type InputHTMLAttributes, type Ref } from 'react';

export interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
  ref?: Ref<HTMLInputElement>;
}

/** Label, hint and error are linked to the input for screen readers; 16 px text avoids iOS zoom. */
export function TextField({ label, error, hint, id, className = '', ref, ...inputProps }: TextFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const hintId = hint ? `${inputId}-hint` : undefined;
  const errorId = error ? `${inputId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <label htmlFor={inputId} className="text-sm font-medium text-slate-800">
        {label}
      </label>
      <input
        id={inputId}
        ref={ref}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className="min-h-11 rounded-lg border border-slate-300 bg-white px-3 text-base aria-invalid:border-red-600"
        {...inputProps}
      />
      {hint && (
        <p id={hintId} className="text-xs text-slate-600">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-sm text-red-700">
          {error}
        </p>
      )}
    </div>
  );
}
```

`frontend/src/shared/ui/FormAlert.tsx`:

```tsx
export function FormAlert({ message }: { message: string | null | undefined }) {
  if (!message) {
    return null;
  }
  return (
    <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
      {message}
    </div>
  );
}
```

`frontend/src/shared/ui/States.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { Button } from './Button';

export function LoadingState() {
  const { t } = useTranslation();
  return (
    <p role="status" className="p-6 text-center text-slate-600">
      {t('states.loading')}
    </p>
  );
}

export function EmptyState({ message }: { message?: string }) {
  const { t } = useTranslation();
  return <p className="rounded-xl border border-dashed border-slate-300 p-6 text-center text-slate-600">{message ?? t('states.empty')}</p>;
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <div role="alert" className="flex flex-col items-center gap-3 rounded-xl border border-red-200 bg-red-50 p-6 text-center text-red-800">
      <p>{describeError(error)}</p>
      {onRetry && (
        <Button variant="secondary" onClick={onRetry}>
          {t('actions.retry')}
        </Button>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Implement the auth feature**

`frontend/src/features/auth/api.ts`:

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, refreshCsrfToken, unwrap } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import { ME_QUERY_KEY } from '../../shared/api/queryKeys';
import type { components } from '../../shared/api/schema';

export type Me = components['schemas']['MeDto'];
export type Role = Me['role'];
export type LoginInput = components['schemas']['LoginRequest'];
export type ChangePasswordInput = components['schemas']['ChangePasswordCommand'];

const HTTP_UNAUTHORIZED = 401;
const ME_STALE_TIME_MS = 60_000;

/** null = no session (401); other errors propagate. */
async function fetchMe(): Promise<Me | null> {
  try {
    return await unwrap(api.GET('/api/v1/auth/me'));
  } catch (error) {
    if (error instanceof ApiError && error.status === HTTP_UNAUTHORIZED) {
      return null;
    }
    throw error;
  }
}

export function useMe() {
  return useQuery({ queryKey: ME_QUERY_KEY, queryFn: fetchMe, staleTime: ME_STALE_TIME_MS });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: LoginInput) => {
      const result = await unwrap(api.POST('/api/v1/auth/login', { body: input }));
      await refreshCsrfToken();
      return result;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await unwrap(api.POST('/api/v1/auth/logout'));
      await refreshCsrfToken();
    },
    onSuccess: () => {
      queryClient.clear();
      queryClient.setQueryData(ME_QUERY_KEY, null);
    },
  });
}

export function useChangePassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ChangePasswordInput) => unwrap(api.POST('/api/v1/auth/change-password', { body: input })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }),
  });
}
```

`frontend/src/features/auth/session.ts`:

```ts
import { createContext, useContext } from 'react';

import type { Me, Role } from './api';

export const SessionContext = createContext<Me | null>(null);

export const ADMIN_ONLY: readonly Role[] = ['admin'];

/** The signed-in user; only valid inside RequireSession. */
export function useSession(): Me {
  const me = useContext(SessionContext);
  if (me === null) {
    throw new Error('useSession must be used inside RequireSession.');
  }
  return me;
}
```

`frontend/src/features/auth/guards.tsx`:

```tsx
import type { PropsWithChildren } from 'react';
import { Navigate, useLocation } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { ErrorState, LoadingState } from '../../shared/ui/States';

import { useMe, type Role } from './api';
import { SessionContext, useSession } from './session';

export function RequireSession({ children }: PropsWithChildren) {
  const me = useMe();
  const location = useLocation();

  if (me.isPending) {
    return <LoadingState />;
  }
  if (me.isError) {
    return <ErrorState error={me.error} onRetry={() => void me.refetch()} />;
  }
  if (me.data === null) {
    return <Navigate to={ROUTES.login} replace state={{ from: location.pathname }} />;
  }
  if (me.data.mustChangePassword && location.pathname !== ROUTES.changePassword) {
    return <Navigate to={ROUTES.changePassword} replace />;
  }
  return <SessionContext value={me.data}>{children}</SessionContext>;
}

export function RequireRole({ roles, children }: PropsWithChildren<{ roles: readonly Role[] }>) {
  const me = useSession();
  if (!roles.includes(me.role)) {
    return <Navigate to={ROUTES.home} replace />;
  }
  return children;
}
```

`frontend/src/features/auth/passwordSchema.ts`:

```ts
import { z } from 'zod';

import { ERROR_CODES } from '../../shared/api/errors';

/** Mirrors backend PasswordRules (ADR-0004). */
export const PASSWORD_MIN_LENGTH = 12;
export const PASSWORD_MAX_LENGTH = 128;
const LETTER_PATTERN = /\p{L}/u;
const DIGIT_PATTERN = /\d/;
const MISMATCH_MESSAGE_KEY = 'auth:changePassword.mismatch';

export const newPasswordRule = z
  .string()
  .min(PASSWORD_MIN_LENGTH, ERROR_CODES.passwordPolicy)
  .max(PASSWORD_MAX_LENGTH, ERROR_CODES.passwordPolicy)
  .regex(LETTER_PATTERN, ERROR_CODES.passwordPolicy)
  .regex(DIGIT_PATTERN, ERROR_CODES.passwordPolicy);

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, ERROR_CODES.required),
    newPassword: newPasswordRule,
    confirmPassword: z.string(),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: MISMATCH_MESSAGE_KEY,
    path: ['confirmPassword'],
  });

export type ChangePasswordValues = z.infer<typeof changePasswordSchema>;
```

`frontend/src/features/auth/LoginPage.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useLogin, useMe } from './api';

const loginSchema = z.object({
  email: z.string().trim().min(1, ERROR_CODES.required),
  password: z.string().min(1, ERROR_CODES.required),
});

type LoginValues = z.infer<typeof loginSchema>;

const EMPTY_LOGIN: LoginValues = { email: '', password: '' };

function redirectTarget(state: unknown): string {
  if (typeof state === 'object' && state !== null && 'from' in state) {
    const from = (state as { from: unknown }).from;
    if (typeof from === 'string' && from.startsWith('/') && from !== ROUTES.login) {
      return from;
    }
  }
  return ROUTES.home;
}

export function LoginPage() {
  const { t } = useTranslation('auth');
  const me = useMe();
  const login = useLogin();
  const navigate = useNavigate();
  const location = useLocation();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema), defaultValues: EMPTY_LOGIN });

  if (me.data) {
    return <Navigate to={ROUTES.home} replace />;
  }

  const onSubmit = handleSubmit((values) =>
    login.mutate(values, { onSuccess: () => void navigate(redirectTarget(location.state), { replace: true }) }),
  );

  return (
    <main className="flex min-h-dvh items-center justify-center p-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold">{t('login.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t('login.subtitle')}</p>
        <form className="mt-6 flex flex-col gap-4" noValidate onSubmit={(event) => void onSubmit(event)}>
          <FormAlert message={login.error ? describeError(login.error) : null} />
          <TextField label={t('login.email')} type="email" autoComplete="username" error={fieldError(errors.email)} {...register('email')} />
          <TextField
            label={t('login.password')}
            type="password"
            autoComplete="current-password"
            hint={t('login.lockoutHint')}
            error={fieldError(errors.password)}
            {...register('password')}
          />
          <Button type="submit" isLoading={login.isPending}>
            {t('login.submit')}
          </Button>
        </form>
      </div>
    </main>
  );
}
```

`frontend/src/features/auth/ChangePasswordPage.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useChangePassword, useLogout } from './api';
import { changePasswordSchema, type ChangePasswordValues } from './passwordSchema';
import { useSession } from './session';

const EMPTY_VALUES: ChangePasswordValues = { currentPassword: '', newPassword: '', confirmPassword: '' };
const SERVER_FIELDS = ['currentPassword', 'newPassword'] as const;

export function ChangePasswordPage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const changePassword = useChangePassword();
  const logout = useLogout();
  const navigate = useNavigate();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ChangePasswordValues>({ resolver: zodResolver(changePasswordSchema), defaultValues: EMPTY_VALUES });

  const onSubmit = handleSubmit(({ currentPassword, newPassword }) =>
    changePassword.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => void navigate(ROUTES.home, { replace: true }),
        onError: (error) => applyServerErrors(error, setError, SERVER_FIELDS),
      },
    ),
  );

  const alert = changePassword.error && !isHandledByFields(changePassword.error, SERVER_FIELDS) ? describeError(changePassword.error) : null;

  return (
    <main className="flex min-h-dvh items-center justify-center p-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold">{t('changePassword.title')}</h1>
        {me.mustChangePassword && <p className="mt-2 rounded-lg bg-brand-50 p-3 text-sm text-brand-900">{t('changePassword.requiredNotice')}</p>}
        <form className="mt-6 flex flex-col gap-4" noValidate onSubmit={(event) => void onSubmit(event)}>
          <FormAlert message={alert} />
          <TextField label={t('changePassword.current')} type="password" autoComplete="current-password" error={fieldError(errors.currentPassword)} {...register('currentPassword')} />
          <TextField
            label={t('changePassword.new')}
            type="password"
            autoComplete="new-password"
            hint={t('changePassword.hint')}
            error={fieldError(errors.newPassword)}
            {...register('newPassword')}
          />
          <TextField label={t('changePassword.confirm')} type="password" autoComplete="new-password" error={fieldError(errors.confirmPassword)} {...register('confirmPassword')} />
          <Button type="submit" isLoading={changePassword.isPending}>
            {t('changePassword.submit')}
          </Button>
          <Button variant="ghost" isLoading={logout.isPending} onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}>
            {tc('actions.logout')}
          </Button>
        </form>
      </div>
    </main>
  );
}
```

- [ ] **Step 6: Implement shell, pages, query client, router and entry**

`frontend/src/shared/ui/AppShell.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Link, NavLink, Outlet, useNavigate } from 'react-router';

import { useLogout } from '../../features/auth/api';
import { useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';

import { Button } from './Button';

const NAV_LINK_CLASS = 'rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 aria-[current=page]:bg-brand-50 aria-[current=page]:text-brand-900';

export function AppShell() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const logout = useLogout();
  const navigate = useNavigate();

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white">
        <div className="mx-auto flex h-14 max-w-5xl items-center gap-2 px-4">
          <Link to={ROUTES.home} className="mr-auto text-base font-semibold text-brand-900">
            {tc('appName')}
          </Link>
          <nav aria-label={t('shell.mainNav')} className="flex items-center gap-1">
            <NavLink to={ROUTES.profile} className={NAV_LINK_CLASS}>
              {t('shell.profile')}
            </NavLink>
            {me.role === 'admin' && (
              <NavLink to={ROUTES.admin} className={NAV_LINK_CLASS}>
                {t('shell.admin')}
              </NavLink>
            )}
          </nav>
          <Button variant="ghost" isLoading={logout.isPending} onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}>
            {tc('actions.logout')}
          </Button>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl p-4 sm:p-6">
        <Outlet />
      </main>
    </div>
  );
}
```

`frontend/src/shared/ui/NotFoundPage.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-4 p-4 text-center">
      <h1 className="text-xl font-semibold">{t('notFound.title')}</h1>
      <Link to={ROUTES.home} className="text-brand-700 underline">
        {t('notFound.back')}
      </Link>
    </main>
  );
}
```

`frontend/src/features/home/HomePage.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { useSession } from '../auth/session';

const CARD_CLASS = 'block rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-300';

export function HomePage() {
  const { t } = useTranslation('auth');
  const me = useSession();

  return (
    <section className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('home.greeting', { name: me.firstName })}</h1>
        <p className="mt-1 text-slate-600">{t('home.intro')}</p>
      </div>
      <ul className="grid gap-3 sm:grid-cols-2">
        <li>
          <Link to={ROUTES.profile} className={CARD_CLASS}>
            <span className="block font-medium">{t('home.profileCard')}</span>
            <span className="mt-1 block text-sm text-slate-600">{t('home.profileCardDescription')}</span>
          </Link>
        </li>
        {me.role === 'admin' && (
          <li>
            <Link to={ROUTES.admin} className={CARD_CLASS}>
              <span className="block font-medium">{t('home.adminCard')}</span>
              <span className="mt-1 block text-sm text-slate-600">{t('home.adminCardDescription')}</span>
            </Link>
          </li>
        )}
      </ul>
    </section>
  );
}
```

`frontend/src/features/profile/ProfilePage.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { useSession } from '../auth/session';

export function ProfilePage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const rows = [
    { label: t('profile.name'), value: `${me.firstName} ${me.lastName}` },
    { label: t('profile.email'), value: me.email },
    { label: t('profile.role'), value: tc(`roles.${me.role}`) },
    { label: t('profile.department'), value: me.departmentName ?? t('profile.notSet') },
    { label: t('profile.location'), value: me.locationName ?? t('profile.notSet') },
  ];

  return (
    <section className="flex max-w-xl flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('profile.title')}</h1>
      <dl className="divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white">
        {rows.map((row) => (
          <div key={row.label} className="flex flex-col gap-1 p-4 sm:flex-row sm:justify-between">
            <dt className="text-sm text-slate-600">{row.label}</dt>
            <dd className="font-medium break-words">{row.value}</dd>
          </div>
        ))}
      </dl>
      <Link to={ROUTES.changePassword} className="self-start text-brand-700 underline">
        {t('profile.changePassword')}
      </Link>
    </section>
  );
}
```

`frontend/src/app/queryClient.ts`:

```ts
import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';

import { ApiError, ERROR_CODES } from '../shared/api/errors';
import { ME_QUERY_KEY } from '../shared/api/queryKeys';

const MAX_RETRIES = 2;
const HTTP_UNAUTHORIZED = 401;
const HTTP_CLIENT_ERROR_MIN = 400;
const HTTP_SERVER_ERROR_MIN = 500;

function shouldRetry(failureCount: number, error: unknown): boolean {
  const isClientError = error instanceof ApiError && error.status >= HTTP_CLIENT_ERROR_MIN && error.status < HTTP_SERVER_ERROR_MIN;
  return !isClientError && failureCount < MAX_RETRIES;
}

/** A 401 anywhere means the session ended (deactivated, role changed): drop "me" so the guard sends the user to login. */
function handleSessionError(queryClient: QueryClient, error: unknown): void {
  if (!(error instanceof ApiError)) {
    return;
  }
  if (error.status === HTTP_UNAUTHORIZED) {
    queryClient.setQueryData(ME_QUERY_KEY, null);
  } else if (error.code === ERROR_CODES.passwordChangeRequired) {
    void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
  }
}

export function createQueryClient(options: { retry?: boolean } = {}): QueryClient {
  const queryClient: QueryClient = new QueryClient({
    queryCache: new QueryCache({ onError: (error) => handleSessionError(queryClient, error) }),
    mutationCache: new MutationCache({ onError: (error) => handleSessionError(queryClient, error) }),
    defaultOptions: {
      queries: { retry: options.retry === false ? false : shouldRetry, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
  return queryClient;
}
```

`frontend/src/app/router.tsx` (Task 14 adds the admin children):

```tsx
import { Outlet, type RouteObject } from 'react-router';

import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { RequireSession } from '../features/auth/guards';
import { LoginPage } from '../features/auth/LoginPage';
import { HomePage } from '../features/home/HomePage';
import { ProfilePage } from '../features/profile/ProfilePage';
import { ROUTES } from '../shared/routes';
import { AppShell } from '../shared/ui/AppShell';
import { NotFoundPage } from '../shared/ui/NotFoundPage';

export const routes: RouteObject[] = [
  { path: ROUTES.login, element: <LoginPage /> },
  {
    element: (
      <RequireSession>
        <Outlet />
      </RequireSession>
    ),
    children: [
      { path: ROUTES.changePassword, element: <ChangePasswordPage /> },
      {
        element: <AppShell />,
        children: [
          { index: true, element: <HomePage /> },
          { path: ROUTES.profile, element: <ProfilePage /> },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
];
```

`frontend/src/app/App.tsx`:

```tsx
import { QueryClientProvider } from '@tanstack/react-query';
import { createBrowserRouter, RouterProvider } from 'react-router';

import { createQueryClient } from './queryClient';
import { routes } from './router';

const router = createBrowserRouter(routes);
const queryClient = createQueryClient();

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
```

`frontend/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App';
import './index.css';
import './shared/i18n';

const rootElement = document.getElementById('root');
if (rootElement === null) {
  throw new Error('Root element #root is missing.');
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

If the installed React Router major exports `RouterProvider` from `react-router/dom` instead of `react-router`, import it from there (check the React Router docs via context7).

- [ ] **Step 7: Run tests, lint, typecheck, build**

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run && npm run lint && npm run build; echo "exit=$?"`
Expected: all tests PASS, zero lint warnings, build succeeds, `exit=0`.

- [ ] **Step 8: Extend `tools/build.sh` to the frontend**

Replace `tools/build.sh`:

```bash
#!/usr/bin/env bash
# Build gate: lint and tests must pass before the Release build and the frontend bundle.
set -euo pipefail
cd "$(dirname "$0")/.."

tools/lint.sh
tools/test.sh
dotnet build backend/Tgysm.slnx -c Release --nologo
(cd frontend && npm run build)

echo "Frontend chunks:"
ls -lh frontend/dist/assets | awk 'NR > 1 { print $5, $9 }'
```

Run: `/Users/lektuel/claude/TGYSM/tools/build.sh; echo "exit=$?"`
Expected: `exit=0` and a chunk list with `vendor-react`, `vendor-router`, … and no Vite chunk-size warning.

- [ ] **Step 9: Manual check in the browser**

Run `tools/db-up.sh && tools/migrate.sh`, start the API (`cd backend && dotnet run --project src/Tgysm.Api --launch-profile http`) and Vite (`cd frontend && npm run dev`). In Chrome at `http://localhost:5173`, log in with the admin created in Task 6: the change-password page appears; change the password; the home page greets the admin. Stop both processes.

- [ ] **Step 10: Checkpoint (no commit)**

Run: `git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 14: Admin shell, shared list/dialog UI, users screens

**Files:**
- Create: `frontend/src/shared/ui/{SelectField,Dialog,Sheet,ConfirmDialog,StatusBadge,Pagination,ResponsiveList,TemporaryPasswordDialog}.tsx`
- Create: `frontend/src/shared/lib/useDebouncedValue.ts`
- Create: `frontend/src/features/admin/AdminLayout.tsx`
- Create: `frontend/src/features/admin/shared/{queryKeys,referenceOptions}.ts`
- Create: `frontend/src/features/admin/locations/{api,locationTree}.ts`
- Create: `frontend/src/features/admin/users/{api.ts,userSchema.ts,UsersPage.tsx,UserFormDialog.tsx}`
- Modify: `frontend/src/app/router.tsx`
- Test: `frontend/src/features/admin/locations/locationTree.test.ts`, `frontend/src/features/admin/users/UsersPage.test.tsx`

**Interfaces:**
- Consumes: Task 13 API layer, UI, guards.
- Produces:
  - UI: `SelectField({ label, options: SelectOption[], error?, placeholder?, ...select })`, `type SelectOption = { value: string; label: string }`, `Dialog({ open, onOpenChange, title, description?, footer?, children })`, `Sheet({ open, onOpenChange, title, children })`, `ConfirmDialog({ title, message, confirmLabel, variant?, isPending, error, onConfirm, onCancel })`, `StatusBadge({ isActive })`, `Pagination({ page, pageSize, totalCount, onPageChange })`, `ResponsiveList<T>({ items, columns: Column<T>[], getKey, renderCard, caption })`, `type Column<T> = { key; header; renderCell(item) }`, `TemporaryPasswordDialog({ password, onClose })`
  - `useDebouncedValue<T>(value, delayMs)`
  - `adminKeys` (`users`, `departments`, `locations`, `teams`, `audit`); `REFERENCE_PAGE_SIZE = 100`; `useDepartmentOptions()`, `useLocationOptions()`, `withCurrentOption(options, id, label)`
  - `type LocationItem`, `useLocations()`; `buildLocationTree(items)`, `flattenLocationTree(nodes)`, `collectSubtreeIds(node)`, `findLocationNode(nodes, id)`, `type LocationNode = { item; depth; path; children }`
  - Users: `type UserListItem`, `type UserRole`, `useUsers(params)`, `useCreateUser()`, `useUpdateUser()`, `useSetUserActive()`, `useResetUserPassword()`; `USER_ROLES`, `userFormSchema`, `toFormValues(user)`, `toRequestFields(values)`; `UsersPage`, `UserFormDialog({ user, onClose, onCreated })`
  - Admin routes: `/admin` → `AdminLayout` (admin only) with index redirect to `/admin/users`; `/admin/users` lazy

- [ ] **Step 1: Write the failing tests**

`frontend/src/features/admin/locations/locationTree.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import type { LocationItem } from './api';
import { buildLocationTree, collectSubtreeIds, findLocationNode, flattenLocationTree } from './locationTree';

const item = (id: string, name: string, parentId: string | null, isActive = true): LocationItem => ({ id, name, parentId, isActive, version: 1 });

describe('locationTree', () => {
  const items = [
    item('b', 'Ceyhan Kampüsü', null),
    item('a', 'Çankaya Kampüsü', null),
    item('a1', 'A Blok', 'a'),
    item('a1f', 'Kat 1', 'a1'),
    item('orphan', 'Kayıp', 'missing-parent'),
  ];

  it('nests children, sorts with Turkish collation and treats orphans as roots', () => {
    const roots = buildLocationTree(items);

    expect(roots.map((node) => node.item.name)).toEqual(['Ceyhan Kampüsü', 'Çankaya Kampüsü', 'Kayıp']);
    expect(roots[1]?.children[0]?.children[0]?.path).toBe('Çankaya Kampüsü / A Blok / Kat 1');
  });

  it('flattens depth-first with depth levels', () => {
    const flat = flattenLocationTree(buildLocationTree(items));

    expect(flat.map((node) => [node.item.id, node.depth])).toEqual([
      ['b', 0],
      ['a', 0],
      ['a1', 1],
      ['a1f', 2],
      ['orphan', 0],
    ]);
  });

  it('collects a subtree and survives cyclic data', () => {
    const roots = buildLocationTree(items);
    const campus = findLocationNode(roots, 'a');

    expect(campus && [...collectSubtreeIds(campus)].sort()).toEqual(['a', 'a1', 'a1f']);
    expect(buildLocationTree([item('x', 'X', 'y'), item('y', 'Y', 'x')])).toEqual([]);
  });
});
```

`frontend/src/features/admin/users/UsersPage.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, mockApi, type MockRoutes } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const USER = {
  id: 'u-1',
  firstName: 'İlker',
  lastName: 'Işık',
  email: 'ilker@example.com',
  role: 'employee',
  isActive: true,
  departmentId: null,
  departmentName: null,
  locationId: null,
  locationName: null,
  version: 7,
};

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

function setup(extra: MockRoutes = {}) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/admin/users': { status: 200, body: pageOf([USER]) },
    'GET /api/v1/admin/departments': { status: 200, body: pageOf([]) },
    'GET /api/v1/admin/locations': { status: 200, body: [] },
    ...extra,
  });
}

describe('UsersPage', () => {
  it('lists users', async () => {
    setup();
    renderRoute('/admin/users');

    expect((await screen.findAllByText('İlker Işık')).length).toBeGreaterThan(0);
  });

  it('sends the search term to the server', async () => {
    const mock = setup();
    const { user } = renderRoute('/admin/users');

    await user.type(await screen.findByLabelText('Kullanıcı ara'), 'ilker');

    await waitFor(() => expect(mock.calls.some((r) => new URL(r.url).searchParams.get('search') === 'ilker')).toBe(true));
  });

  it('creates a user and shows the temporary password once', async () => {
    let posted: unknown;
    setup({
      'POST /api/v1/admin/users': async (request) => {
        posted = await request.json();
        return { status: 201, body: { userId: 'u-2', temporaryPassword: 'Temp-pass-1234' } };
      },
    });
    const { user } = renderRoute('/admin/users');

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Ad'), 'Deniz');
    await user.type(within(dialog).getByLabelText('Soyad'), 'Aydın');
    await user.type(within(dialog).getByLabelText('E-posta'), 'deniz@example.com');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await screen.findByTestId('temporary-password')).toHaveTextContent('Temp-pass-1234');
    expect(posted).toEqual({ firstName: 'Deniz', lastName: 'Aydın', email: 'deniz@example.com', role: 'employee', departmentId: null, locationId: null });
  });

  it('shows a taken email under the email field', async () => {
    setup({ 'POST /api/v1/admin/users': { status: 409, body: { code: 'users.email_taken' } } });
    const { user } = renderRoute('/admin/users');

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Ad'), 'Ali');
    await user.type(within(dialog).getByLabelText('Soyad'), 'Veli');
    await user.type(within(dialog).getByLabelText('E-posta'), 'ALI@example.com');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Bu e-posta adresi başka bir hesapta kullanılıyor.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('E-posta')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows conflict message when the user was changed elsewhere', async () => {
    setup({ 'PUT /api/v1/admin/users/u-1': { status: 409, body: { code: 'concurrency.conflict' } } });
    const { user } = renderRoute('/admin/users');

    await user.click((await screen.findAllByRole('button', { name: 'Düzenle' }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('başka biri tarafından değiştirildi');
  });

  it('deactivates after confirmation and sends the row version', async () => {
    let posted: unknown;
    setup({
      'POST /api/v1/admin/users/u-1/deactivate': async (request) => {
        posted = await request.json();
        return { status: 200, body: { ...USER, isActive: false, mustChangePassword: false, createdAt: '2026-09-26T09:00:00Z', version: 8 } };
      },
    });
    const { user } = renderRoute('/admin/users');

    await user.click((await screen.findAllByRole('button', { name: 'Pasifleştir' }))[0]!);
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('İlker Işık pasifleştirilsin mi?');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 7 }));
  });

  it('keeps employees out of the admin area', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { router } = renderRoute('/admin/users');

    expect(await screen.findByRole('heading', { name: 'Merhaba, Ayşe' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });
});
```

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run src/features/admin`
Expected: FAIL — modules not found.

- [ ] **Step 2: Implement shared UI**

`frontend/src/shared/ui/SelectField.tsx`:

```tsx
import { useId, type Ref, type SelectHTMLAttributes } from 'react';

export interface SelectOption {
  value: string;
  label: string;
}

export interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  options: readonly SelectOption[];
  error?: string;
  /** Adds an empty first option with this label (e.g. "none"). */
  placeholder?: string;
  ref?: Ref<HTMLSelectElement>;
}

export function SelectField({ label, options, error, placeholder, id, className = '', ref, ...selectProps }: SelectFieldProps) {
  const generatedId = useId();
  const selectId = id ?? generatedId;
  const errorId = error ? `${selectId}-error` : undefined;

  return (
    <div className={`flex flex-col gap-1 ${className}`}>
      <label htmlFor={selectId} className="text-sm font-medium text-slate-800">
        {label}
      </label>
      <select
        id={selectId}
        ref={ref}
        aria-invalid={error ? true : undefined}
        aria-describedby={errorId}
        className="min-h-11 rounded-lg border border-slate-300 bg-white px-3 text-base aria-invalid:border-red-600"
        {...selectProps}
      >
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && (
        <p id={errorId} className="text-sm text-red-700">
          {error}
        </p>
      )}
    </div>
  );
}
```

`frontend/src/shared/ui/Dialog.tsx`:

```tsx
import { Dialog as RadixDialog } from 'radix-ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

export interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  footer?: ReactNode;
  children?: ReactNode;
}

/** Bottom sheet on phones, centered modal from 640 px. Focus is trapped and returned by Radix. */
export function Dialog({ open, onOpenChange, title, description, footer, children }: DialogProps) {
  const { t } = useTranslation();
  return (
    <RadixDialog.Root open={open} onOpenChange={onOpenChange}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-40 bg-slate-900/40" />
        <RadixDialog.Content
          {...(description === undefined ? { 'aria-describedby': undefined } : {})}
          className="fixed inset-x-0 bottom-0 z-50 flex max-h-[90dvh] flex-col rounded-t-2xl bg-white shadow-xl sm:inset-x-auto sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:w-full sm:max-w-lg sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-2xl"
        >
          <div className="flex items-start justify-between gap-4 border-b border-slate-100 p-4">
            <div>
              <RadixDialog.Title className="text-lg font-semibold">{title}</RadixDialog.Title>
              {description !== undefined && <RadixDialog.Description className="mt-1 text-sm text-slate-600">{description}</RadixDialog.Description>}
            </div>
            <RadixDialog.Close className="min-h-11 min-w-11 rounded-lg text-slate-500 hover:bg-slate-100" aria-label={t('actions.close')}>
              <span aria-hidden="true">×</span>
            </RadixDialog.Close>
          </div>
          <div className="overflow-y-auto p-4">{children}</div>
          {footer !== undefined && <div className="flex flex-col-reverse gap-2 border-t border-slate-100 p-4 sm:flex-row sm:justify-end">{footer}</div>}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}
```

`frontend/src/shared/ui/Sheet.tsx`:

```tsx
import { Dialog as RadixDialog } from 'radix-ui';
import type { ReactNode } from 'react';

export interface SheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  children: ReactNode;
}

/** Left drawer for navigation on small screens. */
export function Sheet({ open, onOpenChange, title, children }: SheetProps) {
  return (
    <RadixDialog.Root open={open} onOpenChange={onOpenChange}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 z-40 bg-slate-900/40" />
        <RadixDialog.Content aria-describedby={undefined} className="fixed inset-y-0 left-0 z-50 w-72 max-w-[85vw] overflow-y-auto bg-white p-4 shadow-xl">
          <RadixDialog.Title className="mb-4 text-base font-semibold">{title}</RadixDialog.Title>
          {children}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}
```

`frontend/src/shared/ui/ConfirmDialog.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import { describeError } from '../api/errorMessage';

import { Button } from './Button';
import { Dialog } from './Dialog';
import { FormAlert } from './FormAlert';

export interface ConfirmDialogProps {
  title: string;
  message: string;
  confirmLabel: string;
  variant?: 'primary' | 'danger';
  isPending: boolean;
  error: unknown;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({ title, message, confirmLabel, variant = 'primary', isPending, error, onConfirm, onCancel }: ConfirmDialogProps) {
  const { t } = useTranslation();
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onCancel();
        }
      }}
      title={title}
      description={message}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            {t('actions.cancel')}
          </Button>
          <Button variant={variant} isLoading={isPending} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <FormAlert message={error ? describeError(error) : null} />
    </Dialog>
  );
}
```

`frontend/src/shared/ui/StatusBadge.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

export function StatusBadge({ isActive }: { isActive: boolean }) {
  const { t } = useTranslation();
  return (
    <span
      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${isActive ? 'bg-emerald-50 text-emerald-800' : 'bg-slate-100 text-slate-700'}`}
    >
      {isActive ? t('status.active') : t('status.inactive')}
    </span>
  );
}
```

`frontend/src/shared/ui/Pagination.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import { Button } from './Button';

const FIRST_PAGE = 1;

export interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const { t } = useTranslation();
  if (totalCount === 0) {
    return null;
  }
  const from = (page - FIRST_PAGE) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);
  const lastPage = Math.ceil(totalCount / pageSize);

  return (
    <nav aria-label={t('pagination.label')} className="flex flex-wrap items-center justify-between gap-2 text-sm">
      <p className="text-slate-600">{t('pagination.summary', { from, to, total: totalCount })}</p>
      <div className="flex gap-2">
        <Button variant="secondary" disabled={page <= FIRST_PAGE} onClick={() => onPageChange(page - 1)}>
          {t('pagination.previous')}
        </Button>
        <Button variant="secondary" disabled={page >= lastPage} onClick={() => onPageChange(page + 1)}>
          {t('pagination.next')}
        </Button>
      </div>
    </nav>
  );
}
```

`frontend/src/shared/ui/ResponsiveList.tsx`:

```tsx
import type { ReactNode } from 'react';

export interface Column<T> {
  key: string;
  header: string;
  renderCell: (item: T) => ReactNode;
}

export interface ResponsiveListProps<T> {
  items: readonly T[];
  columns: readonly Column<T>[];
  getKey: (item: T) => string;
  renderCard: (item: T) => ReactNode;
  caption: string;
}

/** Cards below 768 px, a table above; the hidden variant is display:none, so screen readers see one list. */
export function ResponsiveList<T>({ items, columns, getKey, renderCard, caption }: ResponsiveListProps<T>) {
  return (
    <>
      <ul className="flex flex-col gap-3 md:hidden">
        {items.map((item) => (
          <li key={getKey(item)} className="rounded-xl border border-slate-200 bg-white p-4">
            {renderCard(item)}
          </li>
        ))}
      </ul>
      <div className="hidden overflow-x-auto rounded-xl border border-slate-200 bg-white md:block">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead className="bg-slate-50 text-slate-600">
            <tr>
              {columns.map((column) => (
                <th key={column.key} scope="col" className="px-4 py-3 font-medium">
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={getKey(item)} className="border-t border-slate-100 align-top">
                {columns.map((column) => (
                  <td key={column.key} className="px-4 py-3">
                    {column.renderCell(item)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
```

`frontend/src/shared/ui/TemporaryPasswordDialog.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from './Button';
import { Dialog } from './Dialog';

export function TemporaryPasswordDialog({ password, onClose }: { password: string; onClose: () => void }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const copy = () =>
    navigator.clipboard.writeText(password).then(
      () => setCopied(true),
      () => setCopied(false),
    );

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('temporaryPassword.title')}
      description={t('temporaryPassword.description')}
      footer={<Button onClick={onClose}>{t('actions.close')}</Button>}
    >
      <p className="text-sm text-slate-600">{t('temporaryPassword.label')}</p>
      <p data-testid="temporary-password" className="mt-1 rounded-lg bg-slate-100 p-3 font-mono text-lg break-all select-all">
        {password}
      </p>
      <Button variant="secondary" className="mt-3" onClick={() => void copy()}>
        {copied ? t('actions.copied') : t('actions.copy')}
      </Button>
    </Dialog>
  );
}
```

`frontend/src/shared/lib/useDebouncedValue.ts`:

```ts
import { useEffect, useState } from 'react';

/** Returns value after it stopped changing for delayMs (used for search boxes). */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
```

- [ ] **Step 3: Implement admin shared modules and locations data**

`frontend/src/features/admin/shared/queryKeys.ts`:

```ts
export const adminKeys = {
  users: ['admin', 'users'],
  departments: ['admin', 'departments'],
  locations: ['admin', 'locations'],
  teams: ['admin', 'teams'],
  audit: ['admin', 'audit'],
} as const;
```

`frontend/src/features/admin/locations/api.ts` (Task 15 adds mutations here):

```ts
import { useQuery } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type LocationItem = components['schemas']['LocationItem'];

export function useLocations() {
  return useQuery({ queryKey: adminKeys.locations, queryFn: () => unwrap(api.GET('/api/v1/admin/locations')) });
}
```

`frontend/src/features/admin/locations/locationTree.ts`:

```ts
import { TURKISH_LOCALE } from '../../../shared/lib/turkish';

import type { LocationItem } from './api';

export interface LocationNode {
  item: LocationItem;
  depth: number;
  /** "Kampüs / Bina / Kat" — used as option labels. */
  path: string;
  children: LocationNode[];
}

const PATH_SEPARATOR = ' / ';
const ROOT_KEY = null;
const collator = new Intl.Collator(TURKISH_LOCALE);

/** Builds the tree; items whose parent is missing become roots; cyclic data is dropped instead of looping. */
export function buildLocationTree(items: readonly LocationItem[]): LocationNode[] {
  const ids = new Set(items.map((item) => item.id));
  const childrenOf = new Map<string | null, LocationItem[]>();
  for (const item of items) {
    const parentKey = item.parentId !== null && item.parentId !== undefined && ids.has(item.parentId) ? item.parentId : ROOT_KEY;
    childrenOf.set(parentKey, [...(childrenOf.get(parentKey) ?? []), item]);
  }

  const build = (parentKey: string | null, depth: number, parentPath: string, ancestors: ReadonlySet<string>): LocationNode[] =>
    [...(childrenOf.get(parentKey) ?? [])]
      .filter((item) => !ancestors.has(item.id))
      .sort((left, right) => collator.compare(left.name, right.name))
      .map((item) => {
        const path = parentPath === '' ? item.name : `${parentPath}${PATH_SEPARATOR}${item.name}`;
        return { item, depth, path, children: build(item.id, depth + 1, path, new Set([...ancestors, item.id])) };
      });

  return build(ROOT_KEY, 0, '', new Set());
}

export function flattenLocationTree(nodes: readonly LocationNode[]): LocationNode[] {
  return nodes.flatMap((node) => [node, ...flattenLocationTree(node.children)]);
}

export function findLocationNode(nodes: readonly LocationNode[], id: string): LocationNode | undefined {
  return flattenLocationTree(nodes).find((node) => node.item.id === id);
}

/** The node and all its descendants: invalid parents when moving this node. */
export function collectSubtreeIds(node: LocationNode): Set<string> {
  return new Set(flattenLocationTree([node]).map((entry) => entry.item.id));
}
```

`frontend/src/features/admin/shared/referenceOptions.ts` (Task 15 adds approver and team-candidate options):

```ts
import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { api, unwrap } from '../../../shared/api/client';
import type { SelectOption } from '../../../shared/ui/SelectField';
import { useLocations } from '../locations/api';
import { buildLocationTree, flattenLocationTree } from '../locations/locationTree';

import { adminKeys } from './queryKeys';

/** Reference lists are small; one page of the API maximum (PageRequest.MaxPageSize) covers them. */
export const REFERENCE_PAGE_SIZE = 100;

export function useDepartmentOptions(): SelectOption[] {
  const departments = useQuery({
    queryKey: [...adminKeys.departments, 'options'],
    queryFn: () => unwrap(api.GET('/api/v1/admin/departments', { params: { query: { isActive: true, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
  return useMemo(() => (departments.data?.items ?? []).map((d) => ({ value: d.id, label: d.name })), [departments.data]);
}

export function useLocationOptions(): SelectOption[] {
  const locations = useLocations();
  return useMemo(
    () =>
      flattenLocationTree(buildLocationTree(locations.data ?? []))
        .filter((node) => node.item.isActive)
        .map((node) => ({ value: node.item.id, label: node.path })),
    [locations.data],
  );
}

/** Keeps the record's current (maybe now inactive) reference selectable so editing does not silently clear it. */
export function withCurrentOption(options: SelectOption[], id: string | null | undefined, label: string | null | undefined): SelectOption[] {
  if (!id || options.some((option) => option.value === id)) {
    return options;
  }
  return [...options, { value: id, label: label ?? id }];
}
```

- [ ] **Step 4: Implement the users feature**

`frontend/src/features/admin/users/api.ts`:

```ts
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type UserListItem = components['schemas']['UserListItem'];
export type UserRole = UserListItem['role'];
export type CreateUserInput = components['schemas']['CreateUserCommand'];
export type UpdateUserInput = components['schemas']['UpdateUserRequest'] & { id: string };

export interface UserListParams {
  search: string;
  isActive: boolean | undefined;
  role: UserRole | undefined;
  page: number;
  pageSize: number;
}

export function useUsers(params: UserListParams) {
  return useQuery({
    queryKey: [...adminKeys.users, 'list', params],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/admin/users', {
          params: {
            query: {
              search: params.search === '' ? undefined : params.search,
              isActive: params.isActive,
              role: params.role,
              page: params.page,
              pageSize: params.pageSize,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

function useInvalidateUsers() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: adminKeys.users });
}

export function useCreateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: (body: CreateUserInput) => unwrap(api.POST('/api/v1/admin/users', { body })),
    onSuccess: invalidate,
  });
}

export function useUpdateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateUserInput) => unwrap(api.PUT('/api/v1/admin/users/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetUserActive() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/users/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/users/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useResetUserPassword() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.POST('/api/v1/admin/users/{id}/reset-password', { params: { path: { id } } })),
    onSuccess: invalidate,
  });
}
```

`frontend/src/features/admin/users/userSchema.ts`:

```ts
import { z } from 'zod';

import { ERROR_CODES } from '../../../shared/api/errors';

import type { UserListItem, UserRole } from './api';

/** Mirror backend User.NameMaxLength / EmailMaxLength. */
export const USER_NAME_MAX_LENGTH = 100;
export const USER_EMAIL_MAX_LENGTH = 254;
export const USER_ROLES = ['employee', 'it_staff', 'admin'] as const satisfies readonly UserRole[];
const DEFAULT_ROLE: UserRole = 'employee';
const NO_SELECTION = '';

const name = z.string().trim().min(1, ERROR_CODES.required).max(USER_NAME_MAX_LENGTH, ERROR_CODES.tooLong);

export const userFormSchema = z.object({
  firstName: name,
  lastName: name,
  email: z.string().trim().min(1, ERROR_CODES.required).max(USER_EMAIL_MAX_LENGTH, ERROR_CODES.tooLong).pipe(z.email(ERROR_CODES.invalidEmail)),
  role: z.enum(USER_ROLES),
  departmentId: z.string(),
  locationId: z.string(),
});

export type UserFormValues = z.infer<typeof userFormSchema>;

export function toFormValues(user: UserListItem | null): UserFormValues {
  if (user === null) {
    return { firstName: '', lastName: '', email: '', role: DEFAULT_ROLE, departmentId: NO_SELECTION, locationId: NO_SELECTION };
  }
  return {
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email,
    role: user.role,
    departmentId: user.departmentId ?? NO_SELECTION,
    locationId: user.locationId ?? NO_SELECTION,
  };
}

export function toRequestFields(values: UserFormValues) {
  return {
    firstName: values.firstName,
    lastName: values.lastName,
    email: values.email,
    role: values.role,
    departmentId: values.departmentId === NO_SELECTION ? null : values.departmentId,
    locationId: values.locationId === NO_SELECTION ? null : values.locationId,
  };
}
```

`frontend/src/features/admin/users/UserFormDialog.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { SelectField } from '../../../shared/ui/SelectField';
import { TextField } from '../../../shared/ui/TextField';
import { useDepartmentOptions, useLocationOptions, withCurrentOption } from '../shared/referenceOptions';

import { useCreateUser, useUpdateUser, type UserListItem } from './api';
import { toFormValues, toRequestFields, USER_ROLES, userFormSchema, type UserFormValues } from './userSchema';

const FORM_FIELDS = ['firstName', 'lastName', 'email', 'role', 'departmentId', 'locationId'] as const;
const CODE_FIELDS = { [ERROR_CODES.emailTaken]: 'email' } as const;

export interface UserFormDialogProps {
  user: UserListItem | null;
  onClose: () => void;
  onCreated: (temporaryPassword: string) => void;
}

export function UserFormDialog({ user, onClose, onCreated }: UserFormDialogProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const departmentOptions = withCurrentOption(useDepartmentOptions(), user?.departmentId, user?.departmentName);
  const locationOptions = withCurrentOption(useLocationOptions(), user?.locationId, user?.locationName);
  const create = useCreateUser();
  const update = useUpdateUser();
  const mutation = user === null ? create : update;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<UserFormValues>({ resolver: zodResolver(userFormSchema), defaultValues: toFormValues(user) });

  const onSubmit = handleSubmit((values) => {
    const fields = toRequestFields(values);
    const onError = (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS);
    if (user === null) {
      create.mutate(fields, { onSuccess: (result) => onCreated(result.temporaryPassword), onError });
    } else {
      update.mutate({ id: user.id, version: user.version, ...fields }, { onSuccess: onClose, onError });
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;
  const roleOptions = USER_ROLES.map((role) => ({ value: role, label: tc(`roles.${role}`) }));

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={user === null ? t('users.new') : t('users.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {user === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label={t('users.fields.firstName')} autoComplete="off" error={fieldError(errors.firstName)} {...register('firstName')} />
          <TextField label={t('users.fields.lastName')} autoComplete="off" error={fieldError(errors.lastName)} {...register('lastName')} />
        </div>
        <TextField label={t('users.fields.email')} type="email" autoComplete="off" error={fieldError(errors.email)} {...register('email')} />
        <SelectField label={t('users.fields.role')} options={roleOptions} error={fieldError(errors.role)} {...register('role')} />
        <SelectField
          label={t('users.fields.department')}
          options={departmentOptions}
          placeholder={tc('none')}
          error={fieldError(errors.departmentId)}
          {...register('departmentId')}
        />
        <SelectField label={t('users.fields.location')} options={locationOptions} placeholder={tc('none')} error={fieldError(errors.locationId)} {...register('locationId')} />
      </form>
    </Dialog>
  );
}
```

`frontend/src/features/admin/users/UsersPage.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TemporaryPasswordDialog } from '../../../shared/ui/TemporaryPasswordDialog';
import { TextField } from '../../../shared/ui/TextField';

import { useResetUserPassword, useSetUserActive, useUsers, type UserListItem, type UserRole } from './api';
import { UserFormDialog } from './UserFormDialog';
import { USER_ROLES } from './userSchema';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;
const ALL_ROLES = '';

type StatusFilter = 'all' | 'active' | 'inactive';
const STATUS_FILTERS: readonly StatusFilter[] = ['all', 'active', 'inactive'];
const STATUS_TO_IS_ACTIVE: Record<StatusFilter, boolean | undefined> = { all: undefined, active: true, inactive: false };

type PendingAction = { kind: 'deactivate' | 'reactivate' | 'reset'; user: UserListItem };
type FormState = { mode: 'create' } | { mode: 'edit'; user: UserListItem } | null;

const fullName = (user: UserListItem) => `${user.firstName} ${user.lastName}`;

interface UserActionsProps {
  user: UserListItem;
  onEdit: (user: UserListItem) => void;
  onAction: (action: PendingAction) => void;
}

function UserActions({ user, onEdit, onAction }: UserActionsProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" onClick={() => onEdit(user)}>
        {tc('actions.edit')}
      </Button>
      <Button variant="secondary" onClick={() => onAction({ kind: user.isActive ? 'deactivate' : 'reactivate', user })}>
        {user.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
      <Button variant="ghost" onClick={() => onAction({ kind: 'reset', user })}>
        {t('users.resetPassword')}
      </Button>
    </div>
  );
}

export function UsersPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<StatusFilter>('all');
  const [role, setRole] = useState<UserRole | typeof ALL_ROLES>(ALL_ROLES);
  const [page, setPage] = useState(FIRST_PAGE);
  const [form, setForm] = useState<FormState>(null);
  const [pending, setPending] = useState<PendingAction | null>(null);
  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const users = useUsers({
    search: debouncedSearch,
    isActive: STATUS_TO_IS_ACTIVE[status],
    role: role === ALL_ROLES ? undefined : role,
    page,
    pageSize: PAGE_SIZE,
  });
  const setActive = useSetUserActive();
  const resetPassword = useResetUserPassword();

  const editUser = (user: UserListItem) => setForm({ mode: 'edit', user });
  const openAction = (action: PendingAction) => {
    setActive.reset();
    resetPassword.reset();
    setPending(action);
  };
  const confirmAction = () => {
    if (pending === null) {
      return;
    }
    if (pending.kind === 'reset') {
      resetPassword.mutate(pending.user.id, {
        onSuccess: (result) => {
          setPending(null);
          setTemporaryPassword(result.temporaryPassword);
        },
      });
      return;
    }
    setActive.mutate(
      { id: pending.user.id, version: pending.user.version, isActive: pending.kind === 'reactivate' },
      { onSuccess: () => setPending(null) },
    );
  };

  const confirmText = pending === null ? null : {
    deactivate: { message: t('users.confirmDeactivate', { name: fullName(pending.user) }), label: tc('actions.deactivate') },
    reactivate: { message: t('users.confirmReactivate', { name: fullName(pending.user) }), label: tc('actions.reactivate') },
    reset: { message: t('users.confirmReset', { name: fullName(pending.user) }), label: t('users.resetPassword') },
  }[pending.kind];

  const columns: Column<UserListItem>[] = [
    { key: 'name', header: t('users.columns.name'), renderCell: (user) => <span className="font-medium">{fullName(user)}</span> },
    { key: 'email', header: t('users.columns.email'), renderCell: (user) => <span className="break-all">{user.email}</span> },
    { key: 'role', header: t('users.columns.role'), renderCell: (user) => tc(`roles.${user.role}`) },
    { key: 'department', header: t('users.columns.department'), renderCell: (user) => user.departmentName ?? tc('none') },
    { key: 'status', header: t('users.columns.status'), renderCell: (user) => <StatusBadge isActive={user.isActive} /> },
    { key: 'actions', header: t('users.columns.actions'), renderCell: (user) => <UserActions user={user} onEdit={editUser} onAction={openAction} /> },
  ];

  const renderCard = (user: UserListItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium">{fullName(user)}</p>
          <p className="text-sm break-all text-slate-600">{user.email}</p>
        </div>
        <StatusBadge isActive={user.isActive} />
      </div>
      <p className="text-sm text-slate-700">
        {tc(`roles.${user.role}`)} · {user.departmentName ?? tc('none')}
      </p>
      <UserActions user={user} onEdit={editUser} onAction={openAction} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('users.title')}</h1>
        <Button onClick={() => setForm({ mode: 'create' })}>{t('users.new')}</Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <TextField
          type="search"
          label={t('users.searchLabel')}
          placeholder={t('users.searchPlaceholder')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(FIRST_PAGE);
          }}
        />
        <SelectField
          label={t('users.statusFilter')}
          value={status}
          options={STATUS_FILTERS.map((value) => ({ value, label: tc(`status.${value}`) }))}
          onChange={(event) => {
            setStatus(event.target.value as StatusFilter);
            setPage(FIRST_PAGE);
          }}
        />
        <SelectField
          label={t('users.roleFilter')}
          value={role}
          options={[{ value: ALL_ROLES, label: tc('roles.all') }, ...USER_ROLES.map((value) => ({ value, label: tc(`roles.${value}`) }))]}
          onChange={(event) => {
            setRole(event.target.value as UserRole | typeof ALL_ROLES);
            setPage(FIRST_PAGE);
          }}
        />
      </div>

      {users.isPending && <LoadingState />}
      {users.isError && <ErrorState error={users.error} onRetry={() => void users.refetch()} />}
      {users.data && users.data.items.length === 0 && <EmptyState />}
      {users.data && users.data.items.length > 0 && (
        <>
          <ResponsiveList items={users.data.items} columns={columns} getKey={(user) => user.id} renderCard={renderCard} caption={t('users.listCaption')} />
          <Pagination page={users.data.page} pageSize={users.data.pageSize} totalCount={users.data.totalCount} onPageChange={setPage} />
        </>
      )}

      {form !== null && (
        <UserFormDialog
          key={form.mode === 'edit' ? form.user.id : 'new'}
          user={form.mode === 'edit' ? form.user : null}
          onClose={() => setForm(null)}
          onCreated={(password) => {
            setForm(null);
            setTemporaryPassword(password);
          }}
        />
      )}
      {pending !== null && confirmText !== null && (
        <ConfirmDialog
          title={confirmText.label}
          message={confirmText.message}
          confirmLabel={tc('actions.confirm')}
          variant={pending.kind === 'deactivate' ? 'danger' : 'primary'}
          isPending={setActive.isPending || resetPassword.isPending}
          error={setActive.error ?? resetPassword.error}
          onConfirm={confirmAction}
          onCancel={() => setPending(null)}
        />
      )}
      {temporaryPassword !== null && <TemporaryPasswordDialog password={temporaryPassword} onClose={() => setTemporaryPassword(null)} />}
    </section>
  );
}
```

- [ ] **Step 5: Implement the admin layout and routes**

`frontend/src/features/admin/AdminLayout.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, Outlet } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Sheet } from '../../shared/ui/Sheet';

const NAV_ITEMS = [
  { to: ROUTES.adminUsers, labelKey: 'nav.users' },
  { to: ROUTES.adminDepartments, labelKey: 'nav.departments' },
  { to: ROUTES.adminLocations, labelKey: 'nav.locations' },
  { to: ROUTES.adminTeams, labelKey: 'nav.teams' },
  { to: ROUTES.adminAudit, labelKey: 'nav.audit' },
] as const;

const LINK_CLASS = 'block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-100 aria-[current=page]:bg-brand-50 aria-[current=page]:text-brand-900';

function AdminNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation('admin');
  return (
    <nav aria-label={t('nav.label')}>
      <ul className="flex flex-col gap-1">
        {NAV_ITEMS.map((item) => (
          <li key={item.to}>
            <NavLink to={item.to} className={LINK_CLASS} onClick={onNavigate}>
              {t(item.labelKey)}
            </NavLink>
          </li>
        ))}
        <li className="mt-4 border-t border-slate-100 pt-4">
          <NavLink to={ROUTES.home} end className={LINK_CLASS} onClick={onNavigate}>
            {t('nav.backToApp')}
          </NavLink>
        </li>
      </ul>
    </nav>
  );
}

/** Drawer menu under 1024 px, fixed sidebar above (ADR-0003). */
export function AdminLayout() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[16rem_1fr]">
      <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-slate-200 bg-white px-2 lg:hidden">
        <Button variant="ghost" aria-label={tc('actions.openMenu')} onClick={() => setMenuOpen(true)}>
          <span aria-hidden="true">☰</span>
        </Button>
        <span className="font-semibold">{t('title')}</span>
      </header>
      <Sheet open={menuOpen} onOpenChange={setMenuOpen} title={t('nav.label')}>
        <AdminNav onNavigate={() => setMenuOpen(false)} />
      </Sheet>
      <aside className="hidden border-r border-slate-200 bg-white p-4 lg:block">
        <p className="mb-4 px-3 text-base font-semibold">{t('title')}</p>
        <AdminNav />
      </aside>
      <main className="mx-auto w-full max-w-6xl min-w-0 p-4 sm:p-6">
        <Outlet />
      </main>
    </div>
  );
}
```

In `frontend/src/app/router.tsx`, add imports:

```tsx
import { Navigate } from 'react-router';

import { AdminLayout } from '../features/admin/AdminLayout';
import { RequireRole } from '../features/auth/guards';
import { ADMIN_ONLY } from '../features/auth/session';
```

(merge `Navigate` into the existing `react-router` import and `RequireRole` into the existing `guards` import), and add this child after the `AppShell` child inside the `RequireSession` route:

```tsx
      {
        path: ROUTES.admin,
        element: (
          <RequireRole roles={ADMIN_ONLY}>
            <AdminLayout />
          </RequireRole>
        ),
        children: [
          { index: true, element: <Navigate to={ROUTES.adminUsers} replace /> },
          { path: ROUTES.adminUsers, lazy: async () => ({ Component: (await import('../features/admin/users/UsersPage')).UsersPage }) },
        ],
      },
```

- [ ] **Step 6: Run tests, lint, build**

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run && npm run lint && npm run build; echo "exit=$?"`
Expected: all PASS, `exit=0`. If `react/no-unstable-nested-components` flags the `renderCell` / `renderCard` functions, move them to module-level functions that receive `t`/`tc` as arguments — do not add a disable comment.

- [ ] **Step 7: Manual check at 360 px**

With API + Vite running (Task 13 Step 9), open Chrome DevTools device mode at 360×800: `/admin/users` shows cards, the ☰ button opens the drawer, the create dialog opens as a bottom sheet, the page has no horizontal scroll. At 1280 px the table and sidebar show.

- [ ] **Step 8: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/lint.sh && /Users/lektuel/claude/TGYSM/tools/test.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 15: Departments, locations, teams and audit screens

**Files:**
- Create: `frontend/src/shared/i18n/dynamic.ts`
- Modify: `frontend/src/features/admin/shared/referenceOptions.ts`, `frontend/src/features/admin/locations/api.ts`, `frontend/src/app/router.tsx`
- Create: `frontend/src/features/admin/departments/{api.ts,DepartmentsPage.tsx,DepartmentFormDialog.tsx}`
- Create: `frontend/src/features/admin/locations/{LocationsPage.tsx,LocationFormDialog.tsx}`
- Create: `frontend/src/features/admin/teams/{api.ts,TeamsPage.tsx,TeamFormDialog.tsx,TeamMembersDialog.tsx}`
- Create: `frontend/src/features/admin/audit/{api.ts,AuditPage.tsx}`
- Test: `frontend/src/features/admin/departments/DepartmentsPage.test.tsx`, `frontend/src/features/admin/locations/LocationsPage.test.tsx`, `frontend/src/features/admin/teams/TeamsPage.test.tsx`, `frontend/src/features/admin/audit/AuditPage.test.tsx`

**Interfaces:**
- Consumes: Task 14 UI, `adminKeys`, `REFERENCE_PAGE_SIZE`, `withCurrentOption`, location tree helpers, users API types.
- Produces:
  - `translateOr(key, fallback)` for runtime-built i18n keys
  - `useAdminOptions()`, `useTeamCandidateOptions()`
  - Departments: `useDepartments(params)`, `useCreateDepartment()`, `useUpdateDepartment()`, `useSetDepartmentActive()`, `DepartmentsPage`
  - Locations: `useCreateLocation()`, `useUpdateLocation()`, `useSetLocationActive()`, `LocationsPage`
  - Teams: `useTeams(params)`, `useTeam(id)`, `useCreateTeam()`, `useUpdateTeam()`, `useSetTeamActive()`, `useAddTeamMember()`, `useRemoveTeamMember()`, `TeamsPage`
  - Audit: `useAuditEvents(params)`, `AuditPage`
  - Routes `/admin/departments`, `/admin/locations`, `/admin/teams`, `/admin/audit` (lazy)

- [ ] **Step 1: Write the failing tests**

`frontend/src/features/admin/departments/DepartmentsPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });
const APPROVER = { id: 'a-1', firstName: 'Onay', lastName: 'Veren', email: 'onay@example.com', role: 'admin', isActive: true, departmentId: null, departmentName: null, locationId: null, locationName: null, version: 1 };

describe('DepartmentsPage', () => {
  it('creates a department with an admin approver', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/departments': { status: 200, body: pageOf([]) },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([APPROVER]) },
      'POST /api/v1/admin/departments': async (request) => {
        posted = await request.json();
        return { status: 201, body: { id: 'd-1', name: 'Finans', isActive: true, approverUserId: 'a-1', approverName: 'Onay Veren', version: 1 } };
      },
    });
    const { user } = renderRoute('/admin/departments');

    await user.click(await screen.findByRole('button', { name: 'Yeni departman' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Departman adı'), 'Finans');
    await user.selectOptions(within(dialog).getByLabelText('Onaylayıcı yönetici'), await within(dialog).findByRole('option', { name: 'Onay Veren' }));
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    await screen.findByRole('heading', { name: 'Departmanlar' });
    expect(posted).toEqual({ name: 'Finans', approverUserId: 'a-1' });
  });

  it('shows a duplicate name under the name field', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/departments': { status: 200, body: pageOf([]) },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([]) },
      'POST /api/v1/admin/departments': { status: 409, body: { code: 'departments.name_taken' } },
    });
    const { user } = renderRoute('/admin/departments');

    await user.click(await screen.findByRole('button', { name: 'Yeni departman' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Departman adı'), 'insan kaynakları');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Bu adla bir departman zaten var.')).toBeInTheDocument();
  });
});
```

`frontend/src/features/admin/locations/LocationsPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const LOCATIONS = [
  { id: 'c', name: 'Merkez Kampüs', parentId: null, isActive: true, version: 1 },
  { id: 'b', name: 'A Blok', parentId: 'c', isActive: true, version: 1 },
];

describe('LocationsPage', () => {
  it('shows the tree and adds a child under the chosen parent', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/locations': { status: 200, body: LOCATIONS },
      'POST /api/v1/admin/locations': async (request) => {
        posted = await request.json();
        return { status: 201, body: { id: 'f', name: 'Kat 1', parentId: 'b', isActive: true, version: 1 } };
      },
    });
    const { user } = renderRoute('/admin/locations');

    const blockRow = (await screen.findByText('A Blok')).closest('li')!;
    await user.click(within(blockRow).getByRole('button', { name: 'Alt lokasyon ekle' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Üst lokasyon')).toHaveValue('b');
    await user.type(within(dialog).getByLabelText('Lokasyon adı'), 'Kat 1');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    await screen.findByRole('heading', { name: 'Lokasyonlar' });
    expect(posted).toEqual({ name: 'Kat 1', parentId: 'b' });
  });

  it('does not offer the location itself or its children as a new parent', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/locations': { status: 200, body: LOCATIONS },
    });
    const { user } = renderRoute('/admin/locations');

    const campusRow = (await screen.findByText('Merkez Kampüs')).closest('li')!;
    await user.click(within(campusRow).getAllByRole('button', { name: 'Düzenle' })[0]!);
    const parentSelect = within(await screen.findByRole('dialog')).getByLabelText('Üst lokasyon');

    expect(within(parentSelect).queryByRole('option', { name: /A Blok/ })).not.toBeInTheDocument();
    expect(within(parentSelect).queryByRole('option', { name: 'Merkez Kampüs' })).not.toBeInTheDocument();
  });
});
```

`frontend/src/features/admin/teams/TeamsPage.test.tsx`:

```tsx
import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });
const TEAM = { id: 't-1', name: 'Saha Destek', isActive: true, memberCount: 1, version: 3 };
const MEMBER = { userId: 'm-1', firstName: 'Mert', lastName: 'Can', email: 'mert@example.com', role: 'it_staff', isActive: true };
const CANDIDATE = { id: 'm-2', firstName: 'Elif', lastName: 'Su', email: 'elif@example.com', role: 'it_staff', isActive: true, departmentId: null, departmentName: null, locationId: null, locationName: null, version: 1 };
const EMPLOYEE = { ...CANDIDATE, id: 'e-1', firstName: 'Çalışan', lastName: 'Kişi', email: 'calisan@example.com', role: 'employee' };

describe('TeamsPage', () => {
  it('lists members, offers only IT staff and admins, and shows server errors', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/teams': { status: 200, body: pageOf([TEAM]) },
      'GET /api/v1/admin/teams/t-1': { status: 200, body: { id: 't-1', name: 'Saha Destek', isActive: true, version: 3, members: [MEMBER] } },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([CANDIDATE, EMPLOYEE]) },
      'POST /api/v1/admin/teams/t-1/members': { status: 409, body: { code: 'teams.member_inactive' } },
    });
    const { user } = renderRoute('/admin/teams');

    await user.click((await screen.findAllByRole('button', { name: 'Üyeleri yönet' }))[0]!);
    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText('Mert Can')).toBeInTheDocument();
    const select = within(dialog).getByLabelText('Eklenecek kişi');
    expect(within(select).queryByRole('option', { name: /Çalışan Kişi/ })).not.toBeInTheDocument();

    await user.selectOptions(select, await within(select).findByRole('option', { name: /Elif Su/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Üye ekle' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Pasif kullanıcı ekibe eklenemez.');
  });
});
```

`frontend/src/features/admin/audit/AuditPage.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const EVENT = {
  id: 'e-1',
  occurredAt: '2026-09-26T09:05:00Z',
  actorUserId: 'a-1',
  actorName: 'Tugay İltuş',
  action: 'users.role_changed',
  entityType: 'user',
  entityId: 'u-1',
  reason: null,
  oldValues: '{"role":"employee"}',
  newValues: '{"role":"it_staff"}',
  correlationId: 'corr-1',
};

describe('AuditPage', () => {
  it('shows Turkish labels, local time and change details, and filters by entity type', async () => {
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/audit-events': { status: 200, body: { items: [EVENT], page: 1, pageSize: 20, totalCount: 1 } },
    });
    const { user } = renderRoute('/admin/audit');

    expect((await screen.findAllByText('Rol değiştirildi')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Tugay İltuş').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/12:05/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/"it_staff"/).length).toBeGreaterThan(0);

    await user.selectOptions(screen.getByLabelText('Kayıt türü'), 'team');

    await waitFor(() => expect(mock.calls.some((r) => new URL(r.url).searchParams.get('entityType') === 'team')).toBe(true));
  });
});
```

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run src/features/admin`
Expected: the four new files FAIL (routes/pages missing); users tests still pass.

- [ ] **Step 2: Add runtime-key translation and reference options**

`frontend/src/shared/i18n/dynamic.ts`:

```ts
import i18n from '.';

// Keys built at runtime (audit actions, entity types) cannot use the typed key overloads.
const translateKey = i18n.t.bind(i18n) as unknown as (key: string) => string;

/** Translates a runtime-built key, or returns the fallback when the key does not exist. */
export function translateOr(key: string, fallback: string): string {
  return i18n.exists(key) ? translateKey(key) : fallback;
}
```

Append to `frontend/src/features/admin/shared/referenceOptions.ts` (add `import type { UserRole } from '../users/api';` with the other imports):

```ts
function useActiveUsers(role: UserRole | undefined) {
  return useQuery({
    queryKey: [...adminKeys.users, 'options', role ?? 'all'],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/users', { params: { query: { isActive: true, role, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
}

/** Active admins: department approvers must be admins (ADR-0005). */
export function useAdminOptions(): SelectOption[] {
  const admins = useActiveUsers('admin');
  return useMemo(() => (admins.data?.items ?? []).map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName}` })), [admins.data]);
}

/** Active IT staff and admins: the only roles allowed in teams. */
export function useTeamCandidateOptions(): SelectOption[] {
  const users = useActiveUsers(undefined);
  return useMemo(
    () =>
      (users.data?.items ?? [])
        .filter((u) => u.role !== 'employee')
        .map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName} (${u.email})` })),
    [users.data],
  );
}
```

- [ ] **Step 3: Implement departments**

`frontend/src/features/admin/departments/api.ts`:

```ts
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type DepartmentItem = components['schemas']['DepartmentItem'];
export type CreateDepartmentInput = components['schemas']['CreateDepartmentCommand'];
export type UpdateDepartmentInput = components['schemas']['UpdateDepartmentRequest'] & { id: string };

export interface DepartmentListParams {
  search: string;
  isActive: boolean | undefined;
  page: number;
  pageSize: number;
}

export function useDepartments(params: DepartmentListParams) {
  return useQuery({
    queryKey: [...adminKeys.departments, 'list', params],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/admin/departments', {
          params: { query: { search: params.search === '' ? undefined : params.search, isActive: params.isActive, page: params.page, pageSize: params.pageSize } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

/** Department names also appear in user lists. */
function useInvalidateDepartments() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: adminKeys.departments });
    await queryClient.invalidateQueries({ queryKey: adminKeys.users });
  };
}

export function useCreateDepartment() {
  const invalidate = useInvalidateDepartments();
  return useMutation({ mutationFn: (body: CreateDepartmentInput) => unwrap(api.POST('/api/v1/admin/departments', { body })), onSuccess: invalidate });
}

export function useUpdateDepartment() {
  const invalidate = useInvalidateDepartments();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateDepartmentInput) => unwrap(api.PUT('/api/v1/admin/departments/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetDepartmentActive() {
  const invalidate = useInvalidateDepartments();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/departments/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/departments/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}
```

`frontend/src/features/admin/departments/DepartmentFormDialog.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { SelectField } from '../../../shared/ui/SelectField';
import { TextField } from '../../../shared/ui/TextField';
import { useAdminOptions, withCurrentOption } from '../shared/referenceOptions';

import { useCreateDepartment, useUpdateDepartment, type DepartmentItem } from './api';

/** Mirrors backend Department.NameMaxLength. */
const DEPARTMENT_NAME_MAX_LENGTH = 150;
const NO_SELECTION = '';

const departmentSchema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(DEPARTMENT_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  approverUserId: z.string(),
});

type DepartmentFormValues = z.infer<typeof departmentSchema>;

const FORM_FIELDS = ['name', 'approverUserId'] as const;
const CODE_FIELDS = { [ERROR_CODES.departmentNameTaken]: 'name', [ERROR_CODES.approverInvalid]: 'approverUserId' } as const;

export function DepartmentFormDialog({ department, onClose }: { department: DepartmentItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const approverOptions = withCurrentOption(useAdminOptions(), department?.approverUserId, department?.approverName);
  const create = useCreateDepartment();
  const update = useUpdateDepartment();
  const mutation = department === null ? create : update;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<DepartmentFormValues>({
    resolver: zodResolver(departmentSchema),
    defaultValues: { name: department?.name ?? '', approverUserId: department?.approverUserId ?? NO_SELECTION },
  });

  const onSubmit = handleSubmit(({ name, approverUserId }) => {
    const body = { name, approverUserId: approverUserId === NO_SELECTION ? null : approverUserId };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (department === null) {
      create.mutate(body, callbacks);
    } else {
      update.mutate({ id: department.id, version: department.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={department === null ? t('departments.new') : t('departments.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {department === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('departments.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        <SelectField
          label={t('departments.fields.approver')}
          options={approverOptions}
          placeholder={tc('none')}
          error={fieldError(errors.approverUserId)}
          {...register('approverUserId')}
        />
      </form>
    </Dialog>
  );
}
```

`frontend/src/features/admin/departments/DepartmentsPage.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { useDepartments, useSetDepartmentActive, type DepartmentItem } from './api';
import { DepartmentFormDialog } from './DepartmentFormDialog';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;

type FormState = { department: DepartmentItem | null } | null;

interface RowActionsProps {
  department: DepartmentItem;
  onEdit: (department: DepartmentItem) => void;
  onToggle: (department: DepartmentItem) => void;
}

function DepartmentActions({ department, onEdit, onToggle }: RowActionsProps) {
  const { t: tc } = useTranslation();
  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" onClick={() => onEdit(department)}>
        {tc('actions.edit')}
      </Button>
      <Button variant="secondary" onClick={() => onToggle(department)}>
        {department.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
    </div>
  );
}

export function DepartmentsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(FIRST_PAGE);
  const [form, setForm] = useState<FormState>(null);
  const [toggling, setToggling] = useState<DepartmentItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const departments = useDepartments({ search: debouncedSearch, isActive: undefined, page, pageSize: PAGE_SIZE });
  const setActive = useSetDepartmentActive();

  const edit = (department: DepartmentItem) => setForm({ department });
  const openToggle = (department: DepartmentItem) => {
    setActive.reset();
    setToggling(department);
  };

  const columns: Column<DepartmentItem>[] = [
    { key: 'name', header: t('departments.columns.name'), renderCell: (d) => <span className="font-medium">{d.name}</span> },
    { key: 'approver', header: t('departments.columns.approver'), renderCell: (d) => d.approverName ?? tc('none') },
    { key: 'status', header: t('departments.columns.status'), renderCell: (d) => <StatusBadge isActive={d.isActive} /> },
    { key: 'actions', header: t('departments.columns.actions'), renderCell: (d) => <DepartmentActions department={d} onEdit={edit} onToggle={openToggle} /> },
  ];

  const renderCard = (d: DepartmentItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{d.name}</p>
          <p className="text-sm text-slate-600">{d.approverName ?? tc('none')}</p>
        </div>
        <StatusBadge isActive={d.isActive} />
      </div>
      <DepartmentActions department={d} onEdit={edit} onToggle={openToggle} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('departments.title')}</h1>
        <Button onClick={() => setForm({ department: null })}>{t('departments.new')}</Button>
      </div>
      <TextField
        type="search"
        className="sm:max-w-sm"
        label={t('departments.searchLabel')}
        value={search}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(FIRST_PAGE);
        }}
      />

      {departments.isPending && <LoadingState />}
      {departments.isError && <ErrorState error={departments.error} onRetry={() => void departments.refetch()} />}
      {departments.data && departments.data.items.length === 0 && <EmptyState />}
      {departments.data && departments.data.items.length > 0 && (
        <>
          <ResponsiveList items={departments.data.items} columns={columns} getKey={(d) => d.id} renderCard={renderCard} caption={t('departments.listCaption')} />
          <Pagination page={departments.data.page} pageSize={departments.data.pageSize} totalCount={departments.data.totalCount} onPageChange={setPage} />
        </>
      )}

      {form !== null && <DepartmentFormDialog key={form.department?.id ?? 'new'} department={form.department} onClose={() => setForm(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('departments.confirmDeactivate', { name: toggling.name }) : t('departments.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() =>
            setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })
          }
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
```

- [ ] **Step 4: Implement locations**

Append to `frontend/src/features/admin/locations/api.ts` (merge the `@tanstack/react-query` import to `{ useMutation, useQuery, useQueryClient }`):

```ts
export type CreateLocationInput = components['schemas']['CreateLocationCommand'];
export type UpdateLocationInput = components['schemas']['UpdateLocationRequest'] & { id: string };

/** Location names also appear in user lists and option labels. */
function useInvalidateLocations() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: adminKeys.locations });
    await queryClient.invalidateQueries({ queryKey: adminKeys.users });
  };
}

export function useCreateLocation() {
  const invalidate = useInvalidateLocations();
  return useMutation({ mutationFn: (body: CreateLocationInput) => unwrap(api.POST('/api/v1/admin/locations', { body })), onSuccess: invalidate });
}

export function useUpdateLocation() {
  const invalidate = useInvalidateLocations();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateLocationInput) => unwrap(api.PUT('/api/v1/admin/locations/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetLocationActive() {
  const invalidate = useInvalidateLocations();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/locations/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/locations/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}
```

`frontend/src/features/admin/locations/LocationFormDialog.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { SelectField } from '../../../shared/ui/SelectField';
import { TextField } from '../../../shared/ui/TextField';
import { withCurrentOption } from '../shared/referenceOptions';

import { useCreateLocation, useUpdateLocation } from './api';
import { collectSubtreeIds, findLocationNode, flattenLocationTree, type LocationNode } from './locationTree';

/** Mirrors backend Location.NameMaxLength. */
const LOCATION_NAME_MAX_LENGTH = 150;
const NO_PARENT = '';

const locationSchema = z.object({
  name: z.string().trim().min(1, ERROR_CODES.required).max(LOCATION_NAME_MAX_LENGTH, ERROR_CODES.tooLong),
  parentId: z.string(),
});

type LocationFormValues = z.infer<typeof locationSchema>;

const FORM_FIELDS = ['name', 'parentId'] as const;
const CODE_FIELDS = {
  [ERROR_CODES.locationNameTaken]: 'name',
  [ERROR_CODES.locationCycle]: 'parentId',
  [ERROR_CODES.referenceInactive]: 'parentId',
} as const;

export type LocationFormTarget = { kind: 'create'; parentId: string | null } | { kind: 'edit'; node: LocationNode };

export function LocationFormDialog({ roots, target, onClose }: { roots: readonly LocationNode[]; target: LocationFormTarget; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const create = useCreateLocation();
  const update = useUpdateLocation();
  const mutation = target.kind === 'create' ? create : update;

  const excluded = target.kind === 'edit' ? collectSubtreeIds(target.node) : new Set<string>();
  const currentParentId = target.kind === 'edit' ? target.node.item.parentId : target.parentId;
  const parentOptions = withCurrentOption(
    flattenLocationTree(roots)
      .filter((node) => node.item.isActive && !excluded.has(node.item.id))
      .map((node) => ({ value: node.item.id, label: node.path })),
    currentParentId,
    currentParentId ? findLocationNode(roots, currentParentId)?.path : undefined,
  );

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LocationFormValues>({
    resolver: zodResolver(locationSchema),
    defaultValues: { name: target.kind === 'edit' ? target.node.item.name : '', parentId: currentParentId ?? NO_PARENT },
  });

  const onSubmit = handleSubmit(({ name, parentId }) => {
    const body = { name, parentId: parentId === NO_PARENT ? null : parentId };
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (target.kind === 'create') {
      create.mutate(body, callbacks);
    } else {
      update.mutate({ id: target.node.item.id, version: target.node.item.version, ...body }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={target.kind === 'create' ? t('locations.new') : t('locations.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {target.kind === 'create' ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('locations.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
        <SelectField
          label={t('locations.fields.parent')}
          options={parentOptions}
          placeholder={t('locations.noParent')}
          error={fieldError(errors.parentId)}
          {...register('parentId')}
        />
      </form>
    </Dialog>
  );
}
```

`frontend/src/features/admin/locations/LocationsPage.tsx`:

```tsx
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';

import { useLocations, useSetLocationActive, type LocationItem } from './api';
import { LocationFormDialog, type LocationFormTarget } from './LocationFormDialog';
import { buildLocationTree, type LocationNode } from './locationTree';

interface TreeListProps {
  nodes: readonly LocationNode[];
  onEdit: (node: LocationNode) => void;
  onAddChild: (parentId: string) => void;
  onToggle: (item: LocationItem) => void;
}

function LocationTreeList({ nodes, onEdit, onAddChild, onToggle }: TreeListProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <ul className="flex flex-col gap-2">
      {nodes.map((node) => (
        <li key={node.item.id}>
          <div className="flex flex-col gap-2 rounded-xl border border-slate-200 bg-white p-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex min-w-0 items-center gap-2">
              <span className="font-medium break-words">{node.item.name}</span>
              {!node.item.isActive && <StatusBadge isActive={false} />}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" onClick={() => onEdit(node)}>
                {tc('actions.edit')}
              </Button>
              {node.item.isActive && (
                <Button variant="secondary" onClick={() => onAddChild(node.item.id)}>
                  {t('locations.addChild')}
                </Button>
              )}
              <Button variant="ghost" onClick={() => onToggle(node.item)}>
                {node.item.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
              </Button>
            </div>
          </div>
          {node.children.length > 0 && (
            <div className="mt-2 border-l-2 border-slate-200 pl-3 sm:pl-6">
              <LocationTreeList nodes={node.children} onEdit={onEdit} onAddChild={onAddChild} onToggle={onToggle} />
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}

export function LocationsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const locations = useLocations();
  const setActive = useSetLocationActive();
  const [target, setTarget] = useState<LocationFormTarget | null>(null);
  const [toggling, setToggling] = useState<LocationItem | null>(null);
  const roots = useMemo(() => buildLocationTree(locations.data ?? []), [locations.data]);

  const openToggle = (item: LocationItem) => {
    setActive.reset();
    setToggling(item);
  };

  return (
    <section className="flex flex-col gap-4" aria-label={t('locations.treeLabel')}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('locations.title')}</h1>
        <Button onClick={() => setTarget({ kind: 'create', parentId: null })}>{t('locations.new')}</Button>
      </div>

      {locations.isPending && <LoadingState />}
      {locations.isError && <ErrorState error={locations.error} onRetry={() => void locations.refetch()} />}
      {locations.data && roots.length === 0 && <EmptyState />}
      {roots.length > 0 && (
        <LocationTreeList
          nodes={roots}
          onEdit={(node) => setTarget({ kind: 'edit', node })}
          onAddChild={(parentId) => setTarget({ kind: 'create', parentId })}
          onToggle={openToggle}
        />
      )}

      {target !== null && (
        <LocationFormDialog
          key={target.kind === 'edit' ? target.node.item.id : `new-${target.parentId ?? 'root'}`}
          roots={roots}
          target={target}
          onClose={() => setTarget(null)}
        />
      )}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('locations.confirmDeactivate', { name: toggling.name }) : t('locations.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() =>
            setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })
          }
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
```

- [ ] **Step 5: Implement teams**

`frontend/src/features/admin/teams/api.ts`:

```ts
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type TeamItem = components['schemas']['TeamItem'];
export type TeamDetail = components['schemas']['TeamDetail'];

export interface TeamListParams {
  search: string;
  page: number;
  pageSize: number;
}

export function useTeams(params: TeamListParams) {
  return useQuery({
    queryKey: [...adminKeys.teams, 'list', params],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/teams', { params: { query: { search: params.search === '' ? undefined : params.search, page: params.page, pageSize: params.pageSize } } })),
    placeholderData: keepPreviousData,
  });
}

export function useTeam(id: string) {
  return useQuery({
    queryKey: [...adminKeys.teams, 'detail', id],
    queryFn: () => unwrap(api.GET('/api/v1/admin/teams/{id}', { params: { path: { id } } })),
  });
}

function useInvalidateTeams() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: adminKeys.teams });
}

export function useCreateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({ mutationFn: (name: string) => unwrap(api.POST('/api/v1/admin/teams', { body: { name } })), onSuccess: invalidate });
}

export function useUpdateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ id, version, name }: { id: string; version: number; name: string }) =>
      unwrap(api.PUT('/api/v1/admin/teams/{id}', { params: { path: { id } }, body: { version, name } })),
    onSuccess: invalidate,
  });
}

export function useSetTeamActive() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/teams/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/teams/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useAddTeamMember() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, userId }: { teamId: string; userId: string }) =>
      unwrap(api.POST('/api/v1/admin/teams/{id}/members', { params: { path: { id: teamId } }, body: { userId } })),
    onSuccess: invalidate,
  });
}

export function useRemoveTeamMember() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, userId }: { teamId: string; userId: string }) =>
      unwrap(api.DELETE('/api/v1/admin/teams/{id}/members/{userId}', { params: { path: { id: teamId, userId } } })),
    onSuccess: invalidate,
  });
}
```

`frontend/src/features/admin/teams/TeamFormDialog.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { TextField } from '../../../shared/ui/TextField';

import { useCreateTeam, useUpdateTeam, type TeamItem } from './api';

/** Mirrors backend Team.NameMaxLength. */
const TEAM_NAME_MAX_LENGTH = 150;

const teamSchema = z.object({ name: z.string().trim().min(1, ERROR_CODES.required).max(TEAM_NAME_MAX_LENGTH, ERROR_CODES.tooLong) });
type TeamFormValues = z.infer<typeof teamSchema>;

const FORM_FIELDS = ['name'] as const;
const CODE_FIELDS = { [ERROR_CODES.teamNameTaken]: 'name' } as const;

export function TeamFormDialog({ team, onClose }: { team: TeamItem | null; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const formId = useId();
  const create = useCreateTeam();
  const update = useUpdateTeam();
  const mutation = team === null ? create : update;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<TeamFormValues>({ resolver: zodResolver(teamSchema), defaultValues: { name: team?.name ?? '' } });

  const onSubmit = handleSubmit(({ name }) => {
    const callbacks = { onSuccess: onClose, onError: (error: unknown) => applyServerErrors(error, setError, FORM_FIELDS, CODE_FIELDS) };
    if (team === null) {
      create.mutate(name, callbacks);
    } else {
      update.mutate({ id: team.id, version: team.version, name }, callbacks);
    }
  });

  const alert = mutation.error && !isHandledByFields(mutation.error, FORM_FIELDS, CODE_FIELDS) ? describeError(mutation.error) : null;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={team === null ? t('teams.new') : t('teams.edit')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {tc('actions.cancel')}
          </Button>
          <Button type="submit" form={formId} isLoading={mutation.isPending}>
            {team === null ? tc('actions.create') : tc('actions.save')}
          </Button>
        </>
      }
    >
      <form id={formId} noValidate className="flex flex-col gap-4" onSubmit={(event) => void onSubmit(event)}>
        <FormAlert message={alert} />
        <TextField label={t('teams.fields.name')} autoComplete="off" error={fieldError(errors.name)} {...register('name')} />
      </form>
    </Dialog>
  );
}
```

`frontend/src/features/admin/teams/TeamMembersDialog.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { Button } from '../../../shared/ui/Button';
import { Dialog } from '../../../shared/ui/Dialog';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { useTeamCandidateOptions } from '../shared/referenceOptions';

import { useAddTeamMember, useRemoveTeamMember, useTeam } from './api';

const NO_SELECTION = '';

export function TeamMembersDialog({ teamId, teamName, onClose }: { teamId: string; teamName: string; onClose: () => void }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const team = useTeam(teamId);
  const candidates = useTeamCandidateOptions();
  const add = useAddTeamMember();
  const remove = useRemoveTeamMember();
  const [selected, setSelected] = useState(NO_SELECTION);

  const memberIds = new Set(team.data?.members.map((member) => member.userId) ?? []);
  const options = candidates.filter((option) => !memberIds.has(option.value));
  const error = add.error ?? remove.error;

  const addSelected = (event: FormEvent) => {
    event.preventDefault();
    if (selected === NO_SELECTION) {
      return;
    }
    remove.reset();
    add.mutate({ teamId, userId: selected }, { onSuccess: () => setSelected(NO_SELECTION) });
  };

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
      title={t('teams.membersTitle', { name: teamName })}
      footer={<Button onClick={onClose}>{tc('actions.close')}</Button>}
    >
      <div className="flex flex-col gap-4">
        <FormAlert message={error ? describeError(error) : null} />
        <form className="flex flex-col gap-2 sm:flex-row sm:items-end" onSubmit={addSelected}>
          <SelectField
            className="flex-1"
            label={t('teams.memberToAdd')}
            value={selected}
            options={options}
            placeholder={tc('none')}
            onChange={(event) => setSelected(event.target.value)}
          />
          <Button type="submit" disabled={selected === NO_SELECTION} isLoading={add.isPending}>
            {t('teams.addMember')}
          </Button>
        </form>

        {team.isPending && <LoadingState />}
        {team.isError && <ErrorState error={team.error} onRetry={() => void team.refetch()} />}
        {team.data && team.data.members.length === 0 && <EmptyState message={t('teams.noMembers')} />}
        {team.data && team.data.members.length > 0 && (
          <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
            {team.data.members.map((member) => (
              <li key={member.userId} className="flex items-center justify-between gap-2 p-3">
                <div className="min-w-0">
                  <p className="font-medium">{`${member.firstName} ${member.lastName}`}</p>
                  <p className="text-sm break-all text-slate-600">
                    {member.email} · {tc(`roles.${member.role}`)}
                  </p>
                </div>
                <Button
                  variant="ghost"
                  isLoading={remove.isPending && remove.variables?.userId === member.userId}
                  onClick={() => {
                    add.reset();
                    remove.mutate({ teamId, userId: member.userId });
                  }}
                >
                  {t('teams.removeMember')}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Dialog>
  );
}
```

`frontend/src/features/admin/teams/TeamsPage.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { Button } from '../../../shared/ui/Button';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { TextField } from '../../../shared/ui/TextField';

import { useSetTeamActive, useTeams, type TeamItem } from './api';
import { TeamFormDialog } from './TeamFormDialog';
import { TeamMembersDialog } from './TeamMembersDialog';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;

interface TeamActionsProps {
  team: TeamItem;
  onEdit: (team: TeamItem) => void;
  onMembers: (team: TeamItem) => void;
  onToggle: (team: TeamItem) => void;
}

function TeamActions({ team, onEdit, onMembers, onToggle }: TeamActionsProps) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  return (
    <div className="flex flex-wrap gap-2">
      <Button variant="secondary" onClick={() => onMembers(team)}>
        {t('teams.manageMembers')}
      </Button>
      <Button variant="secondary" onClick={() => onEdit(team)}>
        {tc('actions.edit')}
      </Button>
      <Button variant="ghost" onClick={() => onToggle(team)}>
        {team.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
      </Button>
    </div>
  );
}

export function TeamsPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(FIRST_PAGE);
  const [form, setForm] = useState<{ team: TeamItem | null } | null>(null);
  const [membersOf, setMembersOf] = useState<TeamItem | null>(null);
  const [toggling, setToggling] = useState<TeamItem | null>(null);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const teams = useTeams({ search: debouncedSearch, page, pageSize: PAGE_SIZE });
  const setActive = useSetTeamActive();

  const edit = (team: TeamItem) => setForm({ team });
  const openToggle = (team: TeamItem) => {
    setActive.reset();
    setToggling(team);
  };

  const columns: Column<TeamItem>[] = [
    { key: 'name', header: t('teams.columns.name'), renderCell: (team) => <span className="font-medium">{team.name}</span> },
    { key: 'members', header: t('teams.columns.members'), renderCell: (team) => t('teams.memberCount', { count: team.memberCount }) },
    { key: 'status', header: t('teams.columns.status'), renderCell: (team) => <StatusBadge isActive={team.isActive} /> },
    { key: 'actions', header: t('teams.columns.actions'), renderCell: (team) => <TeamActions team={team} onEdit={edit} onMembers={setMembersOf} onToggle={openToggle} /> },
  ];

  const renderCard = (team: TeamItem) => (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium break-words">{team.name}</p>
          <p className="text-sm text-slate-600">{t('teams.memberCount', { count: team.memberCount })}</p>
        </div>
        <StatusBadge isActive={team.isActive} />
      </div>
      <TeamActions team={team} onEdit={edit} onMembers={setMembersOf} onToggle={openToggle} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('teams.title')}</h1>
        <Button onClick={() => setForm({ team: null })}>{t('teams.new')}</Button>
      </div>
      <TextField
        type="search"
        className="sm:max-w-sm"
        label={t('teams.searchLabel')}
        value={search}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(FIRST_PAGE);
        }}
      />

      {teams.isPending && <LoadingState />}
      {teams.isError && <ErrorState error={teams.error} onRetry={() => void teams.refetch()} />}
      {teams.data && teams.data.items.length === 0 && <EmptyState />}
      {teams.data && teams.data.items.length > 0 && (
        <>
          <ResponsiveList items={teams.data.items} columns={columns} getKey={(team) => team.id} renderCard={renderCard} caption={t('teams.listCaption')} />
          <Pagination page={teams.data.page} pageSize={teams.data.pageSize} totalCount={teams.data.totalCount} onPageChange={setPage} />
        </>
      )}

      {form !== null && <TeamFormDialog key={form.team?.id ?? 'new'} team={form.team} onClose={() => setForm(null)} />}
      {membersOf !== null && <TeamMembersDialog key={membersOf.id} teamId={membersOf.id} teamName={membersOf.name} onClose={() => setMembersOf(null)} />}
      {toggling !== null && (
        <ConfirmDialog
          title={toggling.isActive ? tc('actions.deactivate') : tc('actions.reactivate')}
          message={toggling.isActive ? t('teams.confirmDeactivate', { name: toggling.name }) : t('teams.confirmReactivate', { name: toggling.name })}
          confirmLabel={tc('actions.confirm')}
          variant={toggling.isActive ? 'danger' : 'primary'}
          isPending={setActive.isPending}
          error={setActive.error}
          onConfirm={() =>
            setActive.mutate({ id: toggling.id, version: toggling.version, isActive: !toggling.isActive }, { onSuccess: () => setToggling(null) })
          }
          onCancel={() => setToggling(null)}
        />
      )}
    </section>
  );
}
```

- [ ] **Step 6: Implement audit**

`frontend/src/features/admin/audit/api.ts`:

```ts
import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type AuditEventItem = components['schemas']['AuditEventItem'];

export interface AuditListParams {
  entityType: string | undefined;
  page: number;
  pageSize: number;
}

export function useAuditEvents(params: AuditListParams) {
  return useQuery({
    queryKey: [...adminKeys.audit, params],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/audit-events', { params: { query: { entityType: params.entityType, page: params.page, pageSize: params.pageSize } } })),
    placeholderData: keepPreviousData,
  });
}
```

`frontend/src/features/admin/audit/AuditPage.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { translateOr } from '../../../shared/i18n/dynamic';
import { formatDateTime } from '../../../shared/lib/format';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';

import { useAuditEvents, type AuditEventItem } from './api';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const ALL_ENTITIES = '';
const JSON_INDENT = 2;
/** Mirrors backend AuditEntityTypes. */
const ENTITY_TYPES = ['user', 'department', 'location', 'team'] as const;

const actionLabel = (action: string) => translateOr(`admin:auditActions.${action}`, action);
const entityLabel = (entityType: string) => translateOr(`admin:entityTypes.${entityType}`, entityType);

function prettyJson(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  try {
    return JSON.stringify(JSON.parse(value), null, JSON_INDENT);
  } catch {
    return value;
  }
}

function AuditDetails({ event }: { event: AuditEventItem }) {
  const { t } = useTranslation('admin');
  const before = prettyJson(event.oldValues);
  const after = prettyJson(event.newValues);
  return (
    <details className="text-sm">
      <summary className="cursor-pointer text-brand-700">{t('audit.details')}</summary>
      <div className="mt-2 flex flex-col gap-2">
        {before && (
          <div>
            <p className="font-medium">{t('audit.before')}</p>
            <pre className="overflow-x-auto rounded-lg bg-slate-100 p-2 text-xs">{before}</pre>
          </div>
        )}
        {after && (
          <div>
            <p className="font-medium">{t('audit.after')}</p>
            <pre className="overflow-x-auto rounded-lg bg-slate-100 p-2 text-xs">{after}</pre>
          </div>
        )}
        <p className="text-xs break-all text-slate-600">
          {t('audit.correlation')}: {event.correlationId}
        </p>
      </div>
    </details>
  );
}

export function AuditPage() {
  const { t } = useTranslation('admin');
  const [entityType, setEntityType] = useState<string>(ALL_ENTITIES);
  const [page, setPage] = useState(FIRST_PAGE);
  const events = useAuditEvents({ entityType: entityType === ALL_ENTITIES ? undefined : entityType, page, pageSize: PAGE_SIZE });

  const actorName = (event: AuditEventItem) => event.actorName ?? t('audit.system');

  const columns: Column<AuditEventItem>[] = [
    { key: 'time', header: t('audit.columns.time'), renderCell: (event) => <span className="whitespace-nowrap">{formatDateTime(event.occurredAt)}</span> },
    { key: 'actor', header: t('audit.columns.actor'), renderCell: actorName },
    { key: 'action', header: t('audit.columns.action'), renderCell: (event) => actionLabel(event.action) },
    { key: 'entity', header: t('audit.columns.entity'), renderCell: (event) => entityLabel(event.entityType) },
    { key: 'details', header: t('audit.details'), renderCell: (event) => <AuditDetails event={event} /> },
  ];

  const renderCard = (event: AuditEventItem) => (
    <div className="flex flex-col gap-1">
      <p className="font-medium">{actionLabel(event.action)}</p>
      <p className="text-sm text-slate-600">
        {formatDateTime(event.occurredAt)} · {actorName(event)} · {entityLabel(event.entityType)}
      </p>
      <AuditDetails event={event} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('audit.title')}</h1>
      <SelectField
        className="sm:max-w-xs"
        label={t('audit.entityFilter')}
        value={entityType}
        options={[{ value: ALL_ENTITIES, label: t('audit.allEntities') }, ...ENTITY_TYPES.map((type) => ({ value: type, label: entityLabel(type) }))]}
        onChange={(event) => {
          setEntityType(event.target.value);
          setPage(FIRST_PAGE);
        }}
      />

      {events.isPending && <LoadingState />}
      {events.isError && <ErrorState error={events.error} onRetry={() => void events.refetch()} />}
      {events.data && events.data.items.length === 0 && <EmptyState />}
      {events.data && events.data.items.length > 0 && (
        <>
          <ResponsiveList items={events.data.items} columns={columns} getKey={(event) => event.id} renderCard={renderCard} caption={t('audit.listCaption')} />
          <Pagination page={events.data.page} pageSize={events.data.pageSize} totalCount={events.data.totalCount} onPageChange={setPage} />
        </>
      )}
    </section>
  );
}
```

- [ ] **Step 7: Register the routes**

In `frontend/src/app/router.tsx`, add to the admin `children` after the users route:

```tsx
          { path: ROUTES.adminDepartments, lazy: async () => ({ Component: (await import('../features/admin/departments/DepartmentsPage')).DepartmentsPage }) },
          { path: ROUTES.adminLocations, lazy: async () => ({ Component: (await import('../features/admin/locations/LocationsPage')).LocationsPage }) },
          { path: ROUTES.adminTeams, lazy: async () => ({ Component: (await import('../features/admin/teams/TeamsPage')).TeamsPage }) },
          { path: ROUTES.adminAudit, lazy: async () => ({ Component: (await import('../features/admin/audit/AuditPage')).AuditPage }) },
```

- [ ] **Step 8: Run tests, lint, build**

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx vitest run && npm run lint && npm run build; echo "exit=$?"`
Expected: all PASS, `exit=0`, no chunk-size warning.

- [ ] **Step 9: Manual check**

With API + Vite running, at 360 px and 1280 px: create a department with an approver, build a two-level location tree, create a team and add an IT staff member, then open `/admin/audit` and see the Turkish action labels. No horizontal page scroll at 360 px (audit JSON scrolls inside its own box).

- [ ] **Step 10: Checkpoint (no commit)**

Run: `/Users/lektuel/claude/TGYSM/tools/lint.sh && /Users/lektuel/claude/TGYSM/tools/test.sh; echo "exit=$?"` then `git -C /Users/lektuel/claude/TGYSM status --short`

---
### Task 16: E2E, final tool scripts, coverage gates, review and hand-off

**Files:**
- Create: `frontend/playwright.config.ts`, `frontend/e2e/helpers.ts`, `frontend/e2e/admin-onboarding.spec.ts`
- Create: `tools/e2e.sh`, `tools/run.sh`
- Modify: `tools/test.sh` (coverage gates)

**Interfaces:**
- Consumes: everything above; `setup-admin` output line `Temporary password (shown once): <password>`.
- Produces: `tools/e2e.sh` (fresh `tgysm_e2e` database per Playwright project), `tools/run.sh`, final `tools/test.sh` with coverage gates.

- [ ] **Step 1: Install the Playwright browser**

Run: `cd /Users/lektuel/claude/TGYSM/frontend && npx playwright install chromium`

- [ ] **Step 2: Write the Playwright config and helpers**

`frontend/playwright.config.ts`:

```ts
import { defineConfig, devices } from '@playwright/test';

const BASE_URL = process.env.E2E_BASE_URL ?? 'http://localhost:4173';
const MOBILE_VIEWPORT = { width: 360, height: 800 };
const DESKTOP_VIEWPORT = { width: 1280, height: 800 };

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: { baseURL: BASE_URL, trace: 'retain-on-failure', locale: 'tr-TR', timezoneId: 'Europe/Istanbul' },
  projects: [
    { name: 'mobile-360', use: { ...devices['Desktop Chrome'], viewport: MOBILE_VIEWPORT, hasTouch: true } },
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: DESKTOP_VIEWPORT } },
  ],
});
```

`frontend/e2e/helpers.ts`:

```ts
import { expect, type Locator, type Page } from '@playwright/test';

export function requiredEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set; run the E2E suite through tools/e2e.sh.`);
  }
  return value;
}

export async function login(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('E-posta').fill(email);
  await page.getByLabel('Şifre', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Giriş yap' }).click();
}

export async function changePassword(page: Page, current: string, next: string): Promise<void> {
  await page.getByLabel('Mevcut şifre').fill(current);
  await page.getByLabel('Yeni şifre', { exact: true }).fill(next);
  await page.getByLabel('Yeni şifre (tekrar)').fill(next);
  await page.getByRole('button', { name: 'Şifreyi değiştir' }).click();
}

/** Spec: basic flows need no horizontal scroll at 360 px. */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
}

/** The visible card (mobile) or table row (desktop) that contains the text. */
export function visibleEntry(page: Page, text: string): Locator {
  return page.locator('li:visible, tr:visible').filter({ hasText: text });
}
```

- [ ] **Step 3: Write the E2E spec**

`frontend/e2e/admin-onboarding.spec.ts`:

```ts
import { expect, test } from '@playwright/test';

import { changePassword, expectNoHorizontalScroll, login, requiredEnv, visibleEntry } from './helpers';

const ADMIN_NEW_PASSWORD = 'Admin-new-pass-2026';
const USER_NEW_PASSWORD = 'User-new-pass-2026';

test('login form works with the keyboard only', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('E-posta').focus();
  await page.keyboard.type('nobody@example.com');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Wrong-pass-2026');
  await page.keyboard.press('Enter');

  await expect(page.getByRole('alert')).toHaveText('E-posta veya şifre hatalı.');
});

test('admin onboards a user; deactivation ends that user session', async ({ page, browser }) => {
  const adminEmail = requiredEnv('E2E_ADMIN_EMAIL');
  const adminTemporaryPassword = requiredEnv('E2E_ADMIN_TEMP_PASSWORD');
  const userEmail = `e2e-${Date.now()}@example.com`;

  await page.goto('/');
  await expect(page).toHaveURL(/\/login$/);
  await login(page, adminEmail, adminTemporaryPassword);
  await expect(page).toHaveURL(/\/change-password$/);
  await changePassword(page, adminTemporaryPassword, ADMIN_NEW_PASSWORD);
  await expect(page.getByRole('heading', { name: /^Merhaba/ })).toBeVisible();
  await expectNoHorizontalScroll(page);

  await page.goto('/admin/users');
  await page.getByRole('button', { name: 'Yeni kullanıcı' }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Ad', { exact: true }).fill('Deniz');
  await form.getByLabel('Soyad').fill('Aydın');
  await form.getByLabel('E-posta').fill(userEmail);
  await form.getByRole('button', { name: 'Oluştur' }).click();
  const userTemporaryPassword = ((await page.getByTestId('temporary-password').textContent()) ?? '').trim();
  expect(userTemporaryPassword).not.toBe('');
  await page.getByRole('dialog').getByRole('button', { name: 'Kapat' }).last().click();
  await expectNoHorizontalScroll(page);

  const userContext = await browser.newContext();
  const userPage = await userContext.newPage();
  await login(userPage, userEmail, userTemporaryPassword);
  await changePassword(userPage, userTemporaryPassword, USER_NEW_PASSWORD);
  await expect(userPage.getByRole('heading', { name: 'Merhaba, Deniz' })).toBeVisible();
  await userPage.goto('/admin/users');
  await expect(userPage).toHaveURL(/\/$/);

  await page.getByLabel('Kullanıcı ara').fill(userEmail);
  const entry = visibleEntry(page, userEmail);
  await entry.getByRole('button', { name: 'Pasifleştir' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(entry.getByText('Pasif', { exact: true })).toBeVisible();

  await userPage.reload();
  await expect(userPage).toHaveURL(/\/login$/);
  await userContext.close();
});
```

- [ ] **Step 4: Write `tools/e2e.sh`**

```bash
#!/usr/bin/env bash
# E2E: for each Playwright project, recreate tgysm_e2e, create the first admin, start API + preview, run the suite.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="tgysm_e2e"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly ADMIN_DB="postgres"
readonly API_URL="http://localhost:5080"
readonly PREVIEW_URL="http://localhost:4173"
readonly READY_PATH="/api/v1/auth/csrf"
readonly READY_TIMEOUT_SECONDS=60
readonly ADMIN_EMAIL="e2e-admin@example.com"
readonly API_BIN_DIR="backend/src/Tgysm.Api/bin/Debug/net10.0"
readonly LOG_DIR="frontend/test-results/e2e-logs"
readonly TEMP_PASSWORD_PREFIX="Temporary password (shown once): "
readonly PROJECTS=("mobile-360" "desktop")

api_pid=""
preview_pid=""

stop_servers() {
  if [[ -n "$api_pid" ]]; then kill "$api_pid" 2>/dev/null || true; fi
  if [[ -n "$preview_pid" ]]; then kill "$preview_pid" 2>/dev/null || true; fi
  wait 2>/dev/null || true
  api_pid=""
  preview_pid=""
}
trap stop_servers EXIT

wait_for() {
  local url="$1"
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if curl -sf -o /dev/null "$url"; then return 0; fi
    sleep 1
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

mkdir -p "$LOG_DIR"
dotnet build backend/src/Tgysm.Api --nologo -v quiet
(cd frontend && npm run build >/dev/null)

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"

for project in "${PROJECTS[@]}"; do
  psql -h "$DB_HOST" -d "$ADMIN_DB" -qc "DROP DATABASE IF EXISTS \"$DB_NAME\" WITH (FORCE)"
  psql -h "$DB_HOST" -d "$ADMIN_DB" -qc "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\""
  TGYSM_DB_NAME="$DB_NAME" tools/migrate.sh >/dev/null

  setup_output="$(cd "$API_BIN_DIR" && dotnet Tgysm.Api.dll setup-admin --email "$ADMIN_EMAIL" --first-name E2E --last-name Yönetici)"
  temp_password="$(sed -n "s/^${TEMP_PASSWORD_PREFIX}//p" <<<"$setup_output")"
  if [[ -z "$temp_password" ]]; then
    echo "setup-admin did not print a temporary password" >&2
    exit 1
  fi

  # exec keeps $! pointing at the real server process, so kill stops it.
  (cd "$API_BIN_DIR" && exec dotnet Tgysm.Api.dll --urls "$API_URL") >"$LOG_DIR/api-$project.log" 2>&1 &
  api_pid=$!
  (cd frontend && exec node node_modules/vite/bin/vite.js preview) >"$LOG_DIR/preview-$project.log" 2>&1 &
  preview_pid=$!
  wait_for "$API_URL$READY_PATH"
  wait_for "$PREVIEW_URL/login"

  (cd frontend && E2E_ADMIN_EMAIL="$ADMIN_EMAIL" E2E_ADMIN_TEMP_PASSWORD="$temp_password" npx playwright test --project="$project")
  stop_servers
done

echo "E2E passed for: ${PROJECTS[*]}"
```

Run:

```bash
chmod +x /Users/lektuel/claude/TGYSM/tools/e2e.sh
/Users/lektuel/claude/TGYSM/tools/e2e.sh; echo "exit=$?"
```

Expected: both tests pass in `mobile-360` and `desktop`, `exit=0`. On failure, read `frontend/test-results/e2e-logs/*.log` and the Playwright trace; do not retry blindly.

- [ ] **Step 5: Write `tools/run.sh`**

```bash
#!/usr/bin/env bash
# Local development: API on http://localhost:5080 and Vite on http://localhost:5173. Ctrl+C stops both.
set -euo pipefail
cd "$(dirname "$0")/.."

tools/db-up.sh
tools/migrate.sh

pids=()
cleanup() {
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

(cd backend && exec dotnet run --project src/Tgysm.Api --launch-profile http) &
pids+=($!)
(cd frontend && exec npm run dev) &
pids+=($!)
wait
```

Run: `chmod +x /Users/lektuel/claude/TGYSM/tools/run.sh`

- [ ] **Step 6: Add coverage gates to `tools/test.sh`**

Replace `tools/test.sh`:

```bash
#!/usr/bin/env bash
# Test gate: backend + frontend tests with line-coverage gates.
# Backend: Domain + Application >= MIN_LINE_COVERAGE. Frontend: threshold in vite.config.ts (same value).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly MIN_LINE_COVERAGE=80
readonly RESULTS_DIR="backend/TestResults"
readonly SUMMARY_FILE="$RESULTS_DIR/report/Summary.txt"
readonly COVERED_ASSEMBLIES="+Tgysm.Domain;+Tgysm.Application"

rm -rf "$RESULTS_DIR"
dotnet test backend/Tgysm.slnx --nologo --collect:"XPlat Code Coverage" --results-directory "$RESULTS_DIR"
(cd backend && dotnet tool run reportgenerator \
  "-reports:TestResults/**/coverage.cobertura.xml" \
  "-targetdir:TestResults/report" \
  "-reporttypes:TextSummary" \
  "-assemblyfilters:$COVERED_ASSEMBLIES" >/dev/null)

line_coverage="$(awk -F': ' '/^ *Line coverage:/ { sub(/%/, "", $2); print $2; exit }' "$SUMMARY_FILE")"
echo "Backend Domain + Application line coverage: ${line_coverage}%"
if ! awk -v actual="$line_coverage" -v minimum="$MIN_LINE_COVERAGE" 'BEGIN { exit !(actual >= minimum) }'; then
  echo "Backend line coverage ${line_coverage}% is below ${MIN_LINE_COVERAGE}%." >&2
  exit 1
fi

(cd frontend && npm run test:coverage)
```

Run: `/Users/lektuel/claude/TGYSM/tools/test.sh; echo "exit=$?"`
Expected: both coverage lines ≥ 80%, `exit=0`. If a gate fails, add tests for the uncovered behavior (not for trivial getters) and re-run.

- [ ] **Step 7: Run every gate from a clean state**

Run, one at a time, reading each exit code:

```bash
/Users/lektuel/claude/TGYSM/tools/build.sh; echo "build exit=$?"
/Users/lektuel/claude/TGYSM/tools/e2e.sh; echo "e2e exit=$?"
```

Expected: `build exit=0` (lint + tests + coverage + Release build + bundle, chunk list printed, no chunk warning) and `e2e exit=0`.

- [ ] **Step 8: Specialist reviews (tree must be stable — no edits while they run)**

Request one review per area, each over its own files only:
- `ecc:csharp-reviewer` → `backend/src/**`
- `ecc:database-reviewer` → `backend/src/Tgysm.Infrastructure/Persistence/**` (configurations, migrations, trigger)
- `ecc:security-reviewer` → auth, CSRF, session validation, rate limiting, admin policies, `setup-admin`
- `ecc:react-reviewer` → `frontend/src/**`

Fix confirmed findings with a failing test first where behavior changes, then re-run Step 7.

- [ ] **Step 9: Hand-off (no commit)**

Run: `git -C /Users/lektuel/claude/TGYSM status --short`
Report to the owner: gate results with exit codes, coverage numbers, E2E result per project, review findings and fixes, and which docs need a line (`capabilities.md` sections marked `TODO: fill after Phase 1 lands`, `docs/user-guides/PROGRESS.md` admin pages). Wait for the owner's approval before any commit or push.

---

## Self-Review Notes

- **Spec coverage:** §1 criteria 1–8 → Tasks 7–9 (auth, IDOR, setup, revocation), 12 (i18n/Turkish), 14–16 (360 px, keyboard, E2E), 16 (coverage, gates). §2 layout → Task 2. §3 data model → Tasks 3–4. §4 auth → Tasks 6–8. §5 audit → Tasks 4 (trigger), 5 (AuditLog), 9–11 (events), 11/15 (read UI). §6–7 frontend + i18n → Tasks 12–15. §8 testing → every task + Task 16. §9 tools → Tasks 2, 4, 6, 12, 13, 16. §10 ADRs → Task 1.
- **Deliberate refinements of the spec:** password hashing uses `PasswordHasher<T>` behind `IPasswordService` instead of the full `AddIdentityCore` store, so the domain `User` stays framework-free (recorded in ADR-0004). Test isolation uses `TRUNCATE` in `PostgresDatabase.ResetAsync` instead of Respawn, because Respawn's `DELETE` would be rejected by the append-only audit trigger. The error-code translation check runs in `TranslationCoverageTests` against `errors.json` directly instead of exporting a separate JSON file.
- **Known limits (documented, not fixed in Phase 1):** reference dropdowns load at most 100 items (`REFERENCE_PAGE_SIZE`); locations list is capped at 2000; Turkish sort order depends on the database collation (ADR-0002); Data Protection keys must be persisted before production (ADR-0004); two simultaneous "demote each other" requests by the last two admins can both pass the guard (rare, admin-only; revisit with a serializable check if needed).
