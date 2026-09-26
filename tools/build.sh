#!/usr/bin/env bash
# Build gate: lint and tests must pass before the Release build and the frontend bundle.
set -euo pipefail
cd "$(dirname "$0")/.."

tools/lint.sh
tools/test.sh
dotnet build backend/Tgysm.slnx -c Release --nologo
(cd frontend && npm run build)

echo "Frontend chunks:"
ls -lh frontend/dist/assets | awk 'NR > 1 { print $5, $9 }'
