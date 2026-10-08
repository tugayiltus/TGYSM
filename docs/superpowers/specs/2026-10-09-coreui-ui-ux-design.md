# CoreUI UI/UX Redesign — Design

- Date: 2026-10-09
- Owner: Tugay İLTUŞ
- Source review: `docs/superpowers/specs/2026-10-09-ui-ux-review.md` (Turkish; §3 findings UX-01…UX-16, §4 navigation and breadcrumb, §5 page-by-page review, §6 reports, §7–§8 catalog and asset tag, §12 acceptance matrix, §15 task definition)
- Builds on: Phases 1–5 on `master` (commit `a6bba60`)
- Branch: `ui-coreui` (one branch, merged to `master` only after the full QA in §9)
- Out of scope: Phase 6 (release), new report analytics (§6.3 of the review: time series, previous-period comparison), dark theme, CoreUI PRO components.

## 1. Agreed understanding

**Goal.** Replace the presentation layer of the whole product (employee portal, IT workspace, reports, knowledge base, `/admin`, auth screens) with the CoreUI Free React Admin layout: left sidebar on both the application and admin sides, header with account menu, breadcrumb on every signed-in page, mobile-first. Fix the review's UX findings, add an admin-managed brand/model catalog and server-generated asset tags.

**Kept unchanged.** Backend architecture, API contracts (except the additions in §6), authorization and record scope, cookie/CSRF, TanStack Query hooks, the typed API client, react-hook-form + zod schemas, i18n (Turkish text only in `locales/tr/*.json`), route paths, existing data and the sample article, business rules (SLA, approvals, reports maths).

**Owner decisions (2026-10-09)**

| Topic | Decision |
|---|---|
| UI library | Full CoreUI Free conversion; Tailwind and Radix removed. The template is a layout reference; its demo app, Redux and router are not copied. |
| Migration style | Big bang on branch `ui-coreui`: Tailwind/Radix removed first, all screens rewritten, merged at the end. |
| Theme | Light only. |
| Charts | Chart.js via `@coreui/react-chartjs`; Recharts removed. |
| Asset tag | Server assigns `DMR-000001`, `DMR-000002`, … for new assets; existing tags stay unchanged. |
| Catalog misses | A brand/model not in the catalog makes the CSV row fail ("Tanım eksik"); no auto-create. |
| Existing free-text brand/model | Migrated automatically into the catalog (the product is not live; only dev/E2E data exists). No mapping screen. |
| Report default period | Last 30 days for period reports; quick ranges 7 days, 30 days, this month, last month, this year, custom. |
| Data work | Included, in its own tasks with migration and ADR. |

**Success criteria.** A user of each role can do their full job in the new UI at 320/390/1280 px; every signed-in page shows a working breadcrumb and the matching active sidebar item; none of the review's §15.4 "not done" conditions remain; all quality gates and E2E pass.

## 2. Packages

| Add | Version (checked 2026-10-09) |
|---|---|
| `@coreui/react` | 5.13.x |
| `@coreui/coreui` (SCSS) | 5.9.x (+ peer `@popperjs/core` 2.11.x) |
| `@coreui/icons`, `@coreui/icons-react` | 3.1.x, 2.3.x |
| `@coreui/react-chartjs`, `chart.js` | 3.0.x, 4.5.x |
| `sass` (dev) | current, for `src/styles/app.scss` |

Removed: `tailwindcss`, `@tailwindcss/vite`, `radix-ui`, `recharts` and their Vite/ESLint/config hooks. The CoreUI Free template commit used as reference is recorded in ADR-0021 (current `main` HEAD `2609f59`); its MIT license notice is kept in `frontend/THIRD_PARTY_NOTICES.md` for any code adapted from it.

Vite chunk groups: `vendor-coreui` (`@coreui/react`, `@coreui/icons*`, `@popperjs`), `vendor-charts` (`chart.js`, `@coreui/react-chartjs`). The Recharts/d3 patterns are deleted. No chunk may exceed the 500 KB warning.

## 3. Styling

- One stylesheet entry `src/styles/app.scss`: CoreUI variable overrides (brand colour = today's `brand-600` `#1d4ed8` family, font stack, radius), then `@coreui/coreui/scss/coreui`, then a small set of scoped TGYSM rules (`_layout.scss`, `_print.scss` if needed).
- Layout uses CoreUI/Bootstrap grid and utilities (`row`/`col-*`, `d-flex`, `gap-*`, `mb-*`, `text-body-secondary`). Ad-hoc CSS only in the scoped partials, never inline `style` for layout.
- Light theme only: `data-coreui-theme="light"` on `<html>`; no theme switch.
- Touch targets ≥ 44 px on mobile for primary actions and list rows; body text ≥ 16 px on inputs (no iOS zoom).

## 4. Layout and navigation

### 4.1 Layouts

- `AppLayout` (signed-in employee/IT/admin on application pages) and `AdminLayout` (`/admin/*`, admin only). Both: `CSidebar` left, `CHeader` top, `PageHeader` (breadcrumb + H1 + primary action) under the header, then content. A skip link jumps to `<main>`; on route change focus moves to the page H1.
- Sidebar: fixed and collapsible (narrow mode, remembered in `localStorage` with try/catch) from 992 px; below 992 px an offcanvas opened by the header hamburger, closed on navigation with focus returned to the hamburger.
- Header right side: `AccountMenu` (Profilim, Şifre değiştir, Çıkış). Admins see a "Yönetim" link in `AppLayout`; `AdminLayout` shows "Uygulamaya dön". Logout is no longer a primary header button.
- `AuthLayout` (login, forgot/set/change password): centred CoreUI card, no sidebar, no breadcrumb, contextual "Girişe dön" link. The forced password change cannot be escaped through navigation.

### 4.2 Menus

Application menu (role-filtered; employees see only Kişisel):

| Group | Items |
|---|---|
| Kişisel | Ana sayfa, Kayıtlarım, Zimmetlerim, Bilgi bankası |
| IT operasyonu | İş listem, Kuyruk, Onaylar (admin), Envanter |
| Analiz | Raporlar, Dışa aktarımlarım |

Admin menu: Genel bakış · Organizasyon (Kullanıcılar, Departmanlar, Lokasyonlar, Ekipler) · Cihaz kataloğu (Markalar, Modeller) · Hizmet kataloğu (Kategoriler, Talep tipleri) · SLA (Takvim, Politika) · Bildirimler (E-posta şablonları, Gönderimler) · Sistem ve denetim (Sistem, Denetim). The group of the active item is expanded automatically. Hiding a menu item is presentation only; API authorization is unchanged.

"Genel bakış" (`/admin`) shows setup gaps from existing endpoints only: departments without approver, intake team missing, worker status warning. No new endpoint.

### 4.3 Route metadata (single source)

`src/shared/navigation/routeMeta.ts` lists every route: `path`, `titleKey` (i18n), `parent` (route id or none), `roles`, `menu` (group + order, or none), optional `dynamicLabel` key. Sidebar, breadcrumb and the page H1 all read it; no second label dictionary. Dynamic labels (ticket number, asset tag, article title, report/metric name) come from data the page already loaded, through a small `useBreadcrumbLabel(label)` setter; while loading a neutral placeholder shows; on 403/404 no hidden title leaks and no extra request is made for the label.

### 4.4 Breadcrumb

- `CBreadcrumb` with real `Link` items, `aria-label`, last item `aria-current="page"`; follows the review's §4.4.1 map exactly. A menu group without its own page is plain text, never a made-up URL.
- Mobile: root + "…" (a real menu listing the skipped levels) + parent + current; long titles truncate visually, full text stays in the H1 and accessible name; no horizontal page growth.

### 4.5 Return to context

- Lists that open details pass `state: { from: <pathname+search> }`. The detail's "back" target and the parent breadcrumb item use `from` when it is a same-app path from an allowed list (whitelist of list route ids); otherwise the route's default parent. No `navigate(-1)`.
- `RequireSession` keeps `pathname + search` (not hash) as the post-login target, validated as an in-app path (UX-13). Tokens/passwords never enter it.

## 5. Component layer (`src/shared/ui`)

Existing components are rebuilt on CoreUI; their props stay where possible so page logic does not change, but every page's layout markup is rewritten.

| Component | CoreUI base | Notes |
|---|---|---|
| `Button` | `CButton` | `type="button"` default kept; `isLoading` → `CSpinner` + `aria-busy` |
| `TextField`, `TextAreaField`, `SelectField` | `CFormInput`, `CFormTextarea`, `CFormSelect` | label, `invalid` + `feedbackInvalid`, `aria-describedby` for hint and error |
| `Dialog`, `ConfirmDialog`, `TemporaryPasswordDialog` | `CModal` | focus trap and return, `fullscreen="sm-down"` |
| `Sheet` | `COffcanvas` | used by mobile sidebar and `FilterPanel` |
| `StatusBadge` | `CBadge` | colour + text, never colour alone |
| `FormAlert`, `States` | `CAlert`, `CSpinner` | loading / empty / error; error keeps retry |
| `Pagination` | `CPagination` | |
| `ResponsiveList` | `CTable` ≥ 768 px, `CListGroup` cards below | one data definition for both |
| `ForbiddenPage`, `NotFoundPage` | card layout | inside the right layout, no data leak |

New: `PageHeader`, `AppSidebar`, `AppHeader`, `AccountMenu`, `KpiCard`, `FilterPanel` (desktop inline primary filters + "Diğer filtreler"; mobile offcanvas with Uygula / Temizle / Vazgeç), `Toast` (`CToaster`, polite live region), `ComboboxField` (searchable select built from free CoreUI form controls with ARIA combobox pattern and keyboard tests), `UnsavedChangesGuard` (`useBlocker` + `beforeunload`).

Icons: only imported `@coreui/icons` glyphs via `CIcon`; every icon has visible text or an `aria-label`.

## 6. Data changes

### 6.1 Brand/model catalog

- Tables `brands` (`id`, `name`, `normalized_name` unique, `is_active`, `created_at`, `updated_at`, `xmin`) and `asset_models` (`id`, `brand_id` FK, `asset_type`, `name`, `normalized_name`, `is_active`, timestamps, `xmin`; unique `(brand_id, asset_type, normalized_name)`).
- Normalization: collapse inner whitespace, then `TurkishText.ToSearchKey` (trim, Turkish lower-case, `ı`→`i`); punctuation and digits kept (`ThinkPad T14` ≠ `ThinkPad T14s`; ` LENOVO ` = `Lenovo`).
- `assets.model_id` (FK, not null after migration); `assets.brand` and `assets.model` text columns dropped. Brand and type are read through the model; an asset whose `asset_type` differs from its model's type is refused (`ErrorCodes` new code).
- Migration: create the catalog from distinct existing `(brand, asset_type, model)` triples after normalization, link assets, then drop the text columns (single migration, verified by an integration test on seeded data).
- Admin: `/admin/brands`, `/admin/models` (filter by brand) — create, rename, deactivate, reactivate; a used entry is never hard-deleted; inactive entries stay readable on existing assets and are not offered for new ones. Audit actions + `AuditEntityTypes` + Turkish texts.
- API and CSV accept only an active `modelId`; asset form: type → brand (`ComboboxField`) → model filtered by brand and type. A missing entry shows "Tanım eksik — yöneticiye bildirin" (admins get a link to the catalog page).
- CSV: columns `brand` + `model` (+ type) are resolved against the catalog by normalized name; no match → row error "Tanım eksik", nothing is written (existing all-or-nothing commit).
- Reports and filters that showed brand/model read them through the model join.

### 6.2 Asset tag

- PostgreSQL sequence `asset_tag_seq`; the create handler takes `nextval` and formats `DMR-` + 6 digits (`AssetTagFormat` constants). Tags are immutable, never reused; gaps are normal.
- Existing tags are kept as they are; the existing normalized-tag unique index covers old and new tags.
- Create form has no tag field ("Kaydedildiğinde otomatik atanır"); after save a toast shows the assigned tag. Edit cannot change the tag.
- CSV: tag column optional — empty → generated at commit (preview does not consume numbers); filled → kept as a legacy tag and checked for uniqueness.
- Retries: the form disables submit while a create is pending; a resent create of a device with a serial number is caught by the existing serial-number uniqueness. A device without a serial number has no duplicate guard; accepted for this release, no idempotency key (recorded in ADR-0023 with the reason).

### 6.3 Incident device (UX-10)

`CreateIncidentCommand` gains `AssetId?`. The server accepts only an asset currently assigned to the requester and links it through the existing ticket–asset link; otherwise 400 with a new error code. The incident form offers "İlgili cihaz" from "Zimmetlerim" (optional).

## 7. Pages and UX findings

Every screen in the review's §15.3 list is moved to `PageHeader` + toolbar + content. Detail pages put actions first: a sticky right column from 992 px, an action bar above the content below. Each screen is checked for CoreUI layout, breadcrumb (or auth exception), 360 px, loading/empty/error, and role.

Role-based home (existing APIs only): employee — "Arıza bildir" and "Talep oluştur" as primary actions, recent records; IT — assigned to me, team queue counts, SLA risk summary.

Reports (review §6.2): report tabs → quick period (default 30 days) → "Filtreler (n)" (`FilterPanel`) → active filter chips + scope label → `KpiCard`s → Chart.js chart (lazy, `aria-hidden`, bucket list as the text alternative) → "Verileri listele". Asset report keeps no date filter; warranty keeps dates as its expiry window.

| ID | Change |
|---|---|
| UX-01, UX-02, UX-09 | §6.1–§6.2 |
| UX-03 | SLA target table: visible unit labels for minutes and percent; cards on mobile |
| UX-04 | Worker status: job-level health separate from per-instance freshness; stale instances never "Zamanında" |
| UX-05 | Approver assignment confirm button "Onaylayıcıyı ata" |
| UX-06 | Reports layout above |
| UX-07 | Audit filter lists every `AuditEntityTypes` value |
| UX-08 | Intake team badge once per view, desktop and mobile |
| UX-10 | §6.3 |
| UX-11 | Empty knowledge base vs. no search result: different messages |
| UX-12 | Reversed date range still dropped, with a visible warning |
| UX-13 | §4.5 |
| UX-14 | `UnsavedChangesGuard` on article editor, asset form, CSV preview, ticket create forms |
| UX-15 | Glossary (Bilgi bankası, Lokasyon, IT) applied to all `tr/*.json`; a test fails on the banned variants |
| UX-16 | Find and remove the cause of the repeated `HydrateFallback` warning |

Each finding is first re-verified in code; one that does not hold is marked invalid in the plan ledger, not "fixed".

## 8. Testing

- Vitest tests keep querying by role and accessible name; a test changes only when behaviour changes, with the reason in the commit message. Coverage ≥ 80 %.
- New unit tests: `routeMeta` integrity (every router route has meta, parents exist, no cycles), breadcrumb per route family (review §4.4.3 cases), sidebar role filtering and active group, `from` whitelist, `RequireSession` return target, `FilterPanel` apply/clear/cancel, `ComboboxField` keyboard, `UnsavedChangesGuard`, glossary test.
- Backend (integration, real PostgreSQL): catalog CRUD + uniqueness + deactivate rules; type mismatch refused; migration on seeded free-text data; concurrent asset creation yields unique `DMR-` tags; CSV catalog miss → row error and no write; CSV empty tag → generated; incident `AssetId` accepted only for the requester's assigned asset.
- E2E (`tools/e2e.sh`, 360 + 1280 px): existing journeys pass; new journeys — sidebar navigation per role (employee sees no IT/admin items), breadcrumb back from article edit → article → list, filtered queue → ticket → back to the same filtered queue, filtered report → list → back with the same period, admin creates brand + model then an asset gets a `DMR-` tag.
- Browser QA at 320/390/1280 px with separate employee, IT and admin sessions; keyboard-only pass on sidebar, modals, combobox; screenshots of home, ticket detail, inventory, article, report and an admin page for both widths.

## 9. Delivery

- Plan order: packages + styles + Tailwind/Radix removal → `shared/ui` → navigation (route meta, layouts, sidebar, breadcrumb, return-to-context) → pages by group (auth, employee, IT/tickets, approvals, inventory, knowledge base, admin) → UX findings → data (catalog, asset tag, CSV, incident device) → reports + Chart.js + Recharts removal → cleanup (dead code, unused deps, chunk check) → QA.
- Each task ends with `tools/lint.sh` and `tools/test.sh` green; intermediate visuals may be incomplete. At the end `tools/build.sh`, `tools/e2e.sh`, browser QA, then one specialist review each (React, C#, database) on the finished tree; Critical/Important fixed test-first.
- Review §15.4 checklist verified item by item before asking to merge. Merge and push only with owner approval. Markdown docs (`capabilities.md`, `README.md`, `CLAUDE.md`, `SECURITY.md`, ADR index, user-guide `PROGRESS.md`) updated as the last step.
- ADRs: ADR-0021 CoreUI Free as the UI layer (extends ADR-0008; Tailwind/Radix removed); ADR-0022 Chart.js for charts (supersedes the Recharts part of ADR-0020); ADR-0023 brand/model catalog and server-generated asset tags.
