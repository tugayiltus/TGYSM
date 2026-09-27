# ADR-0009: One Ticket Table for Incidents and Requests

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

`tgysm.md` §16 sketches a shared `Ticket` base with `IncidentDetails` and `RequestDetails`. Incidents and requests share status, assignment, comments, attachments, timeline, assets and access scope; they differ in a few fields (request type and approval for requests; detection, root cause and outcome values for incidents). Queues and search always span both kinds.

## Decision

Store both kinds in one `tickets` table with a `kind` column. Kind-specific columns are nullable and guarded by CHECK constraints (for example `request_type_id` is required for requests and must be NULL for incidents). Numbering stays separate per kind (`INC-…`, `REQ-…`, ADR-0011). Domain rules still differ by kind inside the `Ticket` aggregate.

## Alternatives Considered

### Alternative 1: Base table plus detail tables (one-to-one)
- **Pros**: no nullable kind-specific columns; mirrors the analysis sketch.
- **Cons**: every queue, search and detail query needs joins; two rows to keep consistent per ticket; more EF mapping.
- **Why not**: the kind-specific part is a handful of columns; CHECK constraints keep the rules in the database.

### Alternative 2: Two unrelated tables
- **Pros**: fully separate models.
- **Cons**: duplicated comments, attachments, timeline and scope logic; cross-kind queues need UNION queries.
- **Why not**: the shared workflow is most of the model.

## Consequences

### Positive
- Simple queue and search queries; one aggregate for shared rules.

### Negative
- Some columns are NULL for one kind.

### Risks
- New kind-specific fields must come with CHECK constraints, or rules drift into code only.

## Amendment 2026-09-27

Implemented in e12081f (schema) and 3a9e6ce..32bdbbe (workflows). Added `search_text` (Turkish search key of the subject) and `requires_approval` (copied from the request type at creation). `detection` was left out because no workflow uses it. A `fulfilled` outcome was added for completed requests.
