#!/usr/bin/env bash
# Add an EF Core migration: tools/add-migration.sh <Name>
set -euo pipefail
cd "$(dirname "$0")/../backend"

if [[ $# -ne 1 ]]; then
  echo "usage: tools/add-migration.sh <MigrationName>" >&2
  exit 2
fi

dotnet tool run dotnet-ef migrations add "$1" \
  --project src/Tgysm.Infrastructure --startup-project src/Tgysm.Infrastructure \
  --output-dir Persistence/Migrations
