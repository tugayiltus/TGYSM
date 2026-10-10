#!/usr/bin/env bash
# Performance measurement (spec 5.2): times list, search, queue, report, drill-down, export and CSV import calls at pilot
# volume and writes docs/release/performance.md (data volume, machine, commit, median and p95 per operation).
#
#   PERF_PASSWORD=<pilot password> tools/perf-measure.sh
#
# Without PERF_BASE_URL the script starts a Release API against the seeded local pilot database (tools/seed-pilot.sh --fresh
# prints the pilot password once) on its own port. With PERF_BASE_URL it measures that stack instead (for example a
# deployed pilot host) and starts nothing.
#
# Environment: PERF_PASSWORD (required; never stored), PERF_RUNS (default 5; calls per operation), PERF_BASE_URL,
# PERF_IT_EMAIL / PERF_ADMIN_EMAIL (seed accounts by default), TGYSM_DB_HOST / TGYSM_DB_NAME / TGYSM_DB_USER.
#
# The CSV import commit writes 5,000 assets, so the seeded database grows by that much on every run: the commit is timed
# once per run (one sample) while the preview is timed PERF_RUNS times. Re-seed with tools/seed-pilot.sh --fresh to reset.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_pilot}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly PILOT_DB_PREFIX="tgysm_pilot"
readonly API_PORT=5090
readonly API_URL="http://localhost:$API_PORT"
readonly READY_PATH="/api/v1/auth/csrf"
readonly READY_TIMEOUT_SECONDS=60
readonly API_BIN_DIR="backend/src/Tgysm.Api/bin/Release/net10.0"
readonly LOG_DIR="frontend/e2e-logs"
readonly OUTPUT_FILE="docs/release/performance.md"
readonly DEFAULT_RUNS=5
readonly DEFAULT_IT_EMAIL="it01@pilot.example.com"
readonly DEFAULT_ADMIN_EMAIL="admin@pilot.example.com"
readonly VOLUME_PREFIX="VOLUME "

if [[ -z "${PERF_PASSWORD:-}" ]]; then
  echo "PERF_PASSWORD is not set (the pilot password printed by tools/seed-pilot.sh)." >&2
  exit 1
fi

api_pid=""
temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/tgysm-perf.XXXXXX")"
cleanup() {
  if [[ -n "$api_pid" ]]; then kill "$api_pid" 2>/dev/null || true; fi
  wait 2>/dev/null || true
  rm -rf "$temp_dir"
}
trap cleanup EXIT

wait_for() {
  local url="$1"
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if curl -sf -o /dev/null -H "X-Forwarded-Proto: https" "$url"; then return 0; fi
    sleep 1
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

base_url="${PERF_BASE_URL:-}"
target="$base_url"
if [[ -z "$base_url" ]]; then
  if [[ "$DB_NAME" != "$PILOT_DB_PREFIX"* ]]; then
    echo "Refusing to measure against $DB_NAME: only databases named $PILOT_DB_PREFIX* are used." >&2
    exit 1
  fi
  if lsof -nP -iTCP:"$API_PORT" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $API_PORT is already in use; stop the process on it first." >&2
    exit 1
  fi
  mkdir -p "$LOG_DIR"
  dotnet build backend/src/Tgysm.Api -c Release --nologo -v quiet
  export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
  export ASPNETCORE_ENVIRONMENT="Development"
  export Files__RootPath="$temp_dir/files"
  export Email__Mode="Pickup"
  export Email__PickupFolder="$temp_dir/pickup"
  (cd "$API_BIN_DIR" && exec dotnet Tgysm.Api.dll --urls "$API_URL") >"$LOG_DIR/api-perf.log" 2>&1 &
  api_pid=$!
  wait_for "$API_URL$READY_PATH"
  base_url="$API_URL"
  target="local Release API on $API_URL, database $DB_NAME"
fi

export PERF_BASE_URL="$base_url"
export PERF_RUNS="${PERF_RUNS:-$DEFAULT_RUNS}"
export PERF_IT_EMAIL="${PERF_IT_EMAIL:-$DEFAULT_IT_EMAIL}"
export PERF_ADMIN_EMAIL="${PERF_ADMIN_EMAIL:-$DEFAULT_ADMIN_EMAIL}"
export PERF_TEMP_DIR="$temp_dir"

result_file="$temp_dir/result.txt"
(cd frontend && node perf/measure.ts) >"$result_file"

volume_line="$(grep "^$VOLUME_PREFIX" "$result_file")"
volume="${volume_line#"$VOLUME_PREFIX"}"
table="$(grep -v "^$VOLUME_PREFIX" "$result_file")"

if [[ "$(uname -s)" == "Darwin" ]]; then
  machine="$(sysctl -n machdep.cpu.brand_string), $(sysctl -n hw.ncpu) logical cores, $(( $(sysctl -n hw.memsize) / 1073741824 )) GiB RAM, macOS $(sw_vers -productVersion)"
else
  machine="$(uname -srm), $(nproc) logical cores"
fi

mkdir -p "$(dirname "$OUTPUT_FILE")"
{
  echo "# Performance measurement"
  echo
  echo "Run by \`tools/perf-measure.sh\` on $(date -u +%Y-%m-%dT%H:%M:%SZ) at commit \`$(git rev-parse --short HEAD)\`. Spec 5.2; acceptance criterion 18 of \`tgysm.md\` section 20."
  echo
  echo "- Target: $target."
  echo "- Machine: $machine."
  echo "- Data volume before the run (counts the IT staff account sees): $volume."
  echo "- Runs per operation: $PERF_RUNS; times are wall-clock milliseconds from request to the last byte of the body, over HTTP on the same machine (no network, no browser rendering)."
  echo "- The CSV import commit writes 5,000 assets, so each run grows the database by that many; it is timed once per run (one sample), the preview $PERF_RUNS times."
  echo "- No numeric targets are set (criterion 18); outliers are reported to the owner, not tuned."
  echo
  echo "$table"
} >"$OUTPUT_FILE"
echo "Wrote $OUTPUT_FILE"
