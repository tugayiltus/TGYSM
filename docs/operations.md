# TGYSM Operator Manual

For the person who installs, updates, backs up and restores a TGYSM host. The stack is one Docker Compose project on one host (ADR-0024); backups and restore follow ADR-0025. End-user help is in [`docs/user-guides/`](user-guides/README.md).

Every command below runs from the repository root on the host. `tools/compose.sh` is `docker compose -f deploy/compose.yaml --env-file deploy/.env`; any `docker compose` subcommand works through it.

## Stack at a glance

| Service | Image | Role |
|---|---|---|
| `postgres` | `postgres:17` | Database `tgysm`, user `tgysm`, volume `pgdata`. Not published to the host. |
| `migrate` | `tgysm-api` | One-shot `migrate` command: EF Core migrations plus post-migration data steps. `api` and `worker` start only after it exits 0. |
| `api` | `tgysm-api` | API and the web app on HTTP `8080` inside the network. Health check `GET /api/v1/health`. |
| `worker` | `tgysm-worker` | Background jobs (outbox, email, SLA monitor, exports, cleanup). No network listener. |
| `nginx` | `nginx:stable` | TLS on `HTTPS_PORT` (default 443); `HTTP_PORT` (default 80) redirects to HTTPS; proxies to `api:8080`. |
| `backup` | `postgres:17` | Nightly backup at `BACKUP_TIME`. |
| `restore` | `postgres:17` | Profile `restore` only; started by `tools/restore.sh`. |

Volumes: `pgdata` (database), `files` (attachments, export files, pickup mail; `/data/files`), `keys` (Data Protection key ring; `/data/keys`), `backups` (archives, unless `BACKUP_HOST_DIR` is set).

`tgysm-api` and `tgysm-worker` run as the non-root `app` user (uid 1654) and contain no secrets. All settings come from `deploy/.env` at run time.

## Host requirements

- Linux host (on-premises server or cloud VM) with Docker Engine and the Docker Compose v2 plugin (`docker compose`).
- `git` (images are tagged with the Git short hash of the checkout), `bash`, `curl`, `openssl` (only for a self-signed test certificate).
- Inbound TCP on `HTTP_PORT` and `HTTPS_PORT`. Outbound access to the SMTP relay (`SMTP_HOST:SMTP_PORT`).
- A DNS name for the host that matches the certificate and `PUBLIC_BASE_URL`.
- The subnet `TGYSM_SUBNET` (default `172.28.0.0/24`) must not overlap a network the host already uses.
- Running the quality gates on the host (`tools/deploy.sh` without `--skip-gates`) additionally needs the development toolchain: .NET 10 SDK, Node 22 and a PostgreSQL 17 for the tests. A production host normally deploys with `--skip-gates` a commit whose CI run (`.github/workflows/ci.yml`) is green.
- CPU, memory and disk sizing: TODO: decide after the pilot. Reference measurement: `docs/release/performance.md` (2,000 tickets, 500 assets).
- On macOS with Colima (development machines only), Docker shares only the home folder with containers: keep the repository and any archive you restore under your home folder.

## First install

1. Clone the repository and check out the release commit.
2. Create the settings file and fill it in:

   ```bash
   cp deploy/.env.example deploy/.env
   chmod 600 deploy/.env
   ```

   | Key | Meaning |
   |---|---|
   | `TGYSM_DEPLOYMENT` | Unset on a production host. `pilot` only on a pilot or test host: it allows `tools/seed-pilot.sh --compose` (see Pilot data). |
   | `POSTGRES_PASSWORD` | Password of the `tgysm` database user. Long random value. |
   | `PUBLIC_BASE_URL` | Absolute `https://` address users open; used for links in email (`App:PublicBaseUrl`). |
   | `HTTP_PORT`, `HTTPS_PORT` | Host ports of nginx (80, 443). |
   | `TZ`, `APP_TIME_ZONE` | Container clock (backup schedule) and business time zone (`App:TimeZone`); default `Europe/Istanbul`. |
   | `TGYSM_SUBNET` | Internal Docker network; the API trusts `X-Forwarded-For` / `X-Forwarded-Proto` only from it (`Http:KnownProxyNetworks`). |
   | `EMAIL_MODE` | `Smtp` in production; `Pickup` writes `.eml` files to `/data/files/email-pickup` (test hosts). |
   | `EMAIL_FROM` | Sender, e.g. `TGYSM <no-reply@example.com>`. |
   | `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_STARTTLS`, `SMTP_USERNAME`, `SMTP_PASSWORD` | SMTP relay (`Email:Smtp:*`). |
   | `BACKUP_TIME`, `BACKUP_RETENTION_DAYS` | Nightly backup time (`HH:MM` in `TZ`, default `02:30`) and retention in days (default 14). |
   | `BACKUP_HOST_DIR` | Optional absolute host folder for archives; empty means the `backups` volume. |

   `deploy/.env` is git-ignored. It holds the database and SMTP passwords: keep it readable by the operator account only.

   `Files:RootPath=/data/files` and `DataProtection:KeysPath=/data/keys` are set in the images and need no entry.
3. Put the certificate in place. nginx reads exactly `deploy/certs/tgysm.crt` (certificate with its chain) and `deploy/certs/tgysm.key` (private key), mounted read-only. Both are git-ignored.

   ```bash
   chmod 600 deploy/certs/tgysm.key
   ```

   For a test host without a company certificate: `tools/gen-self-signed-cert.sh <host-name>` (default `localhost`; valid 365 days). Browsers warn about a self-signed certificate.
4. Deploy:

   ```bash
   tools/deploy.sh --skip-gates   # or tools/deploy.sh to run tools/lint.sh and tools/test.sh first
   ```

   It refuses when `deploy/certs/tgysm.crt`, `deploy/certs/tgysm.key` or `deploy/.env` is missing, or when the working tree has uncommitted changes (the image tag must name the code it holds). It builds both images on fresh base images (`tools/docker-build.sh`, tags `<git short hash>` and `latest`), records the hash in `deploy/.version` (git-ignored), pulls `postgres` and `nginx`, runs `tools/compose.sh up -d --remove-orphans` and waits up to 180 s for `https://localhost:<HTTPS_PORT>/api/v1/health` through nginx. Exit 0 prints `TGYSM is up on …`.

   Image tag: every `tools/compose.sh` call (and so `tools/backup.sh`, `tools/restore.sh`) uses the tag in `deploy/.version`; a `TGYSM_VERSION` set in the shell overrides it. Without the file the tag is `latest`. Container logs rotate (`json-file`, 5 files of 10 MB per container).
5. Create the first admin (works once; refused when any user exists, ADR-0007). The temporary password is printed once and must be changed at the first sign-in:

   ```bash
   tools/compose.sh run --rm api setup-admin --email <email> --first-name <name> --last-name <name>
   ```

6. Sign in at `PUBLIC_BASE_URL` and follow the first-setup guide page ([`docs/user-guides/first-setup.md`](user-guides/first-setup.md)): departments, locations, intake team, approvers, categories, request types, SLA calendar and policy, device catalog, CSV import, users.
7. Take a first backup (`tools/backup.sh`) and copy it off the host (see Backups).

A locked-out admin gets a new temporary password from the console:

```bash
tools/compose.sh run --rm api reset-password --email <email>
```

### Pilot data (pilot hosts only)

`tools/seed-pilot.sh --compose [--scale <factor>]` runs the `seed-pilot` command in a one-off `api` container with `ASPNETCORE_ENVIRONMENT=Pilot`. Because it sets that environment itself, the command's environment check does not protect a deployed host; the marker does: `--compose` refuses unless `deploy/.env` has `TGYSM_DEPLOYMENT=pilot`. Set it only on a pilot host. The command refuses once the database has a ticket, and writes about 50 employees, 8 IT staff, 500 assets and 2,000 tickets over the past 90 days through the application handlers (`--scale` multiplies the volumes, at most 10). Accounts use the domain `pilot.example.com`; the pilot password is printed once.

- The seed closes the notification outbox rows it wrote, so those are never sent. The Worker's SLA monitor still raises breach notifications later for the open seeded tickets, addressed to `@pilot.example.com`. While only seed data exists, run the pilot host with `EMAIL_MODE=Pickup` or with a relay that drops that domain.
- A failed seed keeps what it wrote (its queued notifications are still closed). Take a backup before seeding; to start over, restore that backup (`tools/restore.sh <archive> --force`).
- A seeded host is never promoted to production: the seeded accounts share one printed password. To go live, reinstall with empty volumes (recommended), or at least create a real admin, deactivate every seeded `@pilot.example.com` account and remove `TGYSM_DEPLOYMENT` from `deploy/.env`.

## Updates

1. Back up first: `tools/backup.sh`. Copy the archive off the host. This archive is the way back if the update must be undone (below).
2. Note the running image tag: `cat deploy/.version` (all built tags: `docker image ls tgysm-api`).
3. Get the new release: `git pull` (or check out the release tag/commit).
4. `tools/deploy.sh --skip-gates`. The `migrate` service runs before `api` and `worker`; when a migration fails, `api` and `worker` do not start and `tools/compose.sh logs migrate` names the failing migration (`Migration failed: …`).
5. Check health (below) and `/admin/system`.

### Rollback

Images of earlier builds stay on the host under their Git short hash until removed.

- **No new migration in the update** (`tools/compose.sh logs migrate` shows `Database is up to date (…)`): pin the previous tag and start it. `tools/deploy.sh` printed it (`Deploying <new> (previous: <old>)`).

  ```bash
  echo <previous short hash> > deploy/.version
  tools/compose.sh up -d
  ```

- **The update applied migrations** (`Applied N migration(s); latest …`): the older image does not know the new schema, and migrations are never rolled back. Pin the previous tag in `deploy/.version` as above, then restore the archive taken in step 1:

  ```bash
  tools/restore.sh <path/to/pre-update-archive.tar.gz> --force
  ```

  Everything written after that backup is lost. `tools/restore.sh` asks the pinned image for its newest migration (`migrate --print-latest`), so pin the tag before restoring; an archive from a newer build is refused (exit 3).

The next `tools/deploy.sh` overwrites `deploy/.version` with its own hash, so `tools/compose.sh` and `tools/restore.sh` always use the images that run. Do not set `TGYSM_VERSION` in `deploy/.env`: `deploy/.version` overrides it.

## Backups

- **What:** one archive `tgysm-YYYYMMDD-HHMMSS.tar.gz` (UTC time) per run with `db.dump` (`pg_dump -Fc`), `files/` (file store), `keys/` (Data Protection key ring) and `manifest.json` (app version, newest migration, creation time, SHA-256 of `db.dump`, number of files in the archive, row count of every table). Dump, counts and migration id come from one database snapshot. A file that changes while the file store is archived is tolerated (GNU tar exit 1); `fileCount` is what the archive holds.
- **When:** every night at `BACKUP_TIME` in `TZ` (service `backup`, `deploy/backup/scheduler.sh`). On demand: `tools/backup.sh` prints `Backup written: /backups/<archive>`.
- **Where:** the `backups` volume, mounted at `/backups` in the `backup` service, or the host folder `BACKUP_HOST_DIR` when set. Archives are written with mode 600 under a temporary name and renamed when complete.
- **Retention:** archives older than `BACKUP_RETENTION_DAYS` (default 14), and `.partial-*` leftovers of interrupted runs older than a day, are deleted after a successful run only. A failed run exits non-zero, logs `Backup FAILED …` in `tools/compose.sh logs backup` and leaves every earlier archive.
- **List:** `tools/compose.sh exec backup ls -l /backups`.
- **Copy off the host:** a backup on the same disk does not survive a lost host. Copy archives regularly to access-restricted storage elsewhere. From the volume:

  ```bash
  tools/compose.sh cp backup:/backups/<archive> <destination-folder>/
  ```

  With `BACKUP_HOST_DIR` the archives are ordinary files in that folder. Archives contain the database and the Data Protection keys: whoever can read one can read all data and unprotect pending account-link tokens. Restrict every copy accordingly.

## Restore

`tools/restore.sh <archive.tar.gz> [--force] [--no-safety-backup]` restores into the stack of this checkout:

1. Asks the image for its newest migration, notes the running services, stops `api`, `worker`, `backup` and `nginx`, starts `postgres`.
2. With `--force` on a database that has tables: takes a safety backup of the current state first (`deploy/backup/backup.sh` in a one-off `backup` container, into the `backups` volume or `BACKUP_HOST_DIR`; `Backup written: …` names it). If it fails, nothing is restored and the services are started again. `--no-safety-backup` skips it (only when a current backup exists elsewhere).
3. Runs the `restore` service (`deploy/backup/restore.sh`). It prints the archive's `createdAt`, `appVersion`, `latestMigration` and file count, then, before writing anything, refuses:
   - exit 2: not a usable TGYSM archive: unreadable, a part missing, `latestMigration` empty or malformed, no `dbSha256` (archives from before checksums: take a new backup), `db.dump` not matching its SHA-256, or a file count different from `fileCount`;
   - exit 3: the archive is from a newer build than the image;
   - exit 4: the database already has tables and `--force` was not given.

   After a refusal the services that were running before are started again, unchanged.
4. Writes the database (`pg_restore --single-transaction`, so a failed restore leaves no half-written schema; with `--force` the `public` schema is dropped first), replaces the `files` and `keys` volumes, then compares the row counts with the manifest (exit 5 on a difference).
5. Starts the stack (`migrate` brings an older archive up to the image's schema) and waits up to 180 s for a healthy `api`.

A failure while writing (any other non-zero exit) leaves the application stopped: fix the cause, then run again with `--force`. The safety backup from step 2 is the way back to the state before the restore.

The archive path is on the host. An archive inside the `backups` volume must first be copied out with `tools/compose.sh cp` (see Backups).

Sessions issued before the backup stay valid after a restore, because the key ring is restored with the data.

### Restore drill

`tools/restore-drill.sh` proves backup and restore end to end without touching the production stack or its volumes. It needs the images (`tools/docker-build.sh`) and a certificate pair in `deploy/certs/`. It starts two throw-away compose projects (`tgysm-drill-source` on ports 9080/9443, `tgysm-drill-target` on 9081/9444, subnets `172.29.1.0/24` and `172.29.2.0/24`), creates data on the source (attachment, finished background export, deactivated user, a signed-in session), backs it up, checks the refusals (newer build, `db.dump` not matching its checksum, non-empty database without `--force`), restores into the empty target, restores again with `--force` (which must take a safety backup first), and checks health, row counts, the attachment and export downloads, the pre-backup session and the deactivated user's refused sign-in (`401 auth.invalid_credentials`). An unreachable target is a failed check: the drill also fails when the check script exits non-zero or reports fewer rows than it should. It tests the images tagged `latest`, not the tag in `deploy/.version`. It removes both projects and writes `docs/release/restore-drill.md` (pass/fail table). Last result: `docs/release/restore-drill.md`.

Run it after an update that changes the images or the backup scripts, and periodically to prove the archives restore.

## Logs, health and status

- Service state: `tools/compose.sh ps`.
- Logs: `tools/compose.sh logs [-f] [service…]`, e.g. `tools/compose.sh logs -f api worker`, `tools/compose.sh logs migrate`, `tools/compose.sh logs backup`. The API and Worker log the resolved file store root at startup (`File store root: …`).
- Health: `curl -fsS https://<host>/api/v1/health` answers `{"status":"ok"}`, or `degraded` when a Worker job's last heartbeat is older than three times its interval. It is anonymous and shows nothing else. Compose uses the same probe inside the network for `api`'s health status.
- `/admin/system` (admin sign-in; Yönetim > Sistem): every Worker job with instance, last run, last error, interval and a stale flag. A stale job or a `lastError` points to `tools/compose.sh logs worker`.
- Email: `/admin/email-deliveries` lists deliveries by status; a `failed` one can be retried there after the cause is fixed.

## Configuration changes

Edit `deploy/.env`, then `tools/compose.sh up -d`. Compose recreates the services whose settings changed; `migrate` runs again first and finds nothing to do.

### Rotating the SMTP password

1. Change the password at the relay.
2. Set the new value in `deploy/.env` (`SMTP_PASSWORD=…`).
3. `tools/compose.sh up -d` (recreates `api`, `worker` and `migrate` with the new value).
4. Check `tools/compose.sh logs worker` and `/admin/email-deliveries`; retry deliveries that failed during the change.

The password lives only in `deploy/.env` and the container environment; it is never logged, audited or stored in the database.

### Renewing the certificate

Replace `deploy/certs/tgysm.crt` and `deploy/certs/tgysm.key`, then `tools/compose.sh restart nginx`.

## Data Protection keys

- The key ring is the folder `/data/keys` in the `keys` volume, shared by `api` and `worker` (`DataProtection:KeysPath`), mode 0700, owned by the `app` user. Outside Development the hosts refuse to start when the setting is empty or the folder is not writable.
- The keys are stored unencrypted inside that volume (the framework logs a warning at startup); protection is the folder permission and restricted access to the host and the backups (ADR-0024). Encrypting the key ring with a certificate is not configured; it would add a secret to issue, rotate and back up (ADR-0024, Alternative 4).
- The keys protect the session cookie, the antiforgery tokens, and the account-link token of a not-yet-sent activation or reset email (`notification_outbox.protected_secret`).
- **Losing the keys** (deleted volume, restore without them, a new host without the archive): every user is signed out; account-link emails still waiting in the outbox can no longer be rendered, so those activation and reset emails are never delivered and the admin must issue a new link or a temporary password. Links already delivered keep working, because only a hash of their token is stored.
- Every backup archive contains the keys, and a restore puts them back. Never move a database to another host without its `keys/` folder.

## Commands reference

| Task | Command |
|---|---|
| Deploy or update | `tools/deploy.sh [--skip-gates]` |
| Build images only | `tools/docker-build.sh [--allow-dirty]` (refuses uncommitted changes; `--allow-dirty` tags `<hash>-dirty`) |
| Any compose command | `tools/compose.sh <args>` |
| First admin | `tools/compose.sh run --rm api setup-admin --email … --first-name … --last-name …` |
| Temporary password | `tools/compose.sh run --rm api reset-password --email …` |
| Apply migrations by hand | `tools/compose.sh run --rm api migrate` |
| Backup now | `tools/backup.sh` |
| Restore | `tools/restore.sh <archive.tar.gz> [--force] [--no-safety-backup]` |
| Restore drill | `tools/restore-drill.sh` |
| Test certificate | `tools/gen-self-signed-cert.sh [host-name]` |
| Pilot seed | `tools/seed-pilot.sh --compose [--scale <factor>]` (needs `TGYSM_DEPLOYMENT=pilot`) |
| Performance run against a host | `PERF_BASE_URL=https://<host> PERF_PASSWORD=… tools/perf-measure.sh` (writes 5,000 assets into that database on every run; pilot hosts only) |

`tools/compose.sh`, `tools/backup.sh` and `tools/restore.sh` honor `TGYSM_ENV_FILE` (another env file) and `COMPOSE_PROJECT_NAME`.
