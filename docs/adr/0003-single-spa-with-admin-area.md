# ADR-0003: One SPA with a Separate `/admin` Area, Same-Origin with the API

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Employees, IT staff and admins use one product (`tgysm.md` §4). `/admin` needs its own navigation and layout but not a separate deployment. The UI must be mobile-first. A native mobile app will come later and will reuse the API.

## Decision

- One React SPA. `/admin/*` routes use `AdminLayout` (drawer menu on small screens, sidebar from 1024 px). Other routes use `AppShell`.
- The API serves under `/api/v1`. In development, Vite proxies `/api` to the API (`http://localhost:5080`). In production the API serves the built SPA from `wwwroot` with a fallback to `index.html`, so browser and API share one origin.
- URL paths are English (`/admin/users`); only visible text is Turkish (ADR-0006).
- Hiding a screen is not authorization: every admin endpoint requires the `admin` policy on the server.
- The API contract is described by OpenAPI; frontend types are generated from it. The future mobile app uses the same contract.

## Alternatives Considered

### Alternative 1: Separate admin SPA
- **Pros**: smaller bundles per audience.
- **Cons**: two builds, duplicated shared UI, two deployments.
- **Why not**: `tgysm.md` §4 requires one application; lazy-loaded admin routes keep the user bundle small.

### Alternative 2: Separate origins (API and SPA on different hosts)
- **Pros**: independent hosting.
- **Cons**: CORS, `SameSite=None` cookies, larger CSRF surface.
- **Why not**: same-origin lets the session cookie stay `SameSite=Strict`. If hosting ever needs two origins, write a new ADR.

## Consequences

### Positive
- One deployment, one cookie domain, simple CSRF model.

### Negative
- Admin and user areas release together.

### Risks
- Development relies on browsers accepting `Secure` cookies on `http://localhost` (Chrome and Firefox do; Safari may not).
