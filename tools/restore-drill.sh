#!/usr/bin/env bash
# Restore drill (tgysm.md §20 item 19, ADR-0025). Self-contained: starts a throw-away source stack, creates data
# (attachment, background export, deactivated user, a signed-in session), backs it up, then restores the archive into a
# second throw-away stack and checks it. Never touches the production stack or its volumes. Needs the images from
# tools/docker-build.sh and a certificate pair in deploy/certs/. Writes docs/release/restore-drill.md.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly SOURCE_PROJECT="tgysm-drill-source"
readonly TARGET_PROJECT="tgysm-drill-target"
readonly SOURCE_HTTPS_PORT=9443
readonly TARGET_HTTPS_PORT=9444
readonly SOURCE_HTTP_PORT=9080
readonly TARGET_HTTP_PORT=9081
readonly SOURCE_SUBNET="172.29.1.0/24"
readonly TARGET_SUBNET="172.29.2.0/24"
readonly READY_TIMEOUT_SECONDS=240
readonly ADMIN_EMAIL="drill-admin@example.com"
readonly REPORT="docs/release/restore-drill.md"
readonly FUTURE_MIGRATION="99999999999999_FutureMigration"
readonly TEMP_PASSWORD_PREFIX="Temporary password (shown once): "
# Exit codes of deploy/backup/restore.sh.
readonly EXIT_NEWER_ARCHIVE=3
readonly EXIT_NOT_EMPTY=4

# Under the repository (git-ignored tmp/): Docker on macOS (Colima) shares only the home folder with containers.
mkdir -p tmp
work="$(mktemp -d "$PWD/tmp/restore-drill.XXXXXX")"
results=()
failures=0

compose_for() {
  local project="$1" env_file="$2"
  shift 2
  COMPOSE_PROJECT_NAME="$project" TGYSM_ENV_FILE="$env_file" tools/compose.sh "$@"
}
source_compose() { compose_for "$SOURCE_PROJECT" "$work/source.env" "$@"; }
target_compose() { compose_for "$TARGET_PROJECT" "$work/target.env" "$@"; }

teardown() {
  source_compose down -v --remove-orphans >/dev/null 2>&1 || true
  target_compose down -v --remove-orphans >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap teardown EXIT

record() {
  local check="$1" expected="$2" actual="$3"
  local result="pass"
  if [[ "$expected" != "$actual" ]]; then result="FAIL"; failures=$((failures + 1)); fi
  results+=("| $check | $expected | $actual | $result |")
  echo "$result: $check (expected $expected, got $actual)"
}

write_env() {
  local file="$1" https_port="$2" http_port="$3" subnet="$4"
  sed -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=drill-$(openssl rand -hex 12)/" \
      -e "s#^PUBLIC_BASE_URL=.*#PUBLIC_BASE_URL=https://localhost:$https_port#" \
      -e "s/^HTTPS_PORT=.*/HTTPS_PORT=$https_port/" \
      -e "s/^HTTP_PORT=.*/HTTP_PORT=$http_port/" \
      -e "s#^TGYSM_SUBNET=.*#TGYSM_SUBNET=$subnet#" \
      -e "s/^EMAIL_MODE=.*/EMAIL_MODE=Pickup/" \
      -e "s/^BACKUP_HOST_DIR=.*/BACKUP_HOST_DIR=/" \
      deploy/.env.example > "$file"
}

wait_healthy() {
  local runner="$1"
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if "$runner" ps api --format '{{.Status}}' 2>/dev/null | grep -q '(healthy)'; then return 0; fi
    sleep 1
  done
  echo "Timed out waiting for the API of $runner" >&2
  return 1
}

table_count() { target_compose exec -T postgres psql -U tgysm -d tgysm -XtAc "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'"; }

for file in deploy/certs/tgysm.crt deploy/certs/tgysm.key; do
  if [[ ! -f "$file" ]]; then echo "Missing $file (tools/gen-self-signed-cert.sh for a test host)" >&2; exit 1; fi
done
write_env "$work/source.env" "$SOURCE_HTTPS_PORT" "$SOURCE_HTTP_PORT" "$SOURCE_SUBNET"
write_env "$work/target.env" "$TARGET_HTTPS_PORT" "$TARGET_HTTP_PORT" "$TARGET_SUBNET"

echo "== Source stack"
source_compose up -d >/dev/null
wait_healthy source_compose
setup="$(source_compose run --rm -T api setup-admin --email "$ADMIN_EMAIL" --first-name Drill --last-name Admin 2>/dev/null)"
temporary="$(sed -n "s/^${TEMP_PASSWORD_PREFIX}//p" <<<"$setup")"
python3 tools/lib/tgysm_drill.py seed "https://localhost:$SOURCE_HTTPS_PORT" "$work/state.json" "$ADMIN_EMAIL" "$temporary"

echo "== Backup"
archive_path="$(source_compose exec -T backup bash /backup/backup.sh | sed -n 's/^Backup written: //p')"
mkdir -p "$work/archives"
source_compose cp "backup:$archive_path" "$work/archives/" >/dev/null
archive="$work/archives/$(basename "$archive_path")"
source_compose down -v >/dev/null 2>&1

echo "== Refusals"
mkdir -p "$work/future"
tar -xzf "$archive" -C "$work/future"
sed -i.bak "s/\"latestMigration\": \"[^\"]*\"/\"latestMigration\": \"$FUTURE_MIGRATION\"/" "$work/future/manifest.json"
rm -f "$work/future/manifest.json.bak"
mkdir -p "$work/future-archive"
tar -czf "$work/future-archive/future.tar.gz" -C "$work/future" db.dump manifest.json files keys
target_compose up -d postgres >/dev/null
code=0
TGYSM_ENV_FILE="$work/target.env" COMPOSE_PROJECT_NAME="$TARGET_PROJECT" tools/restore.sh "$work/future-archive/future.tar.gz" >/dev/null 2>&1 || code=$?
record "archive from a newer build is refused (exit code)" "$EXIT_NEWER_ARCHIVE" "$code"
record "refused restore wrote no table" "0" "$(table_count | tr -d '[:space:]')"

echo "== Restore"
if TGYSM_ENV_FILE="$work/target.env" COMPOSE_PROJECT_NAME="$TARGET_PROJECT" tools/restore.sh "$archive"; then
  record "restore with row counts equal to the manifest" "exit 0" "exit 0"
else
  record "restore with row counts equal to the manifest" "exit 0" "failed"
fi
code=0
TGYSM_ENV_FILE="$work/target.env" COMPOSE_PROJECT_NAME="$TARGET_PROJECT" tools/restore.sh "$archive" >/dev/null 2>&1 || code=$?
record "restore into a non-empty database without --force is refused (exit code)" "$EXIT_NOT_EMPTY" "$code"
wait_healthy target_compose

echo "== Checks on the restored stack"
while IFS= read -r line; do
  check="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["check"])' "$line")"
  expected="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["expected"])' "$line")"
  actual="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["actual"])' "$line")"
  record "$check" "$expected" "$actual"
done < <(python3 tools/lib/tgysm_drill.py check "https://localhost:$TARGET_HTTPS_PORT" "$work/state.json" || true)

mkdir -p "$(dirname "$REPORT")"
{
  echo "# Restore drill"
  echo
  echo "Run by \`tools/restore-drill.sh\` on $(date -u +%Y-%m-%dT%H:%M:%SZ) at commit \`$(git rev-parse --short HEAD)\` (images \`$(docker image inspect tgysm-api:latest --format '{{range .Config.Env}}{{println .}}{{end}}' | sed -n 's/^APP_VERSION=//p')\`)."
  echo "Archive: \`$(basename "$archive")\` ($(du -h "$archive" | cut -f1 | tr -d ' ')). Acceptance criterion 19 of \`tgysm.md\` §20; ADR-0025."
  echo
  echo "| Check | Expected | Actual | Result |"
  echo "|---|---|---|---|"
  printf '%s\n' "${results[@]}"
  echo
  if [[ "$failures" -eq 0 ]]; then echo "Result: **all checks passed**."; else echo "Result: **$failures check(s) failed**."; fi
} > "$REPORT"
echo "Report: $REPORT"
if [[ "$failures" -gt 0 ]]; then exit 1; fi
