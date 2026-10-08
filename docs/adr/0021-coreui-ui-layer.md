# ADR-0021: CoreUI Free as the UI Layer

**Date**: 2026-10-09
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The UI/UX review (`docs/superpowers/specs/2026-10-09-ui-ux-review.md`) asked for a mobile-first product with a left sidebar on both the application and the `/admin` side, a breadcrumb on every signed-in page and one consistent visual language. The owner chose the CoreUI Free React Admin Template as the base for the whole product and a full conversion rather than a new shell around the old screens (`docs/superpowers/specs/2026-10-09-coreui-ui-ux-design.md`). Until now the frontend used Tailwind CSS for layout and styling and Radix for dialogs and sheets (ADR-0008). This ADR extends ADR-0008; React, Vite, TanStack Query, react-router, react-hook-form + zod and i18next stay.

## Decision

- `@coreui/react` 5.13, `@coreui/coreui` 5.9 (SCSS, Bootstrap 5 based), `@coreui/icons` and `@coreui/icons-react` are the UI layer. One stylesheet entry, `src/styles/app.scss`, overrides CoreUI variables (brand blue `#1d4ed8`, font stack, radius, 16 px inputs) and adds a few scoped rules.
- Light theme only: `applyTheme()` sets `data-coreui-theme="light"` on `<html>`.
- `tailwindcss`, `@tailwindcss/vite` and `radix-ui` are removed. Layout uses CoreUI/Bootstrap grid and utility classes.
- The template is a layout reference only: no demo pages, no Redux, no second router. The reference commit is `coreui/coreui-free-react-admin-template@2609f59`; its MIT notice is kept in `frontend/THIRD_PARTY_NOTICES.md`.
- `shared/ui` wraps CoreUI components behind the existing props, so page logic does not change with the library.
- `Dialog` and `Sheet` render CoreUI's modal/offcanvas with `portal={false}` inside our own `createPortal(…, document.body)`. CoreUI's `CConditionalPortal` renders in place first and moves to `<body>` one render later, which remounts the dialog right after it opens (lost focus and input state; found by `UsersPage` "link sent" test, commit of this ADR).
- Vite chunk group `vendor-coreui` holds `@coreui/react`, `@coreui/icons*`, `@coreui/coreui` and `@popperjs/core`.
- The conversion runs as one branch (`ui-coreui`) and is merged when every screen is converted.

## Alternatives Considered

### Alternative 1: Keep Tailwind and Radix, build the sidebar and breadcrumb with them
- **Pros**: no new dependency; smallest change; existing styling stays.
- **Cons**: no ready admin layout; every component keeps its hand-made styling.
- **Why not**: the owner chose CoreUI for the whole product.

### Alternative 2: CoreUI shell only, page content stays Tailwind
- **Pros**: fastest visible change.
- **Cons**: two style systems for good; preflight and utility conflicts; inconsistent screens.
- **Why not**: the review's "not done" list rejects a partial conversion.

### Alternative 3: Adapter-first gradual migration on `master`
- **Pros**: every step shippable; smaller reviews.
- **Cons**: Tailwind and CoreUI coexist for the whole migration (CSS order and reset conflicts).
- **Why not**: the owner chose one big-bang branch.

## Consequences

### Positive
- One visual language and ready components (sidebar, header, breadcrumb, modal, offcanvas, table, form, badge, toast).
- `shared/ui` keeps the page code independent of the library.

### Negative
- The full CoreUI stylesheet is large (about 305 kB, 41 kB gzip) until unused parts are trimmed.
- Every page's markup is rewritten.

### Risks
- Visual regressions while the branch is in progress; mitigated by keeping behaviour tests green per task and a full E2E and browser QA before merge.
- CoreUI component behaviour differs from Radix (focus, portals); covered by `shared/ui` tests.
