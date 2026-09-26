# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| `main` (pre-release, Phase 1) | Yes |

## Reporting a vulnerability

Report privately to the project owner, Tugay İLTUŞ (GitHub [@tugayiltus](https://github.com/tugayiltus)), through a private GitHub security advisory. Do not open a public issue. Include affected endpoint or screen, steps to reproduce, impact, and any proof of concept. Acknowledgement target: 5 business days.

## Security posture

- **Authentication:** local accounts only; PBKDF2 password hashing (`PasswordHasher<T>`); 12–128 character passwords with letters and digits; forced change of temporary passwords; lockout after 5 failures for 15 minutes; per-IP login rate limit; uniform `auth.invalid_credentials` response (ADR-0004).
- **Sessions:** `__Host-tgysm` cookie (`Secure`, `HttpOnly`, `SameSite=Strict`); every request re-validates active status and security stamp; role change, deactivation and password reset end sessions.
- **CSRF:** antiforgery token in `X-CSRF-TOKEN` on every unsafe API request.
- **Authorization:** server-side policies; `/api/v1/admin/*` requires the admin role; hidden UI is not treated as authorization (ADR-0005).
- **Audit:** security and admin events in append-only `audit_events` (database trigger); passwords, hashes and tokens are never logged or audited.
- **Transport:** HTTPS required in production (HSTS outside Development/Testing).
- **Secrets:** no credentials, keys or connection strings in the repository. Development connection strings live in `dotnet user-secrets`; tests read `TGYSM_TEST_ADMIN_CONNECTION`.
- **Dependencies:** versions pinned centrally (`backend/Directory.Packages.props`, `frontend/package-lock.json`).

## Dependency & supply chain

- Automated update tool: TODO: choose (Dependabot or Renovate) when the GitHub repository exists.
- Each bump must pass `tools/lint.sh`, `tools/test.sh` and `tools/e2e.sh` before merge.
- Major version bumps additionally get a manual smoke test of login, forced password change and `/admin/users`.
