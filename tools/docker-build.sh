#!/usr/bin/env bash
# Builds the TGYSM images (ADR-0024): tgysm-api and tgysm-worker, tagged with the Git short hash and "latest", on fresh
# base images (--pull). Refuses a working tree with uncommitted changes, because the hash tag would then name code the
# image does not hold. --allow-dirty builds it anyway and tags it only <hash>-dirty (development and drills only): "latest"
# keeps naming the last committed build.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly API_IMAGE="tgysm-api"
readonly WORKER_IMAGE="tgysm-worker"
readonly LATEST_TAG="latest"
readonly DIRTY_SUFFIX="-dirty"

allow_dirty=false
if [[ "${1:-}" == "--allow-dirty" ]]; then allow_dirty=true; fi

version="$(git rev-parse --short HEAD)"
if [[ -n "$(git status --porcelain)" ]]; then
  if [[ "$allow_dirty" == false ]]; then
    echo "Refused: the working tree has uncommitted changes (git status). Commit them, or pass --allow-dirty for a <hash>$DIRTY_SUFFIX test image." >&2
    exit 1
  fi
  version="$version$DIRTY_SUFFIX"
  echo "Warning: uncommitted changes; tagging $version only (latest is not moved)." >&2
fi
readonly version
tag_latest=true
if [[ "$version" == *"$DIRTY_SUFFIX" ]]; then tag_latest=false; fi
readonly tag_latest

for target in api worker; do
  image="$API_IMAGE"
  if [[ "$target" == "worker" ]]; then image="$WORKER_IMAGE"; fi
  tags=(-t "$image:$version")
  if [[ "$tag_latest" == true ]]; then tags+=(-t "$image:$LATEST_TAG"); fi
  docker build --pull --target "$target" --build-arg "APP_VERSION=$version" "${tags[@]}" .
done

echo "Built $API_IMAGE:$version and $WORKER_IMAGE:$version"
