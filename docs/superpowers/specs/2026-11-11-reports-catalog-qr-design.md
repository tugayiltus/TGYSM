# Report filters, device catalog flow and asset QR labels — design

Date: 2026-11-11. Owner: Tugay İLTUŞ. Branch: `ui-astryx` (continues the Astryx work, ADR-0027; merged to `master` at the end).

Source: the owner's review of the Astryx build (measured at 1440 px and 375 px). Order of work is the owner's: reports first, then the catalog flow, then QR labels, then the smaller findings.

## 1. Report filters and tabs (fix first)

**Problem.** At 1440 px the filter area of every report (Olaylar, Talepler, Operasyon, Transferler, Varlıklar, Garanti) is squeezed to about 200 px: ten fields stack in one column and the filter area alone is 668 px high (`frontend/src/features/reports/ReportFilterBar.tsx`, `Grid columns={FILTER_COLUMNS}`). At 375 px part of the report tab row scrolls sideways, and the browser reports a missing accessibility link between tab and content panel (`ReportsPage.tsx`, `ReportTabs`).

**Goals**
- The filter grid takes the full usable width of its card. The two dates (start, end) stay side by side. The other fields spread over two to four columns depending on the width.
- First view shows only the main filters: period (preset + dates), priority, status and team. The rest sit under a collapsed **"Diğer filtreler"** section. When a hidden filter is active, the section label shows the count, e.g. "Diğer filtreler (2)".
- Active filter chips and the **"Temizle"** button stay visible above the figures at all times, also when "Diğer filtreler" is closed.
- A report only shows the main filters it understands (the existing per-report filter sets stay; a report without priority simply has no priority field).
- Phones (< 768 px): the tab row is replaced by one **"Rapor türü"** selector listing all six reports; choosing one navigates with the same filter carry-over the tabs use today. The filter area keeps today's "Filtreler (n)" drawer.
- Accessibility: the tab list and the report content are linked (tab `aria-controls` ↔ panel `role="tabpanel"` + `aria-labelledby`), or, if the tabs stay links (they navigate), they are marked up as navigation links with `aria-current="page"` instead of a tab pattern. Builder picks whichever Astryx supports cleanly; the browser warning must be gone.

**Non-goals.** No change to which filters exist or to report queries.

**Done when.** At 1440 px no report's filter area is taller than two rows of fields with "Diğer filtreler" closed; at 375 px there is no horizontal scroll on any report page; the accessibility warning is gone; E2E covers both widths.

## 2. Brand and model in the asset form

**Current state.** The admin creates a model with a required brand and device type (`/admin/models`). The asset form (`frontend/src/features/assets/AssetFormDialog.tsx`) already narrows brand by type and model by brand + type; changing the type clears brand and model; changing the brand clears the model. Live data has one brand and one model, so brand switching has not been seen with real data.

**Goals**
- Order **Tür → Marka → Model** is visible: the three fields sit in that order in one row on wide screens, each labelled.
- A later field is disabled until the earlier one has a value (Marka until Tür, Model until Marka).
- An empty list says why, with the chosen values in the text: e.g. "Bu tür için aktif marka yok." and "Bu marka için aktif masaüstü modeli yok." (type name in lower case, Turkish). New strings go to `tr/assets.json`.
- Changing the brand clears the model, and the model list shows only active models of the new brand and the chosen type.
- **Edit exception, tightened.** Today an edited asset's own (possibly inactive) model is added to the options whenever the type matches, even after another brand is chosen. New rule: add it only while **both** the asset's original type **and** original brand are selected.
- `/admin/brands` rows get two actions: **"Modelleri gör"** opens `/admin/models` filtered to that brand (filter held in the URL, like the other list filters; the models page gains a brand filter in its `ListToolbar`), and **"Model ekle"** opens the model create form with that brand pre-selected.

**Done when.** Unit tests cover disabled states, empty-reason texts, model clear on brand change, and the tightened edit exception with two brands; E2E creates two brands and checks the switch.

## 3. QR code on every asset

**Who scans and what opens.** Any signed-in user may scan. The QR holds a permanent link built from the asset id: `{App:PublicBaseUrl}/scan/asset/{assetId}`. The asset id never changes, so labels printed now stay valid; existing assets get the same link with no data migration (nothing is stored — the link is derived).

The `/scan/asset/:id` route (frontend) decides by role after the normal sign-in (an unsigned user goes through `/login` and returns to the scan link):
- IT staff and admin → the asset detail `/assets/:id` (existing permission checks apply).
- Employee → the new incident form `/tickets/new/incident` with that device pre-selected as "İlgili cihaz", showing its asset tag, brand and model.

**Backend changes**
- Incident creation: today the optional `AssetId` must be assigned to the requester (`CreateTicket.cs`). Owner decision: from a QR scan **any** device may be linked, if its status is `InStock`, `Assigned` or `Maintenance`; `Lost` and `Retired` are refused (new `ErrorCodes` constant + Turkish text). Mechanism (my call): the rule applies to every incident creation; the "İlgili cihaz" dropdown still lists only the requester's own devices, so other devices arrive only through a scan.
- **Only creation changes** (owner decision). Linking an asset to an existing ticket later (`Links.cs`, `LinkTicketAssetHandler`) keeps today's rule: an employee may link only a device assigned to them.
- A small read endpoint usable by any signed-in user returns what the incident form shows for a scanned device: id, asset tag, brand, model, type, and a `canReport` flag (false for `Lost`/`Retired`). Nothing else (no assignee, location or notes). 404 for an unknown id.
- When `canReport` is false, the employee scan page shows "Bu cihaz için arıza kaydı açılamaz." with a link to the empty incident form, instead of a prefilled form (my call).
- Security: any signed-in user who knows an asset id (uuid v7) can read its tag, brand, model and type and link it to a new incident. The owner accepts this: the data is not sensitive and the link at creation is already audited (`TicketAssetLinked`). Scans themselves are not audited.
- The frontend needs `App:PublicBaseUrl` to build the QR link (in production it equals the site origin, but a QR must always carry the configured company address, never `localhost`). Expose it to signed-in users (e.g. in an existing session/me response or a small app-info endpoint — builder's choice). Run `tools/gen-api-types.sh` after.

**Asset detail (IT/admin)**
- A **QR kod** card: the QR image, **"İndir"** (downloads the label as PNG) and **"Etiketi yazdır"** (opens the print view for this one asset).

**Label (thermal printer, 50×30 mm)**
- One label per page, `@page { size: 50mm 30mm; margin: 0 }`, printed with the browser print dialog from a print-only view.
- Content: QR on the left (as large as the height allows, with quiet zone); on the right, top to bottom: **asset tag** (largest, bold, readable), brand + model, serial number (if any). No company name (owner decision). Text truncates rather than wraps past the label.
- QR generated in the browser (a small, maintained QR library as a dependency; error correction level M or higher; its own `vendor-*` chunk only if it exceeds the size rule in the React guide).

**Inventory list bulk print**
- `/assets` gets row selection and a **"Seçili cihazların etiketlerini yazdır"** action, enabled when at least one row is selected. It opens the same print view with one label per selected asset, in list order. Selection covers the current page; builder decides whether "select all on page" is offered (yes is expected).
- Purpose: first-time labelling and bulk entry (CSV import) without opening each detail.

**CSV import.** No extra step: imported assets have ids, so they have links; the bulk print covers labelling them. (The owner's "when saved or imported, produce a link" is satisfied by the derived link.)

**Non-goals.** No public (unauthenticated) device page. No stored QR images. No label designer.

**Done when.** Scanning (simulated by opening the link) as employee lands on the prefilled incident form and the created ticket shows the device; as IT lands on the detail; print view renders N labels at 50×30 mm; tests cover the role routing, the backend rule change and the read endpoint's field list.

## 4. Smaller findings

- **Asset form loses changes silently.** "Vazgeç", the dialog close button and Escape all ask the same unsaved-changes confirmation when the form is dirty (today only route leave is guarded). Apply in the shared `FormDialog` path so every form dialog gets it, not only the asset form.
- **Empty report cards.** When a figure's count is 0, drop the "Tümünü listele (0)" link and show in the card that there is no data for the chosen period (period text from the active filter, e.g. "Son 30 gün için kayıt yok.").
- **Terminology.** The report tab "Varlıklar" becomes **"Envanter"** (same set of devices as the Envanter menu). "Zimmetlerim" stays. Check other report strings that say "varlık" for the same set (e.g. the count "{{count}} varlık") and align them to "cihaz" where they mean devices; the builder lists any change outside the tab name in the commit message.

## Constraints

- Astryx components per `frontend/CLAUDE.md`; `tools/lint.sh` (incl. Astryx doctor budget) and `tools/test.sh` coverage gates must pass; no `eslint-disable`; named constants.
- New API error codes get `ErrorCodes` constants and Turkish text; `TranslationCoverageTests` pass.
- Turkish UI text only in `frontend/src/shared/i18n/locales/tr/*.json`.
- The incident asset-rule change is an API contract/business-rule change: record an ADR after it lands.
- Docs (`capabilities.md`, user guide) change only when the owner asks.

## Left to the builder

Exact grid breakpoints and column counts within "two to four"; tab vs. navigation-link markup for reports; QR library choice; where `PublicBaseUrl` is exposed; selection UI details; commit split (one commit per section at least).
