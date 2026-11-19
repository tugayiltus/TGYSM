# Related records and central search — design

Date: 2026-11-19. Owner: Tugay İLTUŞ. Status: draft for owner approval (approaches and cross-team rule chosen by the owner 2026-11-19).
Context: first light-ITIL module after Phase 9 (owner brief 2026-11-18: related records, then Problem, service catalogue + light CMDB, software/licence assets, light Release, monitoring webhook). Builds on ADR-0031 (tasks and changes as separate records) and ADR-0032 (change approval).

## 1. Intent

IT staff cannot see today which incidents or requests led to a change, or which incidents a change caused. A change has links only to assets (`change_assets`); a task links to one ticket or one change. Finding a record means opening the right list and using its own search box.

This module adds:

1. **Typed ticket ↔ change links**, so a change shows the tickets that prompted it and the incidents it caused, and a ticket shows its related changes.
2. **Central search** in the top bar of the app: one box that finds tickets, changes, tasks, assets and knowledge articles the viewer may see, and jumps straight to a record when a full number is typed.

Success: from an incident, IT sees the change that caused it in one click; from a change, the owner team sees the tickets behind it; the change report counts changes that caused incidents; a user who knows a number or a word reaches the record without choosing a list first.

## 2. Goals, non-goals, anti-goals

Goals
- Many-to-many ticket ↔ change links with a kind: `prompted` (the ticket led to the change) and `caused` (the change caused the ticket).
- Link panels on the change detail and the ticket detail.
- A change report figure "Sorun çıkaran değişiklikler".
- A top-bar search across five record types with the existing visibility rules.

Non-goals
- Problem records (`PRB-`) — next module, its own tables.
- Links between other pairs (task ↔ ticket stays as ADR-0031 defines; change ↔ change, ticket ↔ asset already exist or are out of scope).
- A full search page with filters and paging, ranking by relevance, fuzzy matching beyond the existing trigram substring search.
- Searching comments, attachments or audit history.

Anti-goals (failure even if it works)
- Search leaking a record's number or title to someone who may not see it (ADR-0013: out-of-scope records answer "not found"). Links disclose number/title/status to IT staff only, as a deliberate, documented exception (5.2a); never to employees.
- A link that changes the ticket's or the change's state, SLA or version check.
- A search box that slows every screen (one request per pause in typing, never one per keystroke per type).

## 3. Constraints

- Existing scope classes decide visibility: `TicketScope.Visible`, `ChangeScope` (full / summary / none), `TaskScope`, `ArticleAccess`, asset visibility (staff only).
- Substring search uses `EF.Functions.Like` with `LikePattern.Containing` + `LikePattern.EscapeText` on the trigram-indexed columns (`SearchText` on tickets, changes, tasks, knowledge articles; `NormalizedAssetTag`, `NormalizedSerialNumber` on assets).
- English code and identifiers; Turkish text only in locale files; new error codes in `ErrorCodes` + `errors.json`; new audit actions with Turkish text (`TranslationCoverageTests`).
- One new table and one migration (`change_ticket_links`). This is a schema change and gets an ADR.
- Change rules from ADR-0032 stay untouched: linking is not an edit of the change and does not bump its version.

## 4. Approaches considered

**Link storage**
- **A — dedicated `change_ticket_links` table (chosen).** Real foreign keys, a unique pair, one kind column. Matches the project pattern of one table per relation (`change_assets`, ticket ↔ asset links). Problem and Release get their own link tables later.
- B — one generic `record_links` table (source type/id, target type/id, kind). Reusable for future modules, but no foreign keys, scope checks spread over type switches, and it repeats the "common work item" idea ADR-0031 rejected.

**Search**
- **A — one endpoint `GET /api/v1/search?q=` (chosen).** Runs one small query per type through the existing scope, top 5 each, grouped. Visibility stays in one place on the server; one request per search.
- B — frontend calls each list endpoint. No backend work, but five requests per search, employees get 403/404 noise from staff lists, and number-jump logic lives in the browser.
- C — a database view or materialized search index. Scope rules would be written again in SQL; too much for five small queries.

## 5. Design (decided with the owner)

### 5.1 Data

Table `change_ticket_links`:

| Column | Type | Notes |
|---|---|---|
| `id` | uuid v7 | PK |
| `change_id` | uuid | FK `change_requests`, Restrict |
| `ticket_id` | uuid | FK `tickets`, Restrict |
| `kind` | text enum | `prompted` \| `caused`, check constraint |
| `created_by_id` | uuid | FK `users`, Restrict |
| `created_at` | timestamptz | |

- Unique (`change_id`, `ticket_id`): one link per pair; to change the kind, remove and add again.
- Index on `ticket_id` (ticket detail and report lookups).
- `caused` is allowed only for incidents (a change cannot "cause" a service request). `prompted` is allowed for incidents and requests.
- Links are removable (hard delete of the link row is fine: the audit log keeps the history). Nothing else is deleted.

### 5.2 Who may link

- Add or remove a link: admin, or a member of the change's owner team (`ChangeViewer.CanWork`). The ticket is named by number and may belong to any team: the owner team does not need to see the ticket (owner decision 2026-11-19, see 5.2a). A change outside `CanWork` answers by the existing rules (404 without access, 403 with summary access). An unknown ticket number answers 404 `tickets.not_found`.
- Allowed in every change status except `cancelled`, and for closed tickets too (a caused incident is often found after it was closed).
- **5.2a Link disclosure (owner decision, deliberate ADR-0013 exception).** A link discloses the other record's number, title, kind/status (and for changes the window) to **IT staff** who can see either side, even when they cannot open the other record. Opening the record itself still follows its own scope (404 as today). Employees never see links or linked records. Reason: incidents and the changes that cause them are usually owned by different teams; strict scope would leave the panels empty in exactly the case the module exists for.
- Each add/remove writes an audit entry on the change and on the ticket (new audit actions `change.ticket_linked`, `change.ticket_unlinked`), and a timeline line on both. No notification email.
- The change's `version` and `updated_at` are not touched by a link (like ticket comments and links today).

### 5.3 API

- `GET /api/v1/changes/{number}/tickets` — links of a change: number, title, kind (incident/request), status, team, link kind, linked at/by, `canOpen` (viewer may open the ticket). Staff with full or summary access to the change; all links are listed (5.2a).
- `POST /api/v1/changes/{number}/tickets` body `{ ticketNumber, kind }` — add. Errors: `changes.ticket_link_exists` (409), `changes.ticket_link_kind_invalid` (400, `caused` on a request), `changes.cancelled` (409).
- `DELETE /api/v1/changes/{number}/tickets/{ticketNumber}` — remove.
- `GET /api/v1/tickets/{number}/changes` — links of a ticket: change number, title, status, window, owner team, link kind, `canOpen`. Staff who can see the ticket; employees get 404 for the route. All links are listed (5.2a).

### 5.4 Screens

- **Change detail:** panel "İlgili kayıtlar" with two groups, "Bu değişikliğe yol açan kayıtlar" (`prompted`) and "Bu değişikliğin yol açtığı olaylar" (`caused`). Owner team/admin see "Kayıt bağla" (ticket number picker + kind) and a remove action per row.
- **Ticket detail (IT only):** panel "İlgili değişiklikler" with the link kind on each row. No add action here in this module; linking is done from the change (owner team decides). Employees never see the panel.
- Rows the viewer cannot open show number and title without a link and the owning team ("Ağ ekibinin kaydı").
- **Change report:** new figure `caused_incidents` — number of changes with status `Closed` and `ClosedAt` in the period that have at least one `caused` link (same "closed in the period" rule as `by_outcome`; cancelled changes never count); drill-down lists those changes; same pipeline and report scope as the Phase 9 change report.

### 5.5 Central search

- Endpoint `GET /api/v1/search?q=<text>`; minimum 2 characters after trimming; maximum 100 (`SearchLimits`); otherwise 400 `search.query_invalid`.
- If `q` is a full record number (`INC-`/`REQ-`/`CHG-`/`TSK-` or an asset tag `ASSET-YYYY-NNNN`, normalized like the existing `TicketNumber.Normalize`) and the viewer may see that record, the response carries `exact: { type, number }` and the UI navigates straight to it.
- Otherwise, per type, the first 5 matches (`SearchLimits.PerType`) by most recently updated, plus `hasMore` when there are more:
  - tickets — `TicketScope.Visible` + `SearchText` like;
  - changes — `ChangeScope.Visible` (full and summary access, the same set the calendar and list show) + `SearchText` like;
  - tasks — `TaskScope` visible + `SearchText` like;
  - assets — staff only; asset tag or serial number like;
  - knowledge articles — `ArticleAccess` + `SearchText` like.
- Text matches the indexed title/subject (`SearchText`) only; descriptions and partial numbers are not matched (builder's call, see 7). A full number the viewer may not see falls through to the text search silently, with no hint that it exists.
- Each hit: type, number/tag, title (or brand + model for assets), status, link path.
- Employees: tickets they may see and readable knowledge articles only; other groups are absent, not empty-with-count.
- "Tümünü gör" per group opens that type's list with the search filled in (`?search=`), using the existing list search.

UI: a search box in `AppHeader` (desktop: inline field; phone: icon that opens a full-width field). Debounce 300 ms (named constant), results in a popover grouped by type, keyboard navigation (arrows, Enter, Esc), empty state "Sonuç bulunamadı". Shortcut `/` focuses the box when no field has focus.

## 6. Testing

- Domain: link kind rules (`caused` only on incidents), unique pair.
- Integration: add/list/remove from the change; owner team vs other team (404/403); ticket outside the viewer's scope (404); cancelled change (409); duplicate (409); employee gets 404 on `/tickets/{n}/changes`; links do not change the change version; audit actions present.
- Search: each type respects scope (other team's tickets/tasks/changes absent; summary-only change absent; internal KB absent for employees; assets absent for employees); exact number jump only when visible; Turkish `İ/ı` matching through the existing normalization; 1-char and 101-char queries → 400.
- Report: `caused_incidents` figure = drill-down count = export rows; cross-team leak test extended.
- Frontend: link panel add/remove, kind labels, IT-only ticket panel; search box debounce, grouped results, exact jump, keyboard navigation, phone layout.
- E2E: link an incident as `caused` from a change, see it on both details and in the report; search a change by a title word and by number.

## 7. Left to the builder

Calls made by the designer (owner may override):
- `caused_incidents` counts changes, not incidents, on the `ClosedAt` basis, Closed only.
- Search matches `SearchText` and full numbers only; no description or partial-number matching (would need new indexed columns).
- Change search uses `ChangeScope.Visible`, so summary-access viewers find the changes they already see in the list and calendar.
- One audit row per entity (change and ticket) per link action; both histories show it through the existing history/timeline built from audit.

- Exact component choices from Astryx (popover/combobox for search, picker for ticket number).
- Whether the five search queries run sequentially or in parallel on separate contexts (keep one `DbContext` per request; sequential is fine at this size).
- Chunk group for the search component if it pulls in a heavy dependency (not expected).

## 8. Docs and decisions

- ADR-0033 "Typed ticket ↔ change links and central search" (new table, kinds, search scope rules).
- `tgysm.md` §2 and §19 updated with the module, as the owner rule for ITIL modules requires.
- `capabilities.md`, user guide pages and `README.md` at the end of the module.
