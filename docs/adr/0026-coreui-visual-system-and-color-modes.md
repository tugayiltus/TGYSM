# ADR-0026: CoreUI Template Visual System and Color Modes

**Date**: 2026-10-09
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

After the CoreUI conversion (ADR-0021), the owner tested the product and found that the sidebar, the page content and the inputs looked unrelated. The interface had no visual unity. Measured on `master` (`523075f`):

- The dark sidebar showed text only, with no icons and no brand area. `@coreui/icons` was not installed.
- Pages were built in three different ways. List pages put their filters and a bordered table directly on the grey background. Home, ticket detail and profile used cards. Some forms had their own borders.
- Dialog forms used two layouts (a `row g-3` grid and a `d-flex flex-column` stack). Some checkboxes were hand-written `<input>` elements, and a few Tailwind class names were left over.
- ADR-0021 chose a light theme only, with a custom blue (`#1d4ed8`).

The owner asked for one consistent, modern look that follows the CoreUI admin template everywhere. The owner also asked for dark mode, a mobile-first layout, and no layout shift or freeze on phones and desktops. Spec: `docs/superpowers/specs/2026-10-09-ui-cohesion-design.md`.

## Decision

- **Shared building blocks.** All page content goes through `frontend/src/shared/ui`:
  - `PageCard` is a titled card. A titled section is a region landmark.
  - `DataCard` is a list card: filters in the header, the list edge to edge in the body, paging in the footer.
  - `FormSection` is a card with a `row g-3` field grid and right-aligned actions.
  - `StackTable` is a table that stacks into cards below 992 px.
  - `CheckField` is a checkbox, radio or switch.
  - All fields use one label style (`FieldLabel`) with an optional required mark. `TextField` can show a start icon or an end unit in an input group.

  Pages place nothing directly on the page background.
- **Colors.** CoreUI's own palette is used (primary `#5856d6`). The custom `$primary` and `$link-color` are removed. Badges use the `-subtle` and `-emphasis` tokens, which are readable in both modes.
- **Color modes.** The modes are light, dark and auto (follows the system).
  - The choice is chosen in the header `ThemeMenu` and stored under `tgysm-theme`.
  - It is applied to `data-coreui-theme` on `<html>` by a small inline script in `index.html` before the first paint, so dark mode never flashes light.
  - Our own store (`colorMode.ts`, `useSyncExternalStore`) keeps one state for the menu and the charts.
  - Charts read their colors from CoreUI's CSS variables (`chartTheme.ts`) and are re-mounted when the mode changes.
  - This amends ADR-0021: its "light only" clause is replaced. The rest of ADR-0021 stands.
- **Shell.**
  - Sidebar: a dark `CSidebar` with a brand area and a `@coreui/icons` icon on every menu route (`RouteMeta.icon`, required by a test). The narrow mode shows icons only.
  - Header: sticky, with a fixed row height. It holds the area switch, the theme menu, and an avatar account menu. The breadcrumb moves into a header strip.
  - Icons are imported by name from one module (`shared/ui/icons.ts`) and ship in their own `vendor-coreui-icons` chunk.
- **Layout stability.**
  - Loading states reserve the space of their content: skeleton rows, card body minimum heights, and chart placeholders of the chart's height.
  - `html` has `scrollbar-gutter: stable`.
  - Base styles target 360 px; wider layouts use `min-width` breakpoints only.
- **Guard tests.** `frontend/src/styles/styles.test.ts` fails when any of these rules is broken:
  - `features/` uses a raw `<input>`, `<select>`, `<textarea>` or `<table>`, or a hand-made `card` class.
  - Any source uses a hard-coded color class or a hex color. The one exception is the chart palette fallback.
  - `_layout.scss` gains a new `max-width` media query.
  - The scrollbar gutter rule is missing.

  `noTailwind.test.ts` gained the Tailwind leftovers found during this work. The E2E spec `appearance.spec.ts` checks two things at 360 px and 1280 px:
  - Dark mode persists across a reload with no horizontal scroll.
  - Cumulative layout shift stays under 0.1, with no long task over 200 ms, on home, inventory, ticket detail and reports.

Commits: `d7df370`, `0d775aa`, `319c4cb`, `9cfc235`, `4bfbfeb`, `bdd011d`, `263ff67`, `efb23a8`, `2042f19`, `b1ed2cc`, `80982b9`, `2a43bba` and the commit that adds this ADR.

## Alternatives Considered

### Alternative 1: SCSS-only reskin
- **Pros**: Fast. No page markup changes, so there is little risk to tests.
- **Cons**: Cannot add cards around content, icons in the menu or one form grid. The three page structures stay.
- **Why not**: The cause of the inconsistency is structural. A reskin would change colors but keep the disunity the owner reported.

### Alternative 2: Copy the CoreUI template's layout files
- **Pros**: Closest to the template's look out of the box.
- **Cons**: The template's `AppSidebar`, `AppHeader` and `AppContent` would replace our role menus, breadcrumbs, context return and phone panel. All of that would have to be rebuilt inside them.
- **Why not**: Large churn for no visual gain over building the same look on our existing shell.

### Alternative 3: CoreUI's `useColorModes` hook
- **Pros**: Ready-made and matches the template.
- **Cons**: It reads and writes `localStorage` without a guard, so it throws in a private window or with blocked storage. It also keeps its state per component, so the header menu and the charts would not share one mode.
- **Why not**: A small own store with the same behavior is safer and shared.

## Consequences

### Positive
- Every page has the same structure: page header, then cards. A new page that uses the building blocks gets the same look, and the guard tests keep it that way.
- Dark and auto modes work on every page, including charts, with no light flash.
- List, detail and dashboard pages reserve their space while loading. The E2E budget (CLS < 0.1, no task over 200 ms) protects this.

### Negative
- Many page files changed at once. Visual differences are reviewed through screenshots, not unit tests.
- One more dependency pair (`@coreui/icons`, `@coreui/icons-react`) to keep up to date.
- The theme script in `index.html` duplicates the storage key and the mode values. A test checks that the key matches.

### Risks
- Headless Chromium uses overlay scrollbars, so no E2E can see a sideways shift caused by a disappearing scrollbar. Only a static test checks the `scrollbar-gutter` rule.
- A future page can still place loose content outside a card without using a forbidden class. Reviews need to watch for this.
