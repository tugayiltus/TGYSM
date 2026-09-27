# Phase 4 SLA and Email Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. **Ruling (plan format, carried over from Phase 2–3, owner-approved):** tasks list files, interfaces and tests instead of full inline code; code is written once, under TDD, from the spec. Each task: write the listed failing tests, watch them fail, implement, run the task command, record the result.

**Goal:** Deliver Phase 4 — worker process, transactional outbox and email delivery, database templates, activation/reset links, business calendar and SLA — per spec §2–§5.

**Architecture:** A new `Tgysm.Worker` host runs periodic jobs over the same Application layer and claims rows with `FOR UPDATE SKIP LOCKED` leases. Handlers write outbox events in their single save; the worker expands them into per-recipient deliveries and sends through `IEmailSender`. SLA time math lives in Domain (`BusinessTimeCalculator`, `SlaInstance`); ticket handlers drive SLA instances, and `SlaMonitor` raises warning/breach events.

**Tech Stack:** .NET 10, EF Core + Npgsql, PostgreSQL 17, MailKit, ASP.NET Core Data Protection, React 19 + TanStack Query, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` (§2–§5, Phase 4 parts of §8–§9)

## Global Constraints

- All Phase 1–3 constraints apply (English code/URLs/DB, Turkish UI via i18n, zero warnings, no lint disables, no magic literals, `tools/` scripts, TDD, no push, stage named paths only, no AI attribution).
- Backend tests: `dotnet test --solution backend/Tgysm.slnx` (MTP); filters `--filter-class` / `--filter-method`.
- New error codes → `ErrorCodes` + `errors.json`; audit actions → `AuditActions` + `admin.json` `auditActions`; entity types → `AuditEntityTypes` + `entityTypes`; email event types → `NotificationEventTypes` + `admin.json` `emailEvents` (enforced by `TranslationCoverageTests`).
- Time only through `TimeProvider`; tests use `FakeTimeProvider`.
- Every state change: domain method → handler writes entity + history + audit + outbox event (+ SLA change) in one `SaveChanges`.
- Worker: one DI scope per unit of work; interval ≤ 0 → default + warning log; kill nothing, catch per iteration, record in `worker_heartbeats`.
- Tokens, passwords, SMTP secrets never logged, audited, returned, or kept in a delivery body after `sent`/`failed`.
- Seed values (spec §1 assumptions): time zone `Europe/Istanbul`; Mon–Fri 09:00–18:00; targets P1 30 min / 240 min, P2 60 / 480, P3 240 / 3 × 540, P4 480 / 5 × 540 (business minutes, first response / resolution); warning 75 %; pause rules `user`, `manager_approval` → resolution only; activation 72 h, reset 1 h; retry back-off 1, 5, 15, 60, 360 min, max 5 attempts.
- After API changes: `tools/gen-api-types.sh`.

## Review Focus

1. Two worker instances running the same job at once (dispatcher, sender, SLA monitor) — each row processed once; test in Tasks 2, 5, 6, 12 with two parallel runs.
2. SMTP down for longer than all retries, then back — delivery ends `failed`, admin retry sends it; the user action that queued it succeeded at once (Task 6).
3. Priority change while paused, and hold/resume across a holiday or weekend — elapsed time and `due_at` stay correct (Tasks 9, 11).
4. Set-password link used twice in parallel, expired, or after a newer link was issued — one success, others `auth.token_invalid`; forgot-password for unknown/inactive email looks identical (Task 7).
5. Template edited to reference a variable not allowed for its event (e.g. `resolution` in `ticket_created`) — rejected on save; an old stored template never leaks internal fields (Task 4).

---

### Task 1: ADRs 0014–0018
Files: `docs/adr/0014-worker-process-and-row-leases.md`, `0015-transactional-outbox-and-email-delivery.md`, `0016-database-email-templates.md`, `0017-password-set-links.md`, `0018-sla-calculation-model.md`, `docs/adr/README.md`. Template `~/.claude/templates/adr-template.md`, status `accepted`, Deciders `Tugay İLTUŞ`, cite spec commit `3aa02ab`; 0017 links ADR-0004 and ADR-0007.
Verify: index rows match `docs/adr/*.md`.

### Task 2: Worker host, leases, heartbeats
Files: `backend/src/Tgysm.Worker/{Tgysm.Worker.csproj, Program.cs, WorkerOptions.cs, PeriodicJob.cs, HeartbeatRecorder.cs, appsettings.json, appsettings.Development.json}`, `Tgysm.slnx`, `Domain/Operations/WorkerHeartbeat.cs`, configuration + migration `WorkerHeartbeats`, `Application/Abstractions/IAppDbContext.cs` (DbSet), `tools/run.sh` (start worker), `tools/e2e.sh` (start worker on E2E DB), `ArchitectureTests/LayerDependencyTests.cs`.
Interfaces:
- `abstract class PeriodicJob(IServiceScopeFactory scopes, IOptionsMonitor<WorkerOptions> options, TimeProvider clock, ILogger logger) : BackgroundService` with `protected abstract string Name`, `protected abstract Task RunOnceAsync(IServiceProvider scoped, CancellationToken ct)`.
- `WorkerOptions.Jobs: Dictionary<string, JobOptions>`; `JobOptions { int IntervalSeconds; int BatchSize; int LeaseSeconds; }`; `static TimeSpan ResolveInterval(int configured, TimeSpan fallback, ILogger)`.
- `HeartbeatRecorder.RecordAsync(job, instance, error?, ct)` upsert on (`job`, `instance`).
- Lease helper in Infrastructure: `RowLease.ClaimAsync<T>(DbSet<T>, string sqlPredicate, int batch, TimeSpan lease, CancellationToken)` built on `FromSql("… FOR UPDATE SKIP LOCKED")` inside `RunInTransactionAsync`.
Tests: `ArchitectureTests` — Worker does not reference Api; `Application.Tests/WorkerOptionsTests` — 0 and negative intervals fall back and log; `IntegrationTests/Worker/RowLeaseTests` — two parallel claims over 10 rows return disjoint sets totalling 10; expired lease is claimable again; `PeriodicJobTests` — exception in one iteration is recorded and the next iteration runs.

### Task 3: Outbox model and emission from handlers
Files: `Domain/Notifications/{OutboxEvent.cs, OutboxStatus.cs, NotificationEventTypes.cs}`, `Application/Abstractions/INotificationOutbox.cs`, `Application/Notifications/NotificationOutbox.cs`, configuration + migration `NotificationOutbox`, edits in `Features/Tickets/{CreateTicket,AssignTicket,Comments,TicketStatusActions,CreateRelatedRequest}.cs`, `Features/Approvals/ApprovalHandlers.cs`.
Interfaces:
- `NotificationEventTypes` constants: `TicketCreated = "ticket_created"`, `TicketAssigned`, `PublicReply`, `ApprovalRequested`, `ApprovalDecided`, `TicketCompleted`, `SlaWarning`, `SlaBreached`, `AccountActivation`, `PasswordReset`.
- `INotificationOutbox.Add(string eventType, Guid? ticketId, Guid? userId, IReadOnlyDictionary<string, string>? data = null, string? protectedSecret = null)` — adds to the context, never saves.
- `OutboxEvent.Create(type, ticketId, userId, payloadJson, protectedSecret, now)`; statuses `Pending`, `Dispatched`, `Skipped`.
Tests: `IntegrationTests/Notifications/OutboxEmissionTests` — each ticket action writes exactly the expected event (create, claim, transfer, IT public reply, requester reply, internal note → none, approval requested/decided, complete); a failing action (validation 409) writes no event; payload holds ids only (no subject/resolution text).

### Task 4: Email templates
Files: `Domain/Notifications/{EmailTemplate.cs, TemplateVariables.cs, TemplateRenderer.cs}`, configuration + migration `EmailTemplates` (seed Turkish defaults for all 10 events, sample completion text from `tgysm.md` §10), `Features/Notifications/Templates/{ListEmailTemplates,UpdateEmailTemplate}.cs`, `Api/Endpoints/Admin/EmailTemplateEndpoints.cs` (`GET /admin/email-templates`, `PUT /admin/email-templates/{eventType}` with `version`), error codes `email_templates.unknown_variable`, `email_templates.variable_not_allowed`.
Interfaces:
- `TemplateVariables.AllowedFor(string eventType) : IReadOnlySet<string>` per spec §3.4.
- `TemplateRenderer.Validate(string text, string eventType) : IReadOnlyList<string>` (bad names); `TemplateRenderer.Render(string text, IReadOnlyDictionary<string,string> values) : string` — `{{name}}` replace only, no expressions, missing value → empty.
- `EmailTemplate.Update(subject, body, actorId, now)` (subject ≤ 200, body ≤ 10,000, validated).
Tests: `Domain.Tests/TemplateRendererTests` — replaces, ignores `{{ unknown }}` spacing variants consistently, no recursion (value containing `{{x}}` stays literal); `TemplateVariablesTests` — `resolution` only in `ticket_completed`, `actionUrl` only in account events; `IntegrationTests/Notifications/EmailTemplateAdminTests` — admin lists 10 seeded templates, update with not-allowed variable → 400 field error, stale version → 409, IT staff → 403, update audited.

### Task 5: Outbox dispatcher
Files: `Application/Notifications/{RecipientResolver.cs, NotificationContextLoader.cs, DispatchOutbox.cs}`, `Domain/Notifications/{EmailDelivery.cs, DeliveryStatus.cs}`, configuration + migration `EmailDeliveries`, `Worker/Jobs/OutboxDispatcherJob.cs`, `Application/Common/CompanyClock.cs` (format dates in calendar time zone, Turkish).
Interfaces:
- `RecipientResolver.ResolveAsync(OutboxEvent, CancellationToken) : Task<IReadOnlyList<Recipient>>` with `record Recipient(Guid UserId, string Email, string FullName)`; rules from spec §3.3 (actor/author excluded, inactive excluded, team fallback).
- `NotificationContextLoader.LoadAsync(OutboxEvent, Recipient, ct) : Task<IReadOnlyDictionary<string,string>>` — only allowed variables; `ticketUrl` = `App:PublicBaseUrl` + `/tickets/{number}`.
- `DispatchOutboxHandler.HandleAsync(int batch, TimeSpan lease, ct) : Task<int>` — claim, resolve, render, insert deliveries, mark dispatched/skipped in one transaction per event.
- `EmailDelivery.Create(outboxId, recipient, templateType, subject, body, now)`; account events store `body = null` until send.
Tests: `IntegrationTests/Notifications/OutboxDispatcherTests` — completion event renders resolution but not internal note or root cause; public reply by requester goes to assignee; transfer to team only → active members, not actor; inactive requester → `skipped`; two parallel dispatch runs → each event dispatched once, unique (`outbox_id`, `recipient_user_id`) never violated.

### Task 6: Email sending, retry, admin deliveries
Files: `Application/Abstractions/IEmailSender.cs`, `Infrastructure/Email/{EmailOptions.cs, SmtpEmailSender.cs, PickupFolderEmailSender.cs, ErrorSanitizer.cs}`, `Infrastructure/DependencyInjection.cs` (MailKit package, `Email:Mode` switch, fail fast when missing), `Application/Notifications/{SendDueEmails.cs, RetryPolicy.cs}`, `Worker/Jobs/EmailSenderJob.cs`, `Features/Notifications/Deliveries/{ListEmailDeliveries,RetryEmailDelivery}.cs`, `Api/Endpoints/Admin/EmailDeliveryEndpoints.cs` (`GET /admin/email-deliveries`, `POST /admin/email-deliveries/{id}/retry`), audit action `email_delivery.retried`.
Interfaces:
- `IEmailSender.SendAsync(EmailMessage message, CancellationToken)`; `record EmailMessage(string To, string ToName, string Subject, string Body)`.
- `RetryPolicy.NextAttempt(int attempts, DateTimeOffset now) : DateTimeOffset?` (null after 5).
- `SendDueEmailsHandler.HandleAsync(int batch, TimeSpan lease, ct) : Task<int>`; account deliveries render `actionUrl` from `IDataProtector.Unprotect(protected_secret)` at send time, then clear secret and body.
- `ErrorSanitizer.Sanitize(Exception) : string` — type + message, credentials/token patterns removed, ≤ 500 chars.
Tests: `Application.Tests/RetryPolicyTests`; `IntegrationTests/Notifications/EmailSenderTests` (fake failing `IEmailSender`) — failure increments attempts and schedules back-off; 5 failures → `failed`; admin retry → `pending`, next run sends; pickup mode writes one `.eml` with UTF-8 Turkish subject; after send of an account email the stored body and secret are null; two parallel sender runs send each delivery once; `last_error` never contains the SMTP password; queuing ticket action returns 2xx while sender fails.

### Task 7: Password tokens and account links API
Files: `Domain/Users/{PasswordToken.cs, PasswordTokenPurpose.cs}`, configuration + migration `PasswordTokens`, `Application/Security/PasswordTokenIssuer.cs`, `Features/Auth/{ForgotPassword,SetPassword}.cs`, edits `Features/Users/{CreateUser,ResetUserPassword}.cs` (`delivery`), `Api/Endpoints/AuthEndpoints.cs`, `Api/Contracts/{AuthContracts,AdminContracts}.cs`, rate limit policy `forgot-password`, error code `auth.token_invalid`, audit actions `user.activation_link_sent`, `user.password_reset_link_sent`, `user.password_set_by_link`.
Interfaces:
- `PasswordTokenIssuer.IssueAsync(Guid userId, PasswordTokenPurpose purpose, CancellationToken) : Task<string rawToken>` — voids unused tokens of same purpose, stores SHA-256, adds outbox event with protected raw token.
- `enum CredentialDelivery { EmailLink, TemporaryPassword }` on `CreateUserCommand` / `ResetUserPasswordCommand`; result `temporaryPassword` null for `EmailLink`.
- `SetPasswordHandler`: conditional `ExecuteUpdateAsync` on `used_at IS NULL AND expires_at > now`; 0 rows → `auth.token_invalid`; then password + clear `must_change_password` + rotate stamp in the same transaction.
Tests: `IntegrationTests/Auth/PasswordLinkTests` — admin create with `email_link` returns no password and queues activation; set-password with valid token signs in afterwards without forced change; reused/expired/superseded token → 400 `auth.token_invalid`; parallel set-password with one token → one 204; forgot-password for unknown, inactive and active email → identical 202 bodies and headers, event only for active; rate limit → 429; other sessions end after set; `temporary_password` path unchanged; audit rows contain no token.

### Task 8: Account and email admin UI
Files: `frontend/src/features/auth/{SetPasswordPage.tsx, ForgotPasswordPage.tsx}`, login page link, `features/admin/users/*` (delivery radio: "E-posta ile bağlantı gönder" default / "Geçici şifre göster"), `features/admin/email/{EmailTemplatesPage.tsx, EmailTemplateEditor.tsx, EmailDeliveriesPage.tsx}`, `features/admin/system/SystemPage.tsx` (heartbeats; `GET /admin/system/workers` added in this task with its handler `Features/Operations/ListWorkerHeartbeats.cs`; the API health endpoint reports `degraded` when a job's `last_run_at` is older than 3 × its interval — backend test in `IntegrationTests/Operations/WorkerHealthTests`), routes, i18n `auth.json`, `admin.json`.
Tests: set-password strips token from URL, shows `auth.token_invalid` text, validates password rules; forgot-password always shows the same confirmation; user create with email link shows "bağlantı gönderildi" and no password box; template editor lists allowed variables and shows server field error; deliveries filter + retry button on `failed` only; system page marks stale jobs.

### Task 9: Business time calculator
Files: `Domain/Sla/{BusinessCalendarRules.cs, WorkingRange.cs, BusinessTimeCalculator.cs}`.
Interfaces:
- `record BusinessCalendarRules(TimeZoneInfo Zone, IReadOnlyDictionary<DayOfWeek, WorkingRange> Hours, IReadOnlySet<DateOnly> Holidays)`; `record WorkingRange(TimeOnly Start, TimeOnly End)` (Start < End).
- `BusinessTimeCalculator.SecondsBetween(BusinessCalendarRules, DateTimeOffset fromUtc, DateTimeOffset toUtc) : long`; `AddSeconds(BusinessCalendarRules, DateTimeOffset fromUtc, long seconds) : DateTimeOffset`; loop bounded (refuse calendars with no working day → `sla.calendar_empty`).
Tests: `Domain.Tests/BusinessTimeCalculatorTests` — inside one day; start before opening; end after closing; across weekend; holiday skipped; Friday 17:30 + 60 min → Monday 09:30; zero seconds returns start moved to next opening; from > to → 0; DST zone (`Europe/Berlin`, last Sunday of March) keeps wall-clock hours; `AddSeconds` and `SecondsBetween` are inverse over 200 random cases (fixed seed).

### Task 10: SLA calendar and policy
Files: `Domain/Sla/{BusinessCalendar.cs, BusinessHours.cs, Holiday.cs, SlaPolicyVersion.cs, SlaTarget.cs, SlaPauseRule.cs, SlaMetric.cs}`, configurations + migration `SlaSetup` (seed per Global Constraints, `Europe/Istanbul`), `Application/Sla/{CalendarProvider.cs, PolicyProvider.cs}`, `Features/Sla/Admin/{GetCalendar,UpdateCalendar,AddHoliday,RemoveHoliday,GetPolicy,CreatePolicyVersion}.cs`, `Api/Endpoints/Admin/SlaEndpoints.cs`, audit actions `sla.calendar_updated`, `sla.holiday_added`, `sla.holiday_removed`, `sla.policy_versioned`, error codes `sla.invalid_time_zone`, `sla.invalid_hours`, `sla.calendar_empty`, `sla.holiday_exists`, `sla.target_missing`.
Interfaces:
- `CalendarProvider.GetRulesAsync(ct) : Task<BusinessCalendarRules>`.
- `PolicyProvider.GetCurrentAsync(ct) : Task<SlaPolicySnapshot>`; `record SlaPolicySnapshot(Guid VersionId, IReadOnlyList<SlaTarget> Targets, IReadOnlyList<SlaPauseRule> PauseRules)`; `SlaPolicySnapshot.TargetFor(TicketKind, Priority, Guid? requestTypeId, SlaMetric) : SlaTarget` (request type override first).
- `CreatePolicyVersion` requires a target for every kind × priority × metric; old version `is_current = false`, new `true`, under `SerializationLocks.SlaPolicy = 7_005`.
Tests: `Domain.Tests/SlaPolicyTests` (target lookup incl. override); `IntegrationTests/Sla/SlaAdminTests` — non-IANA zone rejected, overlapping/inverted hours rejected, all-closed week rejected, duplicate holiday 409, new version keeps old rows, two parallel versioning calls leave exactly one current, IT staff 403, audited.

### Task 11: SLA instances driven by ticket actions
Files: `Domain/Sla/{SlaInstance.cs, SlaPause.cs, SlaEvent.cs, SlaEventKind.cs, SlaResult.cs}`, configuration + migration `SlaInstances`, `Application/Sla/SlaTracker.cs`, edits in ticket handlers (create, hold, resume, approval granted/rejected/return, priority change, public comment, complete, cancel).
Interfaces:
- `SlaInstance.Start(ticketId, metric, SlaTarget, policyVersionId, now, BusinessCalendarRules, bool paused, HoldReason? pauseReason)`, `Pause(HoldReason, now, rules)`, `Resume(now, rules)`, `ChangeTarget(int targetMinutes, int warningPercent, now, rules)`, `Stop(SlaResult?, now, rules)` (result computed `met`/`breached` when null), each appends `SlaEvent`s; paused → `due_at`/`warn_at` null.
- `SlaTracker` (scoped): `OnCreatedAsync(Ticket, ct)`, `OnHoldAsync(Ticket, HoldReason, ct)`, `OnResumeAsync(Ticket, ct)`, `OnPriorityChangedAsync(Ticket, ct)`, `OnPublicReplyAsync(Ticket, Guid authorId, UserRole authorRole, ct)`, `OnCompletedAsync(Ticket, ct)`, `OnCancelledAsync(Ticket, ct)` — load instances, apply, never save.
Tests: `Domain.Tests/SlaInstanceTests` — pause/resume accumulates only business time; change target while paused keeps elapsed and sets due on resume; stop after due → `breached`; breach recorded before a target raise stays; `IntegrationTests/Sla/SlaTrackingTests` (FakeTimeProvider) — incident creates two running instances with P3 targets; request needing approval starts resolution paused; `user` hold pauses resolution not first response; `vendor` hold pauses nothing; transfer and claim change nothing; first IT public reply stops first response as `met`; requester reply and internal note do not; priority P3→P1 recomputes due from original start; complete → `met`/`breached`; cancel and approval rejection → `cancelled`; policy change after creation does not change existing instances.

### Task 12: SLA monitor
Files: `Application/Sla/MonitorSla.cs`, `Worker/Jobs/SlaMonitorJob.cs`.
Interfaces: `MonitorSlaHandler.HandleAsync(int batch, CancellationToken) : Task<(int Warned, int Breached)>` — conditional `ExecuteUpdateAsync` (`warned_at IS NULL AND warn_at <= now AND stopped_at IS NULL AND running_since IS NOT NULL`); for rows it won, append `SlaEvent` + outbox `sla_warning` / `sla_breached` in one transaction.
Tests: `IntegrationTests/Sla/SlaMonitorTests` — crossing 75 % emits one warning; crossing due emits one breach (and a warning if none yet); paused instance never warns; two parallel monitor runs over 20 due instances → 20 warnings total, no duplicates; recipients are assignee or team members (via dispatcher).

### Task 13: SLA in tickets and admin UI
Files: `Features/Tickets/{TicketDtos,ListTickets,GetTicket}.cs` (`sla` block: metric, due, remaining business seconds, paused, warned, breached, result), `queue=sla_risk`, `tools/gen-api-types.sh` output, `frontend/src/features/tickets/{SlaBadge.tsx, TicketDetailPage.tsx, QueuePage…}`, `features/workspace/WorkspacePage.tsx` ("SLA Riski" card), `features/admin/sla/{SlaCalendarPage.tsx, SlaPolicyPage.tsx}`, i18n `tickets.json`, `admin.json`.
Tests: backend — `sla_risk` queue returns warned/breached running tickets within scope only and is 403 for employees; ticket detail carries the `sla` block for every role in scope; frontend — badge states (running, paused, warned, breached, met), workspace card opens `/workspace/queue?queue=sla_risk`, calendar form validation, policy save creates new version and shows it.

### Task 14: Cleanup job
Files: `Application/Operations/Cleanup.cs`, `Worker/Jobs/CleanupJob.cs`.
Interfaces: `CleanupHandler.HandleAsync(ct)` — delete password tokens expired > 30 days; delete dispatched/skipped outbox events > 30 days with all deliveries `sent`/`failed`. (Phase 5 adds expired exports.)
Tests: `IntegrationTests/Operations/CleanupTests` — only rows past the cutoff are removed; pending events are kept.

### Task 15: E2E, gates, review, commit
Files: `frontend/e2e/{account-links.spec.ts, email.spec.ts, sla.spec.ts}`, `frontend/e2e/world.ts` (pickup-folder reader helper), `tools/e2e.sh` (`Email__Mode=Pickup`, per-run pickup folder).
Steps: E2E at 360 and 1280 px — admin creates user with link → activation `.eml` → set password → sign in; IT completes ticket → completion `.eml` has resolution, no internal note; SLA badge visible on a ticket and SLA Riski card count. Then `tools/build.sh`, `tools/e2e.sh`; specialist reviews (C#, database, security, React) once on the finished tree; fix Critical/Important with RED → GREEN; ask owner before commit; local commit(s) with named paths; name the docs that need a line (`capabilities.md`, `README.md`, `CLAUDE.md`, `SECURITY.md`) and stop.
