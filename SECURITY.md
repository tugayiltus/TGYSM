# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| `main` (pre-release) | Yes |

## Reporting a vulnerability

Report privately to the project owner, Tugay İLTUŞ (GitHub [@tugayiltus](https://github.com/tugayiltus)), through a private GitHub security advisory. Do not open a public issue. Include affected endpoint or screen, steps to reproduce, impact, and any proof of concept. Acknowledgement target: 5 business days.

## Security posture

- **Authentication:** local accounts only; PBKDF2 password hashing (`PasswordHasher<T>`); 12–128 character passwords with letters and digits; forced change of temporary passwords; lockout after 5 failures for 15 minutes; per-IP login rate limit; uniform `auth.invalid_credentials` response (ADR-0004).
- **Sessions:** `__Host-tgysm` cookie (`Secure`, `HttpOnly`, `SameSite=Strict`); every request re-validates active status and security stamp; role change, deactivation and password reset end sessions.
- **CSRF:** antiforgery token in `X-CSRF-TOKEN` on every unsafe API request.
- **Authorization:** server-side policies; `/api/v1/admin/*` requires the admin role; hidden UI is not treated as authorization (ADR-0005).
- **Audit:** security and admin events in append-only `audit_events` (database trigger); passwords, hashes and tokens are never logged or audited.
- **Email:** the SMTP password (`Email:Smtp:Password`) comes from user-secrets or the environment only, never `appsettings`; it is not logged, audited or stored. Send errors are sanitized before they are stored in `email_deliveries.last_error` (credential-shaped pairs redacted, cut to 500 characters). Templates are plain text with a fixed per-event variable allow-list and no expression evaluation, so there is no markup or template injection surface (ADR-0016). `App:PublicBaseUrl` must be an absolute `http`/`https` URL or the hosts refuse to start, so links in email never render as relative paths.
- **Account-link tokens in email:** the token is kept only as a Data Protection–encrypted `notification_outbox.protected_secret`, rendered into the body at send time, and cleared (delivery body becomes `[cleared]`) once the delivery is `sent` or terminally `failed` (ADR-0015). Outbox payloads hold ids and reason codes only, never subject or resolution text. Set-password links are covered under "Password set links" below.
- **Password set links (ADR-0017):** tokens are 32 random bytes; only a hash is stored (`password_tokens.token_hash`), the token exists only in the email. Activation links last 72 hours and reset links 1 hour; each works once, redemption is a single conditional update, so parallel use has one winner. Every invalid, used or expired token gives the same `auth.token_invalid`. Redeeming a link sets the password, rotates the security stamp (ends other sessions) and voids the user's other unused links; unused links are also revoked when an admin changes the user's email address or issues a temporary password, and an account email queued before an address change is never sent to the new address. `POST /auth/forgot-password` answers 202 and does the same work on the request path for any address (the Worker looks the account up later), so timing and response do not reveal whether an account exists; the address is kept in an outbox event for at most 30 days (`Cleanup`). While an account has an unused reset link younger than 15 minutes, further requests are skipped (no new link, no email). `POST /auth/forgot-password` and `POST /auth/set-password` have per-client-IP rate limits (`Security:ForgotPasswordPermitLimitPerMinute`, `Security:SetPasswordPermitLimitPerMinute`, default 5 each). An admin retry of a failed account email issues a new link and revokes the older ones.
- **Known gap, Data Protection keys:** `AddDataProtection()` uses the shared application name `Tgysm` (so the Worker can read what the API protected) but has no key persistence configured. Before production, keys must be persisted and shared between the API and the Worker; losing them makes a pending account-link email unrenderable (ADR-0004, ADR-0015).
- **Worker:** `Tgysm.Worker` has no network listener and no signed-in user; its audit rows carry a generated correlation id and no user id. It needs the database connection string and, for SMTP, the secret above, through environment or user-secrets.
- **Health probe:** `GET /api/v1/health` is anonymous and returns only `{ status }` (`ok` or `degraded`); job names, timestamps and errors are available only to admins at `/api/v1/admin/system/workers`.
- **Transport:** HTTPS required in production (HSTS outside Development/Testing).
- **Secrets:** no credentials, keys or connection strings in the repository. Development connection strings live in `dotnet user-secrets`; tests read `TGYSM_TEST_ADMIN_CONNECTION`.
- **Dependencies:** versions pinned centrally (`backend/Directory.Packages.props`, `frontend/package-lock.json`).

## Dependency & supply chain

- Automated update tool: TODO: choose (Dependabot or Renovate) when the GitHub repository exists.
- Each bump must pass `tools/lint.sh`, `tools/test.sh` and `tools/e2e.sh` before merge.
- Major version bumps additionally get a manual smoke test of login, forced password change and `/admin/users`.
