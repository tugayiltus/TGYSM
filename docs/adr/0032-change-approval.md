# ADR-0032: Change Approval in Its Own Table with a Plan Fingerprint

**Date**: 2026-11-13
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

A normal change needs an approval before it runs (ADR-0031, design `docs/superpowers/specs/2026-11-12-task-and-change-management-design.md`). The owner decided that the approver is an active admin who is neither the author nor the change owner. A change can be edited after it was approved. An approval must not stay valid for a plan the approver never saw.

The ticket `approvals` table (ADR-0009) serves incidents and requests. It belongs to the ticket lifecycle and its queries.

## Decision

- Approval is stored in a new table `change_approvals`, separate from ticket `approvals`.
- Single stage. The approval row is unassigned: any eligible admin decides. Eligible means an active admin who is not the author (`created_by_id`) and not the change owner. The author or owner never approves their own change (`changes.self_approval`, 403).
- At most one pending row per change (partial unique index). A decision is one conditional update on `status = pending` and the approval version; the affected row count decides. The loser gets 409.
- Decisions: approve, reject (reason) and return for revision (reason). The author withdraws.
- Each approval row stores a plan fingerprint: SHA-256 hex over window start and end, sorted affected asset ids, risk, impact, the three plans, and the sorted ids of open-or-completed implementation tasks.
- An edit of a fingerprint field in `Scheduled` voids the approval. The change returns to `PendingApproval` and the admin who approved gets a notification. An edit in `PendingApproval` refreshes the pending fingerprint. Other fields (title, justification, closing note) do not void it. From `Implementing` on, fingerprint fields are locked (`changes.plan_locked`).
- If the only eligible admin is the author or owner, the change waits in `PendingApproval` and the detail says no other admin can approve.
- Emergency changes (approval after the fact), CAB and an approver permission are later work.
- Implemented in Phase 8 (`docs/superpowers/plans/2026-11-13-phase-8-changes.md`).

## Alternatives Considered

### Alternative 1: Reuse `approvals` with a nullable `ticket_id`
- **Pros**: One table, one decision handler and one inbox query.
- **Cons**: Two lifecycles share one table. Every ticket approval query gains a kind filter. Change approvals need a fingerprint and a void state that tickets do not have. A missed filter leaks change approvals into ticket flows.
- **Why not**: The regression risk to released ticket approvals is higher than the saved code.

### Alternative 2: Assign the approval to one named approver
- **Pros**: Clear ownership of the decision.
- **Cons**: If that admin is away or is the author, the change is stuck. Reassigning needs an extra action.
- **Why not**: An unassigned row visible to every eligible admin needs no reassignment and gives the same control.

## Consequences

### Positive
- Ticket approvals stay unchanged.
- An approval always matches the plan the approver saw.
- Concurrent decisions cannot produce two winners.

### Negative
- Decision handlers and the inbox query exist twice, once per approval type.
- A plan edit in `Scheduled` costs a new approval round.

### Risks
- With one admin in the system, no change can be approved. The detail shows this, and a second admin must be created.
- The fingerprint must cover every field that matters. A missed field allows an edit without a void. Tests cover each fingerprint field and the non-fingerprint fields.
- The fingerprint and the eligibility rule can drift from the design. Both are tested against the Phase 8 plan.

## Amendment 2026-11-18

The eligibility rule is stricter than the Decision section states. Besides the author and the change owner, every user who ever submitted or changed the plan of the change is ineligible for the life of the change: fingerprint fields, the asset set, or Uygulama tasks added, cancelled or reopened, in any status including `Draft`. They are recorded in `change_plan_editors` (commit `2d4ae01`); one shared query applies the rule to the decision, `canDecide`, the approvals list, `hasEligibleApprover` and the approval-request recipients. Reason: an admin could otherwise write the plan in `Draft`, let the owner submit, and approve their own plan. The successful close also ignores cancelled Uygulama tasks and needs at least one completed one (commit `6d52d1d`).

## Amendment 2026-11-19
Phase 9 builds on this decision without changing it. Window conflicts are a warning only, never a block, so submit, approval and scheduling are unchanged. Two changes conflict when their half-open windows overlap and they share an affected asset or the same affected service; only `pending_approval`, `scheduled` and `implementing` changes are candidates, and a clash with a change outside the viewer's scope is reported without number and title, so a hidden clash is still visible as a warning. The change detail lists conflicts for full-detail viewers only, and the calendar flags `hasConflict` only where the viewer has full access, so summary access learns nothing about other teams' changes (commits `58840b9`, `df6b848`, `5d90751`). The calendar endpoint is capped at 62 days (`changes.calendar_range`, 400). The change and task reports reuse the report pipeline of ADR-0020; the only schema change is widening the `ck_export_jobs_report` check constraint so export jobs accept `changes` and `tasks` (migration `WidenExportJobsReportConstraint`, commit `cac7bff`).
