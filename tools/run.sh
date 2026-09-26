#!/usr/bin/env bash
# Local development: API on http://localhost:5080 and Vite on http://localhost:5173. Ctrl+C stops both.
set -euo pipefail
cd "$(dirname "$0")/.."

tools/db-up.sh
tools/migrate.sh

pids=()
cleanup() {
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

(cd backend && exec dotnet run --project src/Tgysm.Api --launch-profile http) &
pids+=($!)
(cd frontend && exec npm run dev) &
pids+=($!)
wait
