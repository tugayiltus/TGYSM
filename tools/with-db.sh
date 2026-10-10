#!/usr/bin/env bash
# Runs a command with local PostgreSQL started for its duration, then stops PostgreSQL again.
# Used while the owner keeps the database stopped between runs, e.g. `tools/with-db.sh tools/test.sh`.
# If PostgreSQL was already running before the call, it is left running. With CI=true it only runs the command.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly PG_SERVICE="postgresql@17"
readonly READY_ATTEMPTS=30
readonly READY_DELAY_SECONDS=1

if [[ $# -eq 0 ]]; then
  echo "usage: tools/with-db.sh <command> [args...]" >&2
  exit 2
fi

# CI provides PostgreSQL as a service container: nothing to start or stop.
if [[ "${CI:-}" == "true" ]]; then
  exec "$@"
fi

started_here=false
if ! pg_isready -q; then
  brew services start "$PG_SERVICE" >/dev/null
  started_here=true
fi

stop_db() {
  if [[ "$started_here" == true ]]; then
    brew services stop "$PG_SERVICE" >/dev/null || true
  fi
}
trap stop_db EXIT

for ((i = 0; i < READY_ATTEMPTS; i++)); do
  pg_isready -q && break
  sleep "$READY_DELAY_SECONDS"
done
if ! pg_isready -q; then
  echo "PostgreSQL did not become ready" >&2
  exit 1
fi

"$@"
