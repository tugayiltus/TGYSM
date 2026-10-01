# ADR-0017: Password Set Links

**Date**: 2026-09-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

ADR-0004 (local accounts, cookie session) and ADR-0007 (CLI-first admin setup) established temporary passwords as the way an account gets its first password or a reset. With outbox email now available (ADR-0015), the spec wants a single-use, time-limited email link as the default provisioning and reset path, with the temporary password kept only as an admin fallback and the CLI recovery path (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §1, §4, commit `3aa02ab`). This amends the account-creation and recovery flows of ADR-0004 and ADR-0007 without replacing them.

## Decision

- `password_tokens`: `user_id`, `purpose` (`activation`, `reset`), `token_hash` (SHA-256 of 32 random bytes, base64url — the raw token is never stored), `expires_at`, `used_at`, `created_at`. Issuing a token voids unused tokens of the same user and purpose.
- `POST /admin/users` and `POST /admin/users/{id}/reset-password` take `delivery` = `email_link` (default) or `temporary_password` (keeps today's ADR-0004/ADR-0007 behavior, `Cache-Control: no-store`).
- `POST /auth/forgot-password { email }` always returns 202, rate limited per IP; only active users get a reset event queued. No response difference reveals whether the account exists — the same anti-enumeration posture ADR-0004 already applies to login.
- `POST /auth/set-password { token, newPassword }` returns 204. A conditional update (`used_at IS NULL AND expires_at > now`) decides success; any failure (unknown, used, or expired token) returns the same 400 `auth.token_invalid`, mirroring ADR-0004's single `auth.invalid_credentials` code so the error itself is not an oracle. Success sets the password, clears `must_change_password`, rotates the `security_stamp` (ending other sessions, per ADR-0004) and audits.
- UI: `/set-password?token=…` (activation and reset) and `/forgot-password`; the token is stripped from the address bar after the page loads, to keep it out of browser history and referrer headers.
- `Cleanup` (worker, ADR-0014) deletes tokens 30 days after expiry.
- The temporary-password flow from ADR-0004/ADR-0007 is unchanged and stays available as `delivery=temporary_password` and via `tools/setup-admin.sh` / `tools/reset-password.sh`.

## Alternatives Considered

### Alternative 1: Keep temporary passwords as the only mechanism
- **Pros**: no new table or flow; already implemented and audited (ADR-0004, ADR-0007).
- **Cons**: an admin must communicate the password out of band every time — there is no automated, secure delivery channel — even though outbox email now exists.
- **Why not**: the spec requires link-based self-service as the default now that email delivery is available, keeping the temporary password only as a fallback.

### Alternative 2: A long-lived magic link (single-use, no expiry)
- **Pros**: simpler UX, no "link expired" friction for the user.
- **Cons**: a stale email sitting in an inbox remains a live credential indefinitely, which is a weak posture for a shared or later-compromised mailbox.
- **Why not**: the spec requires time-limited links (72 h activation, 1 h reset per the seed assumptions in §1) to bound the exposure window.

## Consequences

### Positive
- No plaintext password ever needs to be typed or read aloud by an admin on the default path.
- Expiry bounds the exposure window of a leaked or forgotten link; `token_hash` means a database read alone cannot be replayed as a credential.
- The single `auth.token_invalid` code and the constant-202 `forgot-password` response keep account existence unobservable, consistent with ADR-0004's login behavior.

### Negative
- Two account-provisioning paths (link and temporary password) must be built, tested and kept in sync.
- The default path now depends on email delivery (ADR-0015); an SMTP or worker outage delays provisioning unless an admin falls back to `temporary_password`.

### Risks
- A user who never opens the activation email stays unprovisioned; nothing pages an admin about it, so this depends on the admin noticing (e.g. via `/admin/email-deliveries`) and choosing the fallback.

## Amendment 2026-10-01

`POST /auth/forgot-password` no longer looks the account up on the request path. The first implementation (commit `8b8f28e`) queued the reset link synchronously for an active account only, so an active email did more work (token, outbox and audit writes) before the 202 than an unknown or inactive one, and response timing revealed which accounts exist. The fix (Task 7 fix round 1, "defer forgot-password account lookup to the worker") makes the request path validate and normalize the email and write one internal outbox event, `password_reset_requested`, carrying the normalized email, for every well-formed address. The worker's outbox dispatcher resolves it: an active account gets the reset token, the `password_reset` event and the `users.password_reset_link_sent` audit row (no actor); an unknown or inactive email is marked skipped. The internal event type has no template, recipients or translation. `POST /auth/set-password` also gets its own per-IP rate limit, because each request hashes a password before the token lookup.

## Amendment 2026-10-02

Unused links of a user are revoked when the user sets a password, when an admin changes the email address, and when an admin issues a temporary password (commit 2a9e93c). A forgot-password request is skipped (no token, no email) while the account has an unused reset link younger than 15 minutes, including a link an admin issued; the request path is unchanged, so enumeration resistance holds (commit 9ffcba5). An admin retry of a failed account email issues a new link (commit b2ad9f5). The link target is `/set-password?token=…` under `App:PublicBaseUrl`, not `/account/set-password`. `POST /auth/set-password` has a per-client-IP rate limit (`Security:SetPasswordPermitLimitPerMinute`, default 5). The shared Data Protection application name `Tgysm` lets the Worker render the link from the API-protected secret (commit 42947ba).
