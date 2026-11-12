# Task and Change Management — design

Date: 2026-11-12. Owner: Tugay İLTUŞ. Status: approved by the owner on 2026-11-12. Base: `master`.

This reverses the "out of scope" line for Change Management in `tgysm.md` §(Kapsam dışı) for normal changes; CAB, standard and emergency changes and Release Management stay out of the first version.

## Intent

IT staff need to hand work between teams (a backup by the database team inside a network incident) and to run planned changes with approval, a maintenance window, tasks per team, verification and a backout path, in the same plain Turkish UI and the same work list. Modelled on ServiceNow's change record, change tasks and approval, kept small.

## Architecture (decided)

- **Separate models with one shared work list.** `WorkTask` (`TSK-YYYY-NNNNNN`) and `ChangeRequest` (`CHG-YYYY-NNNNNN`) are their own tables; the `Ticket` model, its SLA (ADR-0018) and its approval stay unchanged (ADR-0009). İş listem runs one combined list query over tickets, tasks and changes.
- Rejected: new `TicketKind` values (ticket fields, SLA and outcomes do not fit; every rule would need kind checks); a common parent "work item" table (moves `tickets` and all child tables; high risk).
- **Numbering:** the `ticket_counters (kind, year)` table gets the kinds `task` and `change` (extension of ADR-0011).
- **Approval:** a separate `change_approvals` table with the same columns as `approvals`, one pending row per change (partial unique index) and the approved plan fingerprint. The Onaylar page lists both kinds.
- **Work frame:** `TaskWork` and `ChangeWork` follow `TicketWork` (scope → version → change → one save → fresh detail); no shared base class.
- **Notifications:** `OutboxEvent` gains `SubjectType` + `SubjectId`; `TicketId` stays for compatibility. New events: `task_assigned`, `change_approval_requested`, `change_approval_decided`, `change_backout_started`.
- **History:** `task_events`, `change_events`, assignment history per type, audit through `AuditLog`.
- ADR-0031 (scope and models), ADR-0032 (change approval).

## Task (`WorkTask`)

- Fields: title, description, team (required), assignee (optional, member of the team), priority P1–P4, due date (optional), result note; link: none / incident / request / change (fixed after creation); for change tasks a phase: Hazırlık / Uygulama / Doğrulama / Geri dönüş.
- States: Açık → İşlemde ⇄ Beklemede (reason) → Tamamlandı (result note required) | İptal (reason). Reopen while the linked change is not closed.
- Assignment and transfer follow the ticket rules (assignee in the team; transfer reason from `TransferReason`). Claiming is one conditional update (`assignee_id IS NULL`).
- Completing a task never changes the linked incident or request. **Completing a ticket with open tasks warns and is allowed** (owner decision).
- Due date is not SLA: no `SlaInstance`, no SLA reports; overdue = due date passed and not closed; "Geciken görevler" card.

## Change (`ChangeRequest`, normal change only)

- Fields: title, justification, owner team and change owner (IT staff), affected assets (inventory), affected service (text), risk and impact, planned window (start, end), implementation plan, verification plan, backout plan, actual start/end, outcome, closing note.
- States: Taslak → Onay bekliyor → Planlandı → Uygulanıyor → Doğrulama → Kapalı.
  - Reddet (reason) and "Düzeltmeye gönder" return to Taslak; the author may withdraw from Onay bekliyor.
  - Planlandı: editing a significant field voids the approval and returns to Onay bekliyor (approver notified).
  - İptal (reason) from Taslak, Onay bekliyor, Planlandı; from Uygulanıyor on only closing with an outcome.
  - "Geri dönüşü başlat" from Uygulanıyor or Doğrulama activates Geri dönüş tasks.
- Outcome on Kapalı: **Başarılı**, **Geri alındı**, **Başarısız** (offers "Arıza kaydı aç"; not automatic).
- Closing rules:
  - Başarılı: all Uygulama and Doğrulama tasks Tamamlandı; Hazırlık tasks Tamamlandı or cancelled with reason; verification result note and actual start/end set; open Geri dönüş tasks are cancelled with "gerekmedi".
  - Geri alındı / Başarısız: backout started with at least one Geri dönüş task, those tasks closed, backout note required.
- Before approval nothing runs: the change reaches Uygulanıyor only from Planlandı; Uygulama tasks start only while the change is Uygulanıyor, Doğrulama tasks while Doğrulama; Hazırlık tasks may run in Planlandı.
- **Approval:** single stage; **approver is an active admin, never the author or the change owner** (owner decision). Unassigned approvals are visible to every eligible admin. If the only eligible admin is the author, the change waits in Onay bekliyor with "Onaylayabilecek başka yönetici yok"; no self-approval (emergency changes are a later phase).
- **Plan fingerprint:** window, affected assets, risk/impact, the three plans and the list of Uygulama tasks. From Uygulanıyor on these fields are locked.

## Access

- Employees never see tasks or changes. Everything outside scope answers 404 (ADR-0013).
- Task: admin; members of the task's team; the assignee; the creator. A linked record the viewer cannot see shows as "Bağlı kayıt: erişiminiz yok" without number or title.
- Change, full detail: admin, owner team members, the assigned approver.
- Change, **working summary** for teams that have a task on it (owner decision): number, title, status, window, risk and, by the phase of their tasks, the implementation or backout plan. No approval history, internal notes or other teams' tasks.
- Who may act: any IT staff opens standalone tasks (any team) and changes (own team owns it); adding tasks to a ticket needs `CanWork` on it; adding tasks to a change needs owner team membership (tasks may go to any team); working a task needs membership of its team; admin may do everything except approving own changes.
- Lists, calendar, reports and notifications use the same scope.

## Integrity and concurrency

- FKs `Restrict`; nothing is deleted. Task link: `ticket_id` and `change_id`, a check constraint allows at most one.
- `xmin` row version on every table; stale version → 409. Claim, approve and state changes as single conditional updates; the affected row count decides.

## Maintenance window conflicts

- Compared among changes in Onay bekliyor, Planlandı, Uygulanıyor: windows overlap **and** share an affected asset or the same affected-service text.
- A warning on the change, the approval and the calendar; never a block (no CMDB).

## Screens (Astryx, shared pieces)

- İş listem: new cards Görevlerim, Ekibimin görevleri, Geciken görevler, Değişikliklerim, Bu hafta uygulanacak; one combined list (kind, number, title, status, priority/risk, due/window, team, assignee).
- Görevler `/tasks`, `/tasks/:number`; ticket detail gets a Görevler section.
- Değişiklikler `/changes`, `/changes/:number` (Özet, Plan, Etkilenen varlıklar, Görevler by phase, Onay, Zaman çizgisi; one primary action; warning before an edit that voids the approval).
- Bakım takvimi `/changes/calendar`: week/month on wide screens, agenda list on phones; conflicts marked.
- Onaylar: both kinds with a kind token; change approvals show risk, window, plan summary, conflicts.
- Phone flow: İş listem → Görevlerim → task → Üstlen → Başlat → Tamamla; change owner: Uygulamaya başla → Uygulama bitti → Kapat.

## Phases

1. **F1 Task + İş listem:** `WorkTask`, `TSK-`, scope/work frame, assignment and transfer, ticket Görevler section, combined İş listem query, `task_assigned`, ADR-0031. Exit: A1–A3 and regressions green; E2E task from an incident to another team.
2. **F2 Change + approval:** `ChangeRequest`, `CHG-`, state machine, change task phases, `change_approvals`, fingerprint and voiding, closing and backout, combined Onaylar, notifications, ADR-0032. Exit: A4–A8 and ticket approval regression green.
3. **F3 Calendar + operations:** calendar, conflict warnings, "Bu hafta" card, reports (change outcomes, overdue tasks, task durations) with Excel export. Exit: A9 and report regressions green; perf within limits.
4. **Later:** standard change templates, emergency change (approval after the fact with reason), CAB, an approver permission beside the fixed roles.

## Acceptance

- A1 a team without access to the linked incident sees the task, not the incident (404 on its URL).
- A2 two people claim one task at once: one wins, the other gets 409.
- A3 completing a task leaves the linked incident's status and SLA unchanged.
- A4 Uygulamaya başla and starting an Uygulama task are refused before approval.
- A5 two admins decide one approval at once: one wins, 409 for the other; one pending approval per change.
- A6 author/owner cannot approve; with the author as the only admin the change waits with the warning.
- A7 changing the window in Planlandı voids the approval and notifies the approver.
- A8 Başarılı refused with a missing verification note or an open Uygulama task; backout path closes as Geri alındı only after its tasks close.
- A9 overlapping windows on one asset warn without blocking; the calendar hides out-of-scope changes.
- Regressions: ticket approval (department approver, reassign, invalidate), SLA monitor and SLA risk queue, work queues, the six reports and exports, outbox and email, ticket scope.
- Every API change: `tools/gen-api-types.sh`; Turkish texts for new error codes and audit actions (`TranslationCoverageTests`); `tools/lint.sh`, `tools/test.sh` (coverage ≥ 80 %), `tools/e2e.sh` (stop and restart the dev services); green CI.

## Left to the builder

Exact table/column names, endpoint shapes, the combined list query's paging, calendar component choice, report metric details.
