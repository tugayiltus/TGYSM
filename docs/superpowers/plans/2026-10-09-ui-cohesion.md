# UI Cohesion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every TGYSM screen the CoreUI Free React Admin template's visual language (icon sidebar, sticky header with theme and account menus, breadcrumb strip, all content in cards, one field/table/form structure) with light/dark/auto color modes, mobile first, with no layout shift or freeze.

**Architecture:** Presentation only. New shared building blocks in `frontend/src/shared/ui` (`PageCard`, `DataCard`, `FormSection`, `CheckField`, color-mode store and `ThemeMenu`), changed shell in `frontend/src/shared/layout`, then every page moves onto the blocks. Guard tests in `frontend/src/styles/styles.test.ts` keep the rules from eroding. No route, API, schema, query or i18n-key changes except new keys.

**Tech Stack:** React 19, Vite (Rolldown), TypeScript, CoreUI 5 (`@coreui/react` ~5.13, `@coreui/coreui` ~5.9), new `@coreui/icons` ~3.0 + `@coreui/icons-react` ~2.3, Chart.js via `@coreui/react-chartjs`, Vitest + Testing Library, Playwright.

**Spec:** `docs/superpowers/specs/2026-10-09-ui-cohesion-design.md`

## Global Constraints

- Branch `ui-cohesion` (from `master`); never touch `phase-6-release`.
- Primary color: CoreUI default `#5856d6` — remove the custom `$primary` and `$link-color` overrides.
- Color modes `light`, `dark`, `auto`; storage key `tgysm-theme`; attribute `data-coreui-theme` on `<html>`.
- Sidebar dark (`colorScheme="dark"`) in both modes.
- Mobile first: base styles for 360 px; wider layouts only via `min-width` breakpoints. Only allowed `max-width` media query: the existing `tgysm-stack-table` rule (`@media (max-width: 991.98px)`).
- Layout stability: CLS < 0.1 and no long task > 200 ms on inventory, ticket detail, home and reports at 360 px and 1280 px.
- No hard-coded colors in `src/features` or `src/shared` TSX (`bg-white`, `bg-light`, `text-dark`, `text-black`, hex literals), except the chart palette constants file `src/shared/ui/chartTheme.ts`.
- `features/` uses form controls, tables and cards only through `shared/ui` (no raw `<input`, `<select`, `<textarea`, `<table`, no `card` class) — exceptions: `features/tickets/AttachmentInput.tsx` (native file input, visually hidden behind a button).
- Touch targets ≥ 44 px (`tgysm-touch`). Icons beside text `aria-hidden`; icon-only controls have an accessible name.
- Turkish text only in `frontend/src/shared/i18n/locales/tr/*.json`; English identifiers. No magic numbers/strings: named constants.
- ESLint `--max-warnings=0`, no `eslint-disable`; TypeScript strict; frontend line coverage ≥ 80%.
- Gates per task: `tools/lint.sh` and `tools/test.sh` green before commit. E2E (`tools/e2e.sh`) after Tasks 5, 9 and 11: stop dev services first (`tools/dev-service.sh stop`), restart after (`tools/dev-service.sh start`).
- Stage named paths only. Commit messages without any AI attribution. No push without owner approval.

## Review Focus

1. **Blocked storage** (private mode, Safari ITP): reading/writing `tgysm-theme` must not throw; the page renders in the system mode. CoreUI's `useColorModes` calls `localStorage` without a guard, so this plan uses its own store (`colorMode.ts`) — test in Task 1.
2. **`auto` mode while the OS switches light↔dark** (evening schedule): the page and the charts must follow without reload — test in Task 1 (store) and Task 10 (chart re-render key).
3. **Long names in the 360 px header** (long full name, long breadcrumb, area switch + theme + account menus together): no horizontal scroll — E2E in Task 5 checks the header at 360 px with the E2E admin (longest set of header controls).
4. **Loading → data on slow networks**: skeleton rows and card minimum height must reserve space so the list does not jump — CLS measured in Task 11; skeleton row height test in Task 3.
5. **Opening a dialog or the filter sheet on desktop**: the scrollbar disappears and the page must not shift sideways — `scrollbar-gutter: stable` in Task 3, asserted in the Task 11 E2E (content left edge unchanged after opening a dialog).

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `frontend/src/shared/ui/colorMode.ts` | Color-mode store: read/write `tgysm-theme` safely, resolve `auto`, apply `data-coreui-theme`, subscribe to OS changes; `useColorMode()` hook (`useSyncExternalStore`) | 1 |
| `frontend/src/shared/ui/theme.ts` | Removed (replaced by `colorMode.ts`) | 1 |
| `frontend/index.html` | Inline pre-paint script applying the stored mode; `theme-color` meta | 1 |
| `frontend/src/shared/layout/ThemeMenu.tsx` | Header dropdown: Açık / Koyu / Otomatik | 1 |
| `frontend/src/shared/ui/icons.ts` | The one place icons are imported from `@coreui/icons` and re-exported by role (menu, actions, fields, theme) | 1 |
| `frontend/src/styles/_variables.scss`, `app.scss`, `_layout.scss` | Variables (no `$primary`), layout rules, `scrollbar-gutter`, skeleton, card min-height | 1, 3 |
| `frontend/src/shared/navigation/routeMeta.ts` | `icon` on every menu route | 2 |
| `frontend/src/shared/layout/AppSidebar.tsx` | `CSidebar` + brand + `CSidebarNav` with icons | 2 |
| `frontend/src/shared/layout/AppHeader.tsx`, `AccountMenu.tsx`, `ShellFrame.tsx`, `AppLayout.tsx`, `AdminLayout.tsx` | Sticky header, avatar, theme menu, breadcrumb strip | 2 |
| `frontend/src/shared/ui/PageCard.tsx`, `DataCard.tsx`, `FormSection.tsx` | Card building blocks | 3 |
| `frontend/src/shared/ui/ResponsiveList.tsx`, `States.tsx`, `Pagination.tsx` | Flush table, skeleton loading, footer pagination | 3 |
| `frontend/src/shared/ui/TextField.tsx`, `SelectField.tsx`, `TextAreaField.tsx`, `ComboboxField.tsx`, `FieldLabel.tsx`, `CheckField.tsx` | Unified field look, required mark, start icon, check/switch/radio | 4 |
| `frontend/src/shared/ui/Button.tsx`, `StatusBadge.tsx`, `KpiCard.tsx`, `navigation/PageHeader.tsx` | Icon support, subtle badges, stat widget, header icon/description | 4 |
| `frontend/src/features/**` pages | Adopt the blocks | 5–9 |
| `frontend/src/shared/ui/chartTheme.ts`, `features/reports/DistributionChart.tsx` | Mode-aware chart colors | 8 |
| `frontend/src/styles/styles.test.ts` | Guard tests | 10 |
| `frontend/e2e/appearance.spec.ts` | Dark mode persistence, layout stability, no horizontal scroll | 11 |
| `docs/adr/0026-coreui-visual-system-and-color-modes.md`, `docs/adr/README.md` | Decision record | 11 |

Commands used below (run from repo root unless stated):

- Single test file: `cd frontend && npx vitest run <path>`
- Lint gate: `tools/lint.sh` · Test gate: `tools/test.sh` · E2E: `tools/e2e.sh` (optionally `E2E_PROJECTS="desktop"` and a spec path)

---

### Task 1: Color modes, variables and icons

**Files:**
- Create: `frontend/src/shared/ui/colorMode.ts`, `frontend/src/shared/ui/colorMode.test.ts`, `frontend/src/shared/layout/ThemeMenu.tsx`, `frontend/src/shared/ui/icons.ts`
- Delete: `frontend/src/shared/ui/theme.ts`
- Modify: `frontend/index.html`, `frontend/src/main.tsx`, `frontend/src/styles/_variables.scss`, `frontend/src/styles/app.scss`, `frontend/src/styles/styles.test.ts`, `frontend/package.json`, `frontend/vite.config.ts`, `frontend/src/shared/i18n/locales/tr/common.json`, `frontend/src/shared/layout/layouts.test.tsx`

**Interfaces:**
- Produces: `COLOR_MODES = ['light','dark','auto'] as const`, `type ColorMode`, `COLOR_MODE_STORAGE_KEY = 'tgysm-theme'`, `readStoredMode(): ColorMode`, `resolveMode(mode: ColorMode): 'light' | 'dark'`, `applyColorMode(mode: ColorMode, root?: HTMLElement): void`, `setColorMode(mode: ColorMode): void`, `useColorMode(): { mode: ColorMode; resolved: 'light' | 'dark'; setMode: (mode: ColorMode) => void }`, `initColorMode(): void`; `<ThemeMenu />`; `icons.ts` exports `MENU_ICONS`-ready named icons (see Step 7).

- [ ] **Step 1: Install the icon packages**

Run: `cd frontend && npm install @coreui/icons@~3.0 @coreui/icons-react@~2.3`
Expected: `package.json` dependencies contain `"@coreui/icons": "~3.0.x"`-style ranges. If npm writes `^`, edit both to `~3.0` / `~2.3` like the other CoreUI entries.

- [ ] **Step 2: Write the failing color-mode tests**

`frontend/src/shared/ui/colorMode.test.ts`:

```ts
import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { applyColorMode, COLOR_MODE_STORAGE_KEY, readStoredMode, resolveMode, setColorMode, useColorMode } from './colorMode';

const DARK_QUERY = '(prefers-color-scheme: dark)';

function stubSystem(dark: boolean) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>();
  const mql = {
    matches: dark,
    media: DARK_QUERY,
    addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => listeners.add(listener),
    removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => listeners.delete(listener),
  };
  vi.stubGlobal('matchMedia', vi.fn(() => mql));
  return {
    flip(next: boolean) {
      mql.matches = next;
      listeners.forEach((listener) => listener({ matches: next } as MediaQueryListEvent));
    },
  };
}

describe('color mode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.removeAttribute('data-coreui-theme');
  });
  afterEach(() => vi.restoreAllMocks());

  it('defaults to auto and follows the system', () => {
    stubSystem(true);
    expect(readStoredMode()).toBe('auto');
    expect(resolveMode('auto')).toBe('dark');
  });

  it('writes the choice and the resolved attribute', () => {
    stubSystem(false);
    setColorMode('dark');
    expect(window.localStorage.getItem(COLOR_MODE_STORAGE_KEY)).toBe('dark');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  });

  it('ignores an unknown stored value', () => {
    stubSystem(false);
    window.localStorage.setItem(COLOR_MODE_STORAGE_KEY, 'purple');
    expect(readStoredMode()).toBe('auto');
  });

  it('survives blocked storage', () => {
    stubSystem(false);
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(readStoredMode()).toBe('auto');
    expect(() => setColorMode('dark')).not.toThrow();
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  });

  it('re-resolves auto when the system switches', () => {
    const system = stubSystem(false);
    const { result } = renderHook(() => useColorMode());
    act(() => result.current.setMode('auto'));
    expect(result.current.resolved).toBe('light');
    act(() => system.flip(true));
    expect(result.current.resolved).toBe('dark');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  });

  it('applies a fixed mode without consulting the system', () => {
    stubSystem(true);
    applyColorMode('light');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('light');
  });
});
```

- [ ] **Step 3: Run it to verify it fails**

Run: `cd frontend && npx vitest run src/shared/ui/colorMode.test.ts`
Expected: FAIL — `Failed to resolve import "./colorMode"`.

- [ ] **Step 4: Implement `colorMode.ts`**

```ts
import { useSyncExternalStore } from 'react';

/** Light, dark or the system's choice (ADR-0026). CoreUI reads the resolved mode from `data-coreui-theme`. */
export const COLOR_MODES = ['light', 'dark', 'auto'] as const;
export type ColorMode = (typeof COLOR_MODES)[number];
export type ResolvedMode = Exclude<ColorMode, 'auto'>;

/** Mirrored in the pre-paint script in `index.html`; a test keeps the two equal. */
export const COLOR_MODE_STORAGE_KEY = 'tgysm-theme';
const THEME_ATTRIBUTE = 'data-coreui-theme';
const DARK_QUERY = '(prefers-color-scheme: dark)';
const DEFAULT_MODE: ColorMode = 'auto';

const isColorMode = (value: unknown): value is ColorMode => typeof value === 'string' && (COLOR_MODES as readonly string[]).includes(value);

export function readStoredMode(): ColorMode {
  try {
    const stored = window.localStorage.getItem(COLOR_MODE_STORAGE_KEY);
    return isColorMode(stored) ? stored : DEFAULT_MODE;
  } catch {
    // Private mode or blocked storage: follow the system.
    return DEFAULT_MODE;
  }
}

const systemPrefersDark = () => window.matchMedia(DARK_QUERY).matches;

export function resolveMode(mode: ColorMode): ResolvedMode {
  if (mode !== 'auto') {
    return mode;
  }
  return systemPrefersDark() ? 'dark' : 'light';
}

export function applyColorMode(mode: ColorMode, root: HTMLElement = document.documentElement) {
  root.setAttribute(THEME_ATTRIBUTE, resolveMode(mode));
}

let current: { mode: ColorMode; resolved: ResolvedMode } | null = null;
const listeners = new Set<() => void>();
let systemSubscribed = false;

function snapshot() {
  if (current === null) {
    const mode = readStoredMode();
    current = { mode, resolved: resolveMode(mode) };
  }
  return current;
}

function publish(mode: ColorMode) {
  current = { mode, resolved: resolveMode(mode) };
  applyColorMode(mode);
  listeners.forEach((listener) => listener());
}

function subscribeSystem() {
  if (systemSubscribed) {
    return;
  }
  systemSubscribed = true;
  window.matchMedia(DARK_QUERY).addEventListener('change', () => {
    if (snapshot().mode === 'auto') {
      publish('auto');
    }
  });
}

export function setColorMode(mode: ColorMode) {
  try {
    window.localStorage.setItem(COLOR_MODE_STORAGE_KEY, mode);
  } catch {
    // Not remembered; the mode still applies for this visit.
  }
  publish(mode);
}

/** Called once at start-up: the pre-paint script already set the attribute; this keeps it in sync afterwards. */
export function initColorMode() {
  current = null;
  applyColorMode(snapshot().mode);
  subscribeSystem();
}

function subscribe(listener: () => void) {
  subscribeSystem();
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useColorMode() {
  const state = useSyncExternalStore(subscribe, snapshot, snapshot);
  return { mode: state.mode, resolved: state.resolved, setMode: setColorMode };
}
```

Note: tests stub `matchMedia` per test while the module keeps `systemSubscribed`. Add a test-only reset: export `function resetColorModeForTests() { current = null; systemSubscribed = false; listeners.clear(); }` and call it in the test file's `beforeEach` (import it alongside the others). Keep the name exactly `resetColorModeForTests`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd frontend && npx vitest run src/shared/ui/colorMode.test.ts`
Expected: PASS (6 tests).

- [ ] **Step 6: Pre-paint script, start-up and variables**

`frontend/index.html` — replace the `theme-color` meta and add the script before the module script:

```html
    <meta name="theme-color" content="#5856d6" />
    <title>TGYSM</title>
    <script>
      // Applies the stored color mode before the first paint (no light flash in dark mode). Key mirrors COLOR_MODE_STORAGE_KEY.
      (function () {
        var mode = 'auto';
        try {
          var stored = localStorage.getItem('tgysm-theme');
          if (stored === 'light' || stored === 'dark' || stored === 'auto') mode = stored;
        } catch (e) {}
        if (mode === 'auto') mode = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        document.documentElement.setAttribute('data-coreui-theme', mode);
      })();
    </script>
```

`frontend/src/main.tsx`: replace `import { applyTheme } from './shared/ui/theme';` + `applyTheme();` with `import { initColorMode } from './shared/ui/colorMode';` + `initColorMode();`. Delete `frontend/src/shared/ui/theme.ts`.

`frontend/src/styles/_variables.scss`:

```scss
// TGYSM overrides of CoreUI/Bootstrap variables; loaded before CoreUI so its components pick them up.
// Colors are CoreUI's own (primary #5856d6) so both color modes stay consistent (ADR-0026).
$font-family-sans-serif: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
$border-radius: 0.5rem;
$input-font-size: 1rem; // 16 px keeps iOS from zooming into inputs
```

`frontend/src/styles/app.scss`:

```scss
// Single stylesheet entry (ADR-0021, ADR-0026): variables, CoreUI, then scoped TGYSM rules.
@use "variables" as *;
@use "@coreui/coreui/scss/coreui" with (
  $font-family-sans-serif: $font-family-sans-serif,
  $border-radius: $border-radius,
  $input-font-size: $input-font-size
);
@use "layout";
```

- [ ] **Step 7: Icons module**

`frontend/src/shared/ui/icons.ts` (single import point; tree-shaken named imports):

```ts
// Every icon the UI uses, imported by name so only these ship (ADR-0026).
export {
  cilBell,
  cilBook,
  cilBuilding,
  cilCalendar,
  cilChartPie,
  cilCheckCircle,
  cilCloudDownload,
  cilContrast,
  cilDevices,
  cilEnvelopeClosed,
  cilEnvelopeOpen,
  cilHistory,
  cilHome,
  cilInbox,
  cilLaptop,
  cilList,
  cilLocationPin,
  cilLockLocked,
  cilMenu,
  cilMoon,
  cilNotes,
  cilPeople,
  cilSearch,
  cilSettings,
  cilSpeedometer,
  cilSun,
  cilTag,
  cilTags,
  cilTask,
  cilUser,
  cilAvTimer,
  cilGroup,
  cilScreenDesktop,
} from '@coreui/icons';
```

If `npx tsc -b` reports a name as missing from `@coreui/icons` 3.0, replace it with the closest free icon listed in `node_modules/@coreui/icons/js/free/index.d.ts` and use the new name everywhere the plan uses the old one.

`frontend/vite.config.ts`: the existing `vendor-coreui` group already matches `@coreui/(react|icons|icons-react|coreui)`. Split icons into their own group so the icon set does not grow the component chunk — add before `vendor-coreui` (priority 95):

```ts
            { name: 'vendor-coreui-icons', test: /node_modules[\\/]@coreui[\\/](icons|icons-react)[\\/]/, priority: 95 },
```

and change the `vendor-coreui` test to `/node_modules[\\/](@coreui[\\/](react|coreui|utils)|@popperjs)[\\/]/`.

- [ ] **Step 8: ThemeMenu with test**

i18n `common.json` — add under `layout`:

```json
"theme": {
  "label": "Görünüm",
  "light": "Açık",
  "dark": "Koyu",
  "auto": "Otomatik"
},
"openMenu": "Menüyü aç",
"brand": "TGYSM"
```

(Keep the existing `layout` keys; add these alongside. `openMenu` stays in `actions` if it already exists there — check `actions.openMenu` and do not duplicate it; drop `openMenu` from this block if present.)

`frontend/src/shared/layout/ThemeMenu.tsx`:

```tsx
import { CDropdown, CDropdownItem, CDropdownMenu, CDropdownToggle } from '@coreui/react';
import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';

import { COLOR_MODES, useColorMode, type ColorMode } from '../ui/colorMode';
import { cilContrast, cilMoon, cilSun } from '../ui/icons';

const MODE_ICON: Record<ColorMode, string[]> = { light: cilSun, dark: cilMoon, auto: cilContrast };

/** Header menu for the color mode (spec §2.2): the toggle shows the active mode's icon. */
export function ThemeMenu() {
  const { t } = useTranslation();
  const { mode, setMode } = useColorMode();
  return (
    <CDropdown variant="nav-item" placement="bottom-end">
      <CDropdownToggle caret={false} className="btn btn-ghost-secondary tgysm-touch d-inline-flex align-items-center" aria-label={`${t('layout.theme.label')}: ${t(`layout.theme.${mode}`)}`}>
        <CIcon icon={MODE_ICON[mode]} size="lg" aria-hidden="true" />
      </CDropdownToggle>
      <CDropdownMenu>
        {COLOR_MODES.map((item) => (
          <CDropdownItem key={item} as="button" type="button" active={item === mode} aria-pressed={item === mode} className="d-flex align-items-center gap-2" onClick={() => setMode(item)}>
            <CIcon icon={MODE_ICON[item]} aria-hidden="true" />
            {t(`layout.theme.${item}`)}
          </CDropdownItem>
        ))}
      </CDropdownMenu>
    </CDropdown>
  );
}
```

Add to `frontend/src/shared/layout/layouts.test.tsx` (inside the existing top-level `describe`, using its render helpers; if none fit, render `<ThemeMenu />` directly inside `I18nextProvider`-free render since `src/test/setup.ts` loads i18n):

```tsx
it('switches the color mode from the header menu', async () => {
  const user = userEvent.setup();
  render(<ThemeMenu />);
  await user.click(screen.getByRole('button', { name: /Görünüm/ }));
  await user.click(screen.getByRole('button', { name: 'Koyu' }));
  expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  expect(window.localStorage.getItem('tgysm-theme')).toBe('dark');
  expect(screen.getByRole('button', { name: /Görünüm: Koyu/ })).toBeInTheDocument();
});
```

- [ ] **Step 9: Update `styles.test.ts`**

Replace the "marks the document with the light CoreUI theme" test with:

```ts
import indexHtml from '../../index.html?raw';
import { COLOR_MODE_STORAGE_KEY } from '../shared/ui/colorMode';

it('ships the CoreUI icon packages', () => {
  expect(allDependencies['@coreui/icons']).toMatch(/^[~^]?3\.0(\.|$)/);
  expect(allDependencies['@coreui/icons-react']).toMatch(/^[~^]?2\.3(\.|$)/);
});

it('applies the stored color mode before the first paint with the shared storage key', () => {
  expect(indexHtml).toContain(`localStorage.getItem('${COLOR_MODE_STORAGE_KEY}')`);
  expect(indexHtml).toContain('data-coreui-theme');
});
```

(Move the imports to the top of the file. If TypeScript rejects `?raw`, `vite/client` types are already referenced by `src/vite-env.d.ts`; check it contains `/// <reference types="vite/client" />`.)

- [ ] **Step 10: Run gates**

Run: `cd frontend && npx vitest run src/shared/ui/colorMode.test.ts src/styles/styles.test.ts src/shared/layout/layouts.test.tsx` → PASS. Then `tools/lint.sh` and `tools/test.sh` → both exit 0.

- [ ] **Step 11: Commit**

```bash
git add frontend/package.json frontend/package-lock.json frontend/index.html frontend/vite.config.ts frontend/src/main.tsx frontend/src/shared/ui/colorMode.ts frontend/src/shared/ui/colorMode.test.ts frontend/src/shared/ui/icons.ts frontend/src/shared/layout/ThemeMenu.tsx frontend/src/shared/layout/layouts.test.tsx frontend/src/styles/_variables.scss frontend/src/styles/app.scss frontend/src/styles/styles.test.ts frontend/src/shared/i18n/locales/tr/common.json
git rm frontend/src/shared/ui/theme.ts
git commit -m "feat: light, dark and auto color modes with CoreUI icons"
```

---

### Task 2: Shell — icon sidebar, sticky header, breadcrumb strip

**Files:**
- Modify: `frontend/src/shared/navigation/routeMeta.ts`, `frontend/src/shared/layout/sidebarMenu.ts`, `frontend/src/shared/layout/AppSidebar.tsx`, `frontend/src/shared/layout/AppHeader.tsx`, `frontend/src/shared/layout/AccountMenu.tsx`, `frontend/src/shared/layout/ShellFrame.tsx`, `frontend/src/shared/layout/AppLayout.tsx`, `frontend/src/shared/layout/AdminLayout.tsx`, `frontend/src/shared/navigation/Breadcrumbs.tsx`, `frontend/src/styles/_layout.scss`
- Test: `frontend/src/shared/layout/layouts.test.tsx`, `frontend/src/shared/navigation/navigation.test.tsx`

**Interfaces:**
- Consumes: `ThemeMenu` (Task 1), icons from `shared/ui/icons.ts`.
- Produces: `RouteMeta.icon?: string[]`; `MenuItem` gains `icon: string[]`; `AppHeader` props unchanged except `title` now also feeds the brand; `ShellFrame` renders `<Breadcrumbs />` inside the header.

- [ ] **Step 1: Failing tests**

`navigation.test.tsx` — add:

```ts
it('gives every menu route an icon', () => {
  const missing = ROUTE_IDS.filter((id) => ROUTE_META[id].menu !== undefined && ROUTE_META[id].icon === undefined);
  expect(missing).toEqual([]);
});
```

`layouts.test.tsx` — add (reuse the file's existing helper that renders `AppLayout` for a role; it is used by the current sidebar tests):

```tsx
it('shows an icon beside every sidebar item and keeps the labels for screen readers when narrow', async () => {
  const user = userEvent.setup();
  renderApp({ role: 'admin' }); // existing helper in this file
  const nav = screen.getAllByRole('navigation', { name: 'Ana menü' })[0]!;
  const links = within(nav).getAllByRole('link');
  for (const link of links) {
    expect(link.querySelector('svg')).not.toBeNull();
  }
  await user.click(screen.getByRole('button', { name: 'Menüyü daralt' }));
  expect(within(nav).getByRole('link', { name: 'Envanter' })).toBeInTheDocument();
});

it('puts the breadcrumb in the header', () => {
  renderApp({ role: 'it_staff', path: '/assets' });
  const header = screen.getByRole('banner');
  expect(within(header).getByRole('navigation', { name: 'Konum' })).toBeInTheDocument();
});
```

Use the real names from the file: the nav label is `t('auth:shell.mainNav')` and the breadcrumb label is `t('breadcrumb.label')` — read both from `locales/tr/auth.json` / `common.json` and put those exact strings in the test. If the helper is named differently, use that name; do not create a second helper.

- [ ] **Step 2: Run to verify failure**

Run: `cd frontend && npx vitest run src/shared/navigation/navigation.test.tsx src/shared/layout/layouts.test.tsx`
Expected: FAIL — `icon` missing; no `svg`; breadcrumb not in `banner`.

- [ ] **Step 3: Icons in `routeMeta.ts`**

Add `icon?: string[];` to `RouteMeta` (doc comment: "Sidebar icon; required for menu routes (test-enforced)"). Import the icons from `../ui/icons` and set `icon` on each menu route:

| Route | Icon |
|---|---|
| `home` | `cilHome` |
| `myTickets` | `cilNotes` |
| `myAssets` | `cilLaptop` |
| `kbList` | `cilBook` |
| `workspace` | `cilTask` |
| `workspaceQueue` | `cilInbox` |
| `approvals` | `cilCheckCircle` |
| `assets` | `cilDevices` |
| `reports` | `cilChartPie` |
| `reportExports` | `cilCloudDownload` |
| `adminOverview` | `cilSpeedometer` |
| `adminUsers` | `cilPeople` |
| `adminDepartments` | `cilBuilding` |
| `adminLocations` | `cilLocationPin` |
| `adminTeams` | `cilGroup` |
| `adminCategories` | `cilTags` |
| `adminRequestTypes` | `cilList` |
| `adminBrands` | `cilTag` |
| `adminModels` | `cilScreenDesktop` |
| `adminEmailTemplates` | `cilEnvelopeOpen` |
| `adminEmailDeliveries` | `cilEnvelopeClosed` |
| `adminSlaCalendar` | `cilCalendar` |
| `adminSlaPolicy` | `cilAvTimer` |
| `adminSystem` | `cilSettings` |
| `adminAudit` | `cilHistory` |

`admin()` helper must keep `icon` (it spreads `meta`, so it does). `sidebarMenu.ts`: `MenuItem` gets `icon: string[]`; in `menuFor` map `({ id, to: ROUTE_META[id].path, icon: ROUTE_META[id].icon ?? [] })`.

- [ ] **Step 4: Sidebar**

`AppSidebar.tsx` — `SidebarNav` renders CoreUI nav markup with icons (keep `nav` + `aria-label`, `menuFor`, `activeMenuItem`, `aria-current`, `onNavigate`):

```tsx
<nav aria-label={label}>
  <ul className="sidebar-nav">
    {groups.map((group) => (
      <Fragment key={group.id}>
        <li className="nav-title">{translateOr(`menuGroups.${group.id}`, group.id)}</li>
        {group.items.map((item) => {
          const title = translateOr(ROUTE_META[item.id].titleKey, item.id);
          return (
            <li key={item.id} className="nav-item">
              <Link to={item.to} title={title} className={`nav-link ${item.id === active ? 'active' : ''}`} aria-current={item.id === active ? (item.id === current ? 'page' : 'true') : undefined} onClick={onNavigate}>
                <CIcon icon={item.icon} customClassName="nav-icon" aria-hidden="true" />
                <span className="nav-label">{title}</span>
              </Link>
            </li>
          );
        })}
      </Fragment>
    ))}
  </ul>
</nav>
```

`AppSidebar`:

```tsx
<aside className={`sidebar sidebar-dark border-end d-none d-lg-flex flex-column flex-shrink-0 ${narrow ? 'sidebar-narrow' : ''}`}>
  <div className="sidebar-header border-bottom">
    <Link to={home} className="sidebar-brand d-flex align-items-center gap-2 text-decoration-none text-white min-w-0">
      <CIcon icon={cilDevices} size="xl" aria-hidden="true" className="flex-shrink-0" />
      <span className="sidebar-brand-full fw-semibold text-truncate">{title}</span>
    </Link>
  </div>
  <div className="sidebar-body flex-grow-1 overflow-auto">
    <SidebarNav area={area} label={label} />
  </div>
  <div className="sidebar-footer border-top d-flex justify-content-end">
    <button type="button" className="sidebar-toggler" aria-pressed={narrow} aria-label={narrow ? t('layout.expandSidebar') : t('layout.collapseSidebar')} onClick={onToggleNarrow} />
  </div>
</aside>
```

`home` = `ROUTES.home` for `area === 'app'`, `ROUTES.admin` for `admin` (add an `area`-based constant in the file). In `_layout.scss` add: in narrow mode hide `.nav-label` and `.sidebar-brand-full` visually but keep them for screen readers:

```scss
// Narrow sidebar: icons only; labels stay in the accessibility tree (the link also has a title tooltip).
.sidebar-narrow {
  .nav-label,
  .sidebar-brand-full {
    position: absolute;
    width: 1px;
    height: 1px;
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
}
```

Phone panel (`Sheet` in `ShellFrame`): wrap `SidebarNav` in `<div className="sidebar sidebar-dark w-100 border-0 position-static">` so the icons and dark colors match the desktop sidebar.

- [ ] **Step 5: Header, avatar, breadcrumb strip**

`AppHeader.tsx`:

```tsx
<header className="header header-sticky p-0 mb-0 border-bottom">
  <div className="container-fluid d-flex align-items-center gap-1 gap-sm-2 px-2 px-sm-3 tgysm-header-row min-w-0">
    <button ref={menuButton} type="button" className="header-toggler d-lg-none tgysm-touch" aria-label={t('actions.openMenu')} onClick={() => onOpenMenu(menuButton.current)}>
      <CIcon icon={cilMenu} size="lg" aria-hidden="true" />
    </button>
    <Link to={ROUTES.home} className="fw-semibold text-decoration-none text-body me-auto d-lg-none text-truncate">{title}</Link>
    <span className="me-auto d-none d-lg-block" />
    {areaSwitch}
    <ThemeMenu />
    <AccountMenu />
  </div>
  <div className="container-fluid border-top px-2 px-sm-3 tgysm-header-crumbs min-w-0">
    <Breadcrumbs />
  </div>
</header>
```

`_layout.scss`:

```scss
// Fixed header rows: the page below never moves when the header content changes (spec criterion 8).
.tgysm-header-row {
  min-height: 3.5rem;
}

.tgysm-header-crumbs {
  min-height: 2.5rem;
  display: flex;
  align-items: center;
}
```

`Breadcrumbs.tsx`: `ol` class `breadcrumb my-0 small flex-nowrap min-w-0`. When `crumbs.length === 0` render an empty `<span />` so the strip keeps its height (the strip itself has `min-height`, so returning `null` is also fine — keep `null`).

`ShellFrame.tsx`: remove `<Breadcrumbs />` from `main`; `main` becomes `className="body flex-grow-1 px-3 py-3 py-lg-4 min-w-0"`.

`AccountMenu.tsx`: replace the button content with a `CAvatar`:

```tsx
<CAvatar color="primary" textColor="white" size="md" aria-hidden="true">{initials}</CAvatar>
<span className="d-none d-md-inline text-truncate tgysm-account-name">{`${me.firstName} ${me.lastName}`}</span>
<span className="visually-hidden">{t('layout.accountMenu')}</span>
```

with `const initials = \`${me.firstName.charAt(0)}${me.lastName.charAt(0)}\`;`. Keep the menu behavior (Escape, items) unchanged.

`AppLayout.tsx` / `AdminLayout.tsx`: the area-switch link gets an icon: `<CIcon icon={cilSettings} aria-hidden="true" />` + text inside a `d-inline-flex align-items-center gap-2` link (admin side uses `cilHome`). On phones (below 576 px) show the icon only: wrap the text in `<span className="d-none d-sm-inline">` and add `aria-label` with the full text to the link.

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd frontend && npx vitest run src/shared/navigation src/shared/layout`
Expected: PASS. Fix any existing test that queried the breadcrumb inside `main` to query the header (selector change only).

- [ ] **Step 7: Gates and commit**

`tools/lint.sh`, `tools/test.sh` → exit 0.

```bash
git add frontend/src/shared/navigation/routeMeta.ts frontend/src/shared/navigation/Breadcrumbs.tsx frontend/src/shared/navigation/navigation.test.tsx frontend/src/shared/layout/sidebarMenu.ts frontend/src/shared/layout/AppSidebar.tsx frontend/src/shared/layout/AppHeader.tsx frontend/src/shared/layout/AccountMenu.tsx frontend/src/shared/layout/ShellFrame.tsx frontend/src/shared/layout/AppLayout.tsx frontend/src/shared/layout/AdminLayout.tsx frontend/src/shared/layout/layouts.test.tsx frontend/src/styles/_layout.scss
git commit -m "feat: icon sidebar, sticky header with theme and account menus, breadcrumb strip"
```

---

### Task 3: Card building blocks, flush list, skeleton loading

**Files:**
- Create: `frontend/src/shared/ui/PageCard.tsx`, `frontend/src/shared/ui/DataCard.tsx`, `frontend/src/shared/ui/FormSection.tsx`, `frontend/src/shared/ui/cards.test.tsx`
- Modify: `frontend/src/shared/ui/ResponsiveList.tsx`, `frontend/src/shared/ui/States.tsx`, `frontend/src/shared/ui/Pagination.tsx`, `frontend/src/shared/ui/composites.test.tsx`, `frontend/src/styles/_layout.scss`

**Interfaces:**
- Produces:
  - `PageCard({ title?: string; titleId?: string; actions?: ReactNode; footer?: ReactNode; flush?: boolean; className?: string; as?: 'section' | 'article' | 'div'; children })` — `flush` removes body padding (for tables).
  - `DataCard({ filters?: ReactNode; footer?: ReactNode; children })` — filters in `CCardHeader`, children in a flush `CCardBody` with `tgysm-card-body-min`, footer in `CCardFooter`.
  - `FormSection({ title?: string; actions?: ReactNode; children; className?: string })` — body is `row g-3`; `actions` in a right-aligned footer.
  - `LoadingState({ rows?: number })` — `rows` renders skeleton rows (`TABLE_SKELETON_ROWS = 5` default when used by `DataCard` pages).
  - `ResponsiveList` unchanged props.

- [ ] **Step 1: Failing tests** — `cards.test.tsx`:

```tsx
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DataCard } from './DataCard';
import { FormSection } from './FormSection';
import { PageCard } from './PageCard';
import { LoadingState } from './States';

describe('card building blocks', () => {
  it('PageCard names its region by the title and shows actions and footer', () => {
    render(
      <PageCard title="Bilgiler" actions={<button type="button">Düzenle</button>} footer={<span>alt</span>}>
        içerik
      </PageCard>,
    );
    const region = screen.getByRole('region', { name: 'Bilgiler' });
    expect(within(region).getByRole('button', { name: 'Düzenle' })).toBeInTheDocument();
    expect(within(region).getByText('içerik')).toBeInTheDocument();
    expect(within(region).getByText('alt')).toBeInTheDocument();
    expect(region).toHaveClass('card');
  });

  it('DataCard puts filters in the header, the list in a flush body and paging in the footer', () => {
    const { container } = render(
      <DataCard filters={<span>filtre</span>} footer={<span>sayfa</span>}>
        <span>liste</span>
      </DataCard>,
    );
    expect(container.querySelector('.card-header')).toHaveTextContent('filtre');
    expect(container.querySelector('.card-body')).toHaveClass('p-0', 'tgysm-card-body-min');
    expect(container.querySelector('.card-footer')).toHaveTextContent('sayfa');
  });

  it('FormSection lays fields out in a grid and right-aligns its actions', () => {
    const { container } = render(
      <FormSection title="Hesap" actions={<button type="submit">Kaydet</button>}>
        <div className="col-12">alan</div>
      </FormSection>,
    );
    expect(container.querySelector('.card-body > .row.g-3')).not.toBeNull();
    expect(container.querySelector('.card-footer')).toHaveClass('justify-content-end');
  });

  it('LoadingState reserves the space of the rows it stands for', () => {
    render(<LoadingState rows={3} />);
    const status = screen.getByRole('status');
    expect(status.querySelectorAll('.tgysm-skeleton-row')).toHaveLength(3);
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd frontend && npx vitest run src/shared/ui/cards.test.tsx` → FAIL (modules missing).

- [ ] **Step 3: Implement**

`PageCard.tsx`:

```tsx
import { CCard, CCardBody, CCardFooter, CCardHeader } from '@coreui/react';
import { useId, type ReactNode } from 'react';

export interface PageCardProps {
  title?: string;
  actions?: ReactNode;
  footer?: ReactNode;
  /** Body without padding: tables and lists that run edge to edge. */
  flush?: boolean;
  className?: string;
  bodyClassName?: string;
  children: ReactNode;
}

/** The one container for page content (spec §3): nothing sits directly on the page background. */
export function PageCard({ title, actions, footer, flush = false, className = '', bodyClassName = '', children }: PageCardProps) {
  const titleId = useId();
  return (
    <CCard as="section" aria-labelledby={title === undefined ? undefined : titleId} className={`shadow-sm ${className}`.trim()}>
      {(title !== undefined || actions !== undefined) && (
        <CCardHeader className="d-flex flex-wrap align-items-center justify-content-between gap-2 bg-body">
          {title !== undefined && (
            <h2 id={titleId} className="h6 mb-0 fw-semibold">
              {title}
            </h2>
          )}
          {actions !== undefined && <div className="d-flex flex-wrap gap-2 ms-auto">{actions}</div>}
        </CCardHeader>
      )}
      <CCardBody className={`${flush ? 'p-0' : ''} ${bodyClassName}`.trim() || undefined}>{children}</CCardBody>
      {footer !== undefined && <CCardFooter className="bg-body">{footer}</CCardFooter>}
    </CCard>
  );
}
```

Note: a `section` with `aria-labelledby` is a `region` role; an untitled card stays a plain `section` (no landmark), which is what we want.

`DataCard.tsx`:

```tsx
import { CCard, CCardBody, CCardFooter, CCardHeader } from '@coreui/react';
import type { ReactNode } from 'react';

/** List page card (spec §3): filters on top, the list edge to edge, paging below. */
export function DataCard({ filters, footer, children }: { filters?: ReactNode; footer?: ReactNode; children: ReactNode }) {
  return (
    <CCard className="shadow-sm">
      {filters !== undefined && <CCardHeader className="bg-body py-3">{filters}</CCardHeader>}
      <CCardBody className="p-0 tgysm-card-body-min">{children}</CCardBody>
      {footer !== undefined && <CCardFooter className="bg-body">{footer}</CCardFooter>}
    </CCard>
  );
}
```

`FormSection.tsx`:

```tsx
import { CCard, CCardBody, CCardFooter, CCardHeader } from '@coreui/react';
import { useId, type ReactNode } from 'react';

/** A titled group of form fields in a card; fields are grid columns (`col-12 col-sm-6` …). */
export function FormSection({ title, actions, className = '', children }: { title?: string; actions?: ReactNode; className?: string; children: ReactNode }) {
  const titleId = useId();
  return (
    <CCard as="section" aria-labelledby={title === undefined ? undefined : titleId} className={`shadow-sm ${className}`.trim()}>
      {title !== undefined && (
        <CCardHeader className="bg-body">
          <h2 id={titleId} className="h6 mb-0 fw-semibold">
            {title}
          </h2>
        </CCardHeader>
      )}
      <CCardBody>
        <div className="row g-3">{children}</div>
      </CCardBody>
      {actions !== undefined && <CCardFooter className="bg-body d-flex flex-wrap gap-2 justify-content-end">{actions}</CCardFooter>}
    </CCard>
  );
}
```

`States.tsx` — `LoadingState`:

```tsx
export function LoadingState({ rows }: { rows?: number }) {
  const { t } = useTranslation();
  if (rows === undefined) {
    return (
      <p role="status" className="d-flex align-items-center justify-content-center gap-2 p-4 mb-0 text-body-secondary">
        <CSpinner size="sm" aria-hidden="true" />
        {t('states.loading')}
      </p>
    );
  }
  return (
    <div role="status" className="p-3 d-flex flex-column gap-2">
      <span className="visually-hidden">{t('states.loading')}</span>
      {Array.from({ length: rows }, (_, index) => (
        <span key={index} className="placeholder-glow tgysm-skeleton-row" aria-hidden="true">
          <span className="placeholder col-12 rounded" />
        </span>
      ))}
    </div>
  );
}
```

`EmptyState`: inside cards the dashed border looks doubled — change its classes to `d-flex flex-column align-items-center gap-3 p-4 text-center text-body-secondary` (no border). `ErrorState`: add `className="m-3 …"` so it does not touch the card edges.

`ResponsiveList.tsx`:
- phone list: `ul` → `className="list-group list-group-flush d-md-none"`, `li` → `className="list-group-item py-3"` (still `li`, so E2E `visibleEntry` keeps working; no `card` class).
- table wrapper: `className="d-none d-md-block table-responsive"`; table: `className="table table-hover align-middle mb-0"`; `thead` without `table-light`; `th` gets `className="text-body-secondary small text-uppercase fw-semibold"`; first and last cells get horizontal padding through `_layout.scss`.

`Pagination.tsx`: unchanged markup, it now lives in the `DataCard` footer.

`_layout.scss` additions:

```scss
// Page never shifts sideways when a dialog hides the scrollbar (spec criterion 8).
html {
  scrollbar-gutter: stable;
}

// List card bodies keep their height while loading, empty or failed, so the footer does not jump.
.tgysm-card-body-min {
  min-height: 12rem;
}

// Skeleton row: the height of one table row (align-middle, small badges).
.tgysm-skeleton-row .placeholder {
  height: 2.5rem;
}

.card .table > :not(caption) > * > :first-child {
  padding-left: 1rem;
}

.card .table > :not(caption) > * > :last-child {
  padding-right: 1rem;
}
```

- [ ] **Step 4: Run tests**

Run: `cd frontend && npx vitest run src/shared/ui` → PASS. Existing `composites.test.tsx` assertions on `table-light` or `card card-body` change to the new classes (selector-only edits).

- [ ] **Step 5: Gates and commit**

```bash
git add frontend/src/shared/ui/PageCard.tsx frontend/src/shared/ui/DataCard.tsx frontend/src/shared/ui/FormSection.tsx frontend/src/shared/ui/cards.test.tsx frontend/src/shared/ui/ResponsiveList.tsx frontend/src/shared/ui/States.tsx frontend/src/shared/ui/Pagination.tsx frontend/src/shared/ui/composites.test.tsx frontend/src/styles/_layout.scss
git commit -m "feat: page, data and form cards with skeleton loading and a flush list"
```

---

### Task 4: Fields, buttons, badges, KPI widget, page header

**Files:**
- Create: `frontend/src/shared/ui/FieldLabel.tsx`, `frontend/src/shared/ui/CheckField.tsx`, `frontend/src/shared/ui/fields.test.tsx`
- Modify: `frontend/src/shared/ui/TextField.tsx`, `SelectField.tsx`, `TextAreaField.tsx`, `ComboboxField.tsx`, `Button.tsx`, `StatusBadge.tsx`, `KpiCard.tsx`, `frontend/src/shared/navigation/PageHeader.tsx`, `frontend/src/shared/ui/primitives.test.tsx`, `frontend/src/shared/i18n/locales/tr/common.json`

**Interfaces:**
- Produces:
  - `FieldLabel({ htmlFor, children, required?: boolean })`.
  - `TextField`/`SelectField`/`TextAreaField`/`ComboboxField` gain `required?: boolean` (passes `required`/`aria-required` to the control and shows the mark) and `TextField` gains `startIcon?: string[]`.
  - `CheckField({ label, type?: 'checkbox' | 'radio' | 'switch', error?, hint?, ...inputProps })` with `ref` forwarding (react-hook-form `register` works).
  - `Button` gains `icon?: string[]`.
  - `PageHeader` gains `icon?: string[]`, `description?: string`.
  - `KpiCard` props unchanged, plus optional `icon?: string[]` and `color?: 'primary' | 'info' | 'warning' | 'danger' | 'success'` (default `primary`).

- [ ] **Step 1: Failing tests** — `fields.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { cilSearch } from './icons';
import { Button } from './Button';
import { CheckField } from './CheckField';
import { SelectField } from './SelectField';
import { TextField } from './TextField';

describe('fields', () => {
  it('marks a required field visually and for assistive tech', () => {
    render(<TextField label="Ad" required />);
    const input = screen.getByRole('textbox', { name: /Ad/ });
    expect(input).toBeRequired();
    expect(screen.getByText('*')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByText('(zorunlu)')).toHaveClass('visually-hidden');
  });

  it('puts a start icon in an input group without changing the label link', () => {
    const { container } = render(<TextField label="Ara" type="search" startIcon={cilSearch} />);
    expect(container.querySelector('.input-group .input-group-text svg')).not.toBeNull();
    expect(screen.getByRole('searchbox', { name: 'Ara' })).toBeInTheDocument();
  });

  it('uses the same label style for every field', () => {
    render(
      <>
        <TextField label="A" />
        <SelectField label="B" options={[]} />
      </>,
    );
    for (const label of [screen.getByText('A'), screen.getByText('B')]) {
      expect(label).toHaveClass('form-label', 'fw-medium', 'small');
    }
  });

  it('CheckField renders a CoreUI switch that toggles', async () => {
    const user = userEvent.setup();
    render(<CheckField type="switch" label="Onay gerekli" />);
    const box = screen.getByRole('switch', { name: 'Onay gerekli' });
    await user.click(box);
    expect(box).toBeChecked();
  });

  it('Button shows its icon before the text and hides it from screen readers', () => {
    render(<Button icon={cilSearch}>Ara</Button>);
    const svg = screen.getByRole('button', { name: 'Ara' }).querySelector('svg');
    expect(svg).toHaveAttribute('aria-hidden', 'true');
  });
});
```

i18n `common.json`: add `"field": { "required": "(zorunlu)" }`.

- [ ] **Step 2: Run to verify failure** — `cd frontend && npx vitest run src/shared/ui/fields.test.tsx` → FAIL.

- [ ] **Step 3: Implement**

`FieldLabel.tsx`:

```tsx
import { CFormLabel } from '@coreui/react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** One label style for every field (spec §3); a required field shows "*" and says "(zorunlu)" to screen readers. */
export function FieldLabel({ htmlFor, required = false, children }: { htmlFor: string; required?: boolean; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <CFormLabel htmlFor={htmlFor} className="form-label fw-medium small mb-1">
      {children}
      {required && (
        <>
          <span className="text-danger ms-1" aria-hidden="true">*</span>
          <span className="visually-hidden">{t('field.required')}</span>
        </>
      )}
    </CFormLabel>
  );
}
```

`TextField.tsx` (pattern repeated in `SelectField`, `TextAreaField`; `ComboboxField` uses `FieldLabel` for its label):

```tsx
export function TextField({ label, error, hint, id, className = '', ref, required, startIcon, ...inputProps }: TextFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  const control = (
    <CFormInput id={fieldId} ref={ref} required={required} invalid={Boolean(error)} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...inputProps} />
  );
  return (
    <div className={`d-flex flex-column ${className}`.trim()}>
      <FieldLabel htmlFor={fieldId} required={required}>{label}</FieldLabel>
      {startIcon === undefined ? (
        control
      ) : (
        <CInputGroup className={error ? 'has-validation' : undefined}>
          <CInputGroupText aria-hidden="true">
            <CIcon icon={startIcon} />
          </CInputGroupText>
          {control}
        </CInputGroup>
      )}
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
```

(`TextFieldProps` adds `startIcon?: string[]`.) Inside an input group the invalid feedback must be a sibling inside the group to show: when `startIcon` is set, render `<FieldMessages …/>` inside the `CInputGroup` after `control`, and outside otherwise.

`ComboboxField.tsx`: label via `FieldLabel`; the option list element gets `className="dropdown-menu show w-100 shadow tgysm-combobox-list overflow-auto"` and each option `className={\`dropdown-item ${active ? 'active' : ''}\`}` (keep roles `listbox`/`option`, `aria-selected`, keyboard behavior). Read the file first and change classes only.

`CheckField.tsx`:

```tsx
import { CFormCheck, CFormSwitch } from '@coreui/react';
import type { InputHTMLAttributes, Ref } from 'react';

import { useFieldIds } from './fieldIds';
import { FieldMessages } from './FieldMessages';

export interface CheckFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'size'> {
  label: string;
  type?: 'checkbox' | 'radio' | 'switch';
  error?: string;
  hint?: string;
  ref?: Ref<HTMLInputElement>;
}

/** Checkbox, radio or switch with a linked label and messages; the 44 px row keeps it easy to tap. */
export function CheckField({ label, type = 'checkbox', error, hint, id, className = '', ref, ...inputProps }: CheckFieldProps) {
  const { fieldId, hintId, errorId, describedBy } = useFieldIds(id, hint, error);
  const common = { id: fieldId, ref, label, invalid: Boolean(error), 'aria-describedby': describedBy, ...inputProps };
  return (
    <div className={`d-flex flex-column justify-content-center tgysm-touch ${className}`.trim()}>
      {type === 'switch' ? <CFormSwitch {...common} /> : <CFormCheck type={type} {...common} />}
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
```

`CFormSwitch` renders `role="switch"`; if the installed version does not, add `role="switch"` to `common` for the switch branch.

`Button.tsx`: add `icon?: string[]` to props; render `{!isLoading && icon !== undefined && <CIcon icon={icon} aria-hidden="true" />}` before `children`.

`StatusBadge.tsx`:

```tsx
<CBadge className={isActive ? 'bg-success-subtle text-success-emphasis' : 'bg-secondary-subtle text-secondary-emphasis'}>…</CBadge>
```

`KpiCard.tsx` (template stat widget look; keep `article` + `aria-labelledby`, link behavior, period, description):

```tsx
<article aria-labelledby={titleId} className="card shadow-sm h-100">
  <CCardBody className="d-flex align-items-start gap-3">
    {icon !== undefined && (
      <span className={`d-inline-flex align-items-center justify-content-center rounded-3 flex-shrink-0 tgysm-kpi-icon bg-${color}-subtle text-${color}-emphasis`} aria-hidden="true">
        <CIcon icon={icon} size="xl" />
      </span>
    )}
    <div className="d-flex flex-column gap-1 min-w-0">
      <h2 id={titleId} className="small text-body-secondary text-uppercase fw-semibold mb-0">
        {href === undefined ? title : <Link to={href} className="stretched-link text-reset text-decoration-none">{title}</Link>}
      </h2>
      <p className="mb-0">
        <span className="fs-4 fw-semibold">{value}</span>
        {unit !== undefined && <span className="ms-1 text-body-secondary">{unit}</span>}
      </p>
      <p className="small text-body-secondary mb-0">{period}</p>
      {description !== undefined && <p className="small mb-0">{description}</p>}
    </div>
  </CCardBody>
</article>
```

`_layout.scss`: `.tgysm-kpi-icon { width: 3rem; height: 3rem; }` (fixed size: no shift). `KpiCard` keeps `className="card"` — it is in `shared/ui`, allowed.

`PageHeader.tsx`: render `icon` (`CIcon` `size="xl"`, `aria-hidden`, `text-primary`) before the H1 and `description` as `<p className="text-body-secondary small mb-0 mt-1">` under it; spacing `mb-3 mb-lg-4`.

- [ ] **Step 4: Run tests** — `cd frontend && npx vitest run src/shared` → PASS (update `primitives.test.tsx` class assertions only where they asserted old classes).

- [ ] **Step 5: Gates and commit**

```bash
git add frontend/src/shared/ui/FieldLabel.tsx frontend/src/shared/ui/CheckField.tsx frontend/src/shared/ui/fields.test.tsx frontend/src/shared/ui/TextField.tsx frontend/src/shared/ui/SelectField.tsx frontend/src/shared/ui/TextAreaField.tsx frontend/src/shared/ui/ComboboxField.tsx frontend/src/shared/ui/Button.tsx frontend/src/shared/ui/StatusBadge.tsx frontend/src/shared/ui/KpiCard.tsx frontend/src/shared/navigation/PageHeader.tsx frontend/src/shared/ui/primitives.test.tsx frontend/src/shared/i18n/locales/tr/common.json frontend/src/styles/_layout.scss
git commit -m "feat: unified field labels, check fields, icon buttons, subtle badges and KPI widgets"
```

---

### Task 5: List pages on `DataCard`

**Files (modify):** `features/assets/AssetsPage.tsx`, `features/tickets/MyTicketsPage.tsx`, `features/workspace/WorkspacePage.tsx`, `features/workspace/QueueCard.tsx`, `features/approvals/ApprovalsPage.tsx`, `features/kb/KnowledgeListPage.tsx`, `features/reports/MyExportsPage.tsx`, `features/assets/MyAssetsPage.tsx`, `features/reports/DrilldownPage.tsx`, `features/admin/users/UsersPage.tsx`, `features/admin/departments/DepartmentsPage.tsx`, `features/admin/locations/LocationsPage.tsx`, `features/admin/teams/TeamsPage.tsx`, `features/admin/catalog/CategoriesPage.tsx`, `features/admin/catalog/RequestTypesPage.tsx`, `features/admin/deviceCatalog/BrandsPage.tsx`, `features/admin/deviceCatalog/ModelsPage.tsx`, `features/admin/email/EmailDeliveriesPage.tsx`, `features/admin/email/EmailTemplatesPage.tsx`, `features/admin/audit/AuditPage.tsx` (all under `frontend/src/`), and their existing `*.test.tsx` only where a selector depended on removed markup.

**Interfaces:** Consumes `DataCard`, `LoadingState({ rows })`, `PageHeader({ icon })`, `TextField({ startIcon })` from Tasks 3–4; route icons from `ROUTE_META[id].icon`.

**The rule for every list page:**

1. `PageHeader` gets `icon={ROUTE_META.<routeId>.icon}` (the page's own route id).
2. Filters (the `row g-3` of fields, or `FilterPanel`) move into `DataCard`'s `filters` prop. Search fields get `startIcon={cilSearch}`.
3. Loading uses `<LoadingState rows={TABLE_SKELETON_ROWS} />` with `const TABLE_SKELETON_ROWS = 5;` exported once from `shared/ui/States.tsx` (export it there; import it in pages).
4. `ErrorState`, `EmptyState`, `ResponsiveList` go inside `DataCard` children; `Pagination` goes into `DataCard`'s `footer` (only when data has items).
5. Lists rendered as hand-made cards (`li className="card card-body"` in `KnowledgeListPage`, `ApprovalsPage`; `div className="card card-body h-100"` in `MyAssetsPage`) become `ul className="list-group list-group-flush"` + `li className="list-group-item py-3"` inside `DataCard` — same content, same order.
6. No behavior change: URL params, debounce, handlers, dialogs untouched.

**Worked example — `AssetsPage.tsx` return block:**

```tsx
return (
  <>
    <PageHeader
      icon={ROUTE_META.assets.icon}
      titleOverride={t('list.title')}
      actions={
        <>
          <Link to={ROUTES.assetImport} className={LINK_BUTTON_CLASS}>
            {t('list.import')}
          </Link>
          <Button icon={cilPlus} onClick={() => setCreating(true)}>{t('list.new')}</Button>
        </>
      }
    />
    <DataCard
      filters={
        <div className="row g-3">
          <TextField className="col-12 col-md-4" type="search" startIcon={cilSearch} label={t('list.searchLabel')} value={search} onChange={(event) => { setSearchText(event.target.value); setFilter(PARAM.search, event.target.value); }} />
          <SelectField label={t('list.typeFilter')} placeholder={t('list.allTypes')} options={ASSET_TYPES.map((value) => ({ value, label: t(`types.${value}`) }))} className="col-12 col-md-4" value={type} onChange={(event) => setFilter(PARAM.type, event.target.value)} />
          <SelectField label={t('list.statusFilter')} placeholder={t('list.allStatuses')} options={ASSET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))} className="col-12 col-md-4" value={status} onChange={(event) => setFilter(PARAM.status, event.target.value)} />
        </div>
      }
      footer={assets.data && assets.data.items.length > 0 ? <Pagination page={assets.data.page} pageSize={assets.data.pageSize} totalCount={assets.data.totalCount} onPageChange={setPage} /> : undefined}
    >
      {assets.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
      {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
      {assets.data && assets.data.items.length === 0 && <EmptyState />}
      {assets.data && assets.data.items.length > 0 && <ResponsiveList items={assets.data.items} columns={columns} getKey={(a) => a.id} renderCard={renderCard} caption={t('list.caption')} />}
    </DataCard>
    {creating && <AssetFormDialog asset={null} onClose={() => setCreating(false)} />}
  </>
);
```

Add `cilPlus` to `shared/ui/icons.ts` exports. Primary "new" actions on every list page get `icon={cilPlus}`; import actions get `cilCloudUpload` (add to `icons.ts`).

- [ ] **Step 1: Failing test** — add to `frontend/src/features/assets/AssetsPage.test.tsx` (use the file's existing render helper and mocked API):

```tsx
it('shows the inventory inside one list card with the filters on top', async () => {
  const { container } = renderAssetsPage(); // existing helper in this file; use its real name
  await screen.findByText('DMR-000001'); // a tag from the file's fixture; use the real one
  const card = container.querySelector('.card');
  expect(card?.querySelector('.card-header')).toHaveTextContent('Durum');
  expect(card?.querySelector('.card-body table')).not.toBeNull();
  expect(card?.querySelector('.card-footer nav')).not.toBeNull();
});
```

- [ ] **Step 2:** Run `cd frontend && npx vitest run src/features/assets/AssetsPage.test.tsx` → FAIL.
- [ ] **Step 3:** Apply the worked example to `AssetsPage.tsx`; run the test → PASS.
- [ ] **Step 4:** Apply the rule to each remaining list page, one file at a time; after each file run its test file (`npx vitest run <page>.test.tsx` or the flow test that covers it: `inventoryFlow`, `employeeFlow`, `itFlow`, `kbFlow`, `adminFixes`, `DeviceCatalogPages`). `WorkspacePage` + `QueueCard`: each queue becomes a `PageCard` with the queue title in its header and the list flush in the body (`PageCard flush`).
- [ ] **Step 5:** `tools/lint.sh`, `tools/test.sh` → exit 0.
- [ ] **Step 6: E2E checkpoint** — `tools/dev-service.sh stop && tools/e2e.sh; status=$?; tools/dev-service.sh start; exit $status` → "E2E passed for: mobile-360 desktop". Fix selectors broken only by markup (e.g. `card` locators) without weakening assertions. Add to `navigation.spec.ts` (Review Focus 3) at 360 px with the admin session:

```ts
test('header controls fit a phone screen', async ({ browser }) => {
  const admin = await adminSession(browser);
  await admin.page.goto('/assets/import');
  await expect(admin.page.getByRole('banner').getByRole('navigation')).toBeVisible();
  await expectNoHorizontalScroll(admin.page);
});
```

- [ ] **Step 7: Commit** — `git add` each modified page and test file by path (list them), `frontend/src/shared/ui/icons.ts`, `frontend/src/shared/ui/States.tsx`, `frontend/e2e/navigation.spec.ts`; `git commit -m "feat: list pages in list cards with filters, skeleton rows and paging"`.

---

### Task 6: Detail pages on `PageCard`

**Files (modify):** `frontend/src/features/tickets/TicketDetailPage.tsx`, `features/tickets/TicketActions.tsx`, `features/tickets/Comments.tsx`, `features/tickets/SlaBadge.tsx`, `features/tickets/TicketBadges.tsx`, `features/assets/AssetDetailPage.tsx`, `features/kb/ArticlePage.tsx`, `features/kb/ArticleStatusBadge.tsx` and their tests where selectors depended on removed markup.

**Interfaces:** Consumes `PageCard`, `PageHeader({ icon })`.

**Rule:** main column = one `PageCard` per section (description, fields, comments, timeline, linked assets, history), each titled with the section heading that exists today (move the existing `h2` text into `PageCard title`; keep heading ids where tests or `aria-labelledby` reference them by passing the same text). The aside (`tgysm-sticky-aside`) = `PageCard` per block (actions, facts). Replace `section className="card card-body …"` (`TicketActions`, `AssetDetailPage` operations) and `dl className="card card-body …"` (`AssetDetailPage` fields → `PageCard` with the `dl className="d-grid gap-3 mb-0 tgysm-fields"` inside). Badges use subtle tokens:

| File | Old classes | New classes |
|---|---|---|
| `Comments.tsx` internal badge | `badge bg-warning text-dark` | `badge bg-warning-subtle text-warning-emphasis` |
| `SlaBadge.tsx` `running` | `bg-light text-body` | `bg-secondary-subtle text-secondary-emphasis` |
| `TicketBadges.tsx` `p3` | `bg-light text-body border` | `bg-info-subtle text-info-emphasis` |
| `TicketBadges.tsx` `p4` | `bg-light text-body-secondary border` | `bg-secondary-subtle text-secondary-emphasis` |
| `ArticleStatusBadge.tsx` internal | `bg-dark-subtle text-dark-emphasis` | `bg-secondary-subtle text-secondary-emphasis` |

Keep `p1`/`p2` and any other mapping already using `-subtle`/`-emphasis` (change any `text-bg-*` to `bg-*-subtle text-*-emphasis`).

- [ ] **Step 1: Failing test** — in `frontend/src/features/tickets/itFlow.test.tsx` (it renders the ticket detail), add:

```tsx
it('puts the ticket actions in a titled card', async () => {
  renderTicketDetail(); // the file's existing helper; use its real name and fixture
  const actions = await screen.findByRole('region', { name: 'İşlemler' }); // the existing actions heading text; read it from locales/tr/tickets.json
  expect(actions).toHaveClass('card');
});
```

- [ ] **Step 2:** Run it → FAIL. To make the test fail on today's `card card-body` section, the assertion is on the card header: replace `expect(actions).toHaveClass('card');` with `expect(actions.querySelector('.card-header h2')).not.toBeNull();` before running.
- [ ] **Step 3:** Convert `TicketDetailPage` + `TicketActions` + `Comments`; run → PASS.
- [ ] **Step 4:** Convert `AssetDetailPage`, `ArticlePage`, the badges; run `npx vitest run src/features/tickets src/features/assets src/features/kb` → PASS.
- [ ] **Step 5:** Gates; commit with named paths: `git commit -m "feat: detail pages in titled cards with subtle badges"`.

---

### Task 7: Form pages on `FormSection`, checks on `CheckField`

**Files (modify):** `frontend/src/features/tickets/NewTicketPage.tsx`, `features/kb/ArticleEditorPage.tsx`, `features/profile/ProfilePage.tsx`, `features/auth/ChangePasswordPage.tsx`, `features/admin/sla/SlaPolicyPage.tsx`, `features/admin/sla/SlaCalendarPage.tsx`, `features/admin/system/SystemPage.tsx`, `features/assets/AssetImportPage.tsx`, `features/admin/catalog/RequestTypesPage.tsx`, `features/admin/teams/TeamFormDialog.tsx`, `features/assets/AssetOperationDialog.tsx`, `features/admin/users/DeliveryChoice.tsx`, plus tests where selectors depended on removed markup.

**Interfaces:** Consumes `FormSection`, `CheckField`, `PageCard`, `ResponsiveList`.

**Rules:**
1. Full-page forms: the `form` element wraps one or more `FormSection`s; submit/cancel buttons move into the last section's `actions`. Fields keep their props; their wrappers become grid columns (`className="col-12"`, two-up fields `col-12 col-sm-6`). `ArticleEditorPage`: `form className="card card-body …"` → `form` + `FormSection` (keep `tgysm-editor` width on the `form`).
2. Every `<input type="checkbox">` → `<CheckField label={…} {...register(...)} />` (`RequestTypesPage` `requiresApproval`, `TeamFormDialog` `isDefaultIntake`, `AssetOperationDialog` `retainResponsibility`, `SlaPolicyPage` checkbox, `SlaCalendarPage` day-open — use `type="switch"` for on/off settings: `requiresApproval`, `isDefaultIntake`, day open). The surrounding hand-written `<label>` goes away (the label text becomes `label`).
3. `DeliveryChoice.tsx` radios → `CheckField type="radio"` with the same `name`, `value`, `checked`, `onChange`.
4. Raw `<input>` number fields in `SlaPolicyPage` → `TextField type="number"` with the same props, `className="tgysm-number-input"` kept; the visible label becomes visually hidden only where the column header already labels it today: pass `label` and wrap with `aria-label`-equivalent by setting `TextField`'s label to the column text and adding `className="… tgysm-label-hidden"`; add to `_layout.scss`:

```scss
// Table cell inputs labelled by their column header: the label stays for screen readers only.
.tgysm-label-hidden .form-label {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
```

5. Tables in `SlaPolicyPage` and `AssetImportPage`: move the table into a `PageCard flush` and render it through a new shared `DataTable` only if it fits `ResponsiveList`; otherwise keep the table markup but move it into `frontend/src/shared/ui/StackTable.tsx`:

```tsx
import type { ReactNode } from 'react';

/** A table that becomes one card per row below 992 px (`tgysm-stack-table`); cells carry `data-label`. */
export function StackTable({ caption, head, children }: { caption: string; head: ReactNode; children: ReactNode }) {
  return (
    <div className="table-responsive">
      <table className="table align-middle mb-0 tgysm-stack-table">
        <caption className="visually-hidden">{caption}</caption>
        <thead>{head}</thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  );
}
```

and use `StackTable` in both pages (`AssetImportPage`'s `TABLE_CLASS` constant goes away). Add a unit test in `cards.test.tsx`: `StackTable` renders a `table` with the caption and the `tgysm-stack-table` class.

- [ ] **Step 1: Failing test** — in `frontend/src/features/admin/adminFixes.test.tsx` (or the request types test that covers the form), add:

```tsx
it('asks for approval with a switch', async () => {
  const user = userEvent.setup();
  renderRequestTypeForm(); // existing helper; real name from the file
  const toggle = await screen.findByRole('switch', { name: 'Onay gerekli' }); // real label from locales/tr/admin.json
  await user.click(toggle);
  expect(toggle).toBeChecked();
});
```

- [ ] **Step 2:** Run → FAIL (checkbox, not switch).
- [ ] **Step 3:** Convert `RequestTypesPage`; run → PASS. Add the `StackTable` test, run → FAIL, create `StackTable.tsx`, run → PASS.
- [ ] **Step 4:** Convert the remaining files one by one, running each file's tests after the change.
- [ ] **Step 5:** Gates; commit (named paths incl. `frontend/src/shared/ui/StackTable.tsx`, `frontend/src/shared/ui/cards.test.tsx`, `frontend/src/styles/_layout.scss`): `git commit -m "feat: form pages in form sections, switches and radios through CheckField"`.

---

### Task 8: Dashboards and mode-aware charts

**Files:**
- Create: `frontend/src/shared/ui/chartTheme.ts`, `frontend/src/shared/ui/chartTheme.test.ts`
- Modify: `frontend/src/features/reports/DistributionChart.tsx`, `features/reports/ReportsPage.tsx`, `features/home/HomePage.tsx`, `features/admin/overview/AdminOverviewPage.tsx`, and tests where selectors depended on removed markup.

**Interfaces:**
- Produces: `readChartColors(): { bar: string; text: string; grid: string }` (reads `--cui-primary`, `--cui-body-color`, `--cui-border-color-translucent` from `document.documentElement` via `getComputedStyle`, falling back to `CHART_FALLBACK` constants); `useChartColors(): { colors; key: string }` where `key` is the resolved mode (from `useColorMode().resolved`) so charts re-mount on change.

- [ ] **Step 1: Failing test** — `chartTheme.test.ts`:

```ts
import { renderHook, act } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { setColorMode } from './colorMode';
import { CHART_FALLBACK, readChartColors, useChartColors } from './chartTheme';

describe('chart theme', () => {
  it('falls back to fixed colors when CSS variables are missing (jsdom)', () => {
    expect(readChartColors()).toEqual(CHART_FALLBACK);
  });

  it('reads CoreUI variables when they are set', () => {
    document.documentElement.style.setProperty('--cui-primary', '#5856d6');
    expect(readChartColors().bar).toBe('#5856d6');
    document.documentElement.style.removeProperty('--cui-primary');
  });

  it('changes its key with the color mode so the chart redraws', () => {
    const { result } = renderHook(() => useChartColors());
    act(() => setColorMode('light'));
    const lightKey = result.current.key;
    act(() => setColorMode('dark'));
    expect(result.current.key).not.toBe(lightKey);
  });
});
```

(Stub `matchMedia` as in `colorMode.test.ts` if the store needs it; reuse `resetColorModeForTests` in `beforeEach`.)

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3: Implement** `chartTheme.ts`:

```ts
import { useColorMode } from './colorMode';

/** Used only when CoreUI's CSS variables are not available (tests, very early render). The only hex colors allowed in TSX/TS. */
export const CHART_FALLBACK = { bar: '#5856d6', text: '#252b36', grid: 'rgba(8, 10, 12, 0.175)' } as const;

const cssVar = (name: string) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

export function readChartColors(): { bar: string; text: string; grid: string } {
  return {
    bar: cssVar('--cui-primary') || CHART_FALLBACK.bar,
    text: cssVar('--cui-body-color') || CHART_FALLBACK.text,
    grid: cssVar('--cui-border-color-translucent') || CHART_FALLBACK.grid,
  };
}

/** Colors for the current color mode; `key` changes with the mode so the chart re-mounts with new colors. */
export function useChartColors() {
  const { resolved } = useColorMode();
  return { colors: readChartColors(), key: resolved };
}
```

`DistributionChart.tsx`: remove `BAR_COLOR`; `const { colors, key } = useChartColors();` → `<CChartBar key={key} … backgroundColor: colors.bar …>` and in `options.scales` set `ticks.color = colors.text`, `grid.color = colors.grid` for both axes (keep all other options).

- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5: Dashboards.** `HomePage`, `AdminOverviewPage`: `PageHeader icon` + KPI row `row g-3` of `KpiCard` with an `icon` and `color` each (pick from: tickets `cilNotes`/primary, SLA risk `cilAvTimer`/danger, team `cilGroup`/info, assets `cilDevices`/success, approvals `cilCheckCircle`/warning — match each existing card's meaning), remaining content in `PageCard`s. `ReportsPage`: filters in a `PageCard` (the `FilterPanel`), KPI row as above, each distribution chart in a `PageCard` titled with its existing heading; chart containers keep a fixed height class (existing `tgysm-chart` or add `.tgysm-chart { height: 16rem; }` in `_layout.scss` if missing) so a late chart does not shift the page.
- [ ] **Step 6:** `npx vitest run src/features/home src/features/admin/overview src/features/reports` → PASS; gates; commit with named paths: `git commit -m "feat: dashboards with stat widgets and charts that follow the color mode"`.

---

### Task 9: Auth screens and dialogs

**Files (modify):** `frontend/src/shared/layout/AuthCard.tsx`, `features/auth/LoginPage.tsx`, `features/auth/SetPasswordPage.tsx`, `features/auth/ForgotPasswordPage.tsx`, every `*Dialog.tsx` under `frontend/src/features` whose form uses `d-flex flex-column` for its fields (find with `grep -l "flex-column gap-3" frontend/src/features/**/*Dialog.tsx`), `frontend/src/features/auth/authLayout.test.tsx`.

**Rules:**
- `AuthCard`: brand block on top (`CIcon cilDevices size="3xl" className="text-primary"` + app name `h1 h4`), card `shadow`, body `p-4 p-sm-5`; background `bg-body-tertiary` (unchanged). Keep `main`.
- Login: email `TextField startIcon={cilEnvelopeOpen}`, password `TextField startIcon={cilLockLocked}`; submit `Button className="w-100"`. Same pattern on set/forgot password.
- Dialog forms: `form className="row g-3"`; each field `col-12` or `col-12 col-sm-6` (pairs: first/last name, dates, type/brand…). No other changes.

- [ ] **Step 1: Failing test** — `authLayout.test.tsx`:

```tsx
it('shows the sign-in fields with leading icons', () => {
  const { container } = renderLogin(); // existing helper; real name from the file
  expect(container.querySelectorAll('.input-group .input-group-text svg')).toHaveLength(2);
});
```

- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement `AuthCard` + `LoginPage`; run → PASS. **Step 4:** Other auth pages and dialogs; run `npx vitest run src/features` → PASS.
- [ ] **Step 5:** Gates. **E2E checkpoint** as in Task 5 Step 6. Commit named paths: `git commit -m "feat: template sign-in card and grid dialog forms"`.

---

### Task 10: Guard tests for the whole tree

**Files:**
- Modify: `frontend/src/styles/styles.test.ts`; any file the guards flag.

- [ ] **Step 1: Write the guards**

```ts
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const SRC = join(__dirname, '..');
const FEATURES = join(SRC, 'features');
const SHARED = join(SRC, 'shared');
/** Native file picker kept visually hidden behind a button (spec Global Constraints). */
const RAW_CONTROL_EXCEPTIONS = new Set(['features/tickets/AttachmentInput.tsx']);
const CHART_PALETTE_FILE = 'shared/ui/chartTheme.ts';

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(tsx?|ts)$/.test(name) && !/\.test\.tsx?$/.test(name) ? [path] : [];
  });
}

function offenders(files: string[], pattern: RegExp, skip: Set<string> = new Set()) {
  return files.flatMap((file) => {
    const rel = relative(SRC, file);
    if (skip.has(rel)) return [];
    return readFileSync(file, 'utf8')
      .split('\n')
      .flatMap((line, index) => (pattern.test(line) ? [`${rel}:${index + 1}`] : []));
  });
}

describe('UI cohesion guards (spec §1, §3)', () => {
  const featureFiles = sourceFiles(FEATURES);
  const allFiles = [...featureFiles, ...sourceFiles(SHARED)];

  it('features use form controls and tables only through shared/ui', () => {
    expect(offenders(featureFiles, /<(input|select|textarea|table)[\s>]/, RAW_CONTROL_EXCEPTIONS)).toEqual([]);
  });

  it('features do not build their own cards', () => {
    expect(offenders(featureFiles, /className=[{"'`][^"'`]*\bcard\b/)).toEqual([]);
  });

  it('no hard-coded colors that break dark mode', () => {
    expect(offenders(allFiles, /\b(bg-white|bg-light|text-dark|text-black)\b/)).toEqual([]);
    expect(offenders(allFiles, /['"`]#[0-9a-fA-F]{3,8}['"`]/, new Set([CHART_PALETTE_FILE]))).toEqual([]);
  });

  it('styles are mobile first: no new max-width media queries', () => {
    const scss = readFileSync(join(SRC, 'styles', '_layout.scss'), 'utf8');
    const maxWidthQueries = scss.match(/@media\s*\(max-width:[^)]*\)/g) ?? [];
    expect(maxWidthQueries).toEqual(['@media (max-width: 991.98px)']);
  });
});
```

(Use `import.meta.dirname` instead of `__dirname` if the test environment is ESM-only and `__dirname` is undefined; Vitest provides both under Node 22.)

- [ ] **Step 2: Run** — `cd frontend && npx vitest run src/styles/styles.test.ts`. Expected: PASS if Tasks 5–9 covered everything. Each reported `file:line` is a missed conversion: fix it with the rules of the task that owns that page (never by adding an exception). Prove the guards bite: temporarily add `<div className="card">x</div>` to `features/home/HomePage.tsx`, run → FAIL naming that line, remove it, run → PASS.
- [ ] **Step 3:** Gates; commit: `git add frontend/src/styles/styles.test.ts <fixed files by path>`; `git commit -m "test: guard the shared UI building blocks, dark-mode-safe colors and mobile-first styles"`.

---

### Task 11: Appearance E2E (dark mode, layout stability) and ADR-0026

**Files:**
- Create: `frontend/e2e/appearance.spec.ts`, `docs/adr/0026-coreui-visual-system-and-color-modes.md`
- Modify: `docs/adr/README.md`, `frontend/e2e/helpers.ts`

- [ ] **Step 1: Helpers** — `helpers.ts`:

```ts
/** Starts recording layout shifts and long tasks before the page's own scripts run. */
export async function recordStability(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const w = window as unknown as { __cls: number; __longTasks: number[] };
    w.__cls = 0;
    w.__longTasks = [];
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries() as (PerformanceEntry & { value: number; hadRecentInput: boolean })[]) {
        if (!entry.hadRecentInput) w.__cls += entry.value;
      }
    }).observe({ type: 'layout-shift', buffered: true });
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) w.__longTasks.push(entry.duration);
    }).observe({ type: 'longtask', buffered: true });
  });
}

export const MAX_CLS = 0.1;
export const MAX_LONG_TASK_MS = 200;

export async function expectStable(page: Page): Promise<void> {
  const { cls, longTasks } = await page.evaluate(() => {
    const w = window as unknown as { __cls: number; __longTasks: number[] };
    return { cls: w.__cls, longTasks: w.__longTasks };
  });
  expect(cls).toBeLessThan(MAX_CLS);
  expect(longTasks.filter((duration) => duration > MAX_LONG_TASK_MS)).toEqual([]);
}
```

- [ ] **Step 2: Spec** — `appearance.spec.ts` (runs in both viewport projects):

```ts
import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll, expectStable, recordStability } from './helpers';
import { adminSession, seedWorld } from './world';

test('dark mode is kept after a reload and pages stay inside the screen', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  await admin.page.goto('/assets');
  await admin.page.getByRole('button', { name: /Görünüm/ }).click();
  await admin.page.getByRole('button', { name: 'Koyu' }).click();
  await expect(admin.page.locator('html')).toHaveAttribute('data-coreui-theme', 'dark');
  await admin.page.reload();
  await expect(admin.page.locator('html')).toHaveAttribute('data-coreui-theme', 'dark');
  for (const path of ['/assets', `/tickets/${world.ticketNumber}`, '/reports']) {
    await admin.page.goto(path);
    await expect(admin.page.getByRole('heading', { level: 1 })).toBeVisible();
    await expectNoHorizontalScroll(admin.page);
  }
});

test('lists, details and dashboards do not jump or freeze while loading', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  await recordStability(admin.page);
  for (const path of ['/', '/assets', `/tickets/${world.ticketNumber}`, '/reports']) {
    await admin.page.goto(path);
    await expect(admin.page.getByRole('heading', { level: 1 })).toBeVisible();
    await admin.page.waitForLoadState('networkidle');
    await expectStable(admin.page);
  }
});

test('opening a dialog does not move the page sideways', async ({ browser }) => {
  const admin = await adminSession(browser);
  await seedWorld(admin);
  await admin.page.goto('/assets');
  const heading = admin.page.getByRole('heading', { level: 1 });
  const before = await heading.boundingBox();
  await admin.page.getByRole('button', { name: 'Yeni cihaz' }).click();
  await expect(admin.page.getByRole('dialog')).toBeVisible();
  const after = await heading.boundingBox();
  expect(after?.x).toBe(before?.x);
});
```

`world.ticketNumber` — use the field `seedWorld` already returns for a ticket (read `world.ts`; if it returns none, add one seeded ticket there through the admin API the same way the other seeds work, and return its number).

- [ ] **Step 3: Run** — `tools/dev-service.sh stop && tools/e2e.sh e2e/appearance.spec.ts; status=$?; tools/dev-service.sh start; exit $status` → PASS on both projects. If CLS fails, find the shifting element (log `entry.sources` in the observer temporarily) and reserve its space (fixed height, skeleton, `min-height`) — never raise `MAX_CLS`.
- [ ] **Step 4: Full E2E** — same command without a spec path → "E2E passed for: mobile-360 desktop".
- [ ] **Step 5: ADR-0026** — from `~/.claude/templates/adr-template.md` exactly (same headings/order): title "CoreUI template visual system and color modes"; Status `accepted`; Date `2026-10-09`; Deciders `Tugay İLTUŞ`; context = owner's testing finding (spec §1); decision = shared building blocks rule, CoreUI default palette, light/dark/auto with own store and pre-paint script, icon sidebar, guard tests; alternatives = SCSS-only reskin (pros fast; cons no structural consistency; why not: the cause stays), copy of template layout files (cons conflicts with navigation/breadcrumb/context return), CoreUI `useColorModes` (cons unguarded `localStorage`, no shared state for charts); consequences positive/negative/risks; links ADR-0021 (amends its "light only" — note that ADR-0021 stays accepted and this ADR supersedes only its theme clause) and ADR-0022 (chart colors). Cite the commit SHAs of Tasks 1–10 (`git log --oneline master..HEAD`). Add the row to `docs/adr/README.md`.
- [ ] **Step 6: Final gates and commit** — `tools/lint.sh`, `tools/test.sh`, then `tools/build.sh` (prints chunk sizes; no chunk over 500 KB, `vendor-coreui-icons` present). Commit: `git add frontend/e2e/appearance.spec.ts frontend/e2e/helpers.ts frontend/e2e/world.ts docs/adr/0026-coreui-visual-system-and-color-modes.md docs/adr/README.md`; `git commit -m "test: dark mode and layout stability E2E; ADR-0026 visual system and color modes"`.

After Task 11: report to the owner which docs would need a line (`README.md` stack: icons + color modes; `capabilities.md` UI section) — do not edit them unless asked. Merge/push only with owner approval.
