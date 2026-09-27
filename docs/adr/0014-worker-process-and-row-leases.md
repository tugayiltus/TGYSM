# ADR-0014: Worker Process and Row Leases

**Date**: 2026-09-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Phase 4 needs recurring background work — outbox dispatch, email sending, SLA monitoring, export running, cleanup — that must keep running independent of web traffic and stay safe if more than one instance runs at once (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §1–2, commit `3aa02ab`).

## Decision

- New project `backend/src/Tgysm.Worker` (.NET Generic Host), referencing `Tgysm.Application` and `Tgysm.Infrastructure`, never `Tgysm.Api` (enforced by an architecture test).
- Five jobs, each a `BackgroundService` on a `PeriodicTimer`: `OutboxDispatcher`, `EmailSender`, `SlaMonitor`, `ExportRunner`, `Cleanup`. Interval per job from `Worker:<Job>:IntervalSeconds`; a non-positive value is clamped to the job's default and logged.
- One DI scope and one `DbContext` per unit of work. A failure is logged and recorded; the loop keeps running.
- Claiming work: `SELECT … FOR UPDATE SKIP LOCKED LIMIT n` inside a short transaction sets `locked_until = now + lease`; the work itself runs outside that transaction; completion is a conditional update scoped to the lease owner. A crashed worker's rows become claimable again once the lease expires.
- `worker_heartbeats` (`job`, `instance`, `last_run_at`, `last_error`) is shown on `/admin/system`; an API health endpoint reports stale jobs.
- `tools/run.sh` starts the worker next to the API and Vite; `tools/e2e.sh` starts it against the E2E database.

## Alternatives Considered

### Alternative 1: Run the jobs inside the API process (`IHostedService` in `Tgysm.Api`)
- **Pros**: one process to build and deploy, no extra project.
- **Cons**: background throughput is tied to the web process's restarts and deploys; scaling the API horizontally would run N copies of every job, which still needs the same lease-safety mechanism, so the separate process buys isolation for free while a shared process would not.
- **Why not**: the worker must keep dispatching outbox events and monitoring SLA even while the API is redeployed or restarted; a shared process risks stalling both under one failure.

### Alternative 2: A scheduling/queue library (e.g. Hangfire, Quartz.NET clustering) instead of hand-rolled `SKIP LOCKED` leases
- **Pros**: battle-tested scheduling, retry and dashboards out of the box.
- **Cons**: a new dependency with its own storage schema and, for some features, licensing; more than five fixed jobs need.
- **Why not**: PostgreSQL's `FOR UPDATE SKIP LOCKED` gives the multi-instance safety required (§1 success criterion 3) with no new infrastructure, on the database already in use (ADR-0002).

## Consequences

### Positive
- Two worker instances can run side by side without double-processing the same row.
- The worker restarts and scales independently of the API.
- `worker_heartbeats` gives a direct, queryable signal of a stuck or dead job.

### Negative
- A second deployable unit to build, run and monitor.
- Operators must watch heartbeats; a silently stalled job otherwise shows no symptom until its downstream effect (unsent email, missed SLA breach) is noticed.

### Risks
- Clock skew between worker instances affects when a lease is judged expired; a lease window that is too short relative to job duration lets a slow-but-alive worker's row be reclaimed by another instance before it finishes. The completion update stays conditional on the lease owner so a late finisher cannot overwrite a reclaim, but the row can still be processed twice under specific skew; this is accepted as the same at-least-once posture as email delivery (ADR-0015).
