# Report Filters, Catalog Flow and Asset QR Labels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the squeezed report filters and mobile report tabs, make the Tür → Marka → Model flow explicit, add QR labels (detail, download, single and bulk print, scan-to-report), and fix the owner's smaller findings.

**Architecture:** Frontend changes stay in their feature folders (`features/reports`, `features/assets`, `features/admin/deviceCatalog`, `features/tickets`) plus one shared discard guard in `shared/feedback`. Backend adds one Domain rule (`Asset.CanBeReported`), relaxes incident creation to it, and adds two read endpoints: a scan summary for any signed-in user and label data (with `App:PublicBaseUrl`) for IT staff. QR images are generated in the browser with the `qrcode` package; labels print through a print-only route with `@page { size: 50mm 30mm }`.

**Tech Stack:** .NET 10 Minimal APIs, EF Core, xUnit v3 (MTP); React 19, Astryx 0.7, TanStack Query, react-hook-form + zod, i18next, Vitest, Playwright; new dependency `qrcode` (+ `@types/qrcode` dev).

**Spec:** `docs/superpowers/specs/2026-11-11-reports-catalog-qr-design.md`

## Global Constraints

- Branch `ui-astryx`; commit per task; stage named paths only; no AI attribution; commit only after the task's gates pass.
- UI per `frontend/CLAUDE.md` (look up props with `npx astryx component <Name>`); no `eslint-disable`; no magic numbers/strings (named constants).
- Turkish text only in `frontend/src/shared/i18n/locales/tr/*.json`.
- New API error → `ErrorCodes` constant + `errors.json` text; `TranslationCoverageTests` pass. After endpoint changes run `tools/gen-api-types.sh`.
- Gates before each commit: `tools/lint.sh` exit 0 (incl. Astryx doctor budget 16); affected tests green. Before the final task: `tools/test.sh` and `tools/e2e.sh` (stop dev services first, restart after).
- Label: thermal, 50×30 mm, one label per page; content QR + asset tag (largest) + brand and model + serial number (if any); **no company name**.
- Scan link: `{App:PublicBaseUrl}/scan/asset/{assetId}`.
- Reportable asset statuses: `InStock`, `Assigned`, `Maintenance`. `Lost`, `Retired` refused. Only incident **creation** relaxes; `Links.cs` keeps the assigned-to-me rule.
- Docs (`capabilities.md`, user guide) are not edited in this plan; the final task names what needs a line.

## Review Focus

1. Filter carry-over: switching report via the phone "Rapor türü" selector must keep only the filters the target report understands (same as `tabHref`). Test in Task 2.
2. A hidden filter set only through the URL (e.g. `?categoryId=…` on first load) must show in "Diğer filtreler (1)" and in the chips. Test in Task 1.
3. Editing an asset whose model is inactive: switching brand away and back must restore the old model option; switching type away must drop it. Test in Task 4.
4. Scan link for a non-existent id (typo, deleted DB) shows a 404 state, not a crash or an empty form. Test in Task 10.
5. Bulk print with zero selected stays disabled; print view with an unknown id in `ids` prints the known ones and lists nothing for the unknown (server returns only existing). Tests in Tasks 8 and 9.

---

### Task 1: Report filter layout — main filters, "Diğer filtreler", full width

**Files:**
- Modify: `frontend/src/features/reports/ReportFilterBar.tsx`
- Modify: `frontend/src/features/reports/ReportsPage.tsx` (`ReportView`)
- Modify: `frontend/src/features/reports/useReportFilter.ts` (add constant)
- Modify: `frontend/src/shared/i18n/locales/tr/reports.json`
- Test: `frontend/src/features/reports/ReportsPage.test.tsx`, `frontend/e2e/reports.spec.ts`

**Interfaces:**
- Produces: `export const MAIN_FILTER_KEYS: readonly ReportFilterKey[] = ['from', 'to', 'priority', 'status', 'teamId', 'assetType', 'assetStatus']` in `useReportFilter.ts`; `export function splitFilterFields(report: ReportName): { main: ReportFilterKey[]; other: ReportFilterKey[] }` (order kept from `REPORT_FILTER_FIELDS`).
- Strings: `filters.more` = `"Diğer filtreler"`, `filters.moreCount` = `"Diğer filtreler ({{count}})"`.

- [ ] **Step 1: Write failing tests** in `ReportsPage.test.tsx`:
  - `splitFilterFields('incidents')` → main `['from','to','priority','status','teamId']`, other `['categoryId','assigneeId','departmentId','locationId','slaState']`; `splitFilterFields('operations').other` starts with `'kind'`; `splitFilterFields('assets')` → main `['assetType','assetStatus']`.
  - Desktop render of `/reports/incidents?categoryId=<guid>`: the "Diğer filtreler (1)" control is present and collapsed, "Kategori" field is not visible until it is opened, a chip for the category and the "Temizle" button are visible.
- [ ] **Step 2: Run** `cd frontend && npx vitest run src/features/reports/ReportsPage.test.tsx` — expect FAIL (`splitFilterFields` not exported).
- [ ] **Step 3: Implement.** Desktop: `ReportView` renders `ReportFilterBar` directly in the `SectionCard` (not inside `ListToolbar`'s `HStack`, which is what squeezes it to ~200 px), followed by one row with `ActiveFilterChips` and the "Temizle" button (shown when `activeFilterKeys(filter).length > 0`). Phones keep `ListToolbar` with its drawer, showing main and other fields flat. `ReportFilterBar` takes the full width: dates in their own two-column sub-grid spanning two columns; remaining main fields in a `Grid` with a min column width chosen so a ~1100 px card gives four columns and ~600 px gives two (named constant). "Diğer filtreler" is an Astryx disclosure (pick via `npx astryx component Accordion`/`Disclosure`), collapsed by default, label with count of active `other` filters.
- [ ] **Step 4: Run** the unit test — PASS.
- [ ] **Step 5: E2E** in `reports.spec.ts` (desktop project only): for each of the six reports, the `form[aria-label="Rapor filtreleri"]` bounding box height is ≤ `FILTER_AREA_MAX_HEIGHT_PX = 200` with "Diğer filtreler" closed and its width ≥ 80% of the card width. Run `tools/e2e.sh` later in Task 11 (dev services must be stopped); locally run only `cd frontend && npx playwright test e2e/reports.spec.ts` if the E2E stack is up, else defer.
- [ ] **Step 6: Commit** `fix(frontend): report filters use the card width and keep minor filters under "Diğer filtreler"`.

### Task 2: Phone report selector and accessible report navigation

**Files:**
- Modify: `frontend/src/features/reports/ReportsPage.tsx` (`ReportTabs`, `tabHref`)
- Modify: `frontend/src/shared/i18n/locales/tr/reports.json`
- Test: `frontend/src/features/reports/ReportsPage.test.tsx`, `frontend/e2e/reports.spec.ts`

**Interfaces:**
- Consumes: `tabHref(target, params)` (existing).
- Strings: `page.reportType` = `"Rapor türü"`.

- [ ] **Step 1: Failing tests:** phone viewport (mock `useIsPhone` → true): a selector labelled "Rapor türü" lists the six report names, no element with `role="tab"`; choosing "Talepler" on `/reports/operations?kind=incident&priority=p1` navigates to `/reports/requests?priority=p1` (no `kind`). Desktop: no `role="tab"` element lacks a valid `aria-controls`; the current report link has `aria-current="page"`.
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Implement.** Phones: Astryx `Selector` with `navigate(tabHref(...))`. Desktop: the reports are pages, so render them as navigation links (`nav` with `aria-label={t('page.tabs')}`, current link `aria-current="page"`), styled as the tab row; drop the `TabList` tab pattern (it produced tabs without panels). If Astryx offers a link-tab component that renders `nav`/`aria-current` without `role="tab"`, use it instead.
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: E2E** (phone project): on `/reports/incidents`, `document.documentElement.scrollWidth <= window.innerWidth`; "Rapor türü" switches to "Garanti".
- [ ] **Step 6: Commit** `fix(frontend): phones pick the report from one selector; report links are navigation, not tabs`.

### Task 3: Empty report cards and "Envanter" wording

**Files:**
- Modify: `frontend/src/features/reports/FigureCard.tsx`, `frontend/src/features/reports/formatMetric.ts`
- Modify: `frontend/src/features/reports/period.ts` (period text helper)
- Modify: `frontend/src/shared/i18n/locales/tr/reports.json`
- Test: `frontend/src/features/reports/formatMetric.test.ts`, `frontend/src/features/reports/ReportsPage.test.tsx`

**Interfaces:**
- Produces: `export function periodText(filter: ReportFilterValues, today: string): string | null` in `period.ts` — preset label when `matchPreset` hits (e.g. "Son 30 gün"), else `"dd.MM.yyyy – dd.MM.yyyy"`, `null` without both days. `FigureView` gains `isEmpty: boolean` (true when `value === null`, or `denominator === 0`, or a count figure with `value === 0`).
- Strings: `figure.emptyForPeriod` = `"{{period}} için kayıt yok."`, `figure.empty` = `"Kayıt yok."`; `tabs.assets` = `"Envanter"`; the `"{{count}} varlık"` string becomes `"{{count}} cihaz"`; grep `reports.json` for other "varlık" meaning devices and align (list them in the commit body).

- [ ] **Step 1: Failing tests:** `describeFigure` with `denominator: 0` → `isEmpty: true`; `periodText({from:'2026-10-12',to:'2026-11-11'}, '2026-11-11')` → `"Son 30 gün"`; a rendered empty ratio card has no "Tümünü listele" link and shows "Son 30 gün için kayıt yok."; the title is plain text (no link) when `isEmpty`; tab label "Envanter" present.
- [ ] **Step 2: Run** `npx vitest run src/features/reports` — FAIL.
- [ ] **Step 3: Implement** in `FigureCard` (replace the `noData` line with the period message; snapshot reports without days use `figure.empty`).
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `fix(frontend): empty report cards name the period instead of linking to an empty list; report tab "Envanter"`.

### Task 4: Asset form Tür → Marka → Model

**Files:**
- Modify: `frontend/src/features/assets/AssetFormDialog.tsx`
- Modify: `frontend/src/shared/i18n/locales/tr/assets.json`
- Test: `frontend/src/features/assets/catalogForm.test.tsx`, `frontend/e2e/catalog.spec.ts`

**Interfaces:**
- Strings: `form.brandEmptyForType` = `"Bu tür için aktif marka yok."`, `form.modelEmptyForBrand` = `"Bu marka için aktif {{type}} modeli yok."` (`type` = `t('types.<type>')` lower-cased with `toLocaleLowerCase('tr')`).

- [ ] **Step 1: Failing tests** (catalog fixture with brands A and B, both with a desktop model, A also an inactive desktop model used by the edited asset):
  - New asset: Marka disabled until Tür chosen; Model disabled until Marka chosen; fields in DOM order Tür, Marka, Model.
  - Type with no brand → Marka empty text "Bu tür için aktif marka yok."; brand with no model of the type → "Bu marka için aktif masaüstü modeli yok."
  - Choose A, pick model, switch to B → model cleared, options only B's active desktop models.
  - Edit asset (type desktop, brand A, inactive model): option present; switch brand to B → absent; back to A → present; switch type to laptop → absent.
- [ ] **Step 2: Run** `npx vitest run src/features/assets/catalogForm.test.tsx` — FAIL.
- [ ] **Step 3: Implement:** three fields in one `Grid` row on wide screens; `isDisabled` on Marka/Model; add the asset's own model only when `type === ownType && brandId === ownBrandId` (replace the `ownType`-only condition in `modelOptions`).
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: E2E** in `catalog.spec.ts`: seed two brands each with a desktop model via the admin API; in Envanter → Yeni cihaz pick Masaüstü → brand 1 → its model → brand 2: model empty, options show brand 2's model only.
- [ ] **Step 6: Commit** `fix(frontend): asset form follows Tür, Marka, Model with disabled steps and clear empty reasons`.

### Task 5: Brand row actions "Modelleri gör" and "Model ekle"

**Files:**
- Modify: `frontend/src/features/admin/deviceCatalog/BrandsPage.tsx`, `ModelsPage.tsx`
- Modify: `frontend/src/shared/i18n/locales/tr/admin.json` (or the namespace the catalog pages use)
- Test: `frontend/src/features/admin/deviceCatalog/DeviceCatalogPages.test.tsx`

**Interfaces:**
- Produces: navigation state `{ createModelForBrand: string }` read by `ModelsPage`; the existing URL filter `brand` (`BRAND_FILTER_KEY`) for "Modelleri gör".
- Strings: `deviceCatalog.brands.viewModels` = `"Modelleri gör"`, `deviceCatalog.brands.addModel` = `"Model ekle"`.

- [ ] **Step 1: Failing tests:** brand row has both actions (via `RecordActions` `extra`); "Modelleri gör" navigates to `/admin/models?brand=<id>`; "Model ekle" lands on `/admin/models` with the create dialog open and Marka = that brand; the state is consumed once (closing the dialog does not reopen it on rerender).
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Implement** (`ModelFormDialog` gains `initialBrandId?: string`; clear the location state with `navigate(..., { replace: true, state: null })` after opening).
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `feat(frontend): brand rows open their models and add a model for the brand`.

### Task 6: Discard guard for form dialogs

**Files:**
- Create: `frontend/src/shared/feedback/useDiscardGuard.tsx`
- Modify: `frontend/src/shared/feedback/FormDialog.tsx`, `frontend/src/features/assets/AssetFormDialog.tsx`, callers of `FormDialog` that have a form (pass `isDirty`)
- Test: `frontend/src/shared/feedback/useDiscardGuard.test.tsx`, `frontend/src/features/assets/catalogForm.test.tsx`

**Interfaces:**
- Produces: `export function useDiscardGuard(isDirty: boolean, onClose: () => void): { requestClose: () => void; confirm: ReactNode }` — `requestClose` closes at once when clean, else opens a `ConfirmDialog` reusing the `unsaved.title` / `unsaved.message` / `unsaved.leave` strings; confirming calls `onClose`. `FormDialogProps` gains `isDirty?: boolean` (default `false`).

- [ ] **Step 1: Failing tests:** dirty asset form — "Vazgeç", the dialog close button and Escape each show the confirm and keep the form; confirming closes; clean form closes without confirm. Same three paths for one `FormDialog` user (e.g. the brand form).
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Implement:** route the Vazgeç button, the close button and the dialog's `onOpenChange(false)` (Escape) through `requestClose`; while saving (`isPending`) closing stays as today.
- [ ] **Step 4: Run** `npx vitest run src/shared/feedback src/features` — PASS.
- [ ] **Step 5: Commit** `fix(frontend): form dialogs ask before discarding changes on Vazgeç, close and Escape`.

### Task 7: Backend — reportable assets on new incidents and the scan summary

**Files:**
- Modify: `backend/src/Tgysm.Domain/Assets/Asset.cs` (rule), `backend/src/Tgysm.Domain/Common/ErrorCodes.cs`
- Modify: `backend/src/Tgysm.Application/Features/Tickets/CreateTicket.cs`
- Create: `backend/src/Tgysm.Application/Features/Assets/GetScannedAsset.cs`
- Modify: `backend/src/Tgysm.Api/Http/ApiRoutes.cs`, `backend/src/Tgysm.Api/Endpoints/Assets/AssetEndpoints.cs`
- Modify: `frontend/src/shared/i18n/locales/tr/errors.json`; regenerate `frontend/src/shared/api/schema.d.ts`
- Create: `docs/adr/0028-incident-device-from-qr-scan.md`; Modify: `docs/adr/README.md`
- Test: `backend/tests/Tgysm.Domain.Tests/Assets/AssetReportableTests.cs` (create), `backend/tests/Tgysm.Api.IntegrationTests/Tickets/IncidentAssetTests.cs`, `backend/tests/Tgysm.Api.IntegrationTests/Assets/ScannedAssetTests.cs` (create)

**Interfaces:**
- Produces (Domain): `public static bool CanBeReported(AssetStatus status)` on `Asset` — true for `InStock`, `Assigned`, `Maintenance`.
- Produces (ErrorCodes): `TicketAssetNotReportable = "tickets.asset_not_reportable"`; Turkish `"Bu cihaz için arıza kaydı açılamaz."`.
- Produces (Application): `public sealed record ScannedAssetDto(Guid Id, string AssetTag, AssetType Type, string Brand, string Model, bool CanReport);` `GetScannedAssetHandler.HandleAsync(Guid id, CancellationToken ct)` → `NotFoundException` for unknown id.
- Produces (API): `ApiRoutes.ScanAssets = "/scan/assets"`; `GET /api/v1/scan/assets/{id:guid}` → 200 `ScannedAssetDto` / 404, `RequireAuthorization()` (any signed-in user, not `ItStaffOrAdmin`).

- [ ] **Step 1: Failing tests:**
  - Domain: `CanBeReported` true for the three statuses, false for `Lost`, `Retired`.
  - Integration (rename the existing refusal test): `A_device_assigned_to_someone_else_can_be_named_on_a_new_incident` → 201, ticket links the asset, timeline/audit as today; `An_in_stock_device_can_be_named_on_a_new_incident`; `A_lost_or_retired_device_is_refused_and_no_ticket_is_created` → 400 `tickets.asset_not_reportable`, ticket count unchanged; `An_unknown_device_is_refused` → 400 same code.
  - Integration `ScannedAssetTests`: employee GET returns exactly the six fields (assert JSON property names: `id, assetTag, type, brand, model, canReport`); `canReport` false for a retired asset; 404 unknown id; 401 without session.
  - Existing `Links.cs` tests unchanged and green (later linking still requires assignment).
- [ ] **Step 2: Run** `tools/with-db.sh dotnet test --solution backend/Tgysm.slnx --filter-class "*IncidentAssetTests" --filter-class "*ScannedAssetTests" --filter-class "*AssetReportableTests"` — FAIL.
- [ ] **Step 3: Implement.** In `CreateTicket.cs` replace the assignment check with: load the asset's status inside the same transaction; missing or `!Asset.CanBeReported(status)` → `DomainException(ErrorCodes.TicketAssetNotReportable, …)`. Update the `AssetId` XML doc. Keep `TicketAssetNotAssignedToRequester` (still used by `Links.cs`).
- [ ] **Step 4: Run** the filtered tests, then `TranslationCoverageTests` — PASS. Run `tools/gen-api-types.sh`.
- [ ] **Step 5: ADR-0028** from `~/.claude/templates/adr-template.md`: "A new incident may name any reportable device (QR scan)"; status accepted, Deciders Tugay İLTUŞ, alternatives (keep assigned-only; relax linking too), consequences incl. the accepted read of tag/brand/model by any signed-in user; cite the commit SHA after committing (amend the ADR in the same task's follow-up commit). Add the index row.
- [ ] **Step 6: Commit** `feat(api): new incidents may name any reportable device; scan summary endpoint` (+ ADR commit `docs(adr): ADR-0028 …`).

### Task 8: Backend — label data with the public address

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Assets/GetAssetLabels.cs`
- Modify: `backend/src/Tgysm.Api/Endpoints/Assets/AssetEndpoints.cs`
- Regenerate: `frontend/src/shared/api/schema.d.ts`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Assets/AssetLabelTests.cs` (create)

**Interfaces:**
- Produces: `public sealed record AssetLabelItem(Guid Id, string AssetTag, string Brand, string Model, string? SerialNumber);` `public sealed record AssetLabelsDto(string PublicBaseUrl, IReadOnlyList<AssetLabelItem> Items);` `GetAssetLabelsHandler.HandleAsync(IReadOnlyList<Guid> ids, CancellationToken ct)`; `public const int MaxLabelsPerRequest = 200;` more → validation error 400 (existing validation error shape).
- API: `GET /api/v1/assets/labels?ids=<guid>&ids=<guid>` in the `ItStaffOrAdmin` group. Items in the order of `ids`; unknown ids skipped. `PublicBaseUrl` from `AppOptions.PublicBaseUrl` without trailing slash.

- [ ] **Step 1: Failing tests:** two ids → two items in request order with brand/model/serial; an unknown id among them is skipped; `publicBaseUrl` equals `ApiFactory.PublicBaseUrl`; 201 ids → 400; employee → 403.
- [ ] **Step 2: Run** `--filter-class "*AssetLabelTests"` — FAIL.
- [ ] **Step 3: Implement**; route registered before `/{id:guid}` so `labels` is not parsed as an id.
- [ ] **Step 4: Run** — PASS; `tools/gen-api-types.sh`.
- [ ] **Step 5: Commit** `feat(api): asset label data with the public address`.

### Task 9: Frontend — QR label, detail card, print view, inventory bulk print

**Files:**
- Modify: `frontend/package.json` (`qrcode`, dev `@types/qrcode`), `frontend/vite.config.ts` (chunk group `vendor-qr` only if the build reports it > ~100 KB; otherwise it falls into `vendor-other`)
- Create: `frontend/src/features/assets/labels/scanLink.ts`, `AssetLabel.tsx`, `AssetLabelsPrintPage.tsx`, `downloadLabel.ts`, `labels.css` (print rules), `api.ts` additions in `features/assets/api.ts`
- Modify: `frontend/src/features/assets/AssetDetailPage.tsx`, `AssetsPage.tsx`, `frontend/src/shared/list/DataList.tsx` (optional selection), `frontend/src/shared/routes.ts`, `frontend/src/app/router.tsx`, `frontend/src/shared/i18n/locales/tr/assets.json`
- Test: `frontend/src/features/assets/labels/labels.test.tsx`, `AssetsPage.test.tsx`, `AssetDetailPage.test.tsx`

**Interfaces:**
- Consumes: `GET /api/v1/assets/labels` (Task 8) as `useAssetLabels(ids: readonly string[])`.
- Produces: `scanLink(publicBaseUrl: string, assetId: string): string` → `${base}/scan/asset/${id}`; `ROUTES.assetLabels = '/assets/labels'`, `assetLabelsPath(ids: readonly string[])` → `/assets/labels?ids=a,b`; `ROUTES.scanAsset = '/scan/asset/:id'` (used in Task 10); `LABEL_WIDTH_MM = 50`, `LABEL_HEIGHT_MM = 30`; `downloadLabelPng(item: AssetLabelItem, link: string): Promise<void>` (canvas at 300 dpi, file `<assetTag>.png`). `DataList` gains optional `selection?: { selected: ReadonlySet<string>; onChange(next: Set<string>): void; rowLabel(item): string }`.
- Strings: `labels.card` = `"QR kod"`, `labels.download` = `"İndir"`, `labels.print` = `"Etiketi yazdır"`, `labels.printSelected` = `"Seçili cihazların etiketlerini yazdır"`, `labels.selectedCount` = `"{{count}} cihaz seçili"`.

- [ ] **Step 1: Failing tests:** `scanLink('https://itsm.example.com', id)` exact string; `AssetLabel` renders an `svg`/`img` QR whose encoded text is the scan link (assert the `data-link` attribute or the generator call), the asset tag as the largest text, brand + model, serial only when present, and no company name; print page with two ids renders two `.label` pages; detail page shows the QR card with "İndir" and "Etiketi yazdır" (link to `assetLabelsPath([id])`); Envanter list: "Seçili cihazların etiketlerini yazdır" disabled with none selected, enabled after selecting two rows, links to `assetLabelsPath` in list order; header checkbox selects the page.
- [ ] **Step 2: Run** `npx vitest run src/features/assets` — FAIL.
- [ ] **Step 3: Implement.** QR via `qrcode` `toString(link, { type: 'svg', errorCorrectionLevel: 'M', margin: 1 })`. Print page lives outside `AppLayout` (inside `RequireSession` + `IT_STAFF_OR_ADMIN`), calls `window.print()` once data is loaded, CSS `@page { size: 50mm 30mm; margin: 0 }`, each label `break-after: page`, QR left at label height minus padding, text right, truncation with ellipsis. Selection in `DataList`: use Astryx table selection (`useTableSelection`) on wide screens and a checkbox per card on phones.
- [ ] **Step 4: Run** — PASS; `tools/lint.sh` (doctor budget must hold; print CSS is plain CSS in a feature file — if doctor counts it, keep it in the budget's reasoned exceptions and say so in the commit).
- [ ] **Step 5: Commit** `feat(frontend): asset QR labels — detail card, PNG download, 50×30 mm print, bulk print from Envanter`.

### Task 10: Frontend — scan route and prefilled incident

**Files:**
- Create: `frontend/src/features/assets/labels/ScanAssetPage.tsx`
- Modify: `frontend/src/app/router.tsx`, `frontend/src/features/tickets/NewTicketPage.tsx`, `frontend/src/features/assets/api.ts` (`useScannedAsset(id)`), `frontend/src/shared/i18n/locales/tr/tickets.json`
- Test: `frontend/src/features/assets/labels/ScanAssetPage.test.tsx`, `frontend/src/features/tickets/NewTicketPage.test.tsx`, `frontend/e2e/tickets.spec.ts`

**Interfaces:**
- Consumes: `ROUTES.scanAsset`, `GET /api/v1/scan/assets/{id}` (Task 7).
- Produces: route `/scan/asset/:id` inside `RequireSession` (sign-in returns to it through the existing `from` state) and `AppLayout`; `newIncidentPath(assetId?: string)` → `/tickets/new/incident?asset=<id>`.
- Strings: `new.scannedAsset` = `"Okutulan cihaz"`, `new.assetNotReportable` = `"Bu cihaz için arıza kaydı açılamaz."`, `new.assetNotFound` = `"Cihaz bulunamadı."`.

- [ ] **Step 1: Failing tests:** IT staff/admin on `/scan/asset/<id>` → redirected (replace) to `/assets/<id>`; employee → `/tickets/new/incident?asset=<id>`; new incident with `?asset=` of a reportable device not in "my assets": the "İlgili cihaz" field shows `DMR-… · Brand Model` selected and submit sends that `assetId`; with `canReport: false`: banner "Bu cihaz için arıza kaydı açılamaz.", field empty, form usable; unknown id: banner "Cihaz bulunamadı.", form usable.
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Implement** (scanned device prepended to the options when not already in "my assets").
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: E2E** in `tickets.spec.ts`: employee opens `/scan/asset/<id of a device assigned to another user>` → submits → ticket detail lists that device; IT staff opens the same link → asset detail.
- [ ] **Step 6: Commit** `feat(frontend): scanning an asset QR opens the asset for IT and a prefilled incident for employees`.

### Task 11: Full gates and handover

- [ ] **Step 1:** `tools/lint.sh` → exit 0.
- [ ] **Step 2:** `tools/test.sh` → exit 0 (coverage gates hold).
- [ ] **Step 3:** `tools/dev-service.sh stop`; `tools/e2e.sh` → exit 0 on both viewports; `tools/dev-service.sh start`.
- [ ] **Step 4:** Push `ui-astryx` only with owner approval; watch CI to green.
- [ ] **Step 5:** Tell the owner which docs need a line (not edited): `capabilities.md` (reports UI, asset form, QR labels, scan route, incident asset rule ADR-0028, new endpoints), user guide pages `reports.md`, `inventory.md`, `incidents-and-requests.md`, `admin-catalogs.md` and their screenshots.
