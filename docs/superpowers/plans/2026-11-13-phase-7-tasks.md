# Phase 7 — Tasks and the combined work list (F1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IT staff hand work to any team as a task (`TSK-YYYY-NNNNNN`), standalone or from an incident/request, and see tickets and tasks in one İş listem; the ticket model, its SLA and its approval stay unchanged.

**Architecture:** `WorkTask` is its own aggregate and table next to `Ticket` (ADR-0031, extends ADR-0009/0011/0013/0015). `TaskScope`/`TaskWork` copy the `TicketScope`/`TicketWork` frame (scope → version → change → one save → fresh detail) without a shared base. The combined list is one read query that unions visible tickets and visible tasks into one row shape.

**Tech Stack:** .NET 10, EF Core + Npgsql, FluentValidation, Minimal APIs; React 19, TanStack Query, react-hook-form + zod, Astryx 0.7; xUnit (Microsoft.Testing.Platform), Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-12-task-and-change-management-design.md` (sections Architecture, Task, Access, Integrity, Screens, Phases F1, Acceptance A1–A3).

## Global Constraints

- Phase numbering: this plan is Faz 7 in `tgysm.md`; Faz 8 = Change + approval (F2), Faz 9 = calendar + operations (F3).
- Number format `TSK-YYYY-NNNNNN` (6 digits, same rule as `TicketNumber`); counter row `ticket_counters (kind = 'task', year)`.
- States: `Open` → `InProgress` ⇄ `OnHold` (reason) → `Completed` (result note required) | `Cancelled` (reason). Reopen from `Completed`/`Cancelled` to `Open` (F2 adds "while the linked change is not closed").
- Priority P1–P4 (`Priority` enum). Due date optional; overdue = due date passed and state not `Completed`/`Cancelled`. No `SlaInstance`, no SLA reports for tasks.
- Task team is required; assignee optional and must be an active member of the task's team.
- Link: none or one ticket, fixed after creation. Completing a task never changes the linked ticket. Completing a ticket with open tasks warns and is allowed.
- Employees never see tasks; out of scope answers 404 (ADR-0013).
- Concurrency: `xmin` on every new table; stale version → 409; claim is one conditional update on `assignee_id IS NULL`, affected rows decide.
- FKs `Restrict`; nothing is deleted.
- Every API change: `tools/gen-api-types.sh`; every new error code and audit action has Turkish text (`TranslationCoverageTests`); no magic numbers/strings.
- UI follows `frontend/CLAUDE.md` (Astryx components, `Icons` map, URL filter state, one primary action).
- Gates before each commit: `tools/lint.sh`, `tools/test.sh` (coverage ≥ 80 %); before merge `tools/e2e.sh` (stop dev services, restart after) and green CI.

## Review Focus

- A task linked to a ticket the viewer cannot see: the task opens, the link shows "Bağlı kayıt: erişiminiz yok" with no number or title, and the ticket URL answers 404 (A1).
- Two parallel claims or two parallel saves with the same version: exactly one wins, the other gets 409 (A2).
- Completing, cancelling or reopening a task leaves the linked ticket's status, version and `sla_instances` rows untouched (A3).
- An assignee removed from the team, or deactivated, after assignment: transfer and assign refuse a non-member; the task still lists and opens.
- The combined list pages and sorts tickets and tasks together; an employee calling the task endpoints or the task queues gets 404/empty, never task rows.

---

### Task 1: Decision record and phase table

**Files:**
- Create: `docs/adr/0031-tasks-and-changes-as-separate-records.md`
- Modify: `docs/adr/README.md`, `tgysm.md` (Kapsam dışı line for Change Management, phase table rows 7–9, §20 acceptance additions A1–A9 grouped by phase)
- Create (move): `docs/status.md` "Sıradaki" line points at this plan

- [ ] **Step 1:** Write ADR-0031 from `~/.claude/templates/adr-template.md`: status `accepted`, Deciders `Tugay İLTUŞ`; decision = separate `WorkTask`/`ChangeRequest` tables + one combined list; alternatives rejected = new `TicketKind` values, common work-item parent table (pros/cons from the spec); consequences incl. the `OutboxEvent` subject columns.
- [ ] **Step 2:** Update `tgysm.md`: narrow the out-of-scope line to "CAB, standart/acil değişiklik, Release Management"; add phase rows `7 — Görev`, `8 — Değişiklik`, `9 — Takvim/Rapor` with dependency and exit criteria from the spec; add A1–A9 to acceptance.
- [ ] **Step 3:** Run `tools/check-doc-paths.sh docs/adr/0031-tasks-and-changes-as-separate-records.md tgysm.md` — Expected: exit 0.
- [ ] **Step 4:** Commit `docs: ADR-0031 tasks and changes as separate records; phases 7-9 in tgysm.md`.

### Task 2: Domain — `WorkTask`

**Files:**
- Create: `backend/src/Tgysm.Domain/Tasks/WorkTask.cs`, `TaskEnums.cs` (`TaskStatus` → name it `WorkTaskStatus` to avoid `System.Threading.Tasks.TaskStatus`), `TaskNumber.cs`, `TaskEvent.cs`, `TaskAssignment.cs`
- Modify: `backend/src/Tgysm.Domain/Common/ErrorCodes.cs`, `backend/src/Tgysm.Domain/Audit/AuditActions.cs`
- Test: `backend/tests/Tgysm.Domain.Tests/Tasks/WorkTaskTests.cs`, `TaskNumberTests.cs`

**Interfaces — Produces:**
- `WorkTask.Create(string number, string title, string? description, Guid teamId, Guid? assigneeId, Priority priority, DateOnly? dueDate, Guid? ticketId, Guid createdById, DateTimeOffset now)`
- Methods: `Start(now)`, `Hold(HoldReason reason, string? note, now)`, `Resume(now)`, `Complete(string resultNote, now)`, `Cancel(string reason, now)`, `Reopen(now)`, `AssignTo(Guid? assigneeId, now)`, `TransferTo(Guid teamId, TransferReason reason, now)` (clears assignee), `IsOpen`, `IsOverdue(DateOnly today)`.
- `TaskNumber.Prefix = "TSK"`, `Format(int year, long value)`, `IsValid`, `Normalize`.
- `WorkTaskStatus { Open, InProgress, OnHold, Completed, Cancelled }`; `TaskEventKind { Created, Assigned, Transferred, Started, Held, Resumed, Completed, Cancelled, Reopened, Commented }`.
- Error codes `tasks.invalid_transition`, `tasks.result_required`, `tasks.assignee_not_in_team`, `tasks.already_claimed`; audit actions `tasks.created`, `tasks.assigned`, `tasks.transferred`, `tasks.status_changed`.

- [ ] **Step 1:** Write failing tests: every allowed and refused transition (`DomainException` with `tasks.invalid_transition`), `Complete("")` → `tasks.result_required`, `TransferTo` clears the assignee, `IsOverdue` false for closed tasks, `TaskNumber.Format(2026, 7) == "TSK-2026-000007"`.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*WorkTaskTests" --filter-class "*TaskNumberTests"` — Expected: FAIL (types missing).
- [ ] **Step 3:** Implement the domain types; constants for lengths (title 200, note 4000) beside the entity.
- [ ] **Step 4:** Re-run — Expected: PASS. Commit `feat(tasks): WorkTask domain model and TSK numbers`.

### Task 3: Persistence — tables, counter kind, outbox subject

**Files:**
- Create: `backend/src/Tgysm.Infrastructure/Persistence/Configurations/WorkTaskConfigurations.cs`; migration via `tools/add-migration.sh WorkTasks`
- Modify: `AppDbContext.cs`, `IAppDbContext.cs` (`DbSet<WorkTask> WorkTasks`, `TaskEvents`, `TaskAssignments`), `ITicketNumberGenerator.cs` + `TicketNumberGenerator.cs` (add `Task<long> NextAsync(string counterKind, int year, CancellationToken)`; `TicketKind` overload delegates), `OutboxEvent.cs` + `OutboxEventConfiguration.cs` (`SubjectType string?`, `SubjectId Guid?`; `OutboxSubjectTypes.Ticket/Task`), `INotificationOutbox.cs` (overload `AddForSubject(string eventType, string subjectType, Guid subjectId, Guid? userId, data)`)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Persistence/WorkTaskPersistenceTests.cs`, `backend/tests/Tgysm.Domain.Tests/OutboxEventTests.cs`

- [ ] **Step 1:** Failing tests: a saved task round-trips; `work_tasks.number` unique; `ticket_id` FK `Restrict`; two parallel `NextAsync("task", 2026)` return 1 and 2; outbox row keeps subject columns; existing ticket rows still write `ticket_id`.
- [ ] **Step 2:** Run the class filters — Expected: FAIL.
- [ ] **Step 3:** Configure tables `work_tasks`, `task_events`, `task_assignments` (snake_case, `uuid` v7, `xmin`, indexes on `(team_id, status)`, `(assignee_id, status)`, `ticket_id`, trigram on `title` via `TrigramIndex`); add the migration.
- [ ] **Step 4:** `tools/migrate.sh`, re-run tests — Expected: PASS. Commit `feat(tasks): work task tables, task counter and outbox subject`.

### Task 4: Scope, work frame, create, read

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Tasks/TaskScope.cs` (`TaskViewer`, `TaskScope`), `TaskWork.cs`, `TaskDtos.cs`, `TaskQueries.cs`, `CreateTask.cs`, `GetTask.cs`
- Modify: `SerializationLocks.cs` only if a cross-row rule needs it (none expected), DI registration
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Tasks/TaskWorld.cs`, `TaskScopeTests.cs`, `TaskCreationTests.cs`

**Interfaces — Produces:**
- `TaskViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)`: `CanSee(WorkTask)` = admin, team member, assignee or creator; `CanWork(WorkTask)` = admin or member of the task's team.
- `TaskScope.Visible(viewer)`, `LoadAsync(number, ct)`, `LoadForWorkAsync(number, ct)`; employees always 404.
- `TaskWork.RunAsync(string number, uint version, bool requireWorker, Func<WorkTask, TaskViewer, DateTimeOffset, Task> change, ct) → TaskDetail`.
- `TaskDetail` holds `LinkedTicket? Link` where `LinkedTicket(string? Number, string? Title, TicketKind? Kind, bool Accessible)`; inaccessible = number and title null.
- `CreateTaskCommand(string Title, string? Description, Guid TeamId, Guid? AssigneeId, Priority Priority, DateOnly? DueDate, string? TicketNumber)`; ticket-linked creation needs `TicketViewer.CanWork` on that ticket; standalone needs IT staff/admin.

- [ ] **Step 1:** Failing tests: employee 404 on get and create; A1 (team B member sees a task linked to team A's incident, link `Accessible=false`, incident URL 404); creator without team membership still sees but cannot work; assignee outside the team → 409 `tasks.assignee_not_in_team`; ticket-linked create without `CanWork` → 404; create adds `task_events` Created + audit `tasks.created` + `task_assigned` outbox when assignee set.
- [ ] **Step 2:** Run — Expected: FAIL.
- [ ] **Step 3:** Implement scope, work frame, handlers, validators (`internal sealed`).
- [ ] **Step 4:** Run — Expected: PASS. Commit `feat(tasks): task scope, work frame, create and detail`.

### Task 5: Task actions and notifications

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Tasks/AssignTask.cs` (claim, assign, transfer), `TaskStatusActions.cs` (start, hold, resume, complete, cancel, reopen), `TaskComments.cs` (internal note → `task_events` Commented)
- Modify: `NotificationEventTypes.cs` (`TaskAssigned = "task_assigned"`), `RecipientResolver.cs` (assignee; on transfer without assignee: active team members), `NotificationContextLoader.cs` + `TemplateVariables.cs` (task variables: number, title, team, priority, dueDate, link), email template seed migration for `task_assigned` (Turkish subject/body)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Tasks/TaskActionTests.cs`, `TaskConcurrencyTests.cs`, `backend/tests/Tgysm.Api.IntegrationTests/Notifications/TaskNotificationTests.cs`

- [ ] **Step 1:** Failing tests: A2 (two parallel claims: one 200, one 409 `tasks.already_claimed`; same-version double save: one 409); A3 (complete/cancel/reopen leave the linked ticket's status, `xmin` and `sla_instances` unchanged); transfer requires a `TransferReason` and clears the assignee; non-member work → 404/403 per `TicketScope` convention; `task_assigned` resolves the assignee, never the actor; template renders without missing variables.
- [ ] **Step 2:** Run — Expected: FAIL.
- [ ] **Step 3:** Implement; claim = `ExecuteUpdateAsync` with `Where(assignee_id == null && id == x)`, affected rows 0 → 409; other actions through `TaskWork`.
- [ ] **Step 4:** Run — Expected: PASS. Commit `feat(tasks): claim, assign, transfer, status actions and task_assigned email`.

### Task 6: Endpoints, combined work list, ticket hooks

**Files:**
- Create: `backend/src/Tgysm.Api/Endpoints/Tasks/TaskEndpoints.cs`, `backend/src/Tgysm.Application/Features/Work/ListWorkItems.cs`
- Modify: `ApiRoutes.cs`, ticket detail DTO (`OpenTaskCount`, `Tasks` summary list for viewers with `SeesInternal`), `frontend/src/shared/i18n/locales/tr/errors.json`, `audit.json`, then `tools/gen-api-types.sh`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Tasks/TaskEndpointTests.cs`, `Work/WorkListTests.cs`, `Tickets/TicketStatusTests.cs` (open-task count on detail; completion still allowed)

**Interfaces — Produces:**
- `GET/POST /api/v1/tasks`, `GET /api/v1/tasks/{number}`, `POST /api/v1/tasks/{number}/{claim|assign|transfer|start|hold|resume|complete|cancel|reopen|comments}` (body carries `version`).
- `GET /api/v1/tasks` filters: `queue` (`MyTasks`, `TeamTasks`, `Overdue`, `Unassigned`), `status`, `priority`, `teamId`, `search`, `page`, `pageSize`.
- `GET /api/v1/work-items?queue=` → `PagedResult<WorkItemRow>`; `WorkItemRow(string Kind /* incident|request|task */, string Number, string Title, string Status, Priority Priority, DateTimeOffset? DueAt, string? TeamName, string? AssigneeName, DateTimeOffset UpdatedAt)`; queues `AssignedToMe`, `MyTeams`; sort `UpdatedAt` desc, then `Number`.
- `GET /api/v1/tickets/{number}` gains `openTaskCount` and `tasks[]` (`number`, `title`, `status`, `teamName`, `assigneeName`).

- [ ] **Step 1:** Failing tests: endpoint auth (anonymous 401, employee 404/empty), queue filters, paging across both kinds keeps order and total, overdue uses the company time zone date, ticket detail shows `openTaskCount = 1` and completing it still returns 200, `TranslationCoverageTests` fails until Turkish texts exist.
- [ ] **Step 2:** Run — Expected: FAIL.
- [ ] **Step 3:** Implement; the work list builds two projections to `WorkItemRow`, `Concat`, then pages in SQL; search uses `EF.Functions.Like` + `LikePattern`.
- [ ] **Step 4:** Run tests and `tools/gen-api-types.sh` — Expected: PASS, schema updated. Commit `feat(tasks): task API, combined work list and open-task count on tickets`.

### Task 7: Frontend — task list, detail, create

**Files:**
- Create: `frontend/src/features/tasks/{api.ts,TaskListPage.tsx,TaskDetailPage.tsx,TaskActions.tsx,TaskFormDialog.tsx,TaskBadges.tsx}` + tests; `frontend/src/shared/i18n/locales/tr/tasks.json`
- Modify: `frontend/src/shared/routes.ts` (`tasks`, `taskDetail`), route metadata/breadcrumbs, sidebar entry (IT staff/admin only, `Icons` map), `vite.config.ts` (`feature-tasks` chunk group)
- Test: `TaskListPage.test.tsx`, `TaskDetailPage.test.tsx`, `TaskFormDialog.test.tsx`

- [ ] **Step 1:** Run the Astryx bootstrap from `frontend/CLAUDE.md`; read `Table`, `StatusDot`, `Token`, `Dialog`, `Banner` with `npx astryx component <Name> --dense`.
- [ ] **Step 2:** Failing tests: list has toolbar filters kept in the URL; detail shows one primary action per state (Üstlen / Başlat / Tamamla), complete dialog requires the result note; inaccessible link shows "Bağlı kayıt: erişiminiz yok"; 409 shows the stale-version message and refetches.
- [ ] **Step 3:** Run `npm --prefix frontend test -- tasks` — Expected: FAIL.
- [ ] **Step 4:** Implement on shared pieces (`DataList`, `ListToolbar`, `src/shared/form`, `src/shared/feedback`).
- [ ] **Step 5:** Run tests and `tools/lint.sh` — Expected: PASS. Commit `feat(frontend): task list, detail and create`.

### Task 8: Frontend — İş listem cards and ticket Görevler section

**Files:**
- Modify: `frontend/src/features/workspace/{WorkspacePage.tsx,QueueCard.tsx,queuePath.ts}` (cards Görevlerim, Ekibimin görevleri, Geciken görevler; queue page uses the combined list), `frontend/src/features/tickets/{TicketDetailPage.tsx,TicketActions.tsx}` (Görevler section with "Görev ekle"; completion dialog warns "N açık görev var" and still completes), `tr/workspace.json`, `tr/tickets.json`
- Test: `WorkspacePage.test.tsx`, `TicketActions.test.tsx`, `TicketTasks.test.tsx`

- [ ] **Step 1:** Failing tests: new cards show counts and link to their queues; combined list rows show a kind token; the ticket completion dialog shows the warning when `openTaskCount > 0` and submits; "Görev ekle" pre-links the ticket.
- [ ] **Step 2:** Run — Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run tests, `tools/lint.sh`, `tools/test.sh` — Expected: PASS, coverage ≥ 80 %. Commit `feat(frontend): task cards in the work list and tasks on tickets`.

### Task 9: E2E and phase exit

**Files:**
- Create: `frontend/e2e/tasks.spec.ts`; Modify: `frontend/e2e/world.ts` (two teams, members, an incident in team A)
- Modify: `docs/status.md` (Faz 7 row after merge)

- [ ] **Step 1:** Write E2E: a team A worker opens a task from an incident for team B; a team B member sees it under Görevlerim/Ekibimin görevleri, claims, starts and completes it on phone width; the incident URL answers "bulunamadı" for the team B member; the incident stays open.
- [ ] **Step 2:** Stop dev services, run `tools/e2e.sh` — Expected: all specs pass on both viewports; restart with `tools/dev-service.sh start`.
- [ ] **Step 3:** Run `tools/ui-qa.sh` on the new screens — Expected: no new lines in `tmp/ui-qa/problems.txt`.
- [ ] **Step 4:** Commit `test(e2e): task handed from an incident to another team`.

Exit (Faz 7): A1–A3 green; regressions green (ticket approval, SLA monitor and SLA risk queue, work queues, six reports and exports, outbox/email, ticket scope); green CI; final whole-branch review.
