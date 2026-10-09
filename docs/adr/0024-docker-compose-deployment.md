# ADR-0024: Docker Compose Deployment with Shared Data Protection Keys

**Date**: 2026-10-09
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The first release needs a reproducible way to run TGYSM on one company host (`tgysm.md` §19, §21: hosting was undecided). The owner chose Docker Compose on 2026-10-09 (`docs/superpowers/specs/2026-10-09-phase-6-release-design.md` §2). Three things were open in the code: Data Protection kept its key ring in the framework default (lost with a container, and not shared between the API and the Worker, which must read the account-link secrets the API protects — ADR-0015, ADR-0017; flagged in `SECURITY.md`), forwarded headers were trusted only in Development, and migrations ran only through the EF Core tool. Related: ADR-0001 (modular monolith), ADR-0002 (PostgreSQL), ADR-0012 (file store), ADR-0014 (Worker).

## Decision

- One multi-stage `Dockerfile` builds two images: `tgysm-api` (published API with the Vite build in `wwwroot`; the API keeps serving the SPA) and `tgysm-worker`. Both use the ASP.NET runtime image (the Worker needs it for Data Protection), run as the non-root `app` user, and carry no secret. `tools/docker-build.sh` tags them with the Git short hash and `latest`.
- `deploy/compose.yaml` runs `postgres` (17), a one-shot `migrate` (new API CLI command `migrate`), `api`, `worker` and `nginx`. `api` and `worker` start only after `migrate` completed successfully. Settings come from `deploy/.env` (git-ignored; `deploy/.env.example` lists every key). `tools/compose.sh` always passes the compose file and the env file; `tools/deploy.sh` runs the gates, builds the images and starts the stack.
- nginx terminates TLS with the company certificate mounted from `deploy/certs/` (a self-signed pair from `tools/gen-self-signed-cert.sh` for test hosts), redirects HTTP to HTTPS, and resolves the `api` container through Docker's DNS so a recreated container is found again.
- New configuration `DataProtection:KeysPath`: the key ring is stored in that folder (a volume shared by the API and the Worker, mode 0700, owned by `app`). It is required outside Development; a missing or unwritable folder stops the host at startup.
- New configuration `Http:KnownProxyNetworks` (CIDR): outside Development the API applies `X-Forwarded-For` and `X-Forwarded-Proto` only from these networks; the compose file fixes the internal subnet and passes it. The login rate limit then keys on the real client address, and a client outside the network cannot spoof it.

## Alternatives Considered

### Alternative 1: Plain Linux host with systemd and nginx
- **Pros**: no container runtime; closest to the existing local setup.
- **Cons**: every host is configured by hand; .NET, Node and PostgreSQL versions drift per server; no single artifact to roll back.
- **Why not**: the owner chose Compose; images give one tested artifact per commit.

### Alternative 2: nginx serves the SPA, the API serves only `/api`
- **Pros**: static files from nginx.
- **Cons**: two places that must agree on routes and the SPA fallback; the API already serves the SPA with the correct 404 rules for `/api`.
- **Why not**: no gain at single-company scale.

### Alternative 3: API and Worker in one container
- **Pros**: one service fewer.
- **Cons**: needs a process supervisor; mixed logs; cannot restart or scale one side.
- **Why not**: ADR-0014 keeps them separate hosts.

### Alternative 4: Encrypt the key ring at rest with a certificate
- **Pros**: a copy of the keys volume or a backup alone does not reveal the keys.
- **Cons**: one more secret to issue, rotate and back up; losing it makes every key unreadable.
- **Why not now**: the keys protect short-lived account-link tokens (72 h / 1 h); the folder is private to the app user and backups are access-restricted (ADR-0025). Can be added later through configuration.

## Consequences

### Positive
- A clean host needs only Docker, `deploy/.env` and the certificate; `tools/deploy.sh` does the rest.
- Sessions and pending account-link emails survive restarts, recreated containers and restores (keys persisted and backed up).
- Rate limiting and HTTPS detection are correct behind the proxy; forwarded headers from elsewhere are ignored.

### Negative
- Docker becomes a requirement for production and for the deployment tasks on development machines (Colima on macOS).
- The Data Protection key ring is stored unencrypted inside the keys volume (framework warning at startup).

### Risks
- Anyone with read access to the keys volume or to a backup archive can unprotect account-link tokens until they expire; keep host and backup access restricted (`SECURITY.md`).
- A wrong `TGYSM_SUBNET` makes the API ignore nginx's headers: sign-in then fails because the antiforgery cookie requires HTTPS.

Implemented in commits `0c4ef18` (migrate command), `ec9d625` (key folder), `40b1995` (proxy trust), `2c7559f` (images) and the Compose commit of Task 5.
