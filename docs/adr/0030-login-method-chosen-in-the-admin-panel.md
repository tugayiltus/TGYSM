# ADR-0030: Login Method Chosen in the Admin Panel (Local, Microsoft Entra ID or Active Directory)

**Date**: 2026-11-12
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

ADR-0004 gave TGYSM local accounts only. The company runs on-premises Active Directory with Microsoft 365, and the owner wants employees to sign in with that account, choosing and configuring the method in the admin panel without server changes. Design: `docs/superpowers/specs/2026-11-12-sso-ldap-login-design.md`. Commit: `759792b` (feature branch `feature/sso-ldap-login`).

## Decision

- One row `auth_settings` holds the active method (`local`, `entra`, `ldap`) and the settings of both providers. Secrets (client secret, LDAP bind password) are stored only as Data Protection ciphertext (purpose `Tgysm.AuthSettings`) and never returned, logged or audited.
- A provider can be switched on only after "Bağlantıyı test et" passed with its current settings (a fingerprint of the settings is stored with the test).
- **Microsoft Entra ID:** OpenID Connect code flow with PKCE; options are read from the row and dropped from the cache when settings change. Redirect URI `{App:PublicBaseUrl}/api/v1/auth/oidc/callback`. Only members of the configured tenant sign in: guests (`idp` other than the tenant, `acct` = 1, `#EXT#` names) are refused. The sign-in name (`preferred_username`/`upn`, verified tenant domains) is used as the email; the `email` claim is never used (nOAuth). The email domain must be in the allowed list.
- **Active Directory:** LDAPS (default) or StartTLS, never plain; a managed client (`Novell.Directory.Ldap.NETStandard`) so the Linux image needs no native library. Users type the Windows user name or the email; the service account finds the entry, a bind as that entry checks the password. Attempts are counted atomically per typed name and per found account (5, then the user lockout window).
- **Accounts:** a directory identity matches by stored id (`entra_object_id`, `ldap_object_guid`), else links the user with the same email (never one linked to another id), else creates an employee without a password. Roles stay in TGYSM. Names and email follow the directory.
- **Break-glass:** under a directory method, admins can still sign in with their local password ("Yerel yönetici girişi"); other local passwords do not work and forgot-password sends them nothing. Switching back to Yerel sends every active user without a password a set-password link (ADR-0017).

## Alternatives Considered

### Alternative 1: Settings in deploy/.env
- **Pros**: No secrets in the database; simple options binding.
- **Cons**: Every change needs an operator, a file edit and a restart.
- **Why not**: The owner wants to configure and switch from the panel.

### Alternative 2: Several methods at once
- **Pros**: Gradual migration; users pick.
- **Cons**: Two passwords per person, mixed audit, a larger login page.
- **Why not**: The owner wants exactly one method.

### Alternative 3: System.DirectoryServices.Protocols for LDAP
- **Pros**: Microsoft package.
- **Cons**: Needs native `libldap` in the Linux image.
- **Why not**: A managed client keeps the image unchanged.

### Alternative 4: Roles from AD groups
- **Pros**: IT and admin rights follow the directory.
- **Cons**: Group design and mapping UI; a directory mistake changes TGYSM rights.
- **Why not**: The owner keeps roles in TGYSM.

## Consequences

### Positive
- Employees sign in with their company account; accounts appear on first sign-in.
- Settings change without restart; a wrong setting cannot be switched on untested, and admins keep a local way in.

### Negative
- Directory secrets live in the database, protected by the shared key ring; losing the key ring means re-entering them.
- Live sign-ins against Entra and AD are not covered in CI (fakes and an env-gated LDAP contract test only).

### Risks
- An LDAP user who can edit their own `mail` attribute in AD could link to a TGYSM account with that email; AD should not let users edit `mail`.
- Guest detection depends on claims; configure `acct` and `idp` as optional claims in the Entra app for the strictest check.

## Amendment

This ADR amends ADR-0004 (local accounts only).
