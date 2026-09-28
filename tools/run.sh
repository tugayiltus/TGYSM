#!/usr/bin/env bash
# Local development: API, Worker, and Vite. Ctrl+C stops all three.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly WORKER_CONNECTION="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
# Absolute, independent of dotnet run's own working directory (App:PublicBaseUrl aside, Email:PickupFolder is the
# only Worker setting whose default in appsettings.Development.json is a relative path).
readonly PICKUP_FOLDER="$(pwd)/tmp/email-pickup"

tools/db-up.sh
tools/migrate.sh

pids=()
cleanup() {
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

(cd backend && exec dotnet run --project src/Tgysm.Api --launch-profile http) &
pids+=($!)
# The API reads its connection string from user secrets (tools/db-up.sh); the Worker has no user secrets of its
# own, so it gets the same connection string through the environment instead.
(cd backend && exec env "ConnectionStrings__Default=$WORKER_CONNECTION" DOTNET_ENVIRONMENT=Development \
  "Email__PickupFolder=$PICKUP_FOLDER" dotnet run --project src/Tgysm.Worker) &
pids+=($!)
(cd frontend && exec npm run dev) &
pids+=($!)
wait
