# Phase 9 — Maintenance calendar, window conflicts and change/task reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IT staff see planned changes on a maintenance calendar, get a non-blocking warning when two active changes overlap on an asset or service, find this week's changes from İş listem, and read change and task reports with drill-down and Excel export.

**Architecture:** Conflicts are computed on read by one Application service (`ChangeConflicts`) and surfaced on the change detail, the change approvals list and the calendar; nothing is stored and nothing is blocked. The calendar is one read endpoint over `ChangeScope.Visible`. Change and task reports extend the existing report framework (ADR-0020) the way the asset reports do: a metric base class, a scope method and a drill-down kind per source, so figures, drill-down rows and exports keep sharing one filtered query.

**Tech Stack:** .NET 10, EF Core + Npgsql, FluentValidation, Minimal APIs; React 19, TanStack Query, Astryx 0.7, Chart.js (existing `DistributionChart`); xUnit (Microsoft.Testing.Platform), Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-12-task-and-change-management-design.md` (sections Maintenance window conflicts, Screens, Phases F3, Acceptance A9 and Regressions). Patterns: Phase 8 plan `docs/superpowers/plans/2026-11-13-phase-8-changes.md`; report framework `backend/src/Tgysm.Application/Reports/` (ADR-0020).

## Global Constraints

- Conflict = two changes, both in `PendingApproval`, `Scheduled` or `Implementing`, whose windows overlap **and** that share an affected asset or the same affected-service text. A warning only, never a block (spec: "no CMDB").
- Windows are half-open: `[start, end)`. Overlap = `a.Start < b.End && b.Start < a.End`; touching windows do not conflict.
- Same affected service = equal `TurkishText.ToSearchKey(...)` of non-empty texts (so "Mail", "mail " and "MAİL" match).
- The calendar hides out-of-scope changes (A9): it lists only `ChangeScope.Visible(viewer)`. A conflict with an out-of-scope change still warns but shows no number or title.
- Calendar fields are the summary fields only: number, title, status, risk, window (summary viewers may see them, ADR-0031).
- Weeks and days are company time (`ICompanyClock.Zone`, `App:TimeZone`); a week starts Monday 00:00.
- Reports: admins see all; IT staff see changes whose `OwnerTeamId` and tasks whose `TeamId` is one of their teams (same rule as ticket reports); employees 403. Figure, drill-down and export use the same filtered query.
- No new tables, no migration. Employees 404 on every new change route.
- Every API change: `tools/gen-api-types.sh`; every new error code has Turkish text (`TranslationCoverageTests`); every export header has Turkish text (`ExportHeadersCoverageTests`); no magic numbers/strings.
- UI follows `frontend/CLAUDE.md` (Astryx components, `Icons` map, URL filter state, one primary action per group); new code stays in the `feature-changes`, `feature-tasks` and `feature-reports` chunk groups.
- Gates per task: backend tasks `tools/test.sh`; frontend-only tasks `tools/lint.sh` + `cd frontend && npx vitest run <folders>`; full `tools/test.sh` once at the end. Before merge: `tools/e2e.sh` (stop dev services, restart after), `tools/ui-qa.sh`, green CI.

## Review Focus

- A conflict with a change the viewer cannot see: the warning shows ("Görmediğiniz bir değişiklikle çakışıyor"), the other change's number and title never leave the server. Test in Task 1.
- Touching windows (one ends 10:00, the next starts 10:00) are not a conflict; a one-minute overlap is. Test in Task 1.
- Affected-service texts that differ only in case, spacing or `ı`/`I`/`İ` conflict; an empty or missing service never matches another empty one. Test in Task 1.
- A window across the week boundary or the DST-free company zone: "Bu hafta" counts a change starting Sunday 23:30 company time in that week, not one starting Monday 00:00 of the next. Test in Task 2.
- Invalid calendar ranges (`to` before `from`, more than 62 days, missing dates) answer 400 `changes.calendar_range`, never 500. Test in Task 2.

---

### Task 1: Window conflict detection

**Files:**
- Create: `backend/src/Tgysm.Domain/Changes/ChangeWindow.cs`, `backend/src/Tgysm.Application/Features/Changes/ChangeConflicts.cs`
- Modify: `ChangeDtos.cs` (`ChangeDetail` gains `Conflicts`), `GetChange.cs`, `ListChangeApprovals.cs` (`ChangeApprovalItem` gains `ConflictCount`), DI registration next to `ChangeScope`, then `tools/gen-api-types.sh`
- Test: `backend/tests/Tgysm.Domain.Tests/Changes/ChangeWindowTests.cs`, `backend/tests/Tgysm.Api.IntegrationTests/Changes/ChangeConflictTests.cs`

**Interfaces — Produces:**
- `public static class ChangeWindow { public static readonly ChangeStatus[] ConflictStatuses; public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd); public static bool SameService(string? a, string? b); }`
- `public enum ChangeConflictReason { Asset, Service }` (`Asset` wins when both apply).
- `public sealed record ChangeConflict(string? Number, string? Title, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, ChangeConflictReason Reason);` — `Number`/`Title` null when the other change is outside `ChangeScope.Visible(viewer)`.
- `public sealed class ChangeConflicts(IAppDbContext db, ChangeScope scope) { Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChangeConflict>>> FindAsync(IReadOnlyCollection<Guid> changeIds, ChangeViewer viewer, CancellationToken ct); }` — only subjects in `ConflictStatuses` with a window get entries; others map to an empty list.
- `ChangeDetail.Conflicts : IReadOnlyList<ChangeConflict>` (empty for summary access); `ChangeApprovalItem.ConflictCount : int`.

- [ ] **Step 1:** Domain tests `ChangeWindowTests`: `Overlaps` false for 08:00–10:00 vs 10:00–12:00, true for 08:00–10:01 vs 10:00–12:00; `SameService("Mail ", "MAİL")` true, `SameService("Mail", "Maıl")` true, `SameService(null, null)` false, `SameService("", " ")` false; `ConflictStatuses` equals `[PendingApproval, Scheduled, Implementing]`.
- [ ] **Step 2:** Integration tests `ChangeConflictTests`: two scheduled changes sharing one asset with overlapping windows → each detail lists the other with reason `asset`; same service text, no shared asset → reason `service`; one `draft` and one `scheduled` overlapping → no conflict on either; touching windows → none; the other change owned by a team the viewer is not in and with no task for the viewer's team → one conflict with `number` and `title` null; `GET /change-approvals` row has `conflictCount = 1`; summary-access detail has an empty `conflicts`.
- [ ] **Step 3:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeWindowTests" --filter-class "*ChangeConflictTests"` — Expected: FAIL (types missing).
- [ ] **Step 4:** Implement. `FindAsync` loads candidates in one query (other changes in `ConflictStatuses`, overlapping any subject window, with their asset ids from `change_assets`), then matches assets and service keys in memory; visibility of each candidate through `scope.Visible(viewer)` ids.
- [ ] **Step 5:** Run `tools/gen-api-types.sh`, then `tools/test.sh` — Expected: PASS. Commit `feat(changes): window conflicts on the change detail and in change approvals`.

### Task 2: Calendar endpoint and "this week" queue

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Changes/GetChangeCalendar.cs`
- Modify: `ChangeEndpoints.cs` (`GET /changes/calendar`), `ListChanges.cs` (`ChangeQueue.ThisWeek`), `backend/src/Tgysm.Application/Common/ErrorCodes.cs` (`ChangeCalendarRange = "changes.calendar_range"`), `frontend/src/shared/i18n/locales/tr/errors.json`, then `tools/gen-api-types.sh`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Changes/ChangeCalendarTests.cs` (calendar and this-week cases)

**Interfaces — Consumes:** `ChangeConflicts.FindAsync` (Task 1), `ChangeScope.Visible`, `ICompanyClock.Zone`.

**Interfaces — Produces:**
- `GET /api/v1/changes/calendar?from=yyyy-MM-dd&to=yyyy-MM-dd` (company days, both inclusive, at most `ChangeCalendarLimits.MaxDays = 62`) → `IReadOnlyList<ChangeCalendarItem>` ordered by `WindowStart`, then number.
- `public sealed record ChangeCalendarItem(string Number, string Title, ChangeStatus Status, Level Risk, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, bool HasConflict);` — visible changes with a window overlapping the range, any status except `Draft` and `Cancelled`.
- `ChangeQueue.ThisWeek` (`queue=this_week`): status `Scheduled` or `Implementing` and `WindowStart` within the current company week `[Monday 00:00, next Monday 00:00)`.

- [ ] **Step 1:** Failing tests: calendar lists a visible scheduled change and hides an out-of-scope one (A9); a draft and a cancelled change are not listed; a window starting before `from` and ending inside the range is listed; `hasConflict` true for the two conflicting changes of Task 1's setup; `to < from`, 63 days and a missing `from` each answer 400 `changes.calendar_range`; employee 404; `queue=this_week` with a test clock on Wednesday counts a change starting Sunday 23:30 company time and excludes one starting next Monday 00:00 and a `pending_approval` one; `TranslationCoverageTests` fails until the Turkish text exists.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeCalendarTests" --filter-class "*TranslationCoverageTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement; map the route before `{number}` and with `ProducesProblem` 400/404; validation errors go through the existing FluentValidation filter.
- [ ] **Step 4:** Run `tools/gen-api-types.sh`, then `tools/test.sh` — Expected: PASS. Commit `feat(changes): maintenance calendar endpoint and this-week queue`.

### Task 3: Frontend — Bakım takvimi

**Files:**
- Create: `frontend/src/features/changes/{ChangeCalendarPage.tsx,calendarRange.ts}` + `ChangeCalendarPage.test.tsx`, `calendarRange.test.ts`
- Modify: `frontend/src/features/changes/api.ts` (`useChangeCalendar(from, to)`), `frontend/src/shared/routes.ts` (`changeCalendar: '/changes/calendar'`), `frontend/src/shared/navigation/routeMeta.ts` (`changeCalendar`, parent `changes`), `ChangeListPage.tsx` (secondary action "Bakım takvimi"), `tr/changes.json`, `tr/common.json` (route title)

**Interfaces — Consumes:** `GET /changes/calendar` (Task 2).

**Interfaces — Produces:** `monthRange(month: string): { from: string; to: string }` and `weekRange(day: string): { from: string; to: string }` in `calendarRange.ts` (Monday-first, `yyyy-MM-dd`).

- [ ] **Step 1:** Bootstrap per `frontend/CLAUDE.md`; run `npx astryx search "calendar"` and use an Astryx calendar component if one exists, else a `Grid` month view; read `Token`, `List`/`Item`, `SegmentedControl` (or the project's view switch) with `--dense`.
- [ ] **Step 2:** Failing tests: `monthRange('2026-11')` → `2026-10-26`..`2026-12-06` (whole Monday-first weeks); `weekRange('2026-11-18')` → `2026-11-16`..`2026-11-22`; the page reads `view` (`month` default, `week`) and `date` from the URL (default today in company time) and "Önceki"/"Sonraki"/"Bugün" move by one month or week; wide screens show the month grid or a seven-day week view with each change on its window days, phones (below 768 px) show an agenda list grouped by day; a change with `hasConflict` shows a "Çakışma" token with the warning icon; each entry links to `/changes/:number` with `returnTo`; empty month shows "Bu ayda planlı değişiklik yok."; route reachable from the list's "Bakım takvimi" button.
- [ ] **Step 3:** Run `cd frontend && npx vitest run src/features/changes` — Expected: FAIL.
- [ ] **Step 4:** Implement.
- [ ] **Step 5:** Run the tests and `tools/lint.sh` — Expected: PASS. Commit `feat(frontend): maintenance calendar with month, week and agenda views`.

### Task 4: Frontend — conflict warnings and "Bu hafta uygulanacak"

**Files:**
- Create: `frontend/src/features/changes/ChangeConflictBanner.tsx` + test
- Modify: `ChangeDetailPage.tsx` (banner above Plan), `frontend/src/features/approvals/ChangeApprovalRow.tsx` (conflict token), `ChangeQueueCard.tsx` (accept a `queue` prop) or a new `ThisWeekCard.tsx`, `frontend/src/features/workspace/WorkspacePage.tsx`, `ChangeListPage.tsx` (queue option `this_week`), `tr/changes.json`
- Test: `ChangeDetailPage.test.tsx`, `ApprovalsPage.test.tsx`, `WorkspacePage.test.tsx`, `ChangeListPage.test.tsx`

**Interfaces — Consumes:** `ChangeDetail.conflicts`, `ChangeApprovalItem.conflictCount` (Task 1), `queue=this_week` (Task 2).

- [ ] **Step 1:** Failing tests: detail with two conflicts shows a warning `Banner` "Bakım penceresi 2 değişiklikle çakışıyor" listing each visible one as a link with its window and reason ("Ortak varlık" / "Aynı hizmet"), and a hidden one as "Görmediğiniz bir değişiklik"; no banner for an empty list; Onaylar change row shows "1 çakışma"; İş listem shows the card "Bu hafta uygulanacak" with the `this_week` total and links to `/changes?queue=this_week`; the change list offers the queue "Bu hafta uygulanacak".
- [ ] **Step 2:** Run `cd frontend && npx vitest run src/features/changes src/features/approvals src/features/workspace` — Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the tests and `tools/lint.sh` — Expected: PASS. Commit `feat(frontend): window conflict warnings and the this-week card`.

### Task 5: Change report (backend)

**Files:**
- Create: `backend/src/Tgysm.Application/Reports/Metrics/ChangeMetrics.cs`, `backend/src/Tgysm.Application/Reports/ChangeReportSource.cs`
- Modify: `backend/src/Tgysm.Domain/Reports/ReportName.cs` (`Changes`), `ReportMetric.cs` (`ChangeReportMetric`), `ReportCatalog.cs`, `ReportScope.cs` (`ChangesFor`), `ReportRowSource.cs` (`DrilldownKind.Changes`, `DrilldownChangeRow`), `GetReport.cs`, `GetDrilldown.cs` (`DrilldownResult.Changes`), `ReportExportRows.cs` (`ExportColumns.Changes`), `backend/src/Tgysm.Infrastructure/Reports/ExportHeaders.tr.json`, `ReportFilterBinder.cs` if a key is new, then `tools/gen-api-types.sh`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Reports/ChangeReportTests.cs`, `ReportScopeTests.cs`, `ReportConsistencyTests.cs`, `ExportHeadersCoverageTests` (existing)

**Interfaces — Produces:**
- `ReportName.Changes` (route value `changes`). Filters: `from`, `to`, `teamId` (owner team); other keys ignored like on the asset report.
- `public abstract class ChangeReportMetric : IReportMetric` with `IQueryable<ChangeRequest> Rows(IQueryable<ChangeRequest> filtered, ReportContext context)`, optional `BucketRows`, `DenominatorRows`, `ComputeAsync` — same shape as `ReportMetric` over `ChangeRequest`.
- Metrics (`ChangeMetrics`): `by_outcome` (closed in the period, buckets `successful`/`rolled_back`/`failed`, date basis `ClosedAt`, set on close and on cancel), `success_rate` (successful ÷ closed in the period, percent with numerator and denominator drill-down), `by_risk` (created in the period, buckets `low`/`medium`/`high`), `cancelled` (status `Cancelled` with `ClosedAt` in the period, count).
- `ReportScope.ChangesFor(ReportViewer viewer) : IQueryable<ChangeRequest>` — admin all; IT staff `OwnerTeamId ∈ TeamIds`.
- `public sealed record DrilldownChangeRow(string Number, string Title, ChangeStatus Status, ChangeOutcome? Outcome, Level Risk, string OwnerTeamName, DateTimeOffset? WindowStart, DateTimeOffset? ActualEnd);`
- `ExportColumns.Changes = [number, title, status, outcome, risk, team, window_start, actual_end]` with Turkish headers.

- [ ] **Step 1:** Failing tests: `GET /reports/changes` figures for a seeded world (2 successful, 1 failed, 1 cancelled in the period, 1 closed before it) → `by_outcome` 2/0/1, `success_rate` 66.7 % (2/3), `cancelled` 1; an IT staff member of another team sees zeros; employee 403; drill-down of `by_outcome` bucket `failed` returns the one row; the export of the same drill-down has the same rows and Turkish headers; `ReportConsistencyTests` includes the change report (figure = drill-down count = export row count).
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeReportTests" --filter-class "*ReportScopeTests" --filter-class "*ReportConsistencyTests" --filter-class "*ExportHeadersCoverageTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement following the `AssetReportMetric` path in `GetReport`, `ReportRowSource.Open`, `GetDrilldown` and `ReportExportRows` (a `ChangeReportMetric` branch next to the asset branch); background export (`RunExportJobs`) works through `ReportRowSource` unchanged.
- [ ] **Step 4:** Run `tools/gen-api-types.sh`, then `tools/test.sh` — Expected: PASS. Commit `feat(reports): change report with outcomes, success rate, risk and drill-down`.

### Task 6: Task report (backend)

**Files:**
- Create: `backend/src/Tgysm.Application/Reports/Metrics/TaskMetrics.cs`
- Modify: `ReportName.cs` (`Tasks`), `ReportMetric.cs` (`TaskReportMetric`), `ReportCatalog.cs`, `ReportScope.cs` (`TasksFor`), `ReportRowSource.cs` (`DrilldownKind.Tasks`, `DrilldownTaskRow`), `GetReport.cs`, `GetDrilldown.cs`, `ReportExportRows.cs` (`ExportColumns.Tasks`), `ExportHeaders.tr.json`, then `tools/gen-api-types.sh`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Reports/TaskReportTests.cs`, `ReportScopeTests.cs`, `ReportConsistencyTests.cs`

**Interfaces — Consumes:** the branch pattern Task 5 added (`ChangeReportMetric` beside `AssetReportMetric`).

**Interfaces — Produces:**
- `ReportName.Tasks` (route value `tasks`). Filters: `from`, `to`, `teamId`.
- `TaskReportMetric` over `WorkTask`, same shape as `ChangeReportMetric`.
- Metrics (`TaskMetrics`): `overdue` (snapshot: open — `open`, `in_progress`, `on_hold` — with `DueDate` before today in company time; ignores the period), `by_status` (created in the period, buckets per `WorkTaskStatus`), `completion_time` (status `Completed` with `ClosedAt` in the period; median and average hours from `CreatedAt` to `ClosedAt` through `DurationStats`), `by_team` (created in the period, buckets per team).
- `ReportScope.TasksFor(ReportViewer viewer) : IQueryable<WorkTask>` — admin all; IT staff `TeamId ∈ TeamIds`.
- `public sealed record DrilldownTaskRow(string Number, string Title, WorkTaskStatus Status, Priority Priority, string TeamName, string? AssigneeName, DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt);`
- `ExportColumns.Tasks = [number, title, status, priority, team, assignee, due_date, created_at, completed_at]` (`completed_at` reuses the ticket column key and its header, filled from `ClosedAt`).

- [ ] **Step 1:** Failing tests: seeded tasks (2 overdue open, 1 due today, 1 completed after 4 h, 1 after 8 h) → `overdue` 2, `completion_time` median 6 h; another team's IT staff sees zeros; employee 403; drill-down `overdue` lists the two; export rows equal drill-down rows; consistency test includes the task report.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*TaskReportTests" --filter-class "*ReportScopeTests" --filter-class "*ReportConsistencyTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `tools/gen-api-types.sh`, then `tools/test.sh` — Expected: PASS. Commit `feat(reports): task report with overdue, status, completion time and team`.

### Task 7: Frontend — Değişiklikler and Görevler report tabs

**Files:**
- Modify: `frontend/src/features/reports/{api.ts,useReportFilter.ts,period.ts,ReportsPage.tsx,DrilldownPage.tsx,formatMetric.ts}`, `tr/reports.json`
- Test: `ReportsPage.test.tsx`, `DrilldownPage.test.tsx`, `useReportFilter.test.ts`

**Interfaces — Consumes:** `ReportName` `changes`/`tasks`, `DrilldownResult.changes`/`.tasks` (Tasks 5–6).

- [ ] **Step 1:** Failing tests: `REPORT_NAMES` ends with `changes`, `tasks`; tabs "Değişiklikler" and "Görevler" show; their filter bar offers only the period and Ekip (filters for other reports are dropped from the URL on tab change, as today); figure cards render the four metrics each with Turkish titles; drill-down of a change metric shows columns Numara, Başlık, Durum, Sonuç, Risk, Ekip, Pencere başlangıcı, Gerçek bitiş, and a task drill-down Numara, Başlık, Durum, Öncelik, Ekip, Atanan, Bitiş tarihi; the export button works for both.
- [ ] **Step 2:** Run `cd frontend && npx vitest run src/features/reports` — Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the tests and `tools/lint.sh` — Expected: PASS. Commit `feat(frontend): change and task report tabs with drill-down and export`.

### Task 8: E2E and phase exit

**Files:**
- Create: `frontend/e2e/calendar.spec.ts`; Modify: `frontend/e2e/world.ts` (helpers to create two scheduled changes on one asset), `frontend/e2e/reports.spec.ts` (change report drill-down + export), `frontend/e2e/qa-screens.spec.ts` (calendar, change report)

- [ ] **Step 1:** Write E2E: two changes on one asset with overlapping windows are approved by a second admin; the owner sees the conflict banner on both details and both entries marked on the calendar in month view (desktop) and agenda view (phone); a member of an unrelated team does not see them on the calendar; İş listem "Bu hafta uygulanacak" opens the filtered list; closing one change as Başarılı makes the change report's `by_outcome` count it, and its drill-down exports.
- [ ] **Step 2:** Stop dev services, run `tools/e2e.sh` — Expected: all specs pass on both viewports; restart with `tools/dev-service.sh start`.
- [ ] **Step 3:** Run `tools/ui-qa.sh` — Expected: no lines in `tmp/ui-qa/problems.txt`.
- [ ] **Step 4:** Run `tools/test.sh` and `tools/lint.sh` — Expected: PASS. Commit `test(e2e): conflicting changes on the calendar and the change report`.

Exit (Faz 9): A9 green; regressions green (six existing reports and exports, report scope and cross-team leak tests, Phase 7–8 task and change tests, work queues); `tools/perf-measure.sh` report and calendar timings within the existing limits; green CI; final whole-branch review.
