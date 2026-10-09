# UI Cohesion (CoreUI Template Visual System) — Design

- Date: 2026-10-09
- Owner: Tugay İLTUŞ
- Builds on: the CoreUI conversion on `master` (commit `523075f`; spec `2026-10-09-coreui-ui-ux-design.md`, ADR-0021–0023)
- Branch: `ui-cohesion` (from `master`; `phase-6-release` is not touched and is rebased afterwards)
- Out of scope: new features, text changes, route or flow changes, backend and API changes, CoreUI PRO components.

## 1. Agreed understanding

**Problem (owner's testing, 2026-10-09).** The sidebar, the page content and the inputs look unrelated; the interface lacks consistency and unity. Measured on `master`:

- The dark sidebar is text only (no icons, no brand area); `@coreui/icons` is not installed.
- List pages (inventory, admin lists, queues) put filters and a bordered table straight on the grey background, while home, ticket detail and profile use cards. Pages are built three different ways.
- Some dialog forms use a `row g-3` grid, others a `d-flex flex-column` stack; a checkbox is a hand-written `<input>`.
- The theme is light only (decided in the earlier spec), with a custom blue `#1d4ed8`.

**Goal.** Every screen speaks the CoreUI Free React Admin template's visual language: icon sidebar with a brand area, sticky header with theme and account menus, breadcrumb strip, all content inside cards, one field/table/form structure everywhere, light/dark/auto color modes.

**Kept unchanged.** Route paths, role menus, breadcrumbs and context return, URL-held filters, react-hook-form + zod schemas, TanStack Query hooks, i18n keys (only new keys are added), API contracts, business rules, phone behavior (sidebar panel, filter sheet, card lists below 992 px / 768 px).

**Owner decisions (2026-10-09)**

| Topic | Decision |
|---|---|
| Scope | All screens: application, `/admin`, auth screens, dialogs. Presentation only. |
| Color modes | Light, dark and auto (system), chosen in the header. Reverses "light only" of the earlier spec (ADR-0026). |
| Sidebar | Dark, as in the template. |
| Primary color | CoreUI default `#5856d6`; the custom `$primary` and `$link-color` are dropped. |
| Approach | Shared building blocks (`PageCard`, `DataCard`, `FormSection`, field fixes, icons, color modes) adopted by every page. Not a SCSS-only reskin, not a copy of the template's layout files. |

**Success criteria.**

1. No signed-in page renders content directly on the page background: every page is `PageHeader` + one or more cards.
2. Every sidebar item has an icon; the narrow sidebar shows icons only and keeps the active item visible.
3. Every form control in `features/` comes from `shared/ui`; a test fails on raw `<input`, `<select`, `<textarea`, `<table` or a hand-made `card` class in `features/`.
4. Light, dark and auto modes work on every page; the choice survives a reload with no light flash; charts follow the mode.
5. No hard-coded colors in `features/` or `shared/` (`bg-white`, `bg-light`, `text-dark`, `text-black`, hex values in TSX); a test enforces it.
6. All existing unit tests and E2E pass at 360 px and 1280 px; no horizontal scroll at 360 px in either mode; frontend coverage ≥ 80%.
7. **Mobile first.** Base styles target 360 px; wider layouts are added with `min-width` breakpoints only (`-sm-`, `-md-`, `-lg-` utilities, `@media (min-width: …)`). The guard test fails on new `max-width` media queries outside the existing `tgysm-stack-table` rule.
8. **No layout shift and no freeze, phone and desktop.** On the inventory list, ticket detail, home and reports pages at 360 px and 1280 px: cumulative layout shift < 0.1 from navigation until the data is shown (measured in E2E with a `PerformanceObserver` on `layout-shift`); no long task over 200 ms while navigating between them (`longtask` observer); switching theme or opening the sidebar/filter panel does not move the page content. Means: loading states reserve the space of the content they replace (card bodies keep a minimum height, tables show skeleton rows of the final row height), `scrollbar-gutter: stable` on the page so opening a dialog does not shift it, fixed-size icons and avatars, the sticky header has a fixed height.

**Owner's aim (2026-10-09).** Visual and graphic unity is the main goal: the interface must look good and consistent everywhere.

## 2. Theme and shell

### 2.1 Variables

`src/styles/_variables.scss` keeps only the font stack, `$border-radius: 0.5rem` and `$input-font-size: 1rem` (iOS zoom guard). `$primary` and `$link-color` are removed, so CoreUI's defaults apply in both modes. `app.scss` stops forwarding the removed variables.

### 2.2 Color modes

- CoreUI 5 color modes through `data-coreui-theme` on `<html>`; `useColorModes` from `@coreui/react` stores the choice under one named storage key (`tgysm-theme`) with values `light`, `dark`, `auto`.
- A small inline script in `index.html` applies the stored mode (or the system preference for `auto`/missing) before the bundle loads, so a dark page never flashes light. The script is the only inline script and contains no secrets; the storage key is shared with the TS code through one constant mirrored in a test.
- Header `ThemeMenu`: `CDropdown` with three items (icon + Turkish label: Açık, Koyu, Otomatik); the toggle shows the active mode's icon and has an accessible name.
- Charts: a `useChartTheme()` hook reads `--cui-body-color`, `--cui-border-color-translucent` and the series colors through `getStyle` and re-renders the chart when the mode changes (the `ColorModeChange` is observed through the `data-coreui-theme` attribute).

### 2.3 Sidebar

- `CSidebar` (dark, `colorScheme="dark"`) with `CSidebarHeader` brand area: product icon + "TGYSM" (application) or "Yönetim" (admin); narrow mode shows the icon only.
- `CSidebarNav` with `CNavTitle` group headings and `CNavItem` links; each route in `ROUTE_META` that appears in a menu gets an `icon` field (a named import from `@coreui/icons`). A test fails when a menu route has no icon.
- The existing role filtering (`menuFor`), active item (`activeMenuItem`), `aria-current`, narrow toggle (`useSidebar`) and the phone `Sheet` panel are kept; the phone panel shows the same icons.
- `CSidebarFooter` keeps the narrow toggle (`CSidebarToggler`).

### 2.4 Header and breadcrumb

- `CHeader` `position="sticky"` with a bottom border and a light shadow. First row: menu button (below 992 px), then on the right the area switch ("Yönetim" / "Uygulamaya dön"), `ThemeMenu`, and the account menu with a `CAvatar` showing the user's initials.
- Second row (`CHeader` divider + container): the breadcrumb moves here from the content area, as in the template. Truncation rules (`tgysm-crumb`) and context return are unchanged.
- The page H1 and page actions stay in the content (`PageHeader`), so the focus-on-navigation behavior is unchanged.

### 2.5 Content area

`bg-body-tertiary` background stays; `main` keeps `container-lg`. Rule: no content directly on the background.

### 2.6 Dependencies

`@coreui/icons` and `@coreui/icons-react` (same major line as `@coreui/react` 5). Icons are imported by name (tree-shaken); a `vendor-coreui-icons` chunk group is added to `vite.config.ts`.

### 2.7 Auth screens

The template's login card: centered `CCard` with `CCardBody`, product icon and title; email and password inputs as `CInputGroup` with a leading icon (`cilEnvelopeOpen`, `cilLockLocked`). Set password, forgot password and change password use the same card. Works in both modes.

## 3. Building blocks (`src/shared/ui`)

| Component | Status | Contract |
|---|---|---|
| `PageHeader` | changed | H1 + actions as today; optional `icon` and `description` (`text-body-secondary`). |
| `PageCard` | new | `CCard`; optional header (`title`, optional `actions`); `CCardBody`; optional `footer`. `bodyClassName` for flush content. |
| `DataCard` | new | List page card: header holds `FilterPanel` (behavior unchanged); body holds `ResponsiveList` or `States`; footer holds `Pagination` and the "1–20 / 57 kayıt" count. |
| `FormSection` | new | `PageCard` whose body is a `row g-3` grid; footer has the actions right-aligned (Kaydet/Vazgeç). Used by full-page forms. |
| `ResponsiveList` | changed | Table sits flush in the card: `table table-hover align-middle mb-0`, header `text-body-secondary small text-uppercase` (no `table-light`, no own border/background). Phone cards unchanged. |
| `TextField`, `SelectField`, `ComboboxField`, `TextAreaField` | changed | Label `form-label fw-medium small`; optional `required` mark (`*` `aria-hidden` + visually hidden "zorunlu"); messages via `CFormFeedback`/`CFormText`; `startIcon` puts the control in a `CInputGroup` with a `CInputGroupText` icon (search fields use it). |
| `ComboboxField` list | changed | Styled as `dropdown-menu`/`dropdown-item` so border, shadow and colors match selects in both modes. |
| `CheckField` | new | `CFormCheck` (checkbox or `switch`), label and messages linked like the other fields. Replaces hand-written checkboxes. |
| `StatusBadge` | changed | `CBadge` with `-subtle` backgrounds and `-emphasis` text, readable in both modes. |
| `Button` | changed | Optional `icon` prop (`CIcon`, placed before the text); variants unchanged. |
| `KpiCard` | changed | Template stat widget look (`CWidgetStatsF`-style: colored icon box, value, label); still a link when it has a target. |
| `Dialog` | unchanged API | Dialog forms use the `row g-3` grid with `col-12 col-sm-6` fields. |

**Guard tests (`styles.test.ts`).** Scan `src/features/**/*.tsx` and fail on: raw `<input`, `<select`, `<textarea`, `<table`; a `className` containing `card` (outside `shared/ui`); `bg-white`, `bg-light`, `text-dark`, `text-black`; hex color literals in TSX (also in `src/shared`, except chart palette constants in one file). Each rule names the offending file and line.

## 4. Page migration

| Group | Pages | Target |
|---|---|---|
| Lists | `AssetsPage`, `MyTicketsPage`, `WorkspacePage` (İş listem, Kuyruk), `ApprovalsPage`, `KnowledgeListPage`, `MyExportsPage`, `MyAssetsPage`, `DrilldownPage`, admin `UsersPage`, `DepartmentsPage`, `LocationsPage`, `TeamsPage`, `CategoriesPage`, `RequestTypesPage`, `BrandsPage`, `ModelsPage`, `EmailDeliveriesPage`, `EmailTemplatesPage`, `AuditPage` | `PageHeader` + `DataCard` |
| Detail | `TicketDetailPage`, `AssetDetailPage`, `ArticlePage` | `PageHeader` + main `PageCard`s + aside `PageCard` (sticky from 992 px as today) |
| Forms | `NewTicketPage`, `ArticleEditorPage`, `ProfilePage`, `ChangePasswordPage`, `SlaPolicyPage`, `SlaCalendarPage`, `SystemPage`, `AssetImportPage` | `PageHeader` + `FormSection`s |
| Dashboards | `HomePage`, `AdminOverviewPage`, `ReportsPage` | `PageHeader` + `KpiCard` row + chart/content `PageCard`s |
| Auth | `LoginPage`, `SetPasswordPage`, `ForgotPasswordPage` | Template login card (§2.7) |
| Dialogs | all `*Dialog.tsx` | `row g-3` grid, `CheckField` |

## 5. Order of work

Each step ends green (`tools/lint.sh`, `tools/test.sh`) and is committed separately:

1. Variables, color modes (`ThemeMenu`, inline script, storage constant), icons dependency and chunk group.
2. Shell: sidebar (brand, icons, `CSidebarNav`), header (sticky, avatar, theme menu), breadcrumb strip.
3. Building blocks and field changes (§3) with unit tests.
4. List pages.
5. Detail and form pages.
6. Dashboards and chart theme.
7. Auth screens and dialogs.
8. Guard tests switched on for the whole tree; E2E (both viewports, both modes); ADR-0026.

## 6. Error handling and accessibility

- No behavior changes in error handling: field errors, `FormAlert`, toasts and `States` keep their texts and roles; they only move into cards.
- Contrast: CoreUI tokens in both modes; badges use `-emphasis` text on `-subtle` backgrounds. Icons next to text are `aria-hidden`; icon-only controls (narrow sidebar, theme toggle, menu button) have accessible names.
- Focus outlines are not removed; the page title focus rule (`tgysm-page-title:focus`) is unchanged.
- Touch targets stay ≥ 44 px (`tgysm-touch`).

## 7. Testing

- **Unit:** each new or changed building block; `ThemeMenu` (choice written to storage, `data-coreui-theme` updated, `auto` follows `prefers-color-scheme`); sidebar renders an icon per item and hides labels in narrow mode; menu-route-has-icon test; guard tests in `styles.test.ts`; existing page tests keep passing (role/label selectors).
- **E2E:** existing suites at 360 px and 1280 px; a new spec switches to dark mode, reloads and checks the mode is kept with no horizontal scroll on the inventory, ticket detail and reports pages; a layout-stability spec measures criterion 8 (CLS and long tasks) at both viewports. Selectors broken only by markup changes are updated; no behavior assertions are weakened.
- **Coverage:** frontend line coverage ≥ 80% (`tools/test.sh`).

## 8. Documentation

- ADR-0026 "CoreUI template visual system and color modes": records the dark/auto mode reversal of the light-only decision (links ADR-0021, ADR-0022), the building blocks rule and the guard tests.
- `README.md` / `capabilities.md`: only on the owner's request (named in the final report).
