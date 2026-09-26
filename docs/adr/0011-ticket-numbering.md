# ADR-0011: Ticket Numbering with a Counter Table

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Numbers look like `INC-2026-000001` / `REQ-2026-000001`, restart every year per kind and must be unique under concurrent creation (`tgysm.md` §5.1, §6).

## Decision

A `ticket_counters (kind, year, last_value)` table. The next value comes from one statement: `INSERT … VALUES (kind, year, 1) ON CONFLICT (kind, year) DO UPDATE SET last_value = ticket_counters.last_value + 1 RETURNING last_value`, executed in the same transaction as the ticket insert. Row locking serializes concurrent creators; a unique index on `tickets.number` is the final guard.

## Alternatives Considered

### Alternative 1: PostgreSQL sequences per kind and year
- **Pros**: no row lock contention.
- **Cons**: sequences must be created per year; gaps on rollback; harder to reset.
- **Why not**: yearly reset and gap-free-ish numbers are simpler with a counter row; volume is low.

### Alternative 2: `MAX(number) + 1`
- **Why not**: races produce duplicates.

## Consequences

### Positive
- Readable, yearly numbers; safe under concurrency.

### Negative
- Creators of the same kind wait on one row lock (negligible at expected volume).

### Risks
- A failed transaction still consumed nothing (the counter update rolls back with it).
