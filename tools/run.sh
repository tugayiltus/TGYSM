#!/usr/bin/env bash
# Local development in this terminal: API, Worker, and Vite. Ctrl+C stops all three.
# To keep them running in the background (start at login, restart on exit), use tools/dev-service.sh install.
set -euo pipefail
cd "$(dirname "$0")/.."

if launchctl print "gui/$(id -u)/com.tgysm.dev.api" >/dev/null 2>&1; then
  echo "The dev services are already running under launchd (tools/dev-service.sh status). Stop them first:" >&2
  echo "  tools/dev-service.sh stop" >&2
  exit 1
fi

pids=()
cleanup() {
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

# The API script prepares the database (db-up + migrate) before it starts.
tools/dev-api.sh &
pids+=($!)
tools/dev-worker.sh &
pids+=($!)
tools/dev-web.sh &
pids+=($!)
wait
