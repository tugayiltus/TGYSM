# Phase 2–3 Assets and Tickets Implementation Plan

> Execution: inline (superpowers:executing-plans), owner-approved. Each task: write the listed failing tests, watch them fail, implement, run the task command, ledger the result. **Ruling (plan format):** tasks list files, interfaces and tests instead of full inline code — Phase 1's full-code plan consumed most of the session context; code is written once, under TDD, from the spec.

**Goal:** Deliver Phase 2 (inventory, asset lifecycle, CSV import) and Phase 3 (incidents, requests, assignment, approval, comments/attachments, timeline) per the spec.

**Spec:** `docs/superpowers/specs/2026-09-26-phase-2-3-assets-and-tickets-design.md`

## Global Constraints

- All Phase 1 constraints apply (English code/URLs/DB, Turkish UI via i18n, zero warnings, no lint disables, no magic literals, `tools/` scripts, TDD, no push).
- Backend tests: `dotnet test --solution backend/Tgysm.slnx` (MTP); filters `--filter-class` / `--filter-method`.
- New error codes go to `ErrorCodes` + `errors.json`; new audit actions to `AuditActions` + `admin.json` `auditActions`; new entity types to `AuditEntityTypes` + `entityTypes` (enforced by `TranslationCoverageTests`).
- Every state change: domain method → handler writes entity + history row + audit (+ ticket event) in one `SaveChanges`; cross-row rules under `RunSerializedAsync` or DB constraints.
- New policy `Policies.ItStaffOrAdmin` (roles `it_staff`, `admin` + password changed).
- After API changes: `tools/gen-api-types.sh`.

## Review Focus

1. Out-of-scope ticket IDs/numbers (other requester, other team) on every ticket endpoint, including attachments → 404.
2. Concurrent handover of the same asset; concurrent claim; concurrent approval decisions; concurrent ticket creation numbers.
3. Internal notes/attachments never reach employee responses (detail, timeline, comments, attachment download).
4. CSV with a mix of valid and invalid rows writes nothing; re-committing a committed job does not duplicate.
5. Files: wrong magic bytes, oversize, too many, executable disguised as PDF, TXT with NUL → rejected.

---

### Task 1: ADRs 0009–0013
Files: `docs/adr/0009-single-ticket-table.md`, `0010-asset-lifecycle-and-assignment-consistency.md`, `0011-ticket-numbering.md`, `0012-attachment-storage-and-scanning.md`, `0013-ticket-access-scope.md`, `docs/adr/README.md`.
Verify: ADR index diff OK.

### Task 2: Asset domain
Files: `Domain/Assets/{Asset,AssetType,AssetStatus,AssetAssignment,AssetMovement,AssetMovementKind,AssetNames}.cs`.
Interfaces: `Asset.Create(tag, type, brand, model, serial?, locationId, departmentId?, warrantyEndsOn?, purchasedOn?, notes?, now)`, `UpdateCard(...)`, `static NormalizeTag/NormalizeSerial`, lifecycle: `Assign(userId, actor, requestTicketId?, note?, now) → AssetAssignment`, `Return(actor, locationId, now)`, `Transfer(newUserId, actor, now)`, `StartMaintenance(bool retainResponsibility, actor, now)`, `EndMaintenance(actor, now)`, `MarkLost(actor, reason, now)`, `MarkFound(actor, locationId, now)`, `Retire(actor, reason, now)`; each returns/records `AssetMovement`s; invalid transition → `DomainException(assets.invalid_transition)`.
Tests: `Domain.Tests/AssetTests.cs` — normalization, every allowed transition, every refused transition, maintenance with/without retained responsibility, retire closes assignment, lost keeps history.

### Task 3: Asset persistence
Files: configurations for assets/assignments/movements/import_jobs, migration `Assets`, `IAppDbContext` DbSets.
Tests: `Persistence/AssetPersistenceTests.cs` — duplicate normalized tag rejected, duplicate serial rejected but two NULL serials allowed, second active assignment for one asset rejected (partial unique index).

### Task 4: Asset catalogue API
Files: `Application/Features/Assets/{AssetDtos,ListAssets,GetAsset,CreateAsset,UpdateAsset,GetAssetHistory}.cs`, `Api/Endpoints/AssetEndpoints.cs`, `Policies.ItStaffOrAdmin`.
Tests: `Assets/AssetCatalogueTests.cs` — employee 403, IT creates/lists/filters (type, status, location, search tag/serial/model), tag duplicate → 409 `assets.tag_taken`, serial duplicate → `assets.serial_taken`, inactive location → `common.reference_inactive`, stale version → 409.

### Task 5: Asset lifecycle API + my assets
Files: `Application/Features/Assets/Lifecycle/*.cs`, endpoints, `Features/Assets/ListMyAssets.cs`, `GET /me/assets`.
Tests: `Assets/AssetLifecycleTests.cs` — assign/return/transfer/maintenance/lost/found/retire via API with movements + audit, inactive user refused, invalid transition 409, `/me/assets` shows only own active assignments, race: two parallel assigns → exactly one active assignment.

### Task 6: CSV import
Files: `Application/Features/Assets/Import/{AssetCsvTemplate,AssetCsvParser,PreviewAssetImport,CommitAssetImport}.cs`, `ImportJob` domain + config + migration, endpoints (multipart).
Tests: `Assets/AssetImportTests.cs` — template has BOM + Turkish headers; valid file previews then commits all rows; one bad row → preview errors, commit refused (`imports.has_errors`), nothing written; duplicate in file and vs DB reported; `assigned` status refused; location path matching (Turkish-insensitive, ambiguous refused); re-commit returns same result, no duplicates; > 5,000 rows or > 10 MB refused.

### Task 7: Phase 2 UI
Files: `features/assets/{api.ts, AssetsPage.tsx, AssetDetailPage.tsx, AssetFormDialog.tsx, AssetActionDialogs.tsx, AssetImportPage.tsx, MyAssetsPage.tsx}`, routes `/assets`, `/assets/:id`, `/assets/import`, `/assets/mine`, IT shell nav, i18n `assets.json`.
Tests: list + filters, create with tag-taken field error, lifecycle action dialog sends version/body and shows invalid transition message, import preview renders row errors and disables commit, my assets list; employee cannot open `/assets`.

### Task 8: Ticket domain
Files: `Domain/Tickets/{Ticket,TicketKind,TicketStatus,HoldReason,Impact,Urgency,Priority,PriorityMatrix,TicketOutcome,TicketNumber,Approval,ApprovalStatus,TicketComment,Visibility,TicketAttachment,ScanStatus,TicketEvent,TicketEventKind,TicketAssignment,TransferReason}.cs`, `Domain/Catalog/{Category,RequestType}.cs`, `Team.MarkDefaultIntake/ClearDefaultIntake`.
Tests: `Domain.Tests/TicketTests.cs` (state machine incl. hold reason rules, complete requires resolution, cancel requires reason, incident outcome rules, priority from matrix), `PriorityMatrixTests`, `TicketNumberTests` (`INC-2026-000001`).

### Task 9: Ticket persistence
Files: configurations + migration `Tickets` (all Phase 3 tables, CHECKs, partial uniques, intake flag), `ITicketNumberGenerator` (Infrastructure, counter upsert).
Tests: `Persistence/TicketPersistenceTests.cs` — CHECK violations rejected, one pending approval per ticket, one default intake team, parallel number generation yields distinct sequential numbers.

### Task 10: Catalog admin + reference endpoints
Files: `Features/Catalog/{Categories,RequestTypes}/*`, `Features/Reference/*`, endpoints `/admin/categories`, `/admin/request-types`, `/reference/*`, team intake flag on `UpdateTeam`.
Tests: category tree ≤ 2 levels (`categories.too_deep`), unique names Turkish-insensitive, reference lists active only and filtered by `applies_to`, intake flag moves from one team to another atomically.

### Task 11: Ticket creation, scope, list, detail
Files: `Features/Tickets/{TicketScope,CreateIncident,CreateRequest,ListTickets,GetTicket,TicketDtos}.cs`, `TicketEndpoints.cs`.
Tests: `Tickets/TicketCreationTests.cs` — incident gets `INC-YYYY-…`, intake team, P3; no intake team → `tickets.no_intake_team`; request with approval type → `on_hold`/`manager_approval` + pending approval to department approver; no approver → pending with null approver; employee cannot set priority; `Tickets/TicketScopeTests.cs` — employee sees only own (list/detail 404), IT sees team + own, admin all, search respects scope, internal fields absent for employee.

### Task 12: Assignment
Files: `Features/Tickets/{ClaimTicket,TransferTicket}.cs`.
Tests: claim own team ticket; claim other team 404; transfer requires reason, `other` needs note, target assignee must be active team member (`tickets.assignee_not_in_team`); history + events; race: two claims → one wins (409 for other).

### Task 13: Status and priority
Files: `Features/Tickets/{StartTicket,HoldTicket,ResumeTicket,CompleteTicket,CancelTicket,ChangePriority}.cs`.
Tests: transitions per §3.3; hold reason required; complete requires resolution and closes directly; employee cannot cancel/complete; priority change needs reason and records old/new; completed ticket refuses status changes.

### Task 14: Approvals
Files: `Features/Approvals/{ListMyApprovals,ApproveRequest,RejectRequest,ReassignApprover,InvalidateApproval}.cs`, completion/handover guards.
Tests: approve → open; reject → cancelled/approval_rejected with reason; only assigned approver decides; requester cannot approve own; reassign audited; invalidate → new pending + on_hold; completing unapproved request 409 (`approvals.required`); asset assign against unapproved request 409; race: parallel approve/reject → one wins.

### Task 15: Comments, attachments, storage, timeline
Files: `Application/Abstractions/{IFileStore,IFileScanner}.cs`, `Infrastructure/Files/{LocalFileStore,DevelopmentFileScanner,FileTypeSniffer}.cs`, `Features/Tickets/{AddComment,DownloadAttachment,TicketTimeline}.cs`, multipart on create.
Tests: `FileTypeSnifferTests` (PDF/PNG/JPEG magic bytes, TXT/CSV UTF-8, NUL rejected, EXE-as-PDF rejected); comments public/internal; employee cannot post internal; attachment limits (size, count, type → `attachments.*`); employee download of internal attachment → 404; out-of-scope download → 404; timeline for employee has no internal events.

### Task 16: Related request and ticket assets
Files: `Features/Tickets/{CreateRelatedRequest,LinkTicketAsset,UnlinkTicketAsset}.cs`, asset history includes tickets (IT/admin).
Tests: related request copies public data only, links both ways, starts its own approval; incident completion with `service_request_required` needs relation; employee links only own assigned asset; asset history shows ticket for IT, not reachable by employee.

### Task 17: Phase 3 UI — employee
Files: `features/tickets/{api.ts, NewIncidentPage.tsx, NewRequestPage.tsx, MyTicketsPage.tsx, TicketDetailPage.tsx (read-only parts), Timeline.tsx, AttachmentInput.tsx}`, home cards, i18n `tickets.json`.
Tests: forms validate and submit multipart; request type shows approval hint; my tickets list; detail shows public timeline only.

### Task 18: Phase 3 UI — ticket detail actions
Files: `features/tickets/{TicketActions.tsx, TransferDialog.tsx, HoldDialog.tsx, CompleteDialog.tsx, CancelDialog.tsx, PriorityDialog.tsx, CommentBox.tsx}`.
Tests: actions visible per role; claim; transfer validation; complete requires resolution; internal note tab only for IT; conflict message on stale version.

### Task 19: Phase 3 UI — workspace, approvals, catalog admin
Files: `features/workspace/{WorkspacePage.tsx, QueuePage.tsx}`, `features/approvals/ApprovalsPage.tsx`, `features/admin/catalog/{CategoriesPage.tsx, RequestTypesPage.tsx}`, team intake checkbox.
Tests: workspace cards open filtered queue; approve/reject with reason; categories tree CRUD; intake flag toggle.

### Task 20: E2E, gates, review, commit
Files: `frontend/e2e/tickets.spec.ts`, `frontend/e2e/assets.spec.ts`, `tools/e2e.sh` (seed intake team, category, request type through API in the test).
Steps: E2E flows from spec §6 at 360 and 1280; `tools/build.sh`; `tools/e2e.sh`; specialist reviews (C#, DB, security, React); fix Critical/Important with RED→GREEN; ledger; local commit per green milestone.
