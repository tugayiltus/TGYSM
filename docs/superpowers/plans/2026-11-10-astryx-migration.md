# Astryx Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the whole TGYSM frontend on Astryx and remove CoreUI, without changing what any screen does.

**Architecture:** Bottom-up on branch `ui-astryx`. First the shared layer (`src/shared/ui`, `src/shared/layout`, `src/shared/navigation`) is rebuilt on Astryx with its public props kept where possible, then each feature folder is rebuilt from an Astryx page template, then CoreUI, Sass and the Bootstrap utility classes are removed. Both libraries load until the last task; Astryx CSS is layered, CoreUI unlayered, so unmigrated screens keep their look.

**Tech Stack:** React 19, Vite (Rolldown), TypeScript, TanStack Query, React Router, react-hook-form + zod, i18next, `@astryxdesign/core` 0.7.x with `@astryxdesign/theme-neutral`, Chart.js, Vitest + Testing Library, Playwright.

**Spec:** `docs/adr/0027-astryx-design-system.md` (decision and steps). Owner request: replace the interface completely with Astryx so new screens are faster to build with an AI agent. Setup is done in commit `c59355f`.

## Amendment 2026-11-10 (Task 2 ruling)

Astryx inputs are controlled only (`value` + `onChange(value, e)`), `Selector` is not a native `<select>`, and `Button.icon` is a `ReactNode`; today's wrappers take `register()` spreads and CoreUI icon arrays. Keeping their props is not possible. Instead:

- Features use Astryx components **directly** (`npx astryx component <Name>`), not thin pass-through wrappers.
- Shared code keeps only wrappers that add app behaviour: react-hook-form-bound fields in `src/shared/form/` (`useController`, Turkish `fieldError` → `status`), Turkish loading/empty/error states, confirm dialog, toast helper, responsive list, unsaved-changes guard, app shell.
- The CoreUI wrappers in `src/shared/ui` stay unchanged until their last consumer moves, and are deleted in Task 13. Tasks 2–4 therefore build the new Astryx-side pieces next to them instead of rewriting them in place.

## Global Constraints

- Every screen keeps its behaviour, routes, API calls, permissions and Turkish texts. This is a UI rewrite, not a feature change.
- Accessible names (roles, labels, button texts) stay the same, so `frontend/e2e/*.spec.ts` keeps passing. A test that must change gets the smallest edit and the reason in the commit message.
- Follow `frontend/CLAUDE.md`: start a page from `npx astryx template <id>`, read `npx astryx component <Name>` before using a component, no raw `<div>`/`<span>` for layout, tokens (`var(--color-*|--spacing-*|--radius-*)`) instead of hex/px, no StyleX compiler, no Tailwind (`src/styles/noTailwind.test.ts` stays). Semantic elements a component cannot replace (`<form>`, `<main>` if the shell lacks it) are allowed.
- No new `.scss`/`.css` files and no `className` with Bootstrap utilities (`d-flex`, `mb-3`, `text-body-secondary` …) in migrated files. A remaining style need goes through a component prop or a theme override, never `:root`.
- Turkish text only in `src/shared/i18n/locales/tr/*.json` (ADR-0006). Astryx built-in strings use the shipped `tr-TR` catalog.
- Gates per task: `tools/lint.sh` exit 0; `cd frontend && npm run test:coverage` exit 0 with line coverage ≥ 80%; `cd frontend && npx astryx doctor --built .` with no `fail`. No `eslint-disable`, no magic numbers/strings.
- Version range of `@astryxdesign/*` stays `^0.7.x` (below `0.8.0`).
- `vite.config.ts` chunk groups: new heavy dependency gets its own `vendor-*` group; `chunkSizeWarningLimit` is not raised.
- Commit per task on `ui-astryx`, named paths only, no AI attribution. No push, no merge to `master` without the owner.
- Dev services: stop with `tools/dev-service.sh stop` only for `tools/e2e.sh`, start again afterwards.

## Review Focus

1. **Dark mode switch on every screen:** choosing Koyu/Açık/Otomatik in the header must recolour Astryx and charts at once, with no flash on reload. Test in Task 1 and Task 5.
2. **Phone width (E2E mobile viewport):** lists become cards, side nav becomes a drawer, nothing scrolls sideways. E2E checkpoints in Task 5, 7 and 12; `ResponsiveList` test in Task 4.
3. **Keyboard and focus:** every dialog/drawer traps focus and returns it to the opener; side nav and menus work with arrows/Escape (`e2e/keyboard.spec.ts`). Tests in Task 3 and Task 5.
4. **Long Turkish labels and characters (ğ, ş, İ, ı):** labels wrap, fonts render the Latin Extended glyphs, sorting/upper-casing uses `tr`. Test in Task 2.
5. **Unsaved form leaving:** `UnsavedChangesGuard` still blocks navigation with the Astryx dialog, including browser back. Test in Task 3.

---

### Task 1: Foundation — locale, icons, provider tests, doctor gate

**Files:**
- Modify: `frontend/src/shared/ui/AstryxProvider.tsx`
- Modify: `frontend/src/shared/ui/icons.ts`
- Create: `frontend/src/shared/ui/AstryxProvider.test.tsx`
- Modify: `tools/lint.sh`

**Interfaces:**
- Consumes: `useColorMode()` from `src/shared/ui/colorMode.ts`.
- Produces: `AstryxProvider({children})` wrapping `InternationalizationProvider` (locale `tr-TR`, messages from `@astryxdesign/core/locales/tr-TR.generated.js`), `Theme`, `LinkProvider`. `icons.ts` exports the same names it exports today, each now an Astryx `IconType` (semantic name string where `npx astryx docs icons available-names` has one, else an SVG component). Later tasks import icons only from `icons.ts`.

- [ ] **Step 1: Write failing tests** in `AstryxProvider.test.tsx`: `renders Astryx strings in Turkish` (render a `Pagination` or `Dialog` close button inside the provider, assert a known `tr-TR` label from the catalog file), `follows the dark color mode` (call `setColorMode('dark')`, assert the Theme root reports dark — read how via `npx astryx component Theme`).
- [ ] **Step 2: Run** `cd frontend && npx vitest run src/shared/ui/AstryxProvider.test.tsx` — expect the Turkish test to FAIL.
- [ ] **Step 3: Add `InternationalizationProvider` to `AstryxProvider`; switch `icons.ts` to Astryx icon values.** Check whether `@astryxdesign/theme-neutral` already brings `lucide-react`; use it for non-semantic icons if so, otherwise add `lucide-react` with a `vendor-icons` chunk group (builder's call).
- [ ] **Step 4: Add `npx astryx doctor --built .` (in `frontend/`) to `tools/lint.sh`.** If doctor fails only because CoreUI code is still present, keep it report-only (print, do not fail) with a comment pointing to Task 13, where it becomes a hard gate.
- [ ] **Step 5: Run the gates** (Global Constraints) — all exit 0.
- [ ] **Step 6: Commit** `feat(frontend): Turkish Astryx strings and Astryx icon set`.

### Task 2: Form primitives

**Files:**
- Modify: `frontend/src/shared/ui/{Button,TextField,TextAreaField,SelectField,CheckField,ComboboxField,FieldLabel,FieldMessages,FormAlert,FormSection}.tsx`, `fieldIds.ts` if ids change
- Test: `frontend/src/shared/ui/{fields,primitives}.test.tsx`

**Interfaces:**
- Consumes: Task 1 icons.
- Produces: the same exported names and props (`ButtonProps`, `TextFieldProps`, `SelectOption`, `SelectFieldProps`, `CheckFieldProps`, `ComboboxFieldProps`, `TextAreaFieldProps`, `FormSectionProps`) so the 40 `Button`, 34 `TextField`, 23 `SelectField` call sites compile unchanged. Mapping: `Button`→`Button`; `TextField`→`Field`+`TextInput`; `TextAreaField`→`Field`+`TextArea`; `SelectField`→`Field`+`Selector`; `CheckField`→`CheckboxInput` (or `Switch` where the prop says switch); `ComboboxField`→`Typeahead`; `FormAlert`→`Banner`; `FormSection`→`Section`. Props that only existed for CoreUI styling may be dropped if no call site uses them (`grep` first).

- [ ] **Step 1: Update tests first:** keep every existing role/label assertion; replace class-name assertions with role/attribute assertions; add `long Turkish label wraps and keeps its accessible name` (label `Şirket İçi Değerlendirme Ağırlığı Çok Uzun Bir Başlık` is reachable by `getByLabelText`) and `error message is linked to the input` (`aria-describedby` points at the message).
- [ ] **Step 2: Run** `npx vitest run src/shared/ui/fields.test.tsx src/shared/ui/primitives.test.tsx` — new assertions FAIL.
- [ ] **Step 3: Rebuild the ten components on Astryx** with react-hook-form wiring unchanged (`register`/`Controller` as today).
- [ ] **Step 4: Run the gates.**
- [ ] **Step 5: Commit** `feat(frontend): form primitives on Astryx`.

### Task 3: Overlays, feedback and states

**Files:**
- Modify: `frontend/src/shared/ui/{Dialog,ConfirmDialog,Sheet,Toast,useToast,TemporaryPasswordDialog,UnsavedChangesGuard,States,StatusBadge}.tsx` (`useToast.ts`)
- Test: `frontend/src/shared/ui/{overlays,composites}.test.tsx`

**Interfaces:**
- Produces unchanged exports: `Dialog`/`DialogProps`, `ConfirmDialog`/`ConfirmDialogProps`, `Sheet`/`SheetProps`, `ToastProvider`, `useToast()`, `TemporaryPasswordDialog`, `UnsavedChangesGuard`, `LoadingState`, `EmptyState`, `ErrorState`, `TABLE`, `StatusBadge`. Mapping: `Dialog`→`Dialog`; `ConfirmDialog`→`AlertDialog`; `Sheet`→`Drawer`; toasts→Astryx `Toast`/`useToast`; `LoadingState`→`Spinner` with label (fixes today's `<p>` containing a `<div>` warning); `EmptyState`→`EmptyState`; `ErrorState`→`Banner` status error; `StatusBadge`→`Token` or `StatusDot`+`Text` (Astryx: `Badge` is for counts only).

- [ ] **Step 1: Tests first:** `dialog returns focus to the opener on close`, `Escape closes the sheet`, `toast is announced in a live region`, `loading state renders no block element inside a paragraph` (no React nesting warning: spy on `console.error`, expect not called), `unsaved guard blocks navigation and stays on Vazgeç` (memory router, dirty form, click a link, assert dialog and unchanged location).
- [ ] **Step 2: Run** the two test files — new tests FAIL.
- [ ] **Step 3: Rebuild the components.**
- [ ] **Step 4: Run the gates.**
- [ ] **Step 5: Commit** `feat(frontend): dialogs, toasts and states on Astryx`.

### Task 4: Data display

**Files:**
- Modify: `frontend/src/shared/ui/{DataCard,PageCard,ResponsiveList,StackTable,Pagination,KpiCard,FilterPanel}.tsx`
- Test: `frontend/src/shared/ui/{cards,composites}.test.tsx`

**Interfaces:**
- Produces unchanged exports: `DataCard`, `PageCard`, `ResponsiveList` with `Column`, `StackTable`, `Pagination`, `KpiCard` with `KpiColor`, `FilterPanel`. Mapping: `ResponsiveList`/`StackTable`→`Table` on wide screens, `List`/`Item` on phones (keep today's breakpoint constant); `Pagination`→`Pagination`; `DataCard`/`PageCard`→`Card` (only where content is not a dense list); `KpiCard`→`Card`+`Heading`+`Text` (no `Stat` component exists); `FilterPanel`→`Toolbar`/`Popover` per `table-filter` template.

- [ ] **Step 1: Tests first:** `responsive list shows a table at desktop width and items at phone width` (set `matchMedia`), `table header cells are column headers` (`getAllByRole('columnheader')`), `pagination keeps page buttons reachable by name`.
- [ ] **Step 2: Run** — new tests FAIL.
- [ ] **Step 3: Rebuild the components.**
- [ ] **Step 4: Run the gates.**
- [ ] **Step 5: Commit** `feat(frontend): tables, cards and pagination on Astryx`.

### Task 5: App shell, navigation, error pages + E2E checkpoint

**Files:**
- Modify: `frontend/src/shared/layout/{AppLayout,AdminLayout,ShellFrame,AppSidebar,AppHeader,AccountMenu,ThemeMenu,AuthCard}.tsx`, `sidebarMenu.ts`, `useSidebar.ts`
- Modify: `frontend/src/shared/navigation/{Breadcrumbs,PageHeader}.tsx`
- Modify: `frontend/src/shared/ui/{NotFoundPage,ForbiddenPage}.tsx`
- Test: `frontend/src/shared/layout/layouts.test.tsx`, navigation tests

**Interfaces:**
- Produces unchanged exports: `AppLayout`, `AdminLayout`, `ShellFrame`, `AppSidebar`/`SidebarNav`, `AppHeader`, `AccountMenu`, `ThemeMenu`, `AuthCard`, `Breadcrumbs`, `PageHeader`, `NotFoundPage`, `ForbiddenPage`. Start from `npx astryx template shell-side-nav`: `AppShell` + `SideNav` (+ `MobileNav` on phones) + `TopNav`/`LayoutHeader`; `Breadcrumbs`→`Breadcrumbs`; `PageHeader`→`LayoutHeader`; `AccountMenu`/`ThemeMenu`→`DropdownMenu`; `AuthCard` from `login-card`. `sidebarMenu.ts` data (items, permissions) does not change.

- [ ] **Step 1: Tests first:** keep existing layout assertions; add `theme menu switches Astryx to dark` (open menu, choose Koyu, assert dark), `side nav marks the current route` (`aria-current="page"`), `mobile nav opens as a drawer and closes on Escape`.
- [ ] **Step 2: Run** — new tests FAIL.
- [ ] **Step 3: Rebuild the shell.** `index.html` pre-paint script keeps working for CoreUI until Task 13; check that the Astryx theme also does not flash (if it does, set the Astryx mode attribute in the same script — read `npx astryx docs use-a-theme`).
- [ ] **Step 4: Run the gates.**
- [ ] **Step 5: E2E checkpoint:** `tools/dev-service.sh stop && tools/e2e.sh; echo e2e=$?; tools/dev-service.sh start` — expect `e2e=0` for all viewports.
- [ ] **Step 6: Commit** `feat(frontend): app shell and navigation on Astryx`.

### Task 6: Auth, profile, home, workspace, approvals

**Files:** `frontend/src/features/{auth,profile,home,workspace,approvals}/**/*.tsx` and their tests.

**Interfaces:** Consumes Tasks 1–5. Templates: `login-card` (login, forgot/reset password), `settings` (profile), `dashboard-composition` (home), `table-inbox` (workspace, approvals).

- [ ] **Step 1: Per screen, scaffold with `npx astryx template <id>` into a scratch path, move today's logic into its frame,** delete Bootstrap `className`s and raw `div`/`span`.
- [ ] **Step 2: Run the folder tests** `npx vitest run src/features/auth src/features/profile src/features/home src/features/workspace src/features/approvals` — PASS (update only class-based assertions).
- [ ] **Step 3: Run the gates;** `grep -rn "@coreui\|className=" src/features/{auth,profile,home,workspace,approvals}` returns nothing (except `className` passed to Astryx for a documented reason).
- [ ] **Step 4: Commit** `feat(frontend): auth, home and workspace screens on Astryx`.

### Task 7: Tickets + E2E checkpoint

**Files:** `frontend/src/features/tickets/**/*.tsx` and tests.

**Interfaces:** Templates: `table-filter` (lists), `work-item-detail` or `incident-console` (ticket detail with comments, links, SLA, attachments), `form-two-column` (create). `MetadataList` for ticket fields, `Timestamp` for dates (Turkish locale).

- [ ] **Step 1: Rebuild the screens** as in Task 6 Step 1.
- [ ] **Step 2: Run** `npx vitest run src/features/tickets` — PASS.
- [ ] **Step 3: Run the gates** and the `grep` check for this folder.
- [ ] **Step 4: E2E checkpoint** as in Task 5 Step 5 (`tickets.spec.ts`, `sla.spec.ts`, `email.spec.ts` matter most).
- [ ] **Step 5: Commit** `feat(frontend): ticket screens on Astryx`.

### Task 8: Assets

**Files:** `frontend/src/features/assets/**/*.tsx` and tests.

**Interfaces:** Templates: `table-filter` (inventory list), `detail-page` (asset detail, lifecycle), `form-wizard-inline` or `form-two-column` (CSV import steps), `FileInput` for the upload.

- [ ] Steps as Task 7 Steps 1–3 for `src/features/assets`; then **Commit** `feat(frontend): asset screens on Astryx`.

### Task 9: Knowledge base

**Files:** `frontend/src/features/kb/**/*.tsx` and tests.

**Interfaces:** Template `documentation`. Keep `react-markdown` (no raw HTML, ADR-0008/KB rule) and style its output with Astryx `Text`/`Heading`/`Code`/`Link` through its `components` map; use Astryx `Markdown` only if `npx astryx component Markdown` confirms it never renders raw HTML (builder's call, note it in the commit).

- [ ] Steps as Task 7 Steps 1–3 for `src/features/kb`, plus test `kb article does not render raw HTML` (content `<img src=x onerror=alert(1)>` shows as text); then **Commit** `feat(frontend): knowledge base on Astryx`.

### Task 10: Reports and charts

**Files:**
- Modify: `frontend/src/features/reports/**/*.tsx` (incl. `DistributionChart.tsx`), `frontend/src/shared/ui/chartTheme.ts`, `vite.config.ts` (`CHART_PACKAGES`)
- Modify: `frontend/package.json` (replace `@coreui/react-chartjs` with `react-chartjs-2`)
- Test: `frontend/src/features/reports/*.test.tsx`, `src/shared/ui/chartTheme.test.ts`, `src/test/setup.ts`

**Interfaces:** Template `dashboard`. `chartTheme.ts` reads colours from Astryx tokens via `getComputedStyle(document.documentElement).getPropertyValue('--color-…')` and re-reads on colour mode change (keep its current exported function names). `@astryxdesign/charts` is canary-only, so Chart.js stays (ADR-0022).

- [ ] **Step 1: Tests first:** `chart colours come from Astryx tokens` (set a token on the root in the test, assert the dataset colour), `chart recolours after switching to dark`.
- [ ] **Step 2: Run** — FAIL.
- [ ] **Step 3: Swap the wrapper package, rebuild screens and `chartTheme.ts`.**
- [ ] **Step 4: Run the gates;** check `vendor-charts` still holds Chart.js in the build output.
- [ ] **Step 5: Commit** `feat(frontend): reports and charts on Astryx`.

### Task 11: Admin, part 1 — users, teams, locations, settings

**Files:** the user, team, location and system-settings screens under `frontend/src/features/admin/` plus `admin/shared/`, and their tests (list the exact files with `ls src/features/admin` at the start; split by area so the task stays ≤ ~12 screens).

**Interfaces:** Templates: `settings-sidebar` (admin frame), `table-filter` (lists), `form-two-column` (editors), `settings-dialog` where today a dialog edits.

- [ ] Steps as Task 7 Steps 1–3 for those files; then **Commit** `feat(frontend): admin users, teams and settings on Astryx`.

### Task 12: Admin, part 2 — catalog, SLA, email templates, audit + E2E checkpoint

**Files:** the remaining `frontend/src/features/admin/` screens and tests.

- [ ] Steps as Task 7 Steps 1–4 (E2E checkpoint: `admin-onboarding.spec.ts`, `catalog.spec.ts`); then **Commit** `feat(frontend): admin catalog, SLA and audit on Astryx`.

### Task 13: Remove CoreUI

**Files:**
- Modify: `frontend/package.json`, `package-lock.json` (remove `@coreui/*`, `sass`; remove `@popperjs` if nothing else uses it)
- Delete: `frontend/src/styles/_layout.scss`, `_variables.scss`, `app.scss`; modify `frontend/src/main.tsx`
- Modify: `frontend/src/styles/styles.test.ts` (assert no `@coreui` import anywhere in `src`, no `.scss` file), `frontend/vite.config.ts` (drop `vendor-coreui*` groups), `frontend/src/shared/ui/colorMode.ts` + `frontend/index.html` (drop `data-coreui-theme`; keep the pre-paint step for Astryx if Task 5 needed it), `frontend/src/test/setup.ts`
- Modify: `tools/lint.sh` (doctor becomes a hard gate)
- Modify: `docs/adr/0021-*.md`, `docs/adr/0026-*.md` status → `superseded by ADR-0027`; `docs/adr/README.md` rows; `CLAUDE.md` stack line (CoreUI gone); ADR-0022 amendment note (`react-chartjs-2` instead of `@coreui/react-chartjs`).

- [ ] **Step 1: Test first:** extend `styles.test.ts` with `no source file imports @coreui` and `no scss files remain` — FAIL.
- [ ] **Step 2: Remove packages, styles and config; fix whatever the compiler reports.**
- [ ] **Step 3: Run the gates** (doctor now hard), then `tools/build.sh; echo build=$?` — `build=0`, no chunk size warning.
- [ ] **Step 4: Final E2E** as in Task 5 Step 5.
- [ ] **Step 5: Commit** `refactor(frontend): remove CoreUI and Sass`.
- [ ] **Step 6: Report** to the owner the docs that need lines (`README.md`, `capabilities.md`, user guide screenshots via `tools/guide-screenshots.sh` + `tools/build-user-guide.sh`) — do not edit them unasked.
