# ADR-0015: Transactional Outbox and Email Delivery

**Date**: 2026-09-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Ticket and account changes must trigger email without ever blocking or failing the user's action on an SMTP outage, and without losing a notification if the process crashes mid-flight (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §1, §3, commit `3aa02ab`).

## Decision

- `notification_outbox`: a handler that changes a ticket or account adds an event through `INotificationOutbox` before its single save, so the event is written in the same transaction as the change.
- `OutboxDispatcher` (worker, ADR-0014) claims pending events, resolves recipients from current data, renders subject and body from the event's template (ADR-0016), inserts `email_deliveries` rows and marks the event `dispatched`, all in one transaction. No recipient resolves → `skipped`.
- `EmailSender` claims due deliveries (`sending`, leased), sends through `IEmailSender`, then marks `sent`. On failure: `attempts + 1`, back-off 1, 5, 15, 60, 360 minutes; after 5 attempts, `failed`.
- `IEmailSender` has two implementations: MailKit SMTP in production, a pickup folder (`.eml` files) in development and tests — no live SMTP dependency for local runs or CI.
- Account-link bodies render at send time from `protected_secret` (Data Protection–encrypted); the secret and the stored body of that delivery are cleared after `sent` or `failed` (body replaced by a fixed marker), so a token never sits in a delivery row longer than needed to send it.
- At-least-once delivery is accepted: a crash after SMTP accepted the message but before the row is marked `sent` can repeat one email. The unique key `(outbox_id, recipient_user_id)` on `email_deliveries` prevents an application-level duplicate row, not a duplicate send in that crash window. This is documented behavior, not a hidden gap.
- `/admin/email-deliveries` lists deliveries (status, event, recipient, date) and retries a `failed` one (resets attempts, audited).

## Alternatives Considered

### Alternative 1: Send email synchronously inside the request/handler
- **Pros**: simplest possible flow, no extra tables or worker.
- **Cons**: SMTP latency or an outage blocks or fails the triggering action; a change that is later rolled back cannot un-send an email already handed to SMTP.
- **Why not**: violates the success criterion that an SMTP outage must never roll back or fail the user's action (§1 success criteria item 2).

### Alternative 2: A message broker (e.g. RabbitMQ, Azure Service Bus) instead of a database-backed outbox
- **Pros**: purpose-built delivery guarantees and throughput at larger scale.
- **Cons**: new infrastructure to deploy and operate; publishing to a broker from inside the same transaction as the EF Core write reintroduces the two-phase-commit problem the outbox pattern exists to avoid.
- **Why not**: a row written in the same PostgreSQL transaction as the change gives atomicity for free on infrastructure already in use (ADR-0002); a single-company, modest-volume ITSM tool does not need broker-scale throughput.

## Consequences

### Positive
- The change and the intent to notify commit atomically; a rolled-back change leaves no outbox event.
- An SMTP outage delays email, never the user's action, and recovers automatically once the worker resumes.
- Retries and failures are visible and recoverable from `/admin/email-deliveries`.

### Negative
- Two extra hops (dispatch, send) between a change and the email leaving the system; notification is eventually consistent, so a user can act on a ticket before its email arrives.
- Every notification path needs the outbox table and a template, which is more moving parts than a direct send call.

### Risks
- The accepted at-least-once guarantee means a rare crash window can duplicate one email to one recipient; this is a known, documented trade-off rather than a defect to chase.
- `protected_secret` depends on ASP.NET Core Data Protection key persistence (already flagged as a production risk in ADR-0004); losing the keys before an account-link email sends makes that link unrenderable.
