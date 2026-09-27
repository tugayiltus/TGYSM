# ADR-0010: Asset Lifecycle and Assignment Consistency

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

An asset can have at most one active assignment; handovers, returns, transfers, maintenance, loss and retirement must keep full history (`tgysm.md` §8.2), including under concurrent operations.

## Decision

- `Asset` owns an explicit state machine (`in_stock`, `assigned`, `maintenance`, `lost`, `retired`). Status is never edited directly; only lifecycle operations change it and refuse invalid transitions (`assets.invalid_transition`).
- Assignments are rows in `asset_assignments`; the active one has `returned_at IS NULL`. A partial unique index on `asset_id WHERE returned_at IS NULL` makes a second active assignment impossible, even under a race; `xmin` on `assets` rejects stale writes.
- Every operation appends an `asset_movements` row and an audit event in the same transaction.
- Transfer closes the old assignment and opens the new one in one transaction.
- Maintenance records whether the user stays responsible; if not, the assignment closes.
- CSV import creates assets only (never assignments or updates).

## Alternatives Considered

### Alternative 1: `assigned_user_id` column on `assets`
- **Pros**: simplest read model.
- **Cons**: history must be rebuilt from audit; no DB guarantee for "one active assignment" with history rows.
- **Why not**: assignment history is a first-class requirement.

## Consequences

### Positive
- The database enforces the key invariant; history is queryable.

### Negative
- Current holder requires a join to the active assignment.

### Risks
- A new operation that forgets to write a movement row loses history; handlers share one helper for movements.

## Amendment 2026-09-27

Implemented in e2354ea. Handover against a request is allowed only to that request's requester while it is open, and only after a required approval (5f60b98).
