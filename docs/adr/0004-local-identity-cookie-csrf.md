# ADR-0004: Local Accounts, Cookie Session and Antiforgery Header

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Accounts are local (no SSO, no LDAP in the first release; `tgysm.md` §2). Only admins create users. The first login must force a password change, and role change or deactivation must end active sessions (`tgysm.md` §3.1). The domain `User` must not depend on ASP.NET Core Identity types (ADR-0001).

## Decision

- Password hashing uses ASP.NET Core Identity's `PasswordHasher<T>` (PBKDF2) behind `IPasswordService` in Infrastructure. The full Identity user store is not used; `User` is a plain domain entity.
- Session: ASP.NET Core cookie authentication. Cookie `__Host-tgysm`, `Secure`, `HttpOnly`, `SameSite=Strict`, 8-hour sliding lifetime (configurable `Security:SessionHours`).
- Every request re-reads the user (`OnValidatePrincipal`): an inactive user or a changed `security_stamp` rejects the session immediately. Fresh role and `must_change_password` claims replace the cookie claims on each request.
- `security_stamp` rotates on role change, deactivation, reactivation, password change and admin password reset.
- CSRF: antiforgery token from `GET /api/v1/auth/csrf`, sent back in the `X-CSRF-TOKEN` header on every unsafe `/api` request; checked by `CsrfValidationMiddleware`.
- Brute force: 5 failed logins lock the account for 15 minutes; `POST /api/v1/auth/login` also has a per-IP fixed-window rate limit (`Security:LoginPermitLimitPerMinute`, default 10). All login failures return the same `auth.invalid_credentials` code.
- Password policy: 12–128 characters with at least one letter and one digit.
- While `must_change_password` is true, only `me`, `change-password` and `logout` are allowed (403 `auth.password_change_required` elsewhere).

## Alternatives Considered

### Alternative 1: Full ASP.NET Core Identity (`AddIdentityCore` + EF stores)
- **Pros**: lockout, stamps and hashing built in.
- **Cons**: the domain `User` must inherit `IdentityUser`, Identity tables and conventions leak into the schema and the domain.
- **Why not**: the domain stays framework-free; the few Identity features needed are small and tested.

### Alternative 2: JWT bearer tokens in local storage
- **Pros**: stateless, easy for a future mobile app.
- **Cons**: XSS can steal tokens, revocation needs extra state.
- **Why not**: same-origin cookies are safer for the web app. The mobile app can get its own token flow in a later ADR.

## Consequences

### Positive
- Deactivation and role change take effect on the next request.
- No token in JavaScript-reachable storage.

### Negative
- One database read per authenticated request.

### Risks
- Data Protection keys must be persisted in production, or all sessions end on restart. Address with the hosting decision (Phase 6).

## Amendment 2026-09-26

- The default authorization policy equals the fallback policy, so a bare `RequireAuthorization()` also enforces the pending-password gate (commit 512a3e5).
- Inactive and locked accounts run one password hash check, like unknown emails, so response time does not reveal account state (commit 8b3357d).
- Development and Testing trust `X-Forwarded-Proto` from loopback proxies only; the Vite proxy sends `https` so Secure cookies and antiforgery work on `http://localhost`. Production forwarded-header trust is decided with hosting (Phase 6).
- Known limit: the login rate limit is per client IP; many users behind one NAT share it. Per-account limiting waits for the hosting/proxy decision.

## Amendment 2026-10-02

Password set links, forgot-password and the revocation rules for unused links are specified in ADR-0017. The Data Protection application name is now shared as `Tgysm` between the API and the Worker; key persistence is still open and is tracked in `SECURITY.md` (commit 42947ba).
