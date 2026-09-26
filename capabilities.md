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
