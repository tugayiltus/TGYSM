# ADR-0027: Astryx Design System Replaces CoreUI

**Date**: 2026-11-10
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The frontend is built on CoreUI 5 (ADR-0021) with its visual system and color modes (ADR-0026). About 49 of 168 `.tsx` files import `@coreui/*` directly; the rest go through the wrappers in `src/shared/ui` and `src/shared/layout`.

The owner wants the whole interface rebuilt so that new screens are faster to add with an AI coding agent. CoreUI has no machine-readable component reference, so an agent has to guess props and patterns from examples.

Astryx (`meta/astryx`, MIT, React 19 + StyleX, 174 components, beta `0.7.x`) ships a CLI (`npx astryx component <Name>`, `template`, `docs`, `doctor`) and an agent guide that `astryx init` writes into `frontend/CLAUDE.md`. People and agents read the same reference.

## Decision

Replace CoreUI with Astryx across the frontend, in steps:

1. **Setup (this change).** Install `@astryxdesign/core`, `@astryxdesign/theme-neutral`, `@stylexjs/stylex` and the dev CLI `@astryxdesign/cli`. Import the pre-built CSS in `src/main.tsx` (`reset.css`, `astryx.css`, `theme-neutral/theme.css`) before `styles/app.scss`. Wrap the app in `AstryxProvider` (`src/shared/ui/AstryxProvider.tsx`): `Theme` with the neutral theme, its `mode` taken from the existing color-mode store, and `LinkProvider` with React Router's `Link`. Add the `vendor-astryx` chunk group. Write the Astryx agent guide into `frontend/CLAUDE.md`.
2. **Migration (next changes).** Rebuild shared wrappers and the app shell first, then each feature folder, starting from Astryx templates. During the migration both libraries load: Astryx styles sit in CSS cascade layers, CoreUI stays unlayered, so screens not yet moved look unchanged.
3. **Removal.** When no file imports `@coreui/*`, remove the CoreUI packages, `sass` styles that only served CoreUI, the CoreUI chunk groups, and mark ADR-0021 and ADR-0026 `superseded by ADR-0027`. Charts (ADR-0022) need a replacement for `@coreui/react-chartjs` in that step.

Two setup choices differ from the Astryx defaults:

- **Self-hosted font.** The theme's `fonts.css` loads Figtree from Google Fonts. TGYSM is a company intranet app, so it must not call outside hosts. `@fontsource/figtree` (weights 400–700, Latin and Latin Extended for Turkish) supplies the same `Figtree` family from the app bundle.
- **No generated theme module.** `astryx theme add` writes `src/astryx-themes.ts`, which always imports that `fonts.css`, and the file must not be hand-edited. `AstryxProvider` imports `@astryxdesign/theme-neutral/built` and its stylesheet directly instead.

## Alternatives Considered

### Alternative 1: Stay on CoreUI and improve agent guidance by hand
- **Pros**: No migration cost; stable, released library; the current UI already passed acceptance.
- **Cons**: The agent reference would be hand-written and drift from the library; does not deliver the new interface the owner asked for.
- **Why not**: The owner decided to replace the interface; the main goal is agent-friendly development, which Astryx provides out of the box.

### Alternative 2: shadcn/ui on Tailwind
- **Pros**: Popular, source copied into the repo, large example base for agents.
- **Cons**: Brings Tailwind, which the project rejects (`styles/noTailwind.test.ts`); components are copied code to maintain; no CLI reference for props and page templates.
- **Why not**: The owner chose Astryx.

### Alternative 3: Big-bang rewrite on a long-lived branch
- **Pros**: No period with two libraries loaded.
- **Cons**: A huge diff that cannot be tested or reviewed in parts; `master` cannot take fixes into the branch easily.
- **Why not**: Step-wise migration keeps every commit releasable.

## Consequences

### Positive
- Agents and people look up components with `npx astryx component <Name>` and start pages from `npx astryx template`.
- Theme tokens and light/dark mode come from one theme; the existing light/dark/auto menu keeps working.
- `npx astryx doctor` checks the setup.

### Negative
- Until removal, the bundle carries both libraries (about 300 kB extra CSS, 97 kB extra JS before gzip).
- All screens, their tests and the guide screenshots must be redone.

### Risks
- Astryx is beta (`0.7.x`); a minor bump may break APIs. The version range stays below `0.8.0`, and every bump runs `npx astryx upgrade --from <old> --apply` plus the full gates.
- Astryx comes from a single vendor; a stop in development would leave the project on a frozen version.
- Mixed CoreUI and Astryx screens may look inconsistent during the migration.
