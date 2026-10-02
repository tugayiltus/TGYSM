#!/usr/bin/env bash
# Runs the Vite dev server in the foreground on http://localhost:5173 (strictPort in vite.config.ts, so a restarted
# service always comes back on the same URL). Used by tools/run.sh and by the launchd service (tools/dev-service.sh).
set -euo pipefail
cd "$(dirname "$0")/../frontend"

exec npm run dev
