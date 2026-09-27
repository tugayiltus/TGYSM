# ADR-0016: Database Email Templates

**Date**: 2026-09-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Email wording must be editable in `/admin` without a deploy, seeded with Turkish defaults, and safe against injection since the product sends plain text, not HTML (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §1, §3.4, commit `3aa02ab`).

## Decision

- `email_templates`: one row per `event_type` (unique), with `subject`, `body`, `updated_by`, `updated_at`, `xmin`; seeded by migration with Turkish defaults; edited in `/admin`.
- Plain text only, no HTML — nothing to sanitize and no markup-injection surface.
- Variables are `{{name}}` substitutions from a fixed list per event (`ticketNumber`, `subject`, `status`, `resolution`, `completedAt`, `ticketUrl`, `recipientName`, `actionUrl`, `expiresAt`, `slaMetric`, `dueAt`, scoped to the events that use them); rendering evaluates nothing beyond substitution.
- Saving a template with an unknown or not-allowed variable fails with `email_templates.unknown_variable`.
- Dates render in the company time zone with Turkish formatting.

## Alternatives Considered

### Alternative 1: Hardcoded templates in code (resource files)
- **Pros**: type-safe, no runtime editing surface to secure.
- **Cons**: any wording change needs a deploy; cannot be edited in `/admin` as required.
- **Why not**: the same principle that keeps Turkish UI text in editable i18n files rather than code (ADR-0006) applies to email text — the owner must be able to fix wording without a release.

### Alternative 2: HTML templates with a templating engine (e.g. Razor, Scriban)
- **Pros**: richer formatting and styling than plain text.
- **Cons**: HTML introduces a markup/injection surface that needs an active sanitizer; most ITSM notification email is read in plain-text-capable clients anyway.
- **Why not**: plain text removes the injection class entirely, matching the "no HTML" decision in spec §3.4, and a fixed-variable substitution model (no expression language) removes template injection as well.

## Consequences

### Positive
- Wording changes are an audited admin action (`updated_by`, `updated_at`, `xmin`), not a code change.
- No template injection is possible: variables are a fixed, per-event allow-list with plain substitution, never an evaluated expression.

### Negative
- No rich formatting; links in email bodies are plain URLs.
- A template edit bypasses code review, unlike a code-level wording change.

### Risks
- An admin can introduce a variable-name typo when editing; caught at save time by `email_templates.unknown_variable` rather than silently failing at send time.
