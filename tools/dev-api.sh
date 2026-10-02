#!/usr/bin/env bash
# Runs the API in the foreground for local development on http://localhost:5080 (build + last-good fallback in
# tools/dev-dotnet-host.sh). Used by tools/run.sh and by the launchd service (tools/dev-service.sh).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly API_URL="http://localhost:5080"

tools/db-up.sh
tools/migrate.sh || echo "[dev-api] migrate failed; starting with the current schema" >&2

exec tools/dev-dotnet-host.sh api backend/src/Tgysm.Api Tgysm.Api \
  ASPNETCORE_ENVIRONMENT=Development "ASPNETCORE_URLS=$API_URL"
