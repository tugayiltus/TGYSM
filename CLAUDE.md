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
