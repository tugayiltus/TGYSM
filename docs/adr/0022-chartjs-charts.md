# ADR-0022: Chart.js through CoreUI for Report Charts

**Date**: 2026-10-09
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

ADR-0020 chose Recharts for report charts. With the move to CoreUI as the UI layer (ADR-0021), the owner decided that charts use CoreUI's Chart.js integration instead, so the product has one component family (`docs/superpowers/specs/2026-10-09-coreui-ui-ux-design.md` §2, §7). Reports draw one chart type today: horizontal distribution bars, with the same numbers listed as links next to them.

## Decision

- `chart.js` 4.5 through `@coreui/react-chartjs` 3 (`CChartBar`) replaces Recharts; `recharts` and its helper packages are removed.
- `DistributionChart` stays a lazily loaded component, its canvas is `aria-hidden`, and the bucket list remains the accessible text alternative.
- Vite chunk group `vendor-charts` now holds `chart.js`, `@kurkle/color` and `@coreui/react-chartjs`; it loads only with the first distribution card, never on the entry page.
- Tests replace the CoreUI chart with a plain canvas (jsdom has no canvas; Chart.js throws while resizing a detached canvas there).

## Alternatives Considered

### Alternative 1: Keep Recharts
- **Pros**: already working and tested.
- **Cons**: a second visual and API family next to CoreUI.
- **Why not**: the owner chose CoreUI's Chart.js integration.

### Alternative 2: Plain Chart.js without the CoreUI wrapper
- **Pros**: could register only the bar chart parts (smaller bundle).
- **Cons**: own React lifecycle handling for create/update/destroy.
- **Why not**: the wrapper keeps the code small; the chunk (about 232 kB, 81 kB gzip) is lazy and within the 500 kB budget.

## Consequences

### Positive
- One UI family; charts follow CoreUI theming.

### Negative
- The wrapper registers every Chart.js part, so the chart chunk is larger than a bar-only build.

### Risks
- Canvas output cannot be asserted in unit tests; chart rendering is checked in the browser QA and E2E runs.
