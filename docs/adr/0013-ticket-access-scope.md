# ADR-0013: Ticket Access Scope

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Employees see their own tickets and only public content; IT staff see tickets of their teams; admins see all (`tgysm.md` §4). Search, attachment download and relation lists must follow the same rules; changing an ID must not reveal anything (§20 items 4–5).

## Decision

- One `TicketScope` query filter in Application applies the rule to every ticket query: requester = me, OR (role IT and team in my active memberships), OR admin.
- Out-of-scope tickets answer 404 (`common.not_found`), never 403, so existence is not revealed.
- Visibility filter: employees get only `public` comments, attachments and timeline events; internal fields (root cause, detection) are omitted from their DTO.
- Transfers can move a ticket out of the old team's scope immediately.

## Alternatives Considered

### Alternative 1: 403 for out-of-scope
- **Pros**: clearer error for honest mistakes.
- **Cons**: confirms that a ticket number exists.
- **Why not**: numbers are guessable (sequential).

## Consequences

### Positive
- One place defines scope; every endpoint reuses it.

### Negative
- IT staff lose access to a ticket right after transferring it away.

### Risks
- A new query that skips `TicketScope` leaks data; integration tests cover each endpoint.
