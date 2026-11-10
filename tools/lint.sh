#!/usr/bin/env bash
# Lint gate: C# formatting, ESLint (zero warnings), TypeScript, Astryx doctor (ADR-0027) and the tools/deploy paths named in the docs.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly MIGRATIONS_DIR="backend/src/Tgysm.Infrastructure/Persistence/Migrations"

dotnet format backend/Tgysm.slnx --verify-no-changes --exclude "$MIGRATIONS_DIR"
(cd frontend && npm run lint && npm run typecheck)
# Fails only on a [fail] check; [warn] lines are advice while CoreUI screens remain.
(cd frontend && npx astryx doctor --built .)
tools/check-doc-paths.sh
