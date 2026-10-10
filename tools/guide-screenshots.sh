#!/usr/bin/env bash
# User-guide screenshots (docs/user-guides/images/<id>-<view>.png) from the seeded local pilot database.
# Starts a Release API, the Worker (so Yönetim > Sistem lists real jobs) and the Vite preview, takes the shots listed in
# frontend/guide/manifest.ts, stops all three. The Worker writes its email into a temporary pickup folder (never sent).
#
#   tools/seed-pilot.sh --fresh                       (prints the pilot password once)
#   GUIDE_PASSWORD=<pilot password> tools/guide-screenshots.sh [id ...]
#
# Optional ids limit the run to those manifest entries. Environment: GUIDE_PASSWORD (required; never stored),
# TGYSM_DB_HOST / TGYSM_DB_NAME / TGYSM_DB_USER. Only databases named tgysm_pilot* are used, never the development one.
# Uses API port 5090 and preview port 4173; both must be free (tools/e2e.sh and tools/perf-measure.sh use the same ports).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_pilot}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly PILOT_DB_PREFIX="tgysm_pilot"
readonly API_PORT=5090
readonly PREVIEW_PORT=4173
readonly API_URL="http://localhost:$API_PORT"
readonly PREVIEW_URL="http://localhost:$PREVIEW_PORT"
readonly READY_PATH="/api/v1/auth/csrf"
readonly READY_TIMEOUT_SECONDS=60
readonly API_BIN_DIR="backend/src/Tgysm.Api/bin/Release/net10.0"
readonly WORKER_BIN_DIR="backend/src/Tgysm.Worker/bin/Release/net10.0"
# Short job intervals, as in tools/e2e.sh, so every job has reported before the Sistem page is shot.
readonly JOB_INTERVAL_SECONDS=2
readonly WORKER_JOB_COUNT=5
readonly HEARTBEAT_TABLE="worker_heartbeats"
readonly LOG_DIR="frontend/e2e-logs"
readonly IMAGES_DIR="$PWD/docs/user-guides/images"

if [[ -z "${GUIDE_PASSWORD:-}" ]]; then
  echo "GUIDE_PASSWORD is not set (the pilot password printed by tools/seed-pilot.sh --fresh)." >&2
  exit 1
fi
if [[ "$DB_NAME" != "$PILOT_DB_PREFIX"* ]]; then
  echo "Refusing to use $DB_NAME: only databases named $PILOT_DB_PREFIX* are used." >&2
  exit 1
fi
for port in "$API_PORT" "$PREVIEW_PORT"; do
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $port is already in use; stop the process on it first." >&2
    exit 1
  fi
done

api_pid=""
worker_pid=""
preview_pid=""
temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/tgysm-guide.XXXXXX")"
cleanup() {
  if [[ -n "$api_pid" ]]; then kill "$api_pid" 2>/dev/null || true; fi
  if [[ -n "$worker_pid" ]]; then kill "$worker_pid" 2>/dev/null || true; fi
  if [[ -n "$preview_pid" ]]; then kill "$preview_pid" 2>/dev/null || true; fi
  wait 2>/dev/null || true
  rm -rf "$temp_dir"
}
trap cleanup EXIT

wait_for() {
  local url="$1" pid="$2"
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if curl -sf -o /dev/null -H "X-Forwarded-Proto: https" "$url"; then return 0; fi
    if ! kill -0 "$pid" 2>/dev/null; then
      echo "The process behind $url exited; see $LOG_DIR/*-guide.log." >&2
      return 1
    fi
    sleep 1
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

mkdir -p "$LOG_DIR"
dotnet build backend/src/Tgysm.Api -c Release --nologo -v quiet
dotnet build backend/src/Tgysm.Worker -c Release --nologo -v quiet
(cd frontend && npm run build >/dev/null)

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"
export DOTNET_ENVIRONMENT="Development"
export Files__RootPath="$temp_dir/files"
export Email__Mode="Pickup"
export Email__PickupFolder="$temp_dir/pickup"
export Worker__Jobs__OutboxDispatcher__IntervalSeconds="$JOB_INTERVAL_SECONDS"
export Worker__Jobs__EmailSender__IntervalSeconds="$JOB_INTERVAL_SECONDS"
export Worker__Jobs__SlaMonitor__IntervalSeconds="$JOB_INTERVAL_SECONDS"
export Worker__Jobs__ExportRunner__IntervalSeconds="$JOB_INTERVAL_SECONDS"
export Worker__Jobs__Cleanup__IntervalSeconds="$JOB_INTERVAL_SECONDS"
(cd "$API_BIN_DIR" && exec dotnet Tgysm.Api.dll --urls "$API_URL") >"$LOG_DIR/api-guide.log" 2>&1 &
api_pid=$!
(cd frontend && exec env TGYSM_API_URL="$API_URL" node node_modules/vite/bin/vite.js preview) >"$LOG_DIR/preview-guide.log" 2>&1 &
preview_pid=$!
(cd "$WORKER_BIN_DIR" && exec dotnet Tgysm.Worker.dll) >"$LOG_DIR/worker-guide.log" 2>&1 &
worker_pid=$!
wait_for "$API_URL$READY_PATH" "$api_pid"
wait_for "$PREVIEW_URL$READY_PATH" "$preview_pid"
# Every job has written its heartbeat (the Sistem page lists them).
for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
  jobs="$(psql -h "$DB_HOST" -U "$DB_USER" -d "$DB_NAME" -Atc "SELECT count(DISTINCT job) FROM $HEARTBEAT_TABLE" 2>/dev/null || echo 0)"
  if (( jobs >= WORKER_JOB_COUNT )); then break; fi
  if ! kill -0 "$worker_pid" 2>/dev/null; then
    echo "The Worker exited; see $LOG_DIR/worker-guide.log." >&2
    exit 1
  fi
  sleep 1
done
if (( jobs < WORKER_JOB_COUNT )); then
  echo "Timed out waiting for $WORKER_JOB_COUNT worker jobs to report (got $jobs)." >&2
  exit 1
fi

only="$(IFS=,; echo "$*")"
(cd frontend && GUIDE_BASE_URL="$PREVIEW_URL" GUIDE_IMAGES_DIR="$IMAGES_DIR" GUIDE_ONLY="$only" node guide/screenshots.ts)
