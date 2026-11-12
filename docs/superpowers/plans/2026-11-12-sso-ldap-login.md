# Login with Microsoft (SSO) or Active Directory (LDAP) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The admin picks one login method in the panel (Yerel, Microsoft Entra ID via OpenID Connect, or Active Directory via LDAP), configures and tests it there, and employees sign in with their company account; local admins keep a break-glass login.

**Architecture:** One `auth_settings` row holds the method and the provider settings (secrets protected with Data Protection). A shared `DirectorySignInHandler` turns a verified directory identity into a TGYSM user (link by stored id, else by email inside the company; create as `employee` otherwise) and returns the existing `SessionUser`, so the cookie session is unchanged. LDAP goes through an `ILdapDirectory` port (managed client, runs in the Linux image); Entra goes through the ASP.NET Core OpenID Connect handler whose options are built from the database row at challenge time.

**Tech Stack:** .NET 10 Minimal APIs, EF Core + Npgsql, `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.x, `Novell.Directory.Ldap.NETStandard` (managed LDAP; pick the latest stable on nuget.org), Data Protection; React 19 + Astryx, react-hook-form + zod, TanStack Query.

**Spec:** `docs/superpowers/specs/2026-11-12-sso-ldap-login-design.md`

## Global Constraints

- Branch `feature/sso-ldap-login`; commit per task; stage named paths; no AI attribution; merge to `master` only with owner approval.
- Gates per task: `tools/lint.sh` exit 0, affected tests green; before the last task `tools/test.sh` and `tools/e2e.sh` (stop/start dev services).
- New error codes → `ErrorCodes` + `errors.json`; new audit actions → `AuditActions` + `admin.json` (`TranslationCoverageTests`). After API changes `tools/gen-api-types.sh`.
- Secrets (client secret, bind password) never returned by any API, never logged, never in audit values; stored only as Data Protection ciphertext with purpose `Tgysm.AuthSettings`.
- LDAP: `ldaps` (default, port 636) or `starttls` (port 389); a plain connection is impossible to configure.
- Redirect URI: `{App:PublicBaseUrl}/api/v1/auth/oidc/callback`.
- Directory-created users: role `employee`, department and location empty, empty `PasswordHash`.
- Local admin break-glass works under every method; non-admin local passwords do not work while `entra` or `ldap` is active.
- Email linking only for company identities: Entra `tid` equals the configured tenant and the email domain is in the allowed domain list; LDAP entries under the configured base DN.
- LDAP lockout: 5 failures per normalized identifier → locked for `User.LockoutDuration`; same response for unknown names.
- UI per `frontend/CLAUDE.md`; Turkish strings only in `locales/tr`.
- ADR-0030 records the decision (amends ADR-0004).

## Review Focus

1. Admin switches the method while the provider is down or misconfigured: switching must be refused until the current settings passed "Bağlantıyı test et", and break-glass must still let an admin in afterwards. Test in Task 6.
2. A settings PUT that leaves a secret field empty must keep the stored secret; a PUT that changes host/tenant must invalidate the earlier successful test. Tests in Tasks 2 and 6.
3. A user deactivated in TGYSM signs in through Entra or LDAP: refused, audited, no session. Test in Task 3.
4. The same person signs in by email `Ahmet.Yilmaz@Sirket.com` and by `ahmet.yilmaz` (sAMAccountName): one TGYSM user, not two. Test in Task 4.
5. An Entra token from another tenant with an allowed-looking email: not linked, not created. Test in Task 5.

---

### Task 1: Domain — login method, settings entity, user directory links

**Files:**
- Create: `backend/src/Tgysm.Domain/Auth/LoginMethod.cs`, `LdapSecurity.cs`, `AuthSettings.cs`
- Modify: `backend/src/Tgysm.Domain/Users/User.cs`
- Modify: `backend/src/Tgysm.Infrastructure/Persistence/AppDbContext.cs`, a configuration file for `AuthSettings`, user configuration (unique indexes)
- Create: migration via `tools/add-migration.sh AuthSettings`
- Test: `backend/tests/Tgysm.Domain.Tests/Auth/AuthSettingsTests.cs`, `backend/tests/Tgysm.Domain.Tests/UserTests.cs`

**Interfaces:**
- Produces: `enum LoginMethod { Local, Entra, Ldap }`; `enum LdapSecurity { Ldaps, StartTls }`.
- `AuthSettings` (single row, `Id` fixed `AuthSettings.SingletonId`): `Method`; Entra: `EntraTenantId`, `EntraClientId`, `EntraClientSecretProtected`, `EntraAllowedDomains` (string list, lower-case, no `@`); LDAP: `LdapHost`, `LdapPort`, `LdapSecurity`, `LdapBaseDn`, `LdapBindDn`, `LdapBindPasswordProtected`, `LdapUserFilter` (nullable); `EntraTestedFingerprint`, `LdapTestedFingerprint` (nullable strings); `UpdatedAt`; `xmin` concurrency. Methods: `static AuthSettings CreateDefault(DateTimeOffset now)` (method Local), `void UpdateEntra(string tenantId, string clientId, string? protectedSecret, IReadOnlyList<string> allowedDomains, DateTimeOffset now)` (null secret = keep), `void UpdateLdap(string host, int port, LdapSecurity security, string baseDn, string bindDn, string? protectedPassword, string? userFilter, DateTimeOffset now)`, `string Fingerprint(LoginMethod method)` (SHA-256 hex over the method's non-secret fields plus the protected secret), `void MarkTested(LoginMethod method)`, `bool IsTested(LoginMethod method)` (stored fingerprint equals current), `void SwitchTo(LoginMethod method, DateTimeOffset now)` (throws `DomainException(ErrorCodes.AuthMethodNotTested)` when target ≠ Local and not tested).
- `User` gains `EntraObjectId` (string?, unique), `LdapObjectGuid` (Guid?, unique), `bool HasLocalPassword => PasswordHash != string.Empty`, `static User CreateFromDirectory(string firstName, string lastName, string email, DateTimeOffset now)` (employee, active, empty hash, new security stamp), `void LinkEntra(string objectId, DateTimeOffset now)`, `void LinkLdap(Guid objectGuid, DateTimeOffset now)`, `void RefreshFromDirectory(string firstName, string lastName, string email, DateTimeOffset now)` (does not touch role/department/location/password).
- Error codes: `AuthMethodNotTested = "auth.method_not_tested"` ("Önce bağlantıyı test edin; test başarılı olmadan bu yöntem açılamaz.").

- [ ] **Step 1: Failing tests:** default is Local; `SwitchTo(Ldap)` without test throws `auth.method_not_tested`; after `MarkTested(Ldap)` it switches; changing `LdapHost` after `MarkTested` makes `IsTested(Ldap)` false; `UpdateEntra(..., protectedSecret: null, ...)` keeps the old secret; allowed domains are stored lower-case without `@`; `CreateFromDirectory` gives `Employee`, empty `PasswordHash`, `HasLocalPassword == false`; `RefreshFromDirectory` keeps role and password hash.
- [ ] **Step 2: Run** `tools/with-db.sh dotnet test --solution backend/Tgysm.slnx --filter-class "*AuthSettingsTests" --filter-class "*UserTests" --ignore-exit-code 8` — FAIL (types missing).
- [ ] **Step 3: Implement**, add EF mapping (`auth_settings`, `allowed_domains` as `text[]`), unique partial indexes on the two user columns, migration; the migration inserts the default row (method `local`).
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `feat(domain): login method settings and directory links on users`.

### Task 2: Settings API with write-only secrets

**Files:**
- Create: `backend/src/Tgysm.Application/Abstractions/IAuthSecretProtector.cs`, `backend/src/Tgysm.Infrastructure/Security/AuthSecretProtector.cs`
- Create: `backend/src/Tgysm.Application/Features/Auth/Settings/GetAuthSettings.cs`, `UpdateAuthSettings.cs`, `AuthSettingsDtos.cs`
- Create: `backend/src/Tgysm.Api/Endpoints/AuthSettingsEndpoints.cs`; Modify: `ApiRoutes.cs`, endpoint registration
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Auth/AuthSettingsTests.cs`

**Interfaces:**
- Consumes: Task 1 `AuthSettings`.
- Produces: `IAuthSecretProtector { string Protect(string secret); string Unprotect(string protectedSecret); }` (purpose `Tgysm.AuthSettings`).
- `AuthSettingsDto(LoginMethod Method, EntraSettingsDto Entra, LdapSettingsDto Ldap, string RedirectUri, uint Version)`; `EntraSettingsDto(string TenantId, string ClientId, bool HasClientSecret, IReadOnlyList<string> AllowedDomains, bool IsTested)`; `LdapSettingsDto(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, bool HasBindPassword, string? UserFilter, bool IsTested)`.
- `PUT /api/v1/admin/auth-settings/entra` body `{ tenantId, clientId, clientSecret?, allowedDomains[], version }`; `PUT /api/v1/admin/auth-settings/ldap` body `{ host, port, security, baseDn, bindDn, bindPassword?, userFilter?, version }`; `GET /api/v1/admin/auth-settings`. Admin only. Stale version → 409 (existing concurrency code).
- Audit: `AuditActions.AuthSettingsUpdated = "auth.settings_updated"` with the changed non-secret field names only.

- [ ] **Step 1: Failing tests:** GET returns no field containing a secret value (assert the raw JSON does not contain the posted secret string) and `hasClientSecret: true` after saving one; PUT without `clientSecret` keeps `hasClientSecret: true`; the stored column is not the plain secret (read the row); employee/IT → 403; validation: LDAP `port` 1–65535, required host/baseDn/bindDn, Entra tenant id is a GUID, client id required, domains look like `sirket.com`; audit row has no secret.
- [ ] **Step 2–4:** run (FAIL) → implement → run (PASS); `tools/gen-api-types.sh`.
- [ ] **Step 5: Commit** `feat(api): admin login method settings with write-only secrets`.

### Task 3: Directory sign-in core (link, create, refresh)

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Auth/Directory/DirectoryIdentity.cs`, `DirectorySignInHandler.cs`
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Auth/DirectorySignInTests.cs`

**Interfaces:**
- Produces: `record DirectoryIdentity(LoginMethod Source, string ExternalId, string Email, string FirstName, string LastName)`; `DirectorySignInHandler.HandleAsync(DirectoryIdentity identity, CancellationToken ct) -> Task<SessionUser>`; throws `AuthenticationFailedException` for every refusal.
- Rules: find by `EntraObjectId`/`LdapObjectGuid` for the source; else by `NormalizedEmail` and link; else create with `User.CreateFromDirectory`. Refresh names/email on each sign-in (an email taken by another user → refuse and audit, do not overwrite). Inactive or locked user → refuse. Audit: `auth.directory_sign_in` (source), `users.created_from_directory`, failures as `LoginFailed` with the source.
- Company checks happen in the callers (Tasks 4, 5); this handler trusts its input.

- [ ] **Step 1: Failing tests:** new identity → employee created with names/email, no department/location, `HasLocalPassword` false, session returned; existing local employee with same email (case differs) → linked, same id; existing admin with same email → linked, role stays admin, password hash unchanged; second sign-in matches by external id even after the email changed in the directory (email refreshed); deactivated user → `AuthenticationFailedException` and audit `LoginFailed`; refreshed email colliding with another user → refused.
- [ ] **Step 2–4:** run → implement → run.
- [ ] **Step 5: Commit** `feat(app): directory sign-in links, creates and refreshes users`.

### Task 4: LDAP login

**Files:**
- Create: `backend/src/Tgysm.Application/Abstractions/ILdapDirectory.cs`; `backend/src/Tgysm.Infrastructure/Directory/NovellLdapDirectory.cs`
- Create: `backend/src/Tgysm.Application/Features/Auth/Directory/LdapLoginHandler.cs`, `LdapSignInThrottle.cs` (+ table `ldap_sign_in_failures (key text PK, failed_count int, lockout_end timestamptz null)` via migration)
- Modify: `AuthEndpoints.cs` (`POST /api/v1/auth/login/ldap`, rate limit `Login`)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Auth/LdapLoginTests.cs` (fake `ILdapDirectory` registered in `ApiFactory`), `backend/tests/Tgysm.Api.IntegrationTests/Auth/NovellLdapDirectoryTests.cs` (runs only when `TGYSM_TEST_LDAP_URL` is set; skipped otherwise)

**Interfaces:**
- Produces: `ILdapDirectory { Task<LdapResult> AuthenticateAsync(LdapConnectionSettings settings, string identifier, string password, CancellationToken ct); Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken ct); }`; `record LdapConnectionSettings(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, string BindPassword, string? UserFilter)`; `LdapResult` = success with `DirectoryIdentity` or failure kind (`InvalidCredentials`, `NotFound`, `Unavailable`); `LdapTestResult(bool Ok, string? ErrorCode)`.
- Adapter: bind as service account, search under base DN with `(&(objectClass=user)(|(sAMAccountName={id})(userPrincipalName={id})(mail={id})){userFilter})` (escape the identifier per RFC 4515), then bind as the found DN with the password; map `objectGUID`, `givenName`, `sn`, `mail` (fallback `userPrincipalName`). TLS certificate validation stays on.
- `LdapLoginHandler.HandleAsync(LdapLoginCommand(string Identifier, string Password), CancellationToken ct) -> SessionUser`: only when method is `Ldap` (else `AuthenticationFailedException`); throttle key = `ldap:` + identifier trimmed and lower-cased; 5 failures → lockout; `Unavailable` → `ErrorCodes.AuthDirectoryUnavailable = "auth.directory_unavailable"` ("Dizin sunucusuna ulaşılamıyor. Daha sonra yeniden deneyin.") with 503.

- [ ] **Step 1: Failing tests (fake directory):** good credentials → 200 and cookie; `ahmet.yilmaz` and `Ahmet.Yilmaz@Sirket.com` for the same directory entry → one TGYSM user (Review Focus 4); wrong password 5 times → 6th refused even with the right password, and unknown names behave the same (same status and body); directory unavailable → 503 `auth.directory_unavailable`; method `Local` → the LDAP endpoint refuses.
- [ ] **Step 2–4:** run → implement (adapter + handler + throttle + endpoint) → run.
- [ ] **Step 5: Commit** `feat(auth): Active Directory login over LDAPS or StartTLS`.

### Task 5: Microsoft Entra ID (OpenID Connect)

**Files:**
- Modify: `backend/Directory.Packages.props`, `Tgysm.Api.csproj` (OpenIdConnect package)
- Create: `backend/src/Tgysm.Api/Auth/EntraOptionsSetup.cs` (`IConfigureNamedOptions<OpenIdConnectOptions>` reading `auth_settings`), `backend/src/Tgysm.Api/Auth/EntraEvents.cs`
- Create: `backend/src/Tgysm.Application/Features/Auth/Directory/EntraIdentityMapper.cs`
- Create: `backend/src/Tgysm.Application/Abstractions/IEntraConnectionTester.cs`, `backend/src/Tgysm.Infrastructure/Directory/EntraConnectionTester.cs`
- Modify: `AuthEndpoints.cs` (`GET /api/v1/auth/oidc/start`), `ApiServiceCollectionExtensions.cs` (scheme `entra`, callback path `/api/v1/auth/oidc/callback`, `SignInScheme` = cookie)
- Test: `backend/tests/Tgysm.Application.Tests/Auth/EntraIdentityMapperTests.cs`, `backend/tests/Tgysm.Api.IntegrationTests/Auth/EntraStartTests.cs`

**Interfaces:**
- `EntraIdentityMapper.Map(ClaimsPrincipal principal, string configuredTenantId, IReadOnlyList<string> allowedDomains) -> DirectoryIdentity?`: requires `tid` == configured tenant and `oid`; email from `email`, else `preferred_username`, else `upn`; domain must be in `allowedDomains` (empty list = any domain of the tenant); names from `given_name`/`family_name`, fallback split of `name`. Returns null when any check fails.
- On `OnTicketReceived`: map → `DirectorySignInHandler` → replace the ticket principal with `SessionClaims.CreatePrincipal(session)`; on refusal redirect to `/login?error=sso` and audit. Options are re-read per challenge; saving settings calls `IOptionsMonitorCache<OpenIdConnectOptions>.TryRemove("entra")`.
- `GET /api/v1/auth/oidc/start` → challenge (302 to `https://login.microsoftonline.com/{tenant}/v2.0/...`) only when method is `Entra`, else 404.
- `IEntraConnectionTester.TestAsync(tenantId, clientId, clientSecret, ct) -> (bool Ok, string? ErrorCode)`: fetch the tenant's OpenID metadata, then a client-credentials token request with the secret (proves tenant, client id and secret). Error codes `auth.entra_tenant_not_found`, `auth.entra_client_invalid`, `auth.entra_secret_invalid`, `auth.directory_unavailable`.

- [ ] **Step 1: Failing tests:** mapper: other tenant → null (Review Focus 5); email from `preferred_username` when `email` missing; disallowed domain → null; names fallback; start endpoint with method Entra and a saved tenant/client → 302 whose `Location` contains the client id and the exact redirect URI; method Local → 404.
- [ ] **Step 2–4:** run → implement → run; `tools/gen-api-types.sh`.
- [ ] **Step 5: Commit** `feat(auth): Microsoft Entra ID sign-in configured from the database`.

### Task 6: Test, switch, break-glass and account rules

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Auth/Settings/TestAuthSettings.cs`, `SwitchLoginMethod.cs`, `GetLoginMethod.cs`
- Modify: `Login.cs` (break-glass rule), `ForgotPassword.cs`, `ChangePassword.cs`, `GetMe.cs` (`HasLocalPassword`), `Users/CreateUser.cs`, `AuthSettingsEndpoints.cs`, `AuthEndpoints.cs` (`GET /api/v1/auth/method`, anonymous)
- Test: `backend/tests/Tgysm.Api.IntegrationTests/Auth/LoginMethodSwitchTests.cs`, existing login/forgot/create-user tests

**Interfaces:**
- `POST /api/v1/admin/auth-settings/test` body `{ method }` → `{ ok, errorCode? }`; success calls `MarkTested(method)`; audit `auth.settings_tested`.
- `GET /api/v1/admin/auth-settings/switch-preview?method=local` → `{ usersWithoutPassword: int }`.
- `POST /api/v1/admin/auth-settings/method` body `{ method, version }` → switches (`auth.method_not_tested` 400 when not tested); switching to `Local` issues a `Reset` set-password email (existing `PasswordTokenIssuer` + outbox) to every active user without a local password; audit `auth.method_changed`.
- `GET /api/v1/auth/method` (anonymous) → `{ method }`.
- `LoginHandler`: when method ≠ Local, only users with role `Admin` and a local password may succeed (others fail generically, audit `LoginFailed` with reason `method`).
- `ForgotPassword`: no email for users without a local password while method ≠ Local. `ChangePassword`: users without a local password get `auth.no_local_password`. `MeDto` gains `bool HasLocalPassword`.
- `CreateUser` while method ≠ Local: no temporary password, no activation email, empty hash; the response says `credentialDelivery: "directory"`.

- [ ] **Step 1: Failing tests:** switch to LDAP before a test → 400 `auth.method_not_tested`; test OK → switch OK; change LDAP host after the test → switch refused again (Review Focus 2); with method LDAP an employee's local password login fails and an admin's succeeds (Review Focus 1); switch-preview counts directory-only users; switching to Local enqueues one reset email per directory-only active user; forgot-password for a directory-only user sends nothing; `me.hasLocalPassword` false for them; create user under LDAP sends no email.
- [ ] **Step 2–4:** run → implement → run; `tools/gen-api-types.sh`.
- [ ] **Step 5: ADR-0030** "Login method chosen in the admin panel: local, Entra ID (OIDC) or Active Directory (LDAP)" (amends ADR-0004), index row; commit with the SHA as in earlier ADRs.
- [ ] **Step 6: Commit** `feat(auth): test, switch and break-glass rules for the login method`.

### Task 7: Login page

**Files:**
- Modify: `frontend/src/features/auth/LoginPage.tsx`, `api.ts` (`useLoginMethod`, `useLdapLogin`), `ChangePasswordPage.tsx` / profile menu (hide when `!hasLocalPassword`), `locales/tr/auth.json` (or the namespace the page uses)
- Test: `frontend/src/features/auth/LoginPage.test.tsx`

**Interfaces:**
- Method `local`: today's form. `ldap`: same form, field label "Kullanıcı adı veya e-posta", posts to `/auth/login/ldap`. `entra`: one primary button "Microsoft ile giriş yap" linking to `/api/v1/auth/oidc/start` (full page navigation), plus `?error=sso` banner "Microsoft ile giriş yapılamadı. Hesabınızın yetkili olduğundan emin olun.". Under `ldap`/`entra` a small link "Yerel yönetici girişi" shows the local email/password form. "Şifremi unuttum" shows only for `local` and the break-glass form.

- [ ] **Step 1–4:** tests per method (button/link/label/endpoint), error banner, break-glass form posts to `/auth/login`; run → implement → run.
- [ ] **Step 5: Commit** `feat(frontend): login page follows the chosen login method`.

### Task 8: Admin "Giriş yöntemi" page and user screens

**Files:**
- Create: `frontend/src/features/admin/auth/LoginMethodPage.tsx`, `EntraSettingsForm.tsx`, `LdapSettingsForm.tsx`, `api.ts`, tests
- Modify: `frontend/src/shared/routes.ts`, `navigation/routeMeta.ts` (route `/admin/login-method`, same admin group as `/admin/system`), `app/router.tsx`, `features/admin/users/UserFormDialog.tsx` (note under directory methods), `locales/tr/admin.json`
- Test: `frontend/src/features/admin/auth/LoginMethodPage.test.tsx`

**Interfaces:**
- Page: current method (radio-like `SegmentedControl` or `Selector`), one card per provider with its form (`FormDialog`-less inline forms, discard guard on route leave), secret fields empty with "Kayıtlı" hint and placeholder "Değiştirmek için yazın", "Kaydet", "Bağlantıyı test et" (shows success or the Turkish error), Entra card shows the redirect URI read-only with a copy button, "Bu yöntemi aç" enabled only when that provider `isTested`; switching to Yerel opens a confirm naming `usersWithoutPassword`.
- User create dialog under `ldap`/`entra`: text "Kullanıcı ilk girişinde şirket hesabıyla bağlanır; e-posta gönderilmez." and no credential delivery choice.

- [ ] **Step 1–4:** tests: secrets never prefilled; saving without typing a secret sends no secret field; test button result shown; switch button disabled until tested; Yerel confirm shows the count; menu entry present for admins only; run → implement → run.
- [ ] **Step 5: Commit** `feat(frontend): admin login method page`.

### Task 9: Gates, E2E and handover

- [ ] **Step 1:** E2E `frontend/e2e/login-method.spec.ts`: admin opens "Giriş yöntemi", saves LDAP settings (fake values), "Bu yöntemi aç" stays disabled, switching is not possible; login page still shows the local form. (A live Entra/LDAP sign-in is not testable in CI; covered by integration tests with fakes and the optional `TGYSM_TEST_LDAP_URL` contract test.)
- [ ] **Step 2:** `tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh` → exit 0; restart dev services.
- [ ] **Step 3:** Tell the owner which docs need lines: `SECURITY.md` (auth posture, secrets), `capabilities.md`, `docs/operations.md` (Entra app registration, LDAPS certificate trust in the container), user guide.
