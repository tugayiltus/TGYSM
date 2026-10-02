#!/usr/bin/env bash
# Shared by tools/dev-api.sh and tools/dev-worker.sh: build one .NET project into tmp/dev-artifacts/<name>/build,
# copy a successful build to tmp/dev-artifacts/<name>/current, then exec the app from current/. When the build fails
# (for example while code is being edited), the last good build in current/ is started instead, so a restarted dev
# service stays up. The separate artifacts folder keeps these builds out of the bin/obj that tests and lint use.
#
# Usage: tools/dev-dotnet-host.sh <name> <project-dir> <assembly-name> [env assignments...]
set -euo pipefail
cd "$(dirname "$0")/.."

readonly NAME="$1" PROJECT="$2" ASSEMBLY="$3"
shift 3
readonly ROOT="$(pwd)"
readonly BASE="$ROOT/tmp/dev-artifacts/$NAME"
readonly BUILD_OUTPUT="$BASE/build/bin/$ASSEMBLY/debug"
readonly CURRENT="$BASE/current"

if dotnet build "$PROJECT" --artifacts-path "$BASE/build" --nologo --verbosity quiet; then
  mkdir -p "$CURRENT"
  rsync -a --delete "$BUILD_OUTPUT/" "$CURRENT/"
elif [[ -f "$CURRENT/$ASSEMBLY.dll" ]]; then
  echo "[dev-$NAME] build failed; starting the last good build from $CURRENT" >&2
else
  echo "[dev-$NAME] build failed and no previous build exists" >&2
  exit 1
fi

# Content root is the working directory, so start from the folder that holds appsettings*.json.
cd "$CURRENT"
exec env "$@" dotnet "$ASSEMBLY.dll"
