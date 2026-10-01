#!/usr/bin/env bash
# E2E: for each Playwright project, recreate tgysm_e2e, create the first admin, start API + preview, run the suite.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="tgysm_e2e"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly ADMIN_DB="postgres"
readonly API_URL="http://localhost:5080"
readonly PREVIEW_URL="http://localhost:4173"
readonly READY_PATH="/api/v1/auth/csrf"
readonly READY_TIMEOUT_SECONDS=60
readonly ADMIN_EMAIL="e2e-admin@example.com"
readonly API_BIN_DIR="backend/src/Tgysm.Api/bin/Debug/net10.0"
readonly WORKER_BIN_DIR="backend/src/Tgysm.Worker/bin/Debug/net10.0"
readonly LOG_DIR="frontend/e2e-logs"
readonly TEMP_PASSWORD_PREFIX="Temporary password (shown once): "
readonly E2E_JOB_INTERVAL_SECONDS=2
# The suite signs in several users per spec from one address; the production limit (10 per minute) would answer 429.
readonly E2E_LOGIN_PERMIT_LIMIT_PER_MINUTE=1000
readonly PROJECTS=("mobile-360" "desktop")

api_pid=""
worker_pid=""
preview_pid=""
pickup_folder=""

stop_servers() {
  if [[ -n "$api_pid" ]]; then kill "$api_pid" 2>/dev/null || true; fi
  if [[ -n "$worker_pid" ]]; then kill "$worker_pid" 2>/dev/null || true; fi
  if [[ -n "$preview_pid" ]]; then kill "$preview_pid" 2>/dev/null || true; fi
  wait 2>/dev/null || true
  api_pid=""
  worker_pid=""
  preview_pid=""
}
cleanup() {
  stop_servers
  if [[ -n "$pickup_folder" ]]; then rm -rf "$pickup_folder"; fi
}
trap cleanup EXIT

wait_for() {
  local url="$1"
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if curl -sf -o /dev/null "$url"; then return 0; fi
    sleep 1
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

# A running dev server (tools/run.sh) on these ports would answer instead of the E2E servers.
for port in "${API_URL##*:}" "${PREVIEW_URL##*:}"; do
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $port is already in use; stop tools/run.sh (or the process on it) before E2E." >&2
    exit 1
  fi
done

mkdir -p "$LOG_DIR"
dotnet build backend/src/Tgysm.Api --nologo -v quiet
dotnet build backend/src/Tgysm.Worker --nologo -v quiet
(cd frontend && npm run build >/dev/null)

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"
export DOTNET_ENVIRONMENT="Development"
# Per run and absolute: the Worker runs from its own bin/ directory, and tests must see only this run's mail.
pickup_folder="$(mktemp -d "${TMPDIR:-/tmp}/tgysm-e2e-pickup.XXXXXX")"
export Email__Mode="Pickup"
export Email__PickupFolder="$pickup_folder"
export E2E_EMAIL_PICKUP_FOLDER="$pickup_folder"
# The set-password link in the activation email points at the preview server the tests drive.
export App__PublicBaseUrl="$PREVIEW_URL"
# Short job intervals so a test waits seconds, not the 30 s production default, for an email.
export Security__LoginPermitLimitPerMinute="$E2E_LOGIN_PERMIT_LIMIT_PER_MINUTE"
export Worker__Jobs__OutboxDispatcher__IntervalSeconds="$E2E_JOB_INTERVAL_SECONDS"
export Worker__Jobs__EmailSender__IntervalSeconds="$E2E_JOB_INTERVAL_SECONDS"

for project in "${PROJECTS[@]}"; do
  mkdir -p "$LOG_DIR"
  psql -h "$DB_HOST" -d "$ADMIN_DB" -qc "DROP DATABASE IF EXISTS \"$DB_NAME\" WITH (FORCE)"
  psql -h "$DB_HOST" -d "$ADMIN_DB" -qc "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\""
  TGYSM_DB_NAME="$DB_NAME" tools/migrate.sh >/dev/null

  setup_output="$(cd "$API_BIN_DIR" && dotnet Tgysm.Api.dll setup-admin --email "$ADMIN_EMAIL" --first-name E2E --last-name Yönetici)"
  temp_password="$(sed -n "s/^${TEMP_PASSWORD_PREFIX}//p" <<<"$setup_output")"
  if [[ -z "$temp_password" ]]; then
    echo "setup-admin did not print a temporary password" >&2
    exit 1
  fi

  # exec keeps $! pointing at the real server process, so kill stops it.
  (cd "$API_BIN_DIR" && exec dotnet Tgysm.Api.dll --urls "$API_URL") >"$LOG_DIR/api-$project.log" 2>&1 &
  api_pid=$!
  (cd "$WORKER_BIN_DIR" && exec dotnet Tgysm.Worker.dll) >"$LOG_DIR/worker-$project.log" 2>&1 &
  worker_pid=$!
  (cd frontend && exec node node_modules/vite/bin/vite.js preview) >"$LOG_DIR/preview-$project.log" 2>&1 &
  preview_pid=$!
  # Through the preview proxy: it adds X-Forwarded-Proto, which the Secure antiforgery cookie needs.
  wait_for "$PREVIEW_URL/login"
  wait_for "$PREVIEW_URL$READY_PATH"

  (cd frontend && E2E_ADMIN_EMAIL="$ADMIN_EMAIL" E2E_ADMIN_TEMP_PASSWORD="$temp_password" npx playwright test --project="$project")
  stop_servers
done

echo "E2E passed for: ${PROJECTS[*]}"
