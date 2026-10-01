# TGYSM — ITSM web platform

Single-company ITSM: incident and request management, IT inventory and asset management, SLA, email, knowledge base and reports. Current state: Phases 1–3 done (auth, `/admin`, assets + CSV import, tickets + approvals + comments/attachments). Phase 4 done on branch `phase-4-sla-email` (not merged): Worker host, outbox + email, templates and deliveries, password set links, SLA calendar/policy/instances/monitor, `/admin/system`, UI pages. Next: Phase 5 (knowledge base/reports). Requirements: `tgysm.md`.

## Stack & Architecture
- **Backend:** .NET 10 modular monolith (ADR-0001) — `backend/src/Tgysm.Domain` (rules, no framework deps) → `Tgysm.Application` (handlers + FluentValidation, ports) → `Tgysm.Infrastructure` (EF Core + Npgsql, migrations) → `Tgysm.Api` (Minimal APIs, auth, CLI). `backend/src/Tgysm.Worker` (Generic Host, jobs `OutboxDispatcher`, `EmailSender`, `SlaMonitor`, `Cleanup`, row leases; references Application + Infrastructure, never Api; ADR-0014/0015) runs next to the API. Solution: `backend/Tgysm.slnx`.
- **Database:** PostgreSQL 17, local Homebrew (ADR-0002). snake_case, `uuid` v7, `xmin` concurrency.
- **Frontend:** React 19 + Vite + TypeScript in `frontend/` (ADR-0008). Server state via TanStack Query; typed client `src/shared/api/client.ts` generated from OpenAPI.
- Out of scope unless asked: mobile app. The export runner job, knowledge base and reports belong to Phase 5.

## Commands
Run through `tools/` scripts.
- `tools/db-up.sh` — ensure local PostgreSQL role/database + API user-secret connection string.
- `tools/migrate.sh` — apply EF Core migrations.
- `tools/setup-admin.sh --email … --first-name … --last-name …` — create first admin (once).
- `tools/reset-password.sh --email …` — temporary password from the console (locked-out operator).
- `tools/add-migration.sh <Name>` — add an EF Core migration.
- `tools/run.sh` — API on `http://localhost:5080`, Worker (email to `tmp/email-pickup`) + Vite on `http://localhost:5173`.
- `tools/lint.sh` — `dotnet format --verify-no-changes` + ESLint + TypeScript.
- `tools/test.sh` — backend tests with coverage gate + frontend tests with coverage gate. Backend uses Microsoft.Testing.Platform (repo-root `global.json`): `dotnet test --solution backend/Tgysm.slnx`, filters `--filter-class` / `--filter-method`.
- `tools/e2e.sh` — Playwright E2E on a fresh `tgysm_e2e` database per viewport; refuses to start while `tools/run.sh` holds ports 5080/4173. Also starts the Worker (`Email:Mode=Pickup` into a per-run temp folder, 2 s job intervals; E2E-only env overrides). Tests seed their data through the admin API (`frontend/e2e/world.ts`).
- `tools/gen-api-types.sh` — regenerate OpenAPI JSON and `schema.d.ts` after API contract changes.
- `tools/build.sh` — lint + tests, then Release build + frontend build with chunk sizes.
- `tools/progress.sh [plan-file]` — plan task status from the SDD ledger (`.superpowers/sdd/<plan>/progress.md`) + branch, recent commits, uncommitted changes. Default: newest plan.

## Quality gates (must pass before commit)
- **C#:** `TreatWarningsAsErrors=true`, `Nullable=enable` — zero warnings.
- **C# tests gate builds:** `tools/build.sh` runs `tools/test.sh` first; Domain + Application line coverage ≥ 80%.
- **TS/React:** ESLint errors only, `--max-warnings=0`, no `eslint-disable`; frontend line coverage ≥ 80%.
- **Contracts:** after changing an endpoint, run `tools/gen-api-types.sh`; `TranslationCoverageTests` must pass (every error code and audit action has Turkish text).
- No magic numbers/strings — named constants.

## Conventions
- English code, identifiers, DB names, endpoints, URL paths; Turkish only in `frontend/src/shared/i18n/locales/tr/*.json` (ADR-0006).
- New API error → add a constant to `ErrorCodes` and a Turkish text to `errors.json`.
- Cross-row rules (last admin, location cycles, asset lifecycle, import commit, intake team) run inside `IAppDbContext.RunSerializedAsync` with a key from `SerializationLocks`.
- Ticket endpoints load through `TicketScope` (404 outside scope); work actions go through `TicketWork` (scope → version → change → one save). Comments/links touch `updated_at` without the version check.
- Attachments: `AttachmentTypes` (Domain) decides allowed types; storage through `IFileStore`, scanning through `IFileScanner`.
- `users` has an application-managed `version` (bump only on admin-editable changes via `BumpVersion`); other tables use `xmin`.
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
