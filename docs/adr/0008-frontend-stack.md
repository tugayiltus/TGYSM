# ADR-0008: Frontend Stack

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The web app must be mobile-first, accessible (keyboard, visible focus, form error descriptions, 360 px without horizontal scroll) and typed end-to-end with the API (`tgysm.md` §13, §15).

## Decision

- React 19 + TypeScript (strict) + Vite 8.
- Tailwind CSS v4 for styling; Radix UI primitives (`radix-ui`) for accessible dialogs and drawers.
- React Router for routes; admin pages are lazy-loaded.
- TanStack Query for server state; react-hook-form + zod for forms.
- i18next + react-i18next with typed keys (ADR-0006).
- OpenAPI document generated at build time from the API; `openapi-typescript` generates `schema.d.ts`; `openapi-fetch` is the typed client with an antiforgery middleware.
- Tests: Vitest + React Testing Library; Playwright E2E at 360×800 and 1280×800.
- ESLint flat config per the owner's React rules (render-loop guards as errors, `--max-warnings=0`, no disable comments). ESLint pinned to v9 and TypeScript to ~6.0 until plugins support newer majors.

## Alternatives Considered

### Alternative 1: MUI
- **Pros**: complete component set.
- **Cons**: heavy bundle, Material look, harder custom mobile layouts.
- **Why not**: Tailwind + Radix is lighter and gives full control.

### Alternative 2: Mantine
- **Pros**: rich components including forms and dates.
- **Cons**: medium weight, own styling system.
- **Why not**: same reason; Radix covers the accessibility-critical primitives.

## Consequences

### Positive
- Small bundles, accessible primitives, typed API calls.

### Negative
- More hand-built UI components.

### Risks
- Vite 8 (Rolldown) chunk options differ from Rollup; check current docs when tuning chunks.

## Amendment 2026-09-26

- TypeScript is pinned to ~5.9, not ~6.0: `openapi-typescript` 7.13 requires TypeScript 5 (commit 8b3357d). zod is v4.
- Backend tests run on Microsoft.Testing.Platform (repo-root `global.json`), because the .NET 10 SDK no longer runs xUnit v3 4.x through VSTest; coverage uses coverlet.MTP.
- The API serializes numbers strictly (`JsonNumberHandling.Strict`) so generated TypeScript types are plain `number`.
