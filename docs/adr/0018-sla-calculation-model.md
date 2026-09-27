# ADR-0018: SLA Calculation Model

**Date**: 2026-09-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

SLA results must stay correct across holidays, working-hour boundaries, the time zone, DST, holds and priority changes, and a transfer must never reset a timer (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §1 success criterion 1, §5, commit `3aa02ab`).

## Decision

- Domain `BusinessTimeCalculator` (no framework dependency; input: time zone, weekly hours, holidays): `BusinessSecondsBetween(fromUtc, toUtc)` and `AddBusinessSeconds(fromUtc, seconds)`. It converts to local time per day, clips to working ranges, skips holidays and closed days; DST is handled by converting each local boundary back to UTC rather than doing arithmetic across a DST edge in local time.
- Domain `SlaInstance` state machine: `Start`, `Pause`, `Resume`, `ChangeTarget`, `Stop(result)`. `elapsed_seconds` accumulates on pause and stop; `due_at` / `warn_at` are recomputed on start, resume and target change, and both are `NULL` while paused. Storage is UTC; display uses the calendar time zone.
- Versioned policy: `sla_policy_versions` are never edited — a change creates a new version (partial unique index keeps one `is_current`); `sla_targets` and `sla_pause_rules` belong to a version. One company `business_calendar` + `business_hours` + `holidays`; calendar and holiday changes apply to future accumulation only, never recomputing time already stored.
- Rules: ticket creation starts both instances on the current policy version (a request-type target overrides the priority target); a request starting `on_hold` for approval starts with its resolution instance already paused. First response stops at the first public comment by IT staff or a non-requester admin. Hold/resume pauses and resumes per the instance's own policy version's pause rules. A priority change is a `ChangeTarget` (start time and elapsed time kept, old/new target recorded in `sla_events`, an earlier breach stays recorded). Transfer and claim never touch SLA. Completion stops with `met`/`breached`; cancellation stops with `cancelled`.
- `SlaMonitor` (worker, ADR-0014) runs every 60 s and marks warning/breach via a conditional `ExecuteUpdate` on `warn_at`/`due_at`; only the row whose update actually applied writes the `sla_events` row and the outbox event, so a warning or breach cannot double-fire across worker instances.

## Alternatives Considered

### Alternative 1: Recompute elapsed business time on every read, with no stored `elapsed_seconds`
- **Pros**: no drift is possible — the figure is always derived fresh from the pause/resume log.
- **Cons**: every list or detail view would need the full business calendar (holidays, hours) and would recompute from ticket creation for tickets running months, on every page load; it also breaks the requirement that a calendar change applies only to future accumulation, since there would be no record of which calendar covered which historical stretch.
- **Why not**: storing already-elapsed seconds and only ever calculating the calendar-dependent remainder since `running_since` is far cheaper and directly satisfies the "not recomputed" rule for past periods.

### Alternative 2: A third-party scheduling/SLA library, or a generic cron job recalculating `due_at`
- **Pros**: less domain code to write and unit-test from scratch.
- **Cons**: business-hours-aware elapsed-time accumulation across holds, with exactly-once warning/breach across several worker instances, is a narrow ITSM-specific need; no common .NET library models it directly.
- **Why not**: SLA correctness (§1 success criterion 1) is best served by a small, deeply unit-tested, framework-free Domain calculator that is purpose-built for this accumulation model, rather than adapting a general-purpose library to fit it.

## Consequences

### Positive
- Elapsed time is precomputed and audit-friendly via the append-only `sla_events` log.
- Duplicate warning/breach across worker instances is structurally impossible — only the row whose conditional update applies acts.
- Policy history stays intact; a version is immutable once created, so past SLA instances keep the target they were measured against.

### Negative
- A business-calendar correction (e.g. fixing a wrongly entered holiday) never touches already-elapsed accumulation on existing tickets — only future time uses the corrected calendar.

### Risks
- DST edge cases in `BusinessSecondsBetween` / `AddBusinessSeconds` are easy to get subtly wrong (e.g. a working range spanning the "fall back" hour); these need dedicated tests at each DST boundary, not just ordinary calendar-date tests.
