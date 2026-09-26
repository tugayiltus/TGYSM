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
