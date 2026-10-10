#!/usr/bin/env bash
# Checks that every tools/… and deploy/… path named in the project docs exists in the repository.
#
#   tools/check-doc-paths.sh [file ...]
#
# Without arguments it checks the release docs listed in DEFAULT_DOCS. Prints each missing path with the file that names
# it and exits 1 when any is missing. Paths the operator creates on the host (RUNTIME_PATHS) are not expected in the repo.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DEFAULT_DOCS=(README.md capabilities.md SECURITY.md CLAUDE.md docs/operations.md docs/release/acceptance.md)
# Created on the host from deploy/.env.example; git-ignored.
readonly RUNTIME_PATHS=(deploy/.env)
# A path starts at tools/ or deploy/ not preceded by a path or word character.
readonly PATH_PATTERN='(^|[^A-Za-z0-9_./-])(tools|deploy)/[A-Za-z0-9._/-]*'

docs=("$@")
if [[ ${#docs[@]} -eq 0 ]]; then docs=("${DEFAULT_DOCS[@]}"); fi

is_runtime_path() {
  local candidate="$1" runtime
  for runtime in "${RUNTIME_PATHS[@]}"; do
    if [[ "$candidate" == "$runtime" ]]; then return 0; fi
  done
  return 1
}

checked=0
missing=0
for doc in "${docs[@]}"; do
  if [[ ! -f "$doc" ]]; then
    echo "Missing document: $doc" >&2
    missing=$((missing + 1))
    continue
  fi
  while IFS= read -r found; do
    # Drop the leading delimiter and sentence punctuation after the path.
    path="$(sed -E 's#^[^td]*##; s#[.,]+$##' <<<"$found")"
    if is_runtime_path "$path"; then continue; fi
    checked=$((checked + 1))
    if [[ ! -e "$path" ]]; then
      echo "$doc: $path does not exist" >&2
      missing=$((missing + 1))
    fi
  done < <(grep -oE "$PATH_PATTERN" "$doc" | sort -u)
done

if (( missing > 0 )); then
  echo "$missing missing path(s)." >&2
  exit 1
fi
echo "All $checked tools/ and deploy/ path reference(s) exist."
