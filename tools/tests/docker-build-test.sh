#!/usr/bin/env bash
# Tests tools/docker-build.sh tagging with stub git and docker commands (no Docker needed): a clean tree tags
# <hash> and latest; --allow-dirty on a dirty tree tags only <hash>-dirty and never moves latest.
set -euo pipefail
cd "$(dirname "$0")/../.."

readonly STUB_HASH="abc1234"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

pass() { echo "PASS $1"; }
fail() { echo "FAIL $1"; failures=$((failures + 1)); }

# Stub git: a fixed short hash, and a dirty or clean status depending on STUB_DIRTY.
cat > "$work/git" <<STUB
#!/usr/bin/env bash
case "\$1" in
  rev-parse) echo "$STUB_HASH" ;;
  status) if [[ "\${STUB_DIRTY:-false}" == true ]]; then echo " M tools/docker-build.sh"; fi ;;
esac
STUB
# Stub docker: records its arguments.
cat > "$work/docker" <<STUB
#!/usr/bin/env bash
echo "\$*" >> "$work/docker.log"
STUB
chmod +x "$work/git" "$work/docker"

run_build() { : > "$work/docker.log"; PATH="$work:$PATH" STUB_DIRTY="$1" tools/docker-build.sh "${@:2}" >/dev/null 2>&1; }

if run_build false; then pass "clean tree: exit 0"; else fail "clean tree: exit 0"; fi
for image in tgysm-api tgysm-worker; do
  if grep -q -- "-t $image:$STUB_HASH " "$work/docker.log"; then pass "clean tree: $image:$STUB_HASH"; else fail "clean tree: $image:$STUB_HASH"; fi
  if grep -q -- "-t $image:latest" "$work/docker.log"; then pass "clean tree: $image:latest"; else fail "clean tree: $image:latest"; fi
done

if run_build true; then fail "dirty tree without --allow-dirty: refused"; else pass "dirty tree without --allow-dirty: refused"; fi
if [[ ! -s "$work/docker.log" ]]; then pass "refused build runs no docker"; else fail "refused build runs no docker"; fi

if run_build true --allow-dirty; then pass "--allow-dirty: exit 0"; else fail "--allow-dirty: exit 0"; fi
for image in tgysm-api tgysm-worker; do
  if grep -q -- "-t $image:$STUB_HASH-dirty" "$work/docker.log"; then pass "--allow-dirty: $image:$STUB_HASH-dirty"; else fail "--allow-dirty: $image:$STUB_HASH-dirty"; fi
done
if grep -q ":latest" "$work/docker.log"; then fail "--allow-dirty: latest not moved"; else pass "--allow-dirty: latest not moved"; fi

if [[ "$failures" -gt 0 ]]; then echo "$failures check(s) failed"; exit 1; fi
echo "All docker-build checks passed"
