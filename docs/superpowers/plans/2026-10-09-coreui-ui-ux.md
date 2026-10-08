# CoreUI UI/UX Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Ruling (plan format, carried over from Phase 2–5, owner-approved):** tasks list files, interfaces and tests instead of full inline code; code is written once, under TDD, from the spec. Each task: write the listed failing tests, watch them fail, implement, run the task command, record the result in the ledger.

**Goal:** Replace the whole presentation layer with a CoreUI Free layout (left sidebar on application and admin, header, breadcrumb, mobile-first), fix the review's UX findings, add a brand/model catalog, server-generated `DMR-` asset tags and an incident device link — without changing business rules.

**Architecture:** Big bang on branch `ui-coreui`. Tailwind and Radix go first; `shared/ui` is rebuilt on CoreUI with the same props; a single route-metadata table drives sidebar, breadcrumb and page title; pages are rewritten group by group; data changes (catalog, tag sequence, incident device) land as their own backend+UI tasks; reports move to Chart.js last.

**Tech Stack:** React 19, Vite 8 (Rolldown), TypeScript 5.9, TanStack Query, react-router 8, react-hook-form + zod, i18next; `@coreui/react` 5.13, `@coreui/coreui` 5.9 (SCSS), `@coreui/icons(-react)`, `@coreui/react-chartjs` 3, `chart.js` 4.5, `sass`; .NET 10, EF Core + Npgsql, PostgreSQL 17; Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-10-09-coreui-ui-ux-design.md` (and the Turkish review it cites: `docs/superpowers/specs/2026-10-09-ui-ux-review.md`).

## Global Constraints

- All Phase 1–5 constraints apply: English code/URLs/DB, Turkish UI only in `frontend/src/shared/i18n/locales/tr/*.json`, zero warnings, no `eslint-disable`, no magic literals (named constants), `tools/` scripts, TDD, stage named paths only, no AI attribution, no push.
- Keep unchanged: API contracts except spec §6 additions, authorization/scope, cookie/CSRF, TanStack Query hooks, typed client, zod schemas, route paths, business rules, existing data and the sample article.
- Light theme only (`data-coreui-theme="light"`). CoreUI Free only, no PRO. No Redux, no second router.
- Packages: `@coreui/react` 5.13.x, `@coreui/coreui` 5.9.x, `@popperjs/core` 2.11.x, `@coreui/icons` 3.1.x, `@coreui/icons-react` 2.3.x, `@coreui/react-chartjs` 3.0.x, `chart.js` 4.5.x, `sass` (dev). Removed by the end: `tailwindcss`, `@tailwindcss/vite`, `radix-ui`, `recharts`.
- Chunk groups `vendor-coreui` and `vendor-charts` (chart.js + `@coreui/react-chartjs`); no chunk over the 500 KB warning; charts stay lazy.
- Sidebar fixed/collapsible from 992 px (`SIDEBAR_BREAKPOINT_PX = 992`), offcanvas below. Table vs. card list switch at 768 px.
- Asset tag format `DMR-` + 6 digits from sequence `asset_tag_seq`; prefix `DMR-` is reserved for generated tags.
- Report default period: last 30 days for incidents/requests/operations/transfers; quick ranges 7d, 30d, this month, last month, this year, custom.
- Each task ends with `tools/lint.sh` and `tools/test.sh` green (exit codes). Intermediate visuals may be incomplete; behaviour tests may not regress.
- Vitest/E2E tests query by role and accessible name; change a test only when behaviour changes and say why in the commit message.
- After API changes: `tools/gen-api-types.sh`; new error codes → `ErrorCodes` + `errors.json`; audit actions/entity types → constants + `admin.json` (`TranslationCoverageTests`).
- Architectural decisions → ADR in the task that lands them (ADR-0021 CoreUI, ADR-0022 Chart.js, ADR-0023 catalog + asset tag).
- Local commit per task after green gates; branch merges to `master` only with owner approval after Task 18.

## Review Focus

- Very long labels (article title, team/user names, Turkish words) in sidebar, breadcrumb and table cells at 320 px must truncate without widening the page; Task 6 and Task 9 add 320 px tests with 120-character labels.
- A non-admin opening an `/admin/*` URL or an employee opening `/workspace` directly must get the existing redirect/403 inside the correct layout with no admin menu rendered; Task 6 adds the route-guard tests per role.
- Editing an asset whose model was later deactivated must still show that model and save other changes unchanged; Task 13 adds the integration test and Task 14 the form test.
- A CSV legacy tag that looks like a generated tag (`DMR-000123`) would later collide with the sequence; it must be refused as a row error ("DMR- öneki sistem numaraları için ayrılmıştır"); Task 14 adds the test.
- Opening a modal from inside the mobile offcanvas sidebar or filter panel must not lose focus or leave both overlays stuck; Task 3 adds the focus-return test for nested overlays.

---

### Task 1: Packages, styles and Tailwind/Radix removal

**Files:**
- Modify: `frontend/package.json`, `frontend/package-lock.json`, `frontend/vite.config.ts` (remove `@tailwindcss/vite`; add `vendor-coreui`, `vendor-charts` groups; keep Recharts group only until Task 16), `frontend/src/main.tsx` (import `./styles/app.scss`; set `data-coreui-theme`), `frontend/index.html` if it references Tailwind.
- Create: `frontend/src/styles/app.scss`, `frontend/src/styles/_variables.scss` (brand `#1d4ed8` family, font stack, radius), `frontend/src/styles/_layout.scss`, `frontend/THIRD_PARTY_NOTICES.md` (CoreUI MIT notice, template commit `2609f59`).
- Delete: `frontend/src/index.css`.
- Create: `docs/adr/0021-coreui-ui-layer.md` (+ row in `docs/adr/README.md`).
- Test: `frontend/src/styles/styles.test.ts`.

**Interfaces:**
- Produces: global CoreUI CSS loaded once from `main.tsx`; `THEME = 'light'` constant in `src/shared/ui/theme.ts`.

- [ ] Step 1: Failing test `styles.test.ts`: `package.json` has no `tailwindcss`, `@tailwindcss/vite`, `radix-ui` dependency and has the CoreUI packages at the global-constraint major versions; `document.documentElement` gets `data-coreui-theme="light"` from the bootstrap function exported by `main.tsx` (`applyTheme()`).
- [ ] Step 2: Run `cd frontend && npx vitest run src/styles` — expect FAIL.
- [ ] Step 3: Install/remove packages (`npm i @coreui/react@~5.13 @coreui/coreui@~5.9 @popperjs/core@~2.11 @coreui/icons@~3.1 @coreui/icons-react@~2.3 @coreui/react-chartjs@~3.0 chart.js@~4.5`, `npm i -D sass`, `npm rm tailwindcss @tailwindcss/vite radix-ui`), write the SCSS entry, update `vite.config.ts`. `Dialog.tsx`/`Sheet.tsx` lose Radix here: give them a temporary CoreUI `CModal`/`COffcanvas` body with the same props so the build compiles (Task 3 finishes them).
- [ ] Step 4: `tools/lint.sh` and `tools/test.sh` — expect exit 0 (layout tests may only assert roles/names, not classes; fix any test that asserted Tailwind classes by asserting the behaviour instead).
- [ ] Step 5: Write ADR-0021 (CoreUI Free as UI layer, Tailwind/Radix removed, big-bang branch, alternatives: keep Tailwind + shell only; adapter-first migration). Commit `chore: switch styling to CoreUI and remove Tailwind and Radix`.

### Task 2: Core `shared/ui` primitives on CoreUI

**Files:**
- Modify: `frontend/src/shared/ui/{Button,TextField,TextAreaField,SelectField,FormAlert,States,StatusBadge,Pagination,ResponsiveList}.tsx`.
- Test: matching `*.test.tsx` in `frontend/src/shared/ui/` (create where missing).

**Interfaces:**
- Produces (props unchanged unless listed): `Button({ variant: 'primary'|'secondary'|'ghost'|'danger', isLoading?, type='button', ... })` → `CButton`; `TextField/TextAreaField/SelectField({ label, error?, hint?, ... })` → CoreUI form control with `invalid`, `feedbackInvalid`, `aria-describedby` = hint id + error id; `ResponsiveList<T>({ items, columns: Column<T>[], card: (item) => ReactNode, caption })` renders `CTable` from 768 px and `CListGroup` below (`LIST_TABLE_BREAKPOINT_PX = 768`, via `useMediaQuery` in `shared/lib/useMediaQuery.ts`); `States`: `LoadingState`, `EmptyState({ message, action? })`, `ErrorState({ error, onRetry? })`.

- [ ] Step 1: Failing tests: `Button` defaults `type="button"`, sets `aria-busy` and disables while `isLoading`; each field links label, hint and error through `aria-describedby` and sets `aria-invalid`; `ResponsiveList` renders a table with caption at 1024 px and a list at 360 px (mock `matchMedia`) from the same `columns`; `ErrorState` retry button calls `onRetry`; `StatusBadge` always renders text.
- [ ] Step 2: `npx vitest run src/shared/ui` — expect FAIL on the new assertions.
- [ ] Step 3: Implement on `@coreui/react`; add `useMediaQuery` (with `matchMedia` listener cleanup).
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `feat: rebuild shared form, list and state components on CoreUI`.

### Task 3: Overlays, toast and focus handling

**Files:**
- Modify: `frontend/src/shared/ui/{Dialog,ConfirmDialog,Sheet,TemporaryPasswordDialog}.tsx`.
- Create: `frontend/src/shared/ui/Toast.tsx` (`ToastProvider`, `useToast()`), mount in `frontend/src/app/App.tsx`.
- Test: `Dialog.test.tsx`, `Sheet.test.tsx`, `Toast.test.tsx`.

**Interfaces:**
- Produces: `Dialog({ open, onOpenChange, title, description?, children, footer? })` → `CModal` (`fullscreen="sm-down"`, labelled by title); `Sheet({ open, onOpenChange, title, placement='start', children })` → `COffcanvas`; `useToast(): { show(message: string, tone?: 'success'|'danger'|'info'): void }` rendering into a polite live region.

- [ ] Step 1: Failing tests: Escape closes and focus returns to the trigger; a `Dialog` opened from inside an open `Sheet` returns focus to its trigger inside the sheet and the sheet stays open (Review Focus); `useToast().show` text appears in `role="status"` and disappears after `TOAST_TIMEOUT_MS`.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Commit `feat: rebuild dialogs and sheets on CoreUI and add toasts`.

### Task 4: Composite form and filter components

**Files:**
- Create: `frontend/src/shared/ui/{ComboboxField,FilterPanel,KpiCard,UnsavedChangesGuard}.tsx`.
- Test: matching `*.test.tsx`.

**Interfaces:**
- Produces: `ComboboxField({ label, options: { value: string; label: string }[], value, onChange, error?, emptyText, disabled? })` (ARIA combobox: input + listbox, ArrowUp/Down, Enter, Escape, type-to-filter with `TurkishText`-equivalent `toSearchKey` from `shared/lib/turkish.ts`); `FilterPanel({ title, activeCount, primary: ReactNode, more?: ReactNode, onApply, onClear })` — desktop: `primary` inline + collapsible "Diğer filtreler"; mobile: button "Filtreler (n)" opening a `Sheet` with Uygula / Temizle / Vazgeç (Vazgeç discards draft, Temizle clears and applies); `KpiCard({ title, value, unit?, period, description?, href? })`; `UnsavedChangesGuard({ when: boolean })` using `useBlocker` + `beforeunload`, confirm via `ConfirmDialog`.

- [ ] Step 1: Failing tests: combobox keyboard selection and "no match" text; Turkish search (`ı`/`I` match); `FilterPanel` Vazgeç keeps the applied values, Temizle calls `onClear`, Uygula calls `onApply` once; `UnsavedChangesGuard` blocks a router navigation while `when` and lets it through after confirm; no prompt when `when=false`.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: add combobox, filter panel, KPI card and unsaved-changes guard`.

### Task 5: Route metadata, breadcrumb and return-to-context

**Files:**
- Create: `frontend/src/shared/navigation/{routeMeta.ts,Breadcrumbs.tsx,BreadcrumbLabelContext.tsx,returnTo.ts,PageHeader.tsx}`.
- Modify: `frontend/src/app/router.tsx` (route ids = `routeMeta` ids), `frontend/src/features/auth/guards.tsx` (`RequireSession` keeps `pathname + search`), `frontend/src/shared/routes.ts` (export route ids).
- Modify: `frontend/src/shared/i18n/locales/tr/common.json` (`nav.*` titles, group names, `breadcrumb.label`, `breadcrumb.more`).
- Test: `routeMeta.test.ts`, `Breadcrumbs.test.tsx`, `returnTo.test.ts`, `guards.test.tsx`.

**Interfaces:**
- Produces: `type RouteId`; `ROUTE_META: Record<RouteId, { path: string; titleKey: string; parent?: RouteId; roles?: readonly Role[]; menu?: { area: 'app'|'admin'; group: MenuGroupId; order: number }; dynamic?: boolean }>`; `MENU_GROUPS` (app: `personal`, `itOps`, `analysis`; admin: `overview`, `organization`, `deviceCatalog`, `serviceCatalog`, `sla`, `notifications`, `system`) with i18n keys; `useBreadcrumbLabel(label: string | undefined)` (page sets its dynamic label from already-loaded data); `PageHeader({ actions?: ReactNode, titleOverride?: string })` renders `Breadcrumbs` + `<h1 tabIndex={-1}>` and focuses it on route change; `linkState(from: Location)` and `useReturnTo(defaultTo: string): string` — accepts only same-app paths whose route id is in `RETURNABLE_LISTS`.

- [ ] Step 1: Failing tests:
  - every route in `router.tsx` has meta; every `parent` exists; no cycles; every menu item's `roles` is a subset of its route guard roles;
  - breadcrumb for each family in review §4.4.1 (home, ticket from my list, ticket from queue with filters, asset detail direct URL, article edit → title → list, report metric list keeps the query, admin page with group as plain text) — last item `aria-current="page"`, group without page is not a link;
  - at 360 px a 5-level trail renders root, "…" menu (button listing skipped items), parent, current; a 120-character title does not widen the container (`scrollWidth <= clientWidth`) (Review Focus);
  - `useReturnTo` rejects `//evil.com`, `https://…`, `/admin` when not in the whitelist, and returns the default;
  - `RequireSession` redirects to login with `?next=/reports/incidents?priority=p1` preserved and login returns there.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: add route metadata, breadcrumbs and return-to-context navigation`.

### Task 6: Layouts, sidebar, header and account menu

**Files:**
- Create: `frontend/src/shared/layout/{AppLayout,AdminLayout,AuthLayout,AppSidebar,AppHeader,AccountMenu}.tsx`, `frontend/src/features/admin/overview/AdminOverviewPage.tsx`.
- Delete: `frontend/src/shared/ui/AppShell.tsx` (+ test), `frontend/src/features/admin/AdminLayout.tsx` (+ test) after moving tests.
- Modify: `frontend/src/app/router.tsx`, `tr/common.json`, `tr/admin.json` (overview texts).
- Test: `AppLayout.test.tsx`, `AdminLayout.test.tsx`, `AppSidebar.test.tsx`, `AccountMenu.test.tsx`, `AdminOverviewPage.test.tsx`, `router.guards.test.tsx`.

**Interfaces:**
- Consumes: `ROUTE_META`, `MENU_GROUPS`, `PageHeader`, `Sheet`, `useSession`, `useLogout`.
- Produces: `AppLayout`/`AdminLayout` (`<Outlet/>` inside `<main id="main">`, skip link), `AuthLayout`; `useSidebar(): { narrow, toggleNarrow, mobileOpen, setMobileOpen }` (narrow state persisted under `SIDEBAR_STORAGE_KEY` with try/catch); `AdminOverviewPage` showing departments without approver, missing intake team and worker warning from existing queries.

- [ ] Step 1: Failing tests:
  - employee sees only Kişisel items; IT sees Kişisel + IT operasyonu (no Onaylar) + Analiz; admin sees all + "Yönetim" link; admin layout shows the seven groups and "Uygulamaya dön";
  - the active item and its group are marked (`aria-current`, group expanded) on a deep route (e.g. `/admin/sla/policy`);
  - at 360 px the hamburger opens the offcanvas, choosing an item navigates, closes it and returns focus to the hamburger; at 1280 px the sidebar is visible and the narrow toggle persists across remount (and survives `localStorage` throwing);
  - account menu contains Profilim, Şifre değiştir, Çıkış; Çıkış logs out and navigates to login;
  - employee opening `/admin/users` or `/workspace` gets the existing redirect, IT opening `/admin/users` gets redirected — no admin sidebar is rendered in any case (Review Focus);
  - 120-character team/user name in the header account label truncates at 320 px (Review Focus);
  - overview page lists a department without approver (mock) and links to it.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement; move auth routes under `AuthLayout`.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: add CoreUI app and admin layouts with left sidebar and account menu`.

### Task 7: Auth and profile screens

**Files:**
- Modify: `frontend/src/features/auth/{LoginPage,ForgotPasswordPage,SetPasswordPage,ChangePasswordPage}.tsx`, `frontend/src/features/profile/ProfilePage.tsx`, `frontend/src/shared/ui/{ForbiddenPage,NotFoundPage}.tsx`.
- Test: existing tests of these pages (behaviour unchanged) + new assertions below.

- [ ] Step 1: Failing tests: auth pages render inside `AuthLayout` with no breadcrumb and a "Girişe dön" link (except login); forced change-password route shows no sidebar and no escape link; profile and voluntary change-password show the breadcrumb `Ana sayfa › Profilim (› Şifre değiştir)`; 403/404 render inside the current layout with a "Ana sayfaya dön" action.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Rewrite page markup with CoreUI cards/grid and `PageHeader`.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: move auth, profile and error pages to the CoreUI layout`.

### Task 8: Employee home and ticket creation

**Files:**
- Modify: `frontend/src/features/home/HomePage.tsx`, `frontend/src/features/tickets/{MyTicketsPage,NewTicketPage,TicketList,AttachmentInput}.tsx`, `tr/tickets.json`, `tr/common.json`.
- Test: `HomePage.test.tsx`, `MyTicketsPage.test.tsx`, `NewTicketPage.test.tsx`.

**Interfaces:**
- Consumes: existing ticket/workspace queries; `UnsavedChangesGuard`; `linkState`.

- [ ] Step 1: Failing tests: employee home shows "Arıza bildir" and "Talep oluştur" as the first actions and the latest records; IT home shows assigned-to-me count, team queue counts and SLA risk count from existing endpoints (no new request path); my-tickets links carry `state.from` with the current filters; leaving a filled new-ticket form asks for confirmation (UX-14); breadcrumbs `Ana sayfa › Kayıtlarım › Arıza bildir`.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: role-based home and CoreUI ticket list and forms`.

### Task 9: IT workspace, ticket detail and approvals

**Files:**
- Modify: `frontend/src/features/workspace/WorkspacePage.tsx`, `frontend/src/features/tickets/{TicketDetailPage,TicketActions,TicketActionDialog,Comments,Timeline,TicketLinks,TicketArticles,ReplyArticlePicker,TicketBadges,SlaBadge}.tsx`, `frontend/src/features/approvals/ApprovalsPage.tsx`, `tr/tickets.json`, `tr/admin.json`.
- Test: existing tests + new assertions.

- [ ] Step 1: Failing tests:
  - detail page: actions render before the description in DOM order on mobile and in a sticky aside at 1280 px;
  - opening a ticket from a filtered queue and pressing the parent breadcrumb returns to the same queue URL with its filters (review §4.4.3);
  - breadcrumb shows the ticket number from loaded data; while loading a placeholder; on 404 no number from the URL is shown as title;
  - approver assignment dialog's confirm button is "Onaylayıcıyı ata" (UX-05);
  - intake team badge appears exactly once per row at 360 px and 1280 px (UX-08);
  - 120-character subject truncates in the queue card at 320 px (Review Focus).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: CoreUI workspace, ticket detail and approvals with context return`.

### Task 10: Inventory pages (current data model)

**Files:**
- Modify: `frontend/src/features/assets/{AssetsPage,AssetDetailPage,AssetFormDialog,AssetOperationDialog,AssetImportPage,AssetStatusBadge,MyAssetsPage}.tsx`, `tr/assets.json`.
- Test: existing tests + new assertions.

- [ ] Step 1: Failing tests: asset detail shows "Marka" and "Model" as separate labelled fields (UX-09); direct URL to an asset detail has breadcrumb `Ana sayfa › Envanter › <tag>` with a working Envanter link; inventory filters survive detail → back; CSV preview with rows asks for confirmation before leaving (UX-14); asset form dirty → leave asks (UX-14).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: CoreUI inventory, asset detail and CSV import screens`.

### Task 11: Knowledge base screens

**Files:**
- Modify: `frontend/src/features/kb/{KnowledgeListPage,ArticlePage,ArticleEditorPage,ArticleRevisions,ArticleStatusBadge,ArticleMarkdown}.tsx`, `tr/kb.json`.
- Test: existing tests + new assertions.

- [ ] Step 1: Failing tests: empty knowledge base without a query shows the first-use message, with a query shows the no-result message (UX-11); editor with changes asks before leaving (UX-14); breadcrumb `Ana sayfa › Bilgi bankası › <başlık> › Düzenle`, title item links to the article page; Markdown renderer output unchanged (existing safety tests stay green).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: CoreUI knowledge base list, article and editor screens`.

### Task 12: Admin screens

**Files:**
- Modify: all pages and dialogs under `frontend/src/features/admin/{users,departments,locations,teams,catalog,email,sla,system,audit}/`, `tr/admin.json`.
- Test: existing tests + new assertions.

- [ ] Step 1: Failing tests: SLA policy targets show a visible unit label per value ("dk", "%") and render as cards at 360 px (UX-03); system page separates job health from per-instance freshness and never shows "Zamanında" for an instance older than its threshold (UX-04); audit entity filter offers every value of the frontend `AUDIT_ENTITY_TYPES` list, which a test compares with the backend list exported in the OpenAPI enum (UX-07); every admin page has breadcrumb `Yönetim › <grup> › <sayfa>` with the group as plain text.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: CoreUI admin screens and admin wording fixes`.

### Task 13: Brand/model catalog (backend + admin)

**Files:**
- Create: `backend/src/Tgysm.Domain/Assets/{Brand,AssetModel,CatalogName}.cs`, `backend/src/Tgysm.Application/Features/Assets/Catalog/{ListBrands,CreateBrand,UpdateBrand,SetBrandActive,ListModels,CreateModel,UpdateModel,SetModelActive}.cs`, `backend/src/Tgysm.Infrastructure/Persistence/Configurations/{BrandConfiguration,AssetModelConfiguration}.cs`, migration `AssetCatalog` (via `tools/add-migration.sh AssetCatalog`), `backend/src/Tgysm.Api/Endpoints/Assets/CatalogEndpoints.cs`, `frontend/src/features/admin/deviceCatalog/{BrandsPage,ModelsPage,BrandFormDialog,ModelFormDialog,api}.ts(x)`.
- Modify: `Asset.cs` (`ModelId` replaces `Brand`/`Model`), `AssetConfiguration.cs`, `AssetItem.cs`, `AssetRules.cs`, `CreateAsset.cs`, `UpdateAsset.cs`, `ListAssets.cs`, `ListMyAssets.cs`, `Tickets/Links.cs`, `Reports/ReportRowSource.cs`, `Reports/ReportExportRows.cs`, `AssetEndpoints.cs`, `ErrorCodes.cs`, `AuditActions.cs`, `AuditEntityTypes.cs`, `tr/errors.json`, `tr/admin.json`, `routeMeta.ts`.
- Create: `docs/adr/0023-asset-catalog-and-asset-tags.md` (+ index row).
- Test: `Tgysm.Domain.Tests/Assets/CatalogNameTests.cs`, `Tgysm.Api.IntegrationTests/Assets/CatalogApiTests.cs`, `Tgysm.Api.IntegrationTests/Persistence/AssetCatalogMigrationTests.cs`, frontend `BrandsPage.test.tsx`, `ModelsPage.test.tsx`.

**Interfaces:**
- Produces: `CatalogName.Normalize(string) : string` (collapse whitespace, then `TurkishText.ToSearchKey`); routes `GET/POST /api/v1/admin/brands`, `PUT /api/v1/admin/brands/{id}`, `POST /api/v1/admin/brands/{id}/(activate|deactivate)`, same under `/api/v1/admin/models` (+ `?brandId=&assetType=`), and `GET /api/v1/assets/catalog?assetType=` (IT/admin; active brands and models for forms); `AssetItem` gains `ModelId`, `BrandName`, `ModelName`, `ModelActive`; error codes `assets.catalog_duplicate`, `assets.model_inactive`, `assets.model_type_mismatch`; audit actions `brand.created|updated|activated|deactivated`, `asset_model.*`; entity types `brand`, `asset_model`.

- [ ] Step 1: Failing tests:
  - `CatalogName.Normalize(" LENOVO ") == CatalogName.Normalize("Lenovo")`, `"ThinkPad T14" != "ThinkPad T14s"`, `"IŞIK" == "ışık"`;
  - duplicate brand (normalized) → 409 `assets.catalog_duplicate`; duplicate model within brand+type → 409; same model name under another type allowed;
  - create/update asset with inactive model → 400 `assets.model_inactive`; model type ≠ asset type → 400 `assets.model_type_mismatch`;
  - updating notes on an asset whose model was deactivated afterwards succeeds and keeps the model (Review Focus);
  - deactivate used brand allowed, hard delete endpoint does not exist; inactive entries not in `/assets/catalog`;
  - migration test: seeded assets with `" lenovo "/"Lenovo"` + same model collapse into one brand/model, every asset gets a `model_id`, text columns gone;
  - employees get 403 on catalog endpoints; audit rows written; `TranslationCoverageTests` pass;
  - frontend: brands page lists, creates (409 shows field error), deactivates; models page filters by brand.
- [ ] Step 2: Run (`dotnet test --solution backend/Tgysm.slnx --filter-class '*Catalog*'`, frontend vitest) — expect FAIL.
- [ ] Step 3: Implement; `tools/add-migration.sh AssetCatalog` (data step: insert distinct normalized triples, update `assets.model_id`, drop columns); `tools/migrate.sh`; `tools/gen-api-types.sh`.
- [ ] Step 4: `tools/lint.sh`, `tools/test.sh` — exit 0.
- [ ] Step 5: Write ADR-0023 (catalog part). Commit `feat: add admin-managed brand and model catalog for assets`.

### Task 14: Generated asset tags, catalog-aware asset form and CSV

**Files:**
- Create: `backend/src/Tgysm.Domain/Assets/AssetTagFormat.cs`, `backend/src/Tgysm.Application/Abstractions/IAssetTagGenerator.cs`, `backend/src/Tgysm.Infrastructure/Persistence/AssetTagGenerator.cs`, migration `AssetTagSequence`.
- Modify: `CreateAsset.cs` (no tag input), `UpdateAsset.cs` (tag immutable), `Import/{AssetCsvTemplate,AssetImportModels,PreviewAssetImport,CommitAssetImport}.cs`, `AssetEndpoints.cs`, `ErrorCodes.cs`, `tr/errors.json`, `frontend/src/features/assets/{AssetFormDialog,AssetImportPage,api}.ts(x)`, `tr/assets.json`; amend ADR-0023 (tag part).
- Test: `Tgysm.Domain.Tests/Assets/AssetTagFormatTests.cs`, `Tgysm.Api.IntegrationTests/Assets/AssetTagTests.cs`, `Tgysm.Api.IntegrationTests/Assets/AssetImportCatalogTests.cs`, frontend `AssetFormDialog.test.tsx`, `AssetImportPage.test.tsx`.

**Interfaces:**
- Consumes: `CatalogName.Normalize`, `/assets/catalog`, `ComboboxField`, `useToast`.
- Produces: `AssetTagFormat.Prefix = "DMR-"`, `AssetTagFormat.Digits = 6`, `AssetTagFormat.Format(long) : string`, `AssetTagFormat.IsReserved(string) : bool`; `IAssetTagGenerator.NextAsync(CancellationToken) : Task<string>` (`SELECT nextval('asset_tag_seq')`); create response includes `assetTag`; CSV columns `assetTag` (optional), `assetType`, `brand`, `model`; row errors `assets.import_catalog_missing` ("Tanım eksik"), `assets.import_reserved_tag`.

- [ ] Step 1: Failing tests:
  - `Format(1) == "DMR-000001"`, `Format(1234567) == "DMR-1234567"` (no truncation), `IsReserved("dmr-000005")` true;
  - 20 parallel creates produce 20 distinct `DMR-` tags; tag cannot be changed by update; existing legacy tags untouched;
  - create without serial twice → two assets (documented limitation); create with same serial twice → second 409;
  - CSV: empty tag → generated at commit, preview shows "Otomatik" and consumes no number (sequence value unchanged after preview); legacy tag kept; tag `DMR-000123` → row error `assets.import_reserved_tag` (Review Focus); unknown brand/model → row error `assets.import_catalog_missing` and nothing written;
  - frontend: form has no tag field and shows "Kaydedildiğinde otomatik atanır"; type → brand → model chain filters models; missing definition text + admin-only catalog link; after save a toast shows the assigned tag; editing an asset with an inactive model shows it (disabled option) and saves (Review Focus).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement; migration creates `asset_tag_seq` starting at 1; `tools/migrate.sh`; `tools/gen-api-types.sh`.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Amend ADR-0023 (tag, no idempotency key and why). Commit `feat: generate DMR asset tags and resolve catalog in asset form and CSV import`.

### Task 15: Incident device link (UX-10)

**Files:**
- Modify: `backend/src/Tgysm.Application/Features/Tickets/CreateTicket.cs` (`CreateIncidentCommand(..., Guid? AssetId)`), `TicketEndpoints.cs` contract, `ErrorCodes.cs` (`tickets.asset_not_assigned_to_requester`), `tr/errors.json`, `frontend/src/features/tickets/NewTicketPage.tsx`, `tr/tickets.json`.
- Test: `Tgysm.Api.IntegrationTests/Tickets/IncidentAssetTests.cs`, `NewTicketPage.test.tsx`.

**Interfaces:**
- Consumes: existing ticket–asset link in `Links.cs`, `ListMyAssets`.

- [ ] Step 1: Failing tests: incident with an asset currently assigned to the requester → ticket created and asset linked (visible on both sides); asset assigned to someone else or returned → 400 `tickets.asset_not_assigned_to_requester` and no ticket; no `AssetId` → unchanged behaviour; frontend offers "İlgili cihaz" from my assets only, optional.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement (link inside the same save as the ticket); `tools/gen-api-types.sh`.
- [ ] Step 4: Gates exit 0.
- [ ] Step 5: Commit `feat: let employees pick their assigned device when reporting an incident`.

### Task 16: Reports layout and Chart.js

**Files:**
- Modify: `frontend/src/features/reports/{ReportsPage,ReportFilterBar,FigureCard,DrilldownPage,ExportButton,MyExportsPage,DayField,useReportFilter,DistributionChart}.tsx|ts`, `tr/reports.json`, `frontend/vite.config.ts` (drop Recharts/d3 patterns), `frontend/package.json` (`npm rm recharts`).
- Create: `frontend/src/features/reports/{PeriodPicker,ActiveFilterChips}.tsx`, `docs/adr/0022-chartjs-charts.md` (+ index row; ADR-0020 status note "Recharts part superseded by ADR-0022").
- Test: existing report tests + `PeriodPicker.test.tsx`, `ActiveFilterChips.test.tsx`, `DistributionChart.test.tsx`.

**Interfaces:**
- Consumes: `FilterPanel`, `KpiCard`, `PageHeader`, `useBreadcrumbLabel`.
- Produces: `DEFAULT_PERIOD_DAYS = 30`; `PERIODIC_REPORTS` (incidents, requests, operations, transfers); `PeriodPicker({ from, to, onChange })` with presets `last7`, `last30`, `thisMonth`, `lastMonth`, `thisYear`, `custom` computed in the company time zone from `shared/lib/format.ts`; `useReportFilter` applies the default period only when the URL has neither `from` nor `to`; `readReportFilter` returns `{ filter, droppedRange: boolean }` so the page can warn (UX-12); `DistributionChart` → lazy `CChartBar` (horizontal, sorted, `aria-hidden`), registering only `BarElement`, `CategoryScale`, `LinearScale`, `Tooltip`.

- [ ] Step 1: Failing tests:
  - opening `/reports/incidents` without dates requests the last 30 days and shows "Son 30 gün"; `/reports/assets` sends no dates; warranty keeps its window;
  - `from > to` in the URL shows a visible warning and requests without dates (UX-12);
  - mobile: filters behind "Filtreler (n)" with the active count; chips remove one filter each;
  - KPI cards render value, unit and period; card link keeps the filters (existing drill-down test);
  - `DistributionChart` renders a canvas inside an `aria-hidden` wrapper and the bucket list stays as text; `package.json` no longer lists `recharts`;
  - drill-down "back" keeps the period.
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Implement; remove Recharts.
- [ ] Step 4: Gates exit 0; `cd frontend && npx vite build` shows `vendor-charts` only in the reports lazy graph (not in `index.html` modulepreload).
- [ ] Step 5: Write ADR-0022. Commit `feat: CoreUI report layout with default period and Chart.js charts`.

### Task 17: Glossary, console warning and cleanup

**Files:**
- Create: `frontend/src/shared/i18n/glossary.test.ts`, `docs/superpowers/specs/2026-10-09-ui-glossary.md` (term list).
- Modify: `frontend/src/shared/i18n/locales/tr/*.json` (unify terms), `frontend/src/app/router.tsx` (UX-16 fix), any file with dead Tailwind-era helpers, `frontend/eslint.config.js` if it references Tailwind.
- Test: `glossary.test.ts`, `router.hydrate.test.tsx`.

- [ ] Step 1: Failing tests: no locale value contains banned variants (`Nasıl Yapılır?` outside the home card title it names, `Konum` where `Lokasyon` is the term, `BT` where `IT` is the term — exact list in the glossary file); rendering the router logs no `HydrateFallback` warning (spy on `console.warn`).
- [ ] Step 2: Run — expect FAIL.
- [ ] Step 3: Fix texts; find the warning's cause (route with `loader`/lazy and no `HydrateFallback`) and fix it — no speculative changes: reproduce first in the test.
- [ ] Step 4: Remove unused code and dependencies (`npx depcheck` style check by reading imports; no `className` with Tailwind utilities left: test greps `src/**/*.tsx` for `className="[^"]*\b(px|py|mt|mb|text|bg|flex|grid)-` patterns not from CoreUI/Bootstrap utilities list). Gates exit 0; `npx vite build` no chunk warning.
- [ ] Step 5: Commit `chore: unify UI terms, fix hydrate warning and remove leftover styling code`.

### Task 18: E2E, QA, reviews, merge request and docs

**Files:**
- Create: `frontend/e2e/navigation.spec.ts`, `frontend/e2e/catalog.spec.ts`.
- Modify: existing `frontend/e2e/*.spec.ts` only where flows changed (reason in commit), `frontend/e2e/world.ts` (seed brand/model, employee with an assigned asset).

- [ ] Step 1: E2E at 360 and 1280 px: sidebar per role (employee sees no IT/admin items, admin switches to Yönetim and back); article edit → article → list via breadcrumb; filtered queue → ticket → back to the same filters; filtered report → metric list → back with the same period; admin creates brand + model, IT creates an asset with them and sees a `DMR-` tag in the toast and detail; employee reports an incident with their device.
- [ ] Step 2: `tools/build.sh` and `tools/e2e.sh` (stop dev services first, `tools/dev-service.sh start` after) — exit 0.
- [ ] Step 3: Browser QA at 320/390/1280 px with employee, IT and admin sessions (E2E DB, never dev admin password prompt): keyboard-only pass on sidebar, modals, combobox; console free of errors; screenshots of home, ticket detail, inventory, article, report, one admin page at 390 and 1280 px saved under `tmp/qa/` (not committed).
- [ ] Step 4: One review each on the finished tree: `ecc:react-reviewer`, `ecc:csharp-reviewer`, `ecc:database-reviewer`; fix Critical/Important test-first; re-run gates.
- [ ] Step 5: Check review §15.4 item by item and record the result in the ledger; ask the owner to approve merge to `master` and push.
- [ ] Step 6: After approval: Markdown docs (`capabilities.md` incl. baseline, `README.md`, `CLAUDE.md`, `SECURITY.md`, `docs/adr/README.md`, `docs/user-guides/PROGRESS.md`) per `~/.claude/guides/docs-updates.md`; commit; merge; push.
