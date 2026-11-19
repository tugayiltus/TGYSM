# ADR-0033: Ticket-Change Links and Central Search

**Date**: 2026-11-19
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Incidents and changes were unrelated records. After a failed change, nobody could see which incidents it caused. Staff also had no single place to find a ticket, change, task, asset or article. Design: `docs/superpowers/specs/2026-11-19-related-records-and-search-design.md`. Related decisions: ADR-0013 (ticket visibility), ADR-0031 (tasks and changes as separate records), ADR-0032 (change approval).

## Decision

- **Link table.** A dedicated table `change_ticket_links` joins a change and a ticket (commit `90fb773`). Each link has a kind: `prompted` (the ticket led to the change) or `caused` (the change led to the ticket). A `caused` link accepts only an incident. A unique index on change and ticket stops duplicates.
- **Who may link.** Add or remove: an admin or a member of the change owner team. Summary-access staff get 404 on add and remove (the existing `LoadForWorkAsync` rule), not 403. Add is blocked on a `cancelled` change. Remove stays allowed on a cancelled change, so a wrong link can still be cleaned up. One global `SerializationLocks.ChangeTicketLink` key guards the add; the unique index is the second guard (commit `cc1cbc3`).
- **Event kinds.** The audit and timeline event kind check constraints list the new kinds. A second migration, `ExtendEventKindChecksForTicketLinks`, changes constraints only (commit `cc1cbc3`).
- **Disclosure exception to ADR-0013.** A link shows the number, title, status and team of the other record to IT staff who can see either side, also when they cannot open it (`canOpen` is false). Opening the record still follows its own scope. Employees never see links or linked records.
- **Report.** The change report gets the figure `caused_incidents` ("Sorun çıkaran değişiklikler"), with a drill-down list (commit `0f2fa46`).
- **Central search.** One endpoint `GET /api/v1/search` with a scope per type (commits `2b808f1`, `fee3f1b`). Tickets follow `TicketScope.Visible`. Changes follow `ChangeScope.Visible`, which includes the summary-access changes that the list and calendar already show. Tasks follow `TaskScope`. Articles follow `ArticleAccess`. Assets are for staff only and match by tag or serial; brand and model are not searched because that breaks trigram index use and Turkish casing. A full record number that the viewer can see jumps to the record; if it is not visible, the search falls through silently to the normal hits.
- **UI.** Panels "İlgili kayıtlar" on the change and "İlgili değişiklikler" on the ticket (commit `81f1a6c`). Search is an "Ara" button in the top bar that opens the Astryx `CommandPalette`, with the `/` shortcut, instead of an inline field (commits `0f062d0`, `4053b22`). Staff "Tümünü gör" for tickets opens the new workspace queue `queue=all` ("Tüm kayıtlar"), because no list of all visible tickets existed.

## Alternatives Considered

### Alternative 1: Generic `record_links` table
- **Pros**: One table for every record pair; later link types (task, asset, article) need no migration.
- **Cons**: No foreign keys on both sides; per-pair rules (`caused` only for incidents) move to code; report queries need type filters; audit and event constraints stay unclear.
- **Why not**: Only ticket-change links are needed now. A typed table keeps foreign keys, a simple unique index and fast report joins. A generic table can be added when a second pair appears.

### Alternative 2: The browser calls each list endpoint for search
- **Pros**: No new backend code.
- **Cons**: Five requests per search; employees get 403/404 noise from staff lists; the number-jump logic lives in the browser.
- **Why not**: One endpoint keeps scope rules and number jump in one place on the server.

### Alternative 3: Hide linked records the viewer cannot open
- **Pros**: Keeps ADR-0013 strict.
- **Cons**: Desk staff cannot see that their incident is linked to a change, which is the main use of the feature.
- **Why not**: The owner accepted a limited disclosure to IT staff only.

## Consequences

### Positive
- The change report shows which changes caused incidents, with a drill-down.
- Staff find any visible record from one place.
- Search follows the existing scope classes, so no new visibility rule exists except the link disclosure.

### Negative
- IT staff learn the title and status of records outside their scope, through a link.
- A global lock key makes link adds queue behind each other.
- Asset search by brand or model finds nothing in the palette; the asset list still does.
- A top bar button instead of an inline field changes the layout of the bar.

### Risks
- A later link type needs a new table or a move to a generic table.
- A wrong link by an owner team member shows a record title to other teams until it is removed.
