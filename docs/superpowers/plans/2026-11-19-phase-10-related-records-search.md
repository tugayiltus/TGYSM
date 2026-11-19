# Phase 10 — Related records (ticket ↔ change links) and central search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IT staff link tickets to changes with a kind (`prompted` / `caused`), see those links on both details and in the change report, and every user finds records from one search box in the top bar within their own visibility.

**Architecture:** One new table `change_ticket_links` with real foreign keys, managed from the change (owner team or admin) through Application handlers that follow `ChangeWork` conventions without touching the change version. Links disclose the other record's summary to IT staff on both sides (owner decision, ADR-0033). Central search is one read endpoint that runs a small query per record type through the existing scope classes and returns grouped hits or an exact-number jump.

**Tech Stack:** .NET 10, EF Core + Npgsql (one migration), FluentValidation, Minimal APIs; React 19, TanStack Query, Astryx; xUnit (Microsoft.Testing.Platform), Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-19-related-records-and-search-design.md` (approved 2026-11-19). Patterns: change asset links `backend/src/Tgysm.Application/Features/Changes/ChangeAssets.cs`, ticket asset links `Features/Tickets/Links.cs`, change report `backend/src/Tgysm.Application/Reports/Metrics/ChangeMetrics.cs` (Phase 9), list search in `ListTickets.cs` / `ListChanges.cs` / `ListTasks.cs`.

## Global Constraints

- Link kinds: `prompted` (ticket led to the change; incidents and requests) and `caused` (change caused the ticket; incidents only). One link per (change, ticket) pair; to change the kind, remove and add again.
- Add/remove: admin or a member of the change's owner team (`ChangeViewer.CanWork`); the ticket is named by number and may belong to any team. Not allowed on a `Cancelled` change (409 `changes.ticket_link_cancelled`); allowed on closed tickets.
- Disclosure (ADR-0033): IT staff who can see either side see the other record's number, title, status (and for a change its window and owner team; for a ticket its kind and team) plus `canOpen`. Opening a record still follows its own scope. Employees never see links: 404 on the ticket-side route.
- A link never changes the change's or ticket's `version`, status or SLA; it writes one audit row per entity (`changes.ticket_linked`/`changes.ticket_unlinked` on the change, `tickets.change_linked`/`tickets.change_unlinked` on the ticket) and one event on each timeline (ticket event internal-only).
- Error codes: `changes.ticket_link_exists` (409), `changes.ticket_link_kind_invalid` (400), `changes.ticket_link_cancelled` (409), `search.query_invalid` (400); Turkish text in `errors.json`.
- `caused_incidents` = changes with status `Closed`, `ClosedAt` in the period, at least one `caused` link; counts changes; report scope and pipeline as the Phase 9 change report.
- Search: `q` trimmed, 2–100 chars (`SearchLimits.MinLength = 2`, `MaxLength = 100`), else 400 `search.query_invalid`; `SearchLimits.PerType = 5`, newest `UpdatedAt` first, `hasMore` per group. Text matches `SearchText` (tickets, changes, tasks, KB) and `NormalizedAssetTag`/`NormalizedSerialNumber` (assets) via `EF.Functions.Like` + `LikePattern.Containing` + `LikePattern.EscapeText`. A full number the viewer may see → `exact`; one it may not see falls through silently.
- Search visibility: tickets `TicketScope.Visible`; changes `ChangeScope.Visible` (full + summary); tasks `TaskScope` visible set; assets staff only; KB `ArticleAccess`. Employees get only `tickets` and `articles` groups (others absent from the response).
- UI Turkish copy (exact): panel "İlgili kayıtlar"; groups "Bu değişikliğe yol açan kayıtlar" / "Bu değişikliğin yol açtığı olaylar"; action "Kayıt bağla"; kind labels `prompted` "Değişikliğe yol açtı", `caused` "Değişiklik sonucu oluştu"; ticket panel "İlgili değişiklikler"; not-openable row "{{team}} ekibinin kaydı"; report figure "Sorun çıkaran değişiklikler"; search placeholder "Ara", empty "Sonuç bulunamadı", group link "Tümünü gör".
- Search box: debounce `SEARCH_DEBOUNCE_MS = 300`; `/` focuses it when no input has focus; arrows/Enter/Esc keyboard navigation.
- Every API change: `tools/gen-api-types.sh`; `TranslationCoverageTests` and `ExportHeadersCoverageTests` green; no magic numbers/strings; English code, Turkish only in locale JSON.
- Gates per task: backend tasks `tools/lint.sh` + `tools/test.sh`; frontend-only tasks `tools/lint.sh` + `cd frontend && npx vitest run <folders>`. Before merge: `tools/e2e.sh` (stop dev services, restart after), `tools/ui-qa.sh`, green CI.

## Review Focus

- A ticket from another team linked by the change owner team: the ticket's team sees the change number/title on "İlgili değişiklikler" with `canOpen = false`, and opening the change still answers 404. Test in Task 2.
- An employee who requested the ticket never learns a change exists: `GET /tickets/{n}/changes` 404 for them, ticket detail payload unchanged. Test in Task 2.
- `caused` on a service request → 400 `changes.ticket_link_kind_invalid`; the same pair linked twice → 409, never a 500 from the unique index (race: two adds at once → one 409). Test in Task 2.
- A search for `İnternet`/`internet`/`INTERNET` finds the same tickets; a query with `%` or `_` matches them literally. Test in Task 4.
- A full number of a record the viewer may not see returns no `exact` and no hint (same response shape as a miss). Test in Task 4.

---

### Task 1: Link entity and migration

**Files:**
- Create: `backend/src/Tgysm.Domain/Changes/ChangeTicketLink.cs`, `backend/src/Tgysm.Infrastructure/Persistence/Configurations/ChangeTicketLinkConfiguration.cs`, migration via `tools/add-migration.sh AddChangeTicketLinks`
- Modify: `ChangeEnums.cs` (`ChangeTicketLinkKind`), `ErrorCodes.cs`, `IAppDbContext` + `AppDbContext` (`DbSet<ChangeTicketLink> ChangeTicketLinks`), `frontend/src/shared/i18n/locales/tr/errors.json`
- Test: `backend/tests/Tgysm.Domain.Tests/Changes/ChangeTicketLinkTests.cs`

**Interfaces — Produces:**
- `public enum ChangeTicketLinkKind { Prompted, Caused }` (snake_case in DB/JSON: `prompted`, `caused`; check constraint).
- `public sealed class ChangeTicketLink { Guid Id; Guid ChangeId; Guid TicketId; ChangeTicketLinkKind Kind; Guid CreatedById; DateTimeOffset CreatedAt; public static ChangeTicketLink Create(Guid changeId, Guid ticketId, TicketKind ticketKind, ChangeTicketLinkKind kind, Guid createdById, DateTimeOffset now); }` — throws `DomainException(ErrorCodes.ChangeTicketLinkKindInvalid)` for `Caused` + `TicketKind.Request`.
- Table `change_ticket_links`: FKs Restrict to `change_requests`, `tickets`, `users`; unique (`change_id`, `ticket_id`) named `ux_change_ticket_links_change_ticket`; index `ix_change_ticket_links_ticket_id`.
- `ErrorCodes.ChangeTicketLinkExists = "changes.ticket_link_exists"`, `ChangeTicketLinkKindInvalid = "changes.ticket_link_kind_invalid"`, `ChangeTicketLinkCancelled = "changes.ticket_link_cancelled"`, `SearchQueryInvalid = "search.query_invalid"` (all four now, so Task 4 reuses it).

- [ ] **Step 1:** Domain tests: `Create` with `Prompted` for incident and request succeeds; `Caused` for incident succeeds; `Caused` for request throws with `changes.ticket_link_kind_invalid`.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeTicketLinkTests"` — Expected: FAIL (types missing).
- [ ] **Step 3:** Implement entity, enum, configuration, DbSet, error codes + Turkish texts; `tools/add-migration.sh AddChangeTicketLinks`; check the migration creates only that table, its constraints and indexes.
- [ ] **Step 4:** `tools/lint.sh` and `tools/test.sh` — Expected: exit 0. Commit `feat(changes): change ticket link entity and table`.

### Task 2: Link API on changes and tickets

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Changes/ChangeTicketLinks.cs` (handlers), `backend/tests/Tgysm.Api.IntegrationTests/Changes/ChangeTicketLinkTests.cs`
- Modify: `ChangeEndpoints.cs`, `TicketEndpoints.cs`, `AuditActions.cs`, `ChangeEventKind` (`TicketLinked`, `TicketUnlinked`), `TicketEventKind` (`ChangeLinked`, `ChangeUnlinked`), Turkish audit/timeline texts (`TranslationCoverageTests`), DI, then `tools/gen-api-types.sh`

**Interfaces:**
- Consumes: Task 1 entity and error codes; `ChangeScope`, `ChangeViewer.CanWork`, `TicketScope`, `TicketNumber.Normalize`, `ChangeHistory.Record`, `TicketTimeline.Record` (visibility internal).
- Produces:
  - `public sealed record LinkedTicketItem(string Number, string Title, TicketKind Kind, TicketStatus Status, string TeamName, ChangeTicketLinkKind LinkKind, DateTimeOffset LinkedAt, string LinkedByName, bool CanOpen);`
  - `public sealed record LinkedChangeItem(string Number, string Title, ChangeStatus Status, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string OwnerTeamName, ChangeTicketLinkKind LinkKind, DateTimeOffset LinkedAt, bool CanOpen);`
  - `GET /api/v1/changes/{number}/tickets` → `LinkedTicketItem[]` (full or summary access; else 404); `POST /api/v1/changes/{number}/tickets` body `{ ticketNumber, kind }` → 201 `LinkedTicketItem[]`; `DELETE /api/v1/changes/{number}/tickets/{ticketNumber}` → 200 `LinkedTicketItem[]`; `GET /api/v1/tickets/{number}/changes` → `LinkedChangeItem[]` (staff who can see the ticket; employees 404). Lists ordered by `LinkedAt` descending.
  - Audit actions `ChangeTicketLinked = "changes.ticket_linked"`, `ChangeTicketUnlinked = "changes.ticket_unlinked"`, `TicketChangeLinked = "tickets.change_linked"`, `TicketChangeUnlinked = "tickets.change_unlinked"`.

- [ ] **Step 1:** Integration tests `ChangeTicketLinkTests`: owner team links its own incident as `caused` → change list shows it with `canOpen = true`, audit rows on both entities, an event on each timeline, change `version` unchanged; owner team links another team's incident → 201, that ticket's team gets it on `GET /tickets/{n}/changes` with `canOpen = false` and `GET /changes/{n}` still 404 for them; same pair again → 409 `changes.ticket_link_exists`; two parallel adds of one pair → one 201 and one 409; `caused` on a request → 400; cancelled change → 409 `changes.ticket_link_cancelled`; closed ticket → 201; non-owner staff POST → 403 or 404 by existing change rules; unknown ticket number → 404; employee requester `GET /tickets/{n}/changes` → 404 and ticket detail JSON has no link field; DELETE removes and records unlink audits; ticket timeline event for the requester is not visible (internal).
- [ ] **Step 2:** Run `--filter-class "*ChangeTicketLinkTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement. Add runs inside `RunSerializedAsync` with a new `SerializationLocks` key per change, and maps the unique-index violation to 409 as the second guard. `CanOpen` = `TicketScope` / `ChangeScope` full-or-summary visibility of the other record.
- [ ] **Step 4:** `tools/gen-api-types.sh`, `tools/lint.sh`, `tools/test.sh` — Expected: exit 0. Commit `feat(changes): link tickets to changes as prompted or caused`.

### Task 3: "Sorun çıkaran değişiklikler" report figure

**Files:**
- Modify: `backend/src/Tgysm.Application/Reports/Metrics/ChangeMetrics.cs`, `ReportCatalog.cs`, metric key constants, `ExportHeaders.tr.json` if a new label key is needed, `frontend/src/shared/i18n/locales/tr/reports.json` (`metrics.caused_incidents` = "Sorun çıkaran değişiklikler")
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Reports/ChangeReportTests.cs`, `ReportConsistencyTests.cs`

**Interfaces — Produces:** metric key `caused_incidents` (count shape) on the `changes` report; drill-down `metric=caused_incidents` lists those changes with the existing `DrilldownChangeRow`.

- [ ] **Step 1:** Tests: three changes closed in the period, one with a `caused` link and one with only `prompted` → figure 1; a cancelled change with a `caused` link → not counted; a change closed outside the period with `caused` → not counted; drill-down lists exactly the one; export rows equal drill-down rows; another team's IT staff sees 0; consistency test covers the new metric.
- [ ] **Step 2:** Run `--filter-class "*ChangeReportTests" --filter-class "*ReportConsistencyTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement as a `ChangeReportMetric` on the `ClosedAt` basis with `Status == Closed` and `EXISTS` on `ChangeTicketLinks` with `Kind == Caused`.
- [ ] **Step 4:** `tools/gen-api-types.sh` if the contract changed, `tools/lint.sh`, `tools/test.sh` — Expected: exit 0. Commit `feat(reports): changes that caused incidents in the change report`.

### Task 4: Central search endpoint

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Search/Search.cs` (query, handler, `SearchLimits`, DTOs), `backend/src/Tgysm.Api/Endpoints/SearchEndpoints.cs`, `backend/tests/Tgysm.Api.IntegrationTests/Search/SearchTests.cs`
- Modify: endpoint registration, DI, then `tools/gen-api-types.sh`

**Interfaces — Produces:**
- `public enum SearchHitType { Ticket, Change, Task, Asset, Article }`
- `public sealed record SearchHit(SearchHitType Type, string Key, string Title, string? Status);` — `Key` = number, asset tag, or article id/slug the existing KB route uses.
- `public sealed record SearchGroup(SearchHitType Type, IReadOnlyList<SearchHit> Hits, bool HasMore);`
- `public sealed record SearchResult(SearchHit? Exact, IReadOnlyList<SearchGroup> Groups);` — groups only for types the viewer may search, in enum order, empty groups omitted.
- `GET /api/v1/search?q=` → `SearchResult`; 400 `search.query_invalid`.
- `public static class SearchLimits { public const int MinLength = 2; public const int MaxLength = 100; public const int PerType = 5; }`

- [ ] **Step 1:** Integration tests `SearchTests`: desk staff finds own-team ticket by a title word, not another team's ticket; `İNTERNET`, `internet`, `İnternet` give the same hits; `q` with `%` and `_` matches literally; full ticket number of a visible ticket → `exact` set; of an invisible ticket → `exact` null and response equals a miss; `CHG-` and `TSK-` exact numbers work; asset found by tag fragment and by serial for staff; employee gets only `ticket` and `article` groups (own tickets, readable articles only — an internal article absent); summary-access change found by title; 6 matching tickets → 5 hits + `hasMore`; `q` of 1 char and of 101 chars → 400; whitespace-only → 400.
- [ ] **Step 2:** Run `--filter-class "*SearchTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement. Normalize `q` with `TurkishText.ToSearchKey`; detect full numbers with the existing number normalizers (`TicketNumber`, change/task number classes, asset tag normalizer); run the five queries sequentially on the request's `DbContext`, each `Take(PerType + 1)`.
- [ ] **Step 4:** `tools/gen-api-types.sh`, `tools/lint.sh`, `tools/test.sh` — Expected: exit 0. Commit `feat(search): central search across tickets, changes, tasks, assets and articles`.

### Task 5: Link panels on change and ticket details

**Files:**
- Create: `frontend/src/features/changes/ChangeTicketLinks.tsx` (+ test), `frontend/src/features/tickets/TicketChangeLinks.tsx` (+ test)
- Modify: `features/changes/api.ts`, `features/tickets/api.ts`, `ChangeDetailPage.tsx`, `TicketDetailPage.tsx`, `changes.json`, `tickets.json`, `reports.json` if Task 3 label not yet there

**Interfaces — Consumes:** Task 2 routes and DTOs (generated `schema.d.ts`).

- [ ] **Step 1:** Tests: change detail shows "İlgili kayıtlar" with the two groups and kind labels; owner sees "Kayıt bağla", a non-owner (summary view) does not; the bind dialog sends `{ ticketNumber, kind }` and shows the 409/400 errors in Turkish; remove asks for confirmation and refreshes; a row with `canOpen = false` has no link and shows "{{team}} ekibinin kaydı"; ticket detail for IT shows "İlgili değişiklikler", for an employee renders no panel and makes no request.
- [ ] **Step 2:** Run `cd frontend && npx vitest run src/features/changes src/features/tickets` — Expected: FAIL.
- [ ] **Step 3:** Implement with Astryx components per `frontend/CLAUDE.md`; mutations invalidate the change and ticket link queries and the change timeline.
- [ ] **Step 4:** `tools/lint.sh` and the vitest folders — Expected: exit 0. Commit `feat(frontend): related records on the change and ticket details`.

### Task 6: Search box in the top bar

**Files:**
- Create: `frontend/src/features/search/` (`api.ts`, `GlobalSearch.tsx`, `GlobalSearch.test.tsx`, `searchPaths.ts`), `frontend/src/shared/i18n/locales/tr/search.json` (+ i18n registration)
- Modify: `frontend/src/shared/layout/AppHeader.tsx`, `vite.config.ts` (chunk group `feature-search` if the folder rule needs it)

**Interfaces — Consumes:** Task 4 `GET /search`. Produces: `pathForHit(hit: SearchHit): string` in `searchPaths.ts` and the list path with `?search=` for "Tümünü gör".

- [ ] **Step 1:** Tests: typing fires one request after `SEARCH_DEBOUNCE_MS` (fake timers), none for 1 char; grouped results with Turkish group titles; `exact` navigates directly; Enter opens the highlighted hit, arrows move, Esc closes; "Tümünü gör" opens the list with `?search=`; "Sonuç bulunamadı" on empty; `/` focuses the box only when no input has focus; phone width shows an icon that opens a full-width field.
- [ ] **Step 2:** Run `cd frontend && npx vitest run src/features/search src/shared/layout` — Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** `tools/lint.sh` and the vitest folders — Expected: exit 0. Commit `feat(frontend): central search in the top bar`.

### Task 7: ADR, requirements, E2E and phase exit

**Files:**
- Create: `docs/adr/0033-ticket-change-links-and-central-search.md` (template `~/.claude/templates/adr-template.md`; status accepted; Deciders Tugay İLTUŞ; cite Task 1–4 SHAs; records the disclosure exception to ADR-0013 and the rejected generic link table), `frontend/e2e/related-records.spec.ts`
- Modify: `docs/adr/README.md`, `tgysm.md` §2 (scope now includes ticket ↔ change links and central search) and §19 (roadmap row), `frontend/e2e/world.ts` (helpers), `frontend/e2e/ui-qa.spec.ts` (search popover open)

- [ ] **Step 1:** E2E: infra owner links a desk-team incident as `caused` from the change; desk staff sees it on the incident under "İlgili değişiklikler" without a link; closing the change makes "Sorun çıkaran değişiklikler" 1 and its drill-down lists it; search by a change title word shows the change group; typing a full ticket number opens the ticket; both viewports.
- [ ] **Step 2:** Stop dev services, `tools/e2e.sh`, restart with `tools/dev-service.sh start` — Expected: all pass.
- [ ] **Step 3:** `tools/ui-qa.sh` — Expected: no lines in `tmp/ui-qa/problems.txt`.
- [ ] **Step 4:** `tools/lint.sh`, `tools/test.sh`, `tools/check-doc-paths.sh docs/adr/0033-ticket-change-links-and-central-search.md tgysm.md` — Expected: exit 0. Commit `test(e2e): related records and central search; ADR-0033`.

Exit (Faz 10): Review Focus items green; regressions green (change/ticket detail, change report, list searches, Phase 9 calendar); `tools/perf-measure.sh` adds `GET /search` and stays within limits; green CI; final whole-branch review; docs (`capabilities.md`, `README.md`, `docs/status.md`, user guide) updated as the last step.
