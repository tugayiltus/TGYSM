# ADR-0031: Tasks and Changes as Separate Records with One Combined Work List

**Date**: 2026-11-13
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

IT staff need to hand work between teams. For example, the database team takes a backup inside a network incident. They also need to run planned changes with approval, a maintenance window, tasks per team, verification and a backout path. `tgysm.md` listed Change Management as out of scope. The owner approved a design on 2026-11-12 (`docs/superpowers/specs/2026-11-12-task-and-change-management-design.md`). That design brings tasks and normal changes into scope as Phases 7–9. CAB, standard and emergency changes, and Release Management stay out of scope.

The ticket model (ADR-0009) carries SLA (ADR-0018), approval and outcomes made for incidents and requests. A task has a due date, not an SLA. A change has a window, risk, three plans and its own approval.

## Decision

- `WorkTask` (`TSK-YYYY-NNNNNN`) and `ChangeRequest` (`CHG-YYYY-NNNNNN`) are separate tables and aggregates. `Ticket`, its SLA and its approval stay unchanged.
- Numbers come from the existing `ticket_counters (kind, year)` table with the new kinds `task` and `change` (extends ADR-0011).
- Each record type has its own scope and work frame, `TaskScope`/`TaskWork` and later `ChangeScope`/`ChangeWork`. They copy the `TicketScope`/`TicketWork` pattern (ADR-0013): load in scope with 404 outside it, check the version, apply the change, save once. There is no shared base class.
- İş listem runs one read query. The query unions visible tickets and visible tasks (changes in Phase 8) into one row shape.
- `OutboxEvent` gains `SubjectType` and `SubjectId` for events that are not about a ticket. `TicketId` stays for compatibility (extends ADR-0015).
- A task links to at most one ticket or change. The link is fixed after creation. Completing a task never changes the linked record. Completing a ticket with open tasks shows a warning and is allowed.
- Employees never see tasks or changes. A task is visible to admins, members of its team, its assignee and its creator. A linked record outside the viewer's scope shows without number or title.
- Change approval is recorded separately (ADR-0032, Phase 8).

## Alternatives Considered

### Alternative 1: New `TicketKind` values (`Task`, `Change`)
- **Pros**: The existing tables, numbering, comments, attachments, timeline and lists work at once.
- **Cons**: Ticket fields, SLA start/stop, outcomes and the requester role do not fit tasks or changes. Every ticket rule, report and SLA job would need kind checks. A missed check leaks task rows into incident reports or starts an SLA clock for a change.
- **Why not**: The regression risk to the released ticket flows is higher than the effort it saves.

### Alternative 2: A common parent "work item" table
- **Pros**: One identity, one list and one timeline for all work.
- **Cons**: `tickets` and all its child tables (assignments, comments, attachments, approvals, SLA instances, links, outbox) need to move or re-key in a live database.
- **Why not**: The migration risk is high on released data. The combined list query gives the same user-visible result without moving any data.

## Consequences

### Positive
- Ticket behaviour, SLA and the six reports stay as released.
- Each record type has only the rules that apply to it.
- The work list shows all assigned work in one place.

### Negative
- Comments, history and assignment tables exist per type, so some structure repeats.
- The combined list query must be extended for each new record type.

### Risks
- The scope rules for each type can drift apart. Each type has its own scope tests, including the "linked record not visible" case.
- Union paging over two tables can get slow at pilot volume. Indexes on team, assignee and status, and a performance check in Phase 9, cover this.
