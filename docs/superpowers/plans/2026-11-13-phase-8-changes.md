# Phase 8 — Change management and change approval (F2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IT staff plan normal changes (`CHG-YYYY-NNNNNN`) with a window, three plans, affected assets and phased tasks for any team; a second admin approves before anything runs; the change is implemented, verified and closed as Başarılı, Geri alındı or Başarısız.

**Architecture:** `ChangeRequest` is its own aggregate next to `Ticket` and `WorkTask` (ADR-0031). `ChangeScope`/`ChangeWork` copy the `TaskScope`/`TaskWork` frame. Approval lives in `change_approvals` with the approved plan fingerprint (ADR-0032, new); ticket approvals stay untouched. Change tasks are `WorkTask` rows with `change_id` and a phase; every change state change and every write to a change task runs under one advisory lock, so closing rules and task gating never race.

**Tech Stack:** .NET 10, EF Core + Npgsql, FluentValidation, Minimal APIs; React 19, TanStack Query, react-hook-form + zod, Astryx 0.7; xUnit (Microsoft.Testing.Platform), Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-12-task-and-change-management-design.md` (sections Architecture, Task, Change, Access, Integrity, Screens, Phases F2, Acceptance A4–A8). Phase 7 plan for the patterns this one extends: `docs/superpowers/plans/2026-11-13-phase-7-tasks.md`.

## Global Constraints

- Phase numbering: this plan is Faz 8 in `tgysm.md`. Maintenance calendar, window conflict warnings, "Bu hafta uygulanacak" card and change/task reports are Faz 9 (not here).
- Number format `CHG-YYYY-NNNNNN` (6 digits, same rule as `TaskNumber`); counter row `ticket_counters (kind = 'change', year)`.
- States `ChangeStatus { Draft, PendingApproval, Scheduled, Implementing, Verifying, Closed, Cancelled }` (Taslak, Onay bekliyor, Planlandı, Uygulanıyor, Doğrulama, Kapalı, İptal). Outcome `ChangeOutcome { Successful, RolledBack, Failed }` (Başarılı, Geri alındı, Başarısız) only on `Closed`.
- Risk and impact use the existing `Level` enum (Low, Medium, High). Affected service is free text (≤ 200).
- Task phases `ChangePhase { Preparation, Implementation, Verification, Backout }` (Hazırlık, Uygulama, Doğrulama, Geri dönüş); required when `change_id` is set, null otherwise; check constraint: at most one of `ticket_id`, `change_id`.
- Approval: single stage. Eligible approver = active admin who is neither the author (`created_by_id`) nor the change owner. The approval row is unassigned; any eligible admin decides; one pending row per change (partial unique index). Decisions: approve, reject (reason), return for revision (reason, "Düzeltmeye gönder"); the author withdraws; an edit of a fingerprint field in `Scheduled` voids the approval.
- Plan fingerprint = SHA-256 hex over window start/end, sorted affected asset ids, risk, impact, the three plans and the sorted ids of open-or-completed Uygulama tasks. From `Implementing` on these fields are locked (`changes.plan_locked`).
- Gating: `Implementing` only from `Scheduled`; Uygulama tasks start only while `Implementing`, Doğrulama tasks only while `Verifying`, Hazırlık tasks while `Scheduled`, `Implementing` or `Verifying`, Geri dönüş tasks only after backout started. A change task reopens only while its change is not `Closed`/`Cancelled`.
- Closing: Başarılı only from `Verifying` with every Uygulama and Doğrulama task `Completed`, every Hazırlık task `Completed` or `Cancelled`, verification note, actual start and end; open Geri dönüş tasks are cancelled with reason "Gerekmedi". Geri alındı / Başarısız from `Implementing` or `Verifying` only after backout started, with at least one Geri dönüş task, all of them closed, and a backout note. Cancel (reason) from `Draft`, `PendingApproval`, `Scheduled` cancels the change's open tasks with the same reason.
- Access (ADR-0031): employees 404 everywhere. Full detail: admin, owner team members, the admin who decided or may decide the pending approval. Working summary: members of a team that has a task on the change and those tasks' assignees — number, title, status, window, risk, and the implementation plan (if they have Hazırlık/Uygulama/Doğrulama tasks) or backout plan (Geri dönüş tasks); never approvals, history or other teams' tasks. Acting: owner team members and admins edit and run the change; adding change tasks needs owner team membership; the task may go to any team.
- Concurrency: `xmin` on every new table; stale version → 409 `concurrency.conflict`; a decision is one conditional update on `status = pending` and the approval version; the affected row count decides. Change state changes and change-task writes run inside `IAppDbContext.RunSerializedAsync(SerializationLocks.ChangeWork, …)` (new constant `7_009`).
- Notifications (outbox `SubjectType = "change"`): `change_approval_requested` (every eligible admin; on a void, the admin who approved), `change_approval_decided` (author and change owner), `change_backout_started` (assignees of Geri dönüş tasks, or their teams' active members when unassigned). Turkish templates seeded by migration; variables `changeNumber`, `subject`, `status`, `windowStart`, `windowEnd`, `risk`, `reason`, `changeUrl`, `recipientName`.
- FKs `Restrict`; nothing is deleted. Every API change: `tools/gen-api-types.sh`; every new error code and audit action has Turkish text (`TranslationCoverageTests`); no magic numbers/strings.
- UI follows `frontend/CLAUDE.md` (Astryx components, `Icons` map, URL filter state, one primary action per group, `feature-changes` chunk group).
- Gates before each commit: `tools/lint.sh`, `tools/test.sh` (coverage ≥ 80 %); before merge `tools/e2e.sh` (stop dev services, restart after), `tools/ui-qa.sh`, green CI.

## Review Focus

- Two admins decide one approval at once, or an author edits while an admin approves: exactly one write wins, the other gets 409; never two pending rows (A5).
- The only eligible admin is the author or the change owner: the change waits in `PendingApproval`, the detail shows "Onaylayabilecek başka yönetici yok", and approve answers 403 `changes.self_approval` for that admin (A6).
- A window edit in `Scheduled` voids the approval, returns to `PendingApproval` and queues `change_approval_requested` to the admin who approved (A7); an edit of a non-fingerprint field (title, justification, closing note) does not.
- A task worker who only has a Geri dönüş task opens the change: sees the summary with the backout plan, gets 404 on the change history and approval, and cannot edit (summary scope).
- Closing Başarılı while a task on the change is being completed in parallel: the lock serializes them; the close either sees the task completed or refuses with `changes.open_tasks` — never closes with an open Uygulama task (A8).

---

### Task 1: Decision record

**Files:**
- Create: `docs/adr/0032-change-approval.md` (template `~/.claude/templates/adr-template.md`, status `accepted`, Deciders `Tugay İLTUŞ`)
- Modify: `docs/adr/README.md` (row 0032)

- [ ] **Step 1:** Write ADR-0032: separate `change_approvals` table with fingerprint, unassigned single-stage approval by an eligible admin (not author, not owner), void on fingerprint edit, no self-approval, emergency/CAB later. Alternative rejected: reuse `approvals` with a nullable `ticket_id` (mixes two lifecycles, every ticket approval query gains a kind filter). Links ADR-0031, ADR-0009.
- [ ] **Step 2:** Run `tools/check-doc-paths.sh docs/adr/0032-change-approval.md` — Expected: OK. Commit `docs(adr): ADR-0032 change approval`.

### Task 2: Domain — `ChangeRequest`, `ChangeApproval`, change tasks

**Files:**
- Create: `backend/src/Tgysm.Domain/Changes/{ChangeRequest.cs,ChangeEnums.cs,ChangeNumber.cs,ChangeApproval.cs,ChangeEvent.cs,ChangeAsset.cs,ChangePlanFingerprint.cs}`
- Modify: `backend/src/Tgysm.Domain/Tasks/WorkTask.cs` (`ChangeId`, `Phase`), `backend/src/Tgysm.Domain/Common/ErrorCodes.cs`, `backend/src/Tgysm.Domain/Audit/AuditActions.cs`, `AuditEntityTypes.cs` (`Change = "change"`)
- Test: `backend/tests/Tgysm.Domain.Tests/Changes/{ChangeRequestTests.cs,ChangeApprovalTests.cs,ChangePlanFingerprintTests.cs,ChangeNumberTests.cs}`, `backend/tests/Tgysm.Domain.Tests/Tasks/WorkTaskTests.cs`

**Interfaces — Produces:**
- `ChangeRequest.Create(string number, ChangeDraft draft, Guid ownerTeamId, Guid ownerId, Guid createdById, DateTimeOffset now)`; `ChangeDraft(string Title, string? Justification, string? AffectedService, Level Risk, Level Impact, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? ImplementationPlan, string? VerificationPlan, string? BackoutPlan)`.
- `bool Edit(ChangeDraft draft, DateTimeOffset now)` → returns `true` when a fingerprint field changed; refuses from `Implementing` on (`changes.plan_locked`) and when closed/cancelled (`changes.invalid_transition`).
- `Submit(int implementationTaskCount, now)` (needs window with start < end, all three plans, ≥ 1 Uygulama task: `changes.not_ready`), `ReturnToDraft(now)`, `Schedule(now)`, `BackToPending(now)` (void), `StartImplementation(now)` (sets `ActualStart`), `FinishImplementation(now)`, `StartBackout(now)` (sets `BackoutStartedAt`), `Close(ChangeOutcome outcome, ChangeClosing closing, ChangeTaskTally tasks, now)`, `Cancel(string reason, now)`; `IsOpen`, `IsPlanLocked`.
- `ChangeClosing(string? VerificationNote, string? BackoutNote, string? ClosingNote, DateTimeOffset? ActualEnd)`; `ChangeTaskTally` counts per phase and state (open, completed, cancelled).
- `ChangeApproval.Request(Guid changeId, string fingerprint, now)`; `Approve(actor, now)`, `Reject(actor, reason, now)`, `Return(actor, reason, now)`, `Withdraw(now)`, `Void(reason, now)`, `UpdateFingerprint(string)`; `ChangeApprovalStatus { Pending, Approved, Rejected, Returned, Withdrawn, Void }`.
- `ChangePlanFingerprint.Compute(ChangeRequest change, IEnumerable<Guid> assetIds, IEnumerable<Guid> implementationTaskIds) → string`.
- `WorkTask.Create(…, Guid? ticketId, Guid? changeId, ChangePhase? phase, …)` (refuses both links: `tasks.one_link_only`; phase iff change); `WorkTask.Reopen(now, bool changeOpen)`.
- Error codes: `changes.invalid_transition`, `changes.not_ready`, `changes.plan_locked`, `changes.open_tasks`, `changes.verification_required`, `changes.backout_required`, `changes.self_approval`, `changes.approval_not_pending`, `changes.phase_not_active`, `tasks.one_link_only`. Audit actions: `changes.created`, `changes.updated`, `changes.status_changed`, `changes.approval_decided`, `changes.approval_voided`, `changes.closed`.
- `ChangeEventKind { Created, Updated, Submitted, Withdrawn, Approved, Rejected, Returned, ApprovalVoided, ImplementationStarted, ImplementationFinished, BackoutStarted, Closed, Cancelled, AssetsChanged }`.

- [ ] **Step 1:** Failing tests: every allowed/refused transition; `Submit` without window/plans/Uygulama task → `changes.not_ready`; window end before start → `changes.not_ready`; `Edit` returns `true` for window and plan changes and `false` for title/justification; `Edit` in `Implementing` → `changes.plan_locked`; `Close(Successful)` with an open Uygulama task → `changes.open_tasks`, without verification note → `changes.verification_required`; `Close(RolledBack)` before `StartBackout` or with an open Geri dönüş task → `changes.backout_required`; fingerprint is equal for the same asset set in a different order and differs when one plan changes; `ChangeNumber.Format(2026, 7) == "CHG-2026-000007"`; `WorkTask.Create` with both links → `tasks.one_link_only`; `Reopen(now, changeOpen: false)` → `tasks.invalid_transition`.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*Change*Tests" --filter-class "*WorkTaskTests"` — Expected: FAIL (types missing).
- [ ] **Step 3:** Implement; length constants beside the entity (title 200, affected service 200, plans and notes 4000).
- [ ] **Step 4:** Re-run — Expected: PASS. Commit `feat(changes): ChangeRequest, ChangeApproval and change task phases in the domain`.

### Task 3: Persistence and notification plumbing

**Files:**
- Create: `backend/src/Tgysm.Infrastructure/Persistence/Configurations/{ChangeRequestConfiguration.cs,ChangeApprovalConfiguration.cs,ChangeEventConfiguration.cs,ChangeAssetConfiguration.cs}`; migrations via `tools/add-migration.sh ChangeRequests` and `tools/add-migration.sh ChangeEmailTemplates`
- Modify: `WorkTaskConfiguration.cs` (`change_id` FK, `phase`, check `ck_work_tasks_one_link`, index `(change_id, phase)`), `IAppDbContext.cs`/`AppDbContext.cs` (`ChangeRequests`, `ChangeApprovals`, `ChangeEvents`, `ChangeAssets`), `NotificationEventTypes.cs`, `TemplateVariables.cs`, `RecipientResolver.cs`, `NotificationContextLoader.cs`, `SerializationLocks.cs` (`ChangeWork = 7_009`)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Changes/ChangePersistenceTests.cs`, `Notifications/ChangeNotificationTests.cs`

**Interfaces — Produces:** tables `change_requests` (number unique, `search_text` trigram index, owner team/owner/creator FKs, `xmin`), `change_assets` (unique `(change_id, asset_id)`), `change_approvals` (unique index on `change_id` where `status = 'pending'`, `fingerprint`, `decided_by`, `decided_at`, `reason`, `xmin`), `change_events`; counter kind `change`; three email templates.

- [ ] **Step 1:** Failing tests: two parallel `Request` inserts for one change → one unique-violation; a task row with both `ticket_id` and `change_id` is refused by the check constraint; `change_approval_requested` resolves to every active admin except the author and the owner; `change_backout_started` resolves to backout task assignees, else their team members; `TranslationCoverageTests` covers the new event types.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangePersistenceTests" --filter-class "*ChangeNotificationTests"` — Expected: FAIL.
- [ ] **Step 3:** Configure tables, add both migrations (templates seeded with Turkish subject/body), wire recipients and variables.
- [ ] **Step 4:** Run `tools/migrate.sh` then the tests — Expected: PASS. Commit `feat(changes): change tables, approval index, change email events`.

### Task 4: Scope, work frame, create, edit, read

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Changes/{ChangeScope.cs,ChangeWork.cs,ChangeDtos.cs,CreateChange.cs,EditChange.cs,GetChange.cs,ListChanges.cs,ChangeAssets.cs,ChangeHistory.cs}`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Changes/{ChangeScopeTests.cs,ChangeCreationTests.cs,ChangeEditTests.cs}`

**Interfaces — Produces:**
- `ChangeViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)`; `ChangeAccess { None, Summary, Full }`; `ChangeScope.LoadAsync(number, ct) → (ChangeRequest, ChangeViewer, ChangeAccess)` (404 for `None`), `LoadForWorkAsync` (404 unless owner team member or admin), `Visible(viewer)` for lists (full or summary).
- `ChangeWork.RunAsync(string number, uint version, Func<ChangeRequest, ChangeViewer, DateTimeOffset, Task> change, ct) → ChangeDetail` inside `RunSerializedAsync(SerializationLocks.ChangeWork, …)`; one save; fresh detail.
- `ChangeDetail` (full) and `ChangeSummary` (working summary) records; `GET` returns `ChangeView { Access, Detail?, Summary? }`. `ChangeDetail` carries `assets`, `tasksByPhase`, `approval` (status, decided by, reason, `canDecide`), `hasEligibleApprover`, `canWork`, `version`.
- `CreateChangeCommand(ChangeDraft, Guid OwnerTeamId, Guid OwnerId, IReadOnlyList<Guid> AssetIds)`: owner must be an active IT member of the owner team; non-admin creator must be a member of the owner team (else 404 on that team).
- `EditChangeCommand(string Number, uint Version, ChangeDraft Draft, IReadOnlyList<Guid> AssetIds)`; `ChangeWork` exposes `Func<ChangeRequest, bool fingerprintChanged, DateTimeOffset, CancellationToken, Task>? OnPlanChanged` that Task 5 fills with the voiding and fingerprint refresh.
- `ListChangesQuery(ChangeQueue? Queue, ChangeStatus? Status, Level? Risk, string? Search, int Page, int PageSize)`, `ChangeQueue { Mine, Team, PendingApproval, Open }`.

- [ ] **Step 1:** Failing tests: employee 404 on list, get, create; a member of a team with only a Geri dönüş task gets `access = summary` with the backout plan and no implementation plan, approval or history; a non-member gets 404; create by a non-member of the owner team → 404; owner outside the team → 409 `tasks.assignee_not_in_team`; edit in `Draft` changes fields and assets and writes `Updated`/`AssetsChanged`; stale version → 409.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*Change*Tests"` — Expected: FAIL.
- [ ] **Step 3:** Implement handlers (validators `internal sealed`, `LikePattern` search, audit + `change_events` in the same save).
- [ ] **Step 4:** Re-run — Expected: PASS. Commit `feat(changes): change scope, work frame, create, edit and detail`.

### Task 5: Change approval (submit, decide, withdraw, void)

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Changes/{ChangeApprovalFlow.cs,SubmitChange.cs,DecideChangeApproval.cs,ListChangeApprovals.cs}`
- Modify: `EditChange.cs` (fills `OnPlanChanged`: `PendingApproval` refreshes the pending fingerprint, which changes its version; `Scheduled` voids)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Changes/{ChangeApprovalTests.cs,ChangeApprovalConcurrencyTests.cs}`

**Interfaces — Produces:**
- `SubmitChangeCommand(string Number, uint Version)`, `WithdrawChangeCommand(string Number, uint Version)` (author only).
- `ChangeApprovalFlow.RequestAsync(ChangeRequest, actor, now, ct)` (submit; queues `change_approval_requested`), `VoidAsync(ChangeRequest, string reason, now, ct)` (void approved row, new pending row, notify the approver who approved), `WithdrawAsync`, `IsEligible(Guid userId, ChangeRequest)`, `HasEligibleApproverAsync(ChangeRequest, ct)`.
- `DecideChangeApprovalCommand(string Number, ChangeApprovalDecision Decision, uint ApprovalVersion, string? Reason)`, `ChangeApprovalDecision { Approve, Reject, Return }`: one conditional `ExecuteUpdate` on `status = pending` and version; then the change moves to `Scheduled` or `Draft` in the same transaction; queues `change_approval_decided`.
- `ListChangeApprovalsQuery()` → pending approvals the caller may decide: `ChangeApprovalItem(Guid Id, string ChangeNumber, string Title, Level Risk, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string ImplementationPlanExcerpt, uint Version)`.

- [ ] **Step 1:** Failing tests: submit without window, plans or an Uygulama task → 409 `changes.not_ready`, a ready submit gives `pending_approval` with one pending row; A5 — two admins approve in parallel, one 200 and one 409, exactly one `approved` row and no second pending row; A6 — the author (admin) and the change owner get 403 `changes.self_approval`; with the author as the only active admin the detail has `hasEligibleApprover = false` and the change stays `pending_approval`; reject and return need a reason and return to `draft`; withdraw by the author marks `withdrawn`; A7 — window edit in `Scheduled` voids, returns to `pending_approval`, creates one new pending row and queues `change_approval_requested` to the admin who approved, while a title edit keeps `scheduled`; an edit in `PendingApproval` makes an admin's approve with the old `approvalVersion` answer 409; employee and IT staff get 403 on decide and an empty list.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeApproval*Tests"` — Expected: FAIL.
- [ ] **Step 3:** Implement; decisions audited (`changes.approval_decided`, `changes.approval_voided`).
- [ ] **Step 4:** Re-run plus the ticket approval tests (`--filter-class "*Approval*Tests"`) — Expected: PASS. Commit `feat(changes): single-stage change approval with plan fingerprint and voiding`.

### Task 6: Change workflow and change tasks

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Changes/ChangeStatusActions.cs` (start implementation, finish implementation, start backout, close, cancel)
- Modify: `backend/src/Tgysm.Application/Features/Tasks/{CreateTask.cs,TaskStatusActions.cs,TaskWork.cs,TaskScope.cs,TaskDtos.cs}` (change link, phase gate, reopen rule, lock, `TaskDetail.ChangeLink`)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Changes/{ChangeWorkflowTests.cs,ChangeTaskTests.cs,ChangeClosingTests.cs}`

**Interfaces — Consumes:** `ChangeWork` (Task 4), `ChangeApprovalFlow.VoidAsync` (Task 5). **Produces:** `CreateTaskCommand` gains `string? ChangeNumber`, `ChangePhase? Phase`; `TaskDetail.ChangeLink(string? Number, string? Title, ChangeStatus? Status, ChangePhase Phase, bool Accessible)`; `TaskStatusActions` checks `ChangeGate.CanStart(ChangeStatus, ChangePhase, bool backoutStarted)`.

- [ ] **Step 1:** Failing tests: A4 — `start-implementation` on `PendingApproval` → 409 `changes.invalid_transition`, and starting an Uygulama task while the change is `Scheduled` → 409 `changes.phase_not_active`; a Hazırlık task starts in `Scheduled`; adding a task to a change needs owner team membership (else 404) and may target any team; adding or cancelling an Uygulama task in `Scheduled` voids the approval; A8 — Başarılı refused with an open Uygulama task (`changes.open_tasks`) and without a verification note (`changes.verification_required`), accepted otherwise and open Geri dönüş tasks become `cancelled` with reason "Gerekmedi"; Geri alındı refused before `start-backout` and with an open Geri dönüş task, accepted after they close with a backout note; cancel in `Scheduled` cancels open tasks; reopening a task of a closed change → 409; a task complete and a Başarılı close sent in parallel never leave the change `closed` with an open Uygulama task.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeWorkflowTests" --filter-class "*ChangeTaskTests" --filter-class "*ChangeClosingTests"` — Expected: FAIL.
- [ ] **Step 3:** Implement; change-linked task writes go through `RunSerializedAsync(SerializationLocks.ChangeWork, …)`; `change_backout_started` queued in `start-backout`.
- [ ] **Step 4:** Re-run plus the Phase 7 task tests (`--filter-class "*Task*Tests"`) — Expected: PASS. Commit `feat(changes): change workflow, phased change tasks and closing rules`.

### Task 7: Endpoints, work list and contracts

**Files:**
- Create: `backend/src/Tgysm.Api/Endpoints/Changes/ChangeEndpoints.cs`
- Modify: `ApiRoutes.cs` (`Changes = "/changes"`), `TaskEndpoints.cs` (create body `changeNumber`, `phase`), `backend/src/Tgysm.Application/Features/Work/ListWorkItems.cs` (changes in the union), `frontend/src/shared/i18n/locales/tr/errors.json`, `audit.json`, then `tools/gen-api-types.sh`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Changes/ChangeEndpointTests.cs`, `Work/WorkListTests.cs`

**Interfaces — Produces:**
- `GET/POST /api/v1/changes`, `GET/PUT /api/v1/changes/{number}`, `GET /changes/{number}/timeline` (full access only), `POST /changes/{number}/{submit|withdraw|start-implementation|finish-implementation|start-backout|close|cancel}` (body `version`), `POST /changes/{number}/approval/{approve|reject|return}` (body `approvalVersion`, `reason`), `GET /api/v1/change-approvals` (pending for me).
- `WorkItemRow` gains `Level? Risk`, `DateTimeOffset? WindowStart`; `Priority` becomes nullable; kind `change`. `assigned_to_me` lists open changes I own; `my_teams` open changes of my teams.

- [ ] **Step 1:** Failing tests: anonymous 401, employee 404/empty on every change route; timeline 404 for summary access; the combined list pages tickets, tasks and changes together in `updated_at` order with the right total; `TranslationCoverageTests` fails until Turkish texts exist.
- [ ] **Step 2:** Run `dotnet test --solution backend/Tgysm.slnx --filter-class "*ChangeEndpointTests" --filter-class "*WorkListTests" --filter-class "*TranslationCoverageTests"` — Expected: FAIL.
- [ ] **Step 3:** Map endpoints (`ProducesProblem` 400/403/404/409), extend the union, add Turkish texts, run `tools/gen-api-types.sh`.
- [ ] **Step 4:** Run `tools/test.sh` — Expected: PASS. Commit `feat(changes): change API, change approvals list and changes in the work list`.

### Task 8: Frontend — change list, detail, form, actions

**Files:**
- Create: `frontend/src/features/changes/{api.ts,ChangeListPage.tsx,ChangeDetailPage.tsx,ChangeSummaryView.tsx,ChangeFormDialog.tsx,ChangeActions.tsx,ChangeTasks.tsx,ChangeApprovalBox.tsx,ChangeBadges.tsx}` + tests; `frontend/src/shared/i18n/locales/tr/changes.json`
- Modify: `frontend/src/shared/routes.ts` (`changes`, `changeDetail`), route metadata/breadcrumbs, sidebar entry "Değişiklikler" (IT staff/admin, `Icons` map), `frontend/vite.config.ts` (`feature-changes` group)
- Test: `ChangeListPage.test.tsx`, `ChangeDetailPage.test.tsx`, `ChangeFormDialog.test.tsx`, `ChangeActions.test.tsx`

- [ ] **Step 1:** Bootstrap Astryx docs per `frontend/CLAUDE.md`; read `npx astryx component Tabs --dense`, `Banner`, `DateTimeInput` (or the date input the project already uses).
- [ ] **Step 2:** Failing tests: list filters (queue, status, risk, search) live in the URL; detail sections Özet, Plan, Etkilenen varlıklar, Görevler (grouped by phase, "Görev ekle" with the phase preset), Onay, Zaman çizgisi; one primary action per state (Taslak: Onaya gönder; Planlandı: Uygulamaya başla; Uygulanıyor: Uygulama bitti; Doğrulama: Kapat); editing a fingerprint field in Planlandı asks first ("Bu değişiklik onayı geçersiz kılar"); `hasEligibleApprover = false` shows "Onaylayabilecek başka yönetici yok"; close dialog asks for outcome and shows the note the outcome needs; Başarısız offers "Arıza kaydı aç" (links to a new incident, nothing automatic); summary access renders `ChangeSummaryView` with no actions; 409 shows the stale-version message and refetches.
- [ ] **Step 3:** Run `cd frontend && npx vitest run src/features/changes` — Expected: FAIL.
- [ ] **Step 4:** Implement (react-hook-form + zod, `SectionCard`, `ListToolbar`/`DataList`, action answers stored with `setQueryData` and actions locked while one runs, as in `features/tasks`).
- [ ] **Step 5:** Run tests and `tools/lint.sh` — Expected: PASS. Commit `feat(frontend): change list, detail, form and actions`.

### Task 9: Frontend — Onaylar, change tasks, İş listem

**Files:**
- Modify: `frontend/src/features/approvals/ApprovalsPage.tsx` (both kinds, kind token, change rows show risk, window and plan excerpt, decide with reason), `frontend/src/features/tasks/{TaskFormDialog.tsx,TaskDetailPage.tsx,TaskActions.tsx,api.ts}` (phase field for change tasks, change link and phase on the detail, start disabled with the reason when the phase is not active), `frontend/src/features/workspace/{WorkspacePage.tsx,QueueCard.tsx,queuePath.ts}` (card Değişikliklerim), `frontend/src/features/tasks/WorkItemList.tsx` (change rows: risk and window), `tr/approvals.json`, `tr/tasks.json`, `tr/workspace.json`
- Test: `ApprovalsPage.test.tsx`, `TaskDetailPage.test.tsx`, `WorkspacePage.test.tsx`

- [ ] **Step 1:** Failing tests: Onaylar lists a ticket approval and a change approval with their kind tokens and decides the change one with `approvalVersion`; a change Uygulama task shows "Değişiklik uygulamaya başlayınca başlatılabilir" and a disabled Başla while the change is `scheduled`; İş listem shows the Değişikliklerim card and a change row with risk and window.
- [ ] **Step 2:** Run `cd frontend && npx vitest run src/features/approvals src/features/tasks src/features/workspace` — Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run tests and `tools/lint.sh` — Expected: PASS. Commit `feat(frontend): change approvals in Onaylar, phased change tasks and Değişikliklerim`.

### Task 10: E2E and phase exit

**Files:**
- Create: `frontend/e2e/changes.spec.ts`; Modify: `frontend/e2e/world.ts` (two admins, an owner team, a second team, one asset)

- [ ] **Step 1:** Write E2E: team A member creates a change with one Uygulama task for team B and one Doğrulama task, submits; admin 2 approves on Onaylar; the owner starts implementation on phone width; team B completes its task; owner finishes implementation, team A completes verification, owner closes Başarılı with a verification note; a second change: window edit in Planlandı shows the voiding warning and returns to Onay bekliyor.
- [ ] **Step 2:** Stop dev services, run `tools/e2e.sh` — Expected: all specs pass on both viewports; restart with `tools/dev-service.sh start`.
- [ ] **Step 3:** Run `tools/ui-qa.sh` with the new screens added to `frontend/e2e/qa-screens.spec.ts` — Expected: no lines in `tmp/ui-qa/problems.txt`.
- [ ] **Step 4:** Commit `test(e2e): change planned, approved, implemented and closed across two teams`.

Exit (Faz 8): A4–A8 green; regressions green (ticket approval incl. department approver, reassign, invalidate; Phase 7 task tests; SLA monitor and SLA risk queue; work queues; six reports and exports; outbox/email; ticket scope); green CI; final whole-branch review.
