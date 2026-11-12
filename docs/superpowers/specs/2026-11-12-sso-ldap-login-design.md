# Login with Microsoft (SSO) or Active Directory (LDAP), chosen in the admin panel — design

Date: 2026-11-12. Owner: Tugay İLTUŞ. Branch: a new feature branch from `master`.

## Intent

The company runs on-premises Active Directory with Microsoft 365. Employees should sign in to TGYSM with the account they already use, not a second TGYSM password. The admin chooses the login method in the panel and configures it there, without editing server files or redeploying.

## Goals

- **One active login method**, chosen in a new admin page "Giriş yöntemi" (under `/admin`, Sistem ve denetim group):
  - **Yerel** (today's behavior: TGYSM email + password; the default).
  - **Microsoft (SSO)**: OpenID Connect with Microsoft Entra ID (the Microsoft 365 directory). The login page shows one "Microsoft ile giriş yap" button.
  - **Active Directory (LDAP)**: the login form checks the user name and password against AD. The user may type the Windows user name (`sAMAccountName`, e.g. `ahmet.yilmaz`) or the email / UPN (`ahmet.yilmaz@sirket.com`); both are accepted.
- **Settings in the panel** for each method, with a "Bağlantıyı test et" button that reports success or the exact failure in Turkish before the method is switched on:
  - SSO: tenant id (or authority URL), client id, client secret, optional allowed email domain. The page shows the redirect URI to register in Entra (`{App:PublicBaseUrl}/api/v1/auth/oidc/callback`, read-only, with a copy button).
  - LDAP: server host, port, security (LDAPS by default, StartTLS selectable, plain text not allowed), base DN, bind account DN and password (service account used to search), optional user filter / group DN that limits who may sign in.
  - Secrets (client secret, bind password) are write-only in the UI: shown as "kayıtlı" and replaced only when a new value is typed.
- **First sign-in creates the account automatically** (owner decision): role `employee`, first name, last name and email from the directory (Entra claims or AD attributes `givenName`, `sn`, `mail`/`userPrincipalName`). Department and location stay empty for the admin to fill in. An existing TGYSM user with the same email is linked, not duplicated.
- **Roles stay in TGYSM** (owner decision): everyone arrives as `employee`; the admin gives `it_staff` / `admin` in the user list as today. The directory never changes a role.
- **Local admin break-glass** (owner decision): whatever method is active, local admin accounts can always sign in with their TGYSM password through a small "Yerel yönetici girişi" link on the login page. Non-admin local passwords stop working while SSO or LDAP is active.
- Name and email are refreshed from the directory on every directory sign-in. A deactivated TGYSM user cannot sign in by any method.
- Everything is audited: method changes, settings changes (never the secret values), test runs, directory sign-ins, automatic account creation, failures.

## Linking, roles and switching (owner decisions unless marked "my call")

- **Admins keep both:** an `admin` linked to a directory identity can still sign in with the local password through "Yerel yönetici girişi". So break-glass works as long as one admin exists; it does not depend on a separate account. Non-admin linked users have no working local password while a directory method is active.
- **Email linking only inside the company:** an existing TGYSM user (any role, including `it_staff` / `admin`) is linked by email only when the directory identity belongs to the company: for SSO, the token's tenant (`tid`) equals the configured tenant and the email's domain is in the allowed domain list set in the panel; for LDAP, the entry comes from the configured base DN. The role carries over as it is. The email used is `email`, else `preferred_username` / `upn` (my call), compared case-insensitively.
- **Identity per source** (my call): a user keeps one directory id per source (Entra `oid`, AD `objectGUID`), so switching between SSO and LDAP links again by email once, then by the stored id.
- **Back to Yerel:** when the admin switches to Yerel, every active user without a usable local password gets the existing set-password email (ADR-0017). Before switching, the panel shows how many people will receive it.
- **Admin creates a user while a directory method is active** (my call): the record is created without a temporary password and without a set-password email; it is linked on the user's first directory sign-in by email. The create dialog explains this.
- **LDAP lockout** (my call): failed LDAP attempts count per typed user name (normalized) and per IP with the existing login rate limit. TGYSM locks the name for the existing lockout window after 5 failures, before a typical AD threshold, so wrong attempts in TGYSM do not lock the AD account. Unknown names count the same way, so the response does not reveal whether a name exists.

## Non-goals

- SAML, Google, Okta or several SSO providers at once (OpenID Connect against Entra only; the settings use a generic authority so Keycloak/ADFS may work, but they are not tested).
- Role or department mapping from AD groups.
- Syncing or importing the whole directory in the background; accounts appear on first sign-in only.
- Password change / reset for directory users inside TGYSM (their password belongs to AD / Microsoft).
- Multi-factor authentication inside TGYSM (Entra applies its own MFA in SSO).

## Anti-goals (would be a failure even if it works)

- An admin locked out of the panel because a setting is wrong or the directory is down.
- A directory password or client secret stored in plain text, logged, returned by the API or written to audit.
- LDAP traffic without TLS, or a plain-text bind.
- Switching the method in the panel needs a server restart or a file edit.

## Behavior details

- **Switching method:** only allowed after a successful "Bağlantıyı test et" with the current settings (the API refuses otherwise). Existing sessions stay valid; the new method applies to the next sign-in.
- **Login page** asks the API which method is active (anonymous endpoint returning only the method and the SSO button label) and renders the email/password form (Yerel, LDAP — LDAP shows "Kullanıcı adı veya e-posta") or the Microsoft button (SSO), plus the break-glass link.
- **Accounts:** a user record gets an `AuthSource` (local, entra, ldap) and the directory's stable id (Entra object id `oid`, AD `objectGUID`) once linked; later sign-ins match by that id first, then by email. Directory users have no usable local password; "Şifremi unuttum" and "Şifre değiştir" are hidden for them.
- **Lockout and rate limits** apply to LDAP attempts like local ones; SSO relies on Entra.
- **Session:** after a directory sign-in TGYSM issues its own cookie session exactly as today (same claims, security stamp, antiforgery), so the rest of the app does not change.

## Constraints

- Secrets encrypted at rest with the existing Data Protection key ring (`DataProtection:KeysPath`, ADR-0024), which is shared by API and Worker and backed up.
- Settings live in the database (one settings row), so API instances read the same values; changes take effect without restart (options reloaded on save).
- The OIDC redirect URI uses `App:PublicBaseUrl` (HTTPS in production behind nginx; forwarded headers already trusted per ADR-0024).
- LDAP must work inside the Linux Docker image (`tgysm-api`); a library or system package choice must account for that.
- Usual gates: zero warnings, coverage ≥ 80 %, ESLint/Astryx doctor, E2E; new error codes and audit actions need Turkish text; `tools/gen-api-types.sh` after API changes.
- An ADR records the decision (it changes ADR-0004, local accounts only).
- Docs (`SECURITY.md`, `capabilities.md`, operator manual, user guide) only on request.

## Left to the builder

- OIDC and LDAP libraries (e.g. ASP.NET Core OpenIdConnect handler with options read from the database; `System.DirectoryServices.Protocols` vs a managed LDAP client — pick what runs in the Docker image).
- Exact table and column names, the settings API shape, the test-connection details.
- How LDAP users are found (search by `sAMAccountName` or `userPrincipalName`/`mail` with the service account, then bind as the user).
- Whether break-glass is a separate route or a toggle on the login page.
- E2E strategy: a fake OIDC provider and an LDAP test container (e.g. OpenLDAP) in CI, or contract-level tests where a real directory is not possible.
