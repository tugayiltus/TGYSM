#!/usr/bin/env bash
# Restores a TGYSM backup archive into this stack (ADR-0025): stops the application, restores database, files and keys
# (refused when the database is not empty unless --force, or when the archive is from a newer build), runs migrate,
# starts the stack and waits until the API is healthy. Honors TGYSM_ENV_FILE and COMPOSE_PROJECT_NAME like tools/compose.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly READY_TIMEOUT_SECONDS=180
# Exit codes 2-4 of deploy/backup/restore.sh mean "refused before writing" (not an archive, newer build, database not empty).
readonly FIRST_REFUSAL_CODE=2
readonly LAST_REFUSAL_CODE=4
archive="${1:?usage: tools/restore.sh <archive.tar.gz> [--force]}"
force="${2:-}"
archive_dir="$(cd "$(dirname "$archive")" && pwd)"
archive_name="$(basename "$archive")"

latest="$(tools/compose.sh run --rm --no-deps -T api migrate --print-latest | tail -1)"
# Services running now, restarted unchanged when the restore is refused.
running=()
while IFS= read -r service; do running+=("$service"); done < <(tools/compose.sh ps --services --status running)
tools/compose.sh stop api worker backup nginx >/dev/null 2>&1 || true
tools/compose.sh up -d postgres
for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
  if tools/compose.sh exec -T postgres pg_isready -q -U tgysm -d tgysm; then break; fi
  sleep 1
done

code=0
RESTORE_SOURCE_DIR="$archive_dir" MAX_MIGRATION="$latest" \
  tools/compose.sh --profile restore run --rm -T restore "/restore/$archive_name" ${force:+"$force"} || code=$?
if (( code >= FIRST_REFUSAL_CODE && code <= LAST_REFUSAL_CODE )); then
  # Refused before anything was written: bring back exactly the services that ran before.
  if (( ${#running[@]} > 0 )); then
    tools/compose.sh up -d --no-deps "${running[@]}" >/dev/null
  fi
  exit "$code"
elif (( code != 0 )); then
  echo "Restore failed while writing (exit $code); the application stays stopped. Fix the cause and run again with --force." >&2
  exit "$code"
fi

tools/compose.sh up -d
for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
  if tools/compose.sh ps api --format '{{.Status}}' | grep -q '(healthy)'; then
    echo "Restore finished; the API is healthy."
    exit 0
  fi
  sleep 1
done
echo "Restore finished but the API did not become healthy; see tools/compose.sh logs api" >&2
exit 1
