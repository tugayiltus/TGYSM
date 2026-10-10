#!/usr/bin/env bash
# Tests tools/lib/env-file.sh: values are read the way docker compose reads deploy/.env.
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/lib/env-file.sh

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

expect() {
  local label="$1" key="$2" expected="$3" actual
  actual="$(env_value "$work/test.env" "$key")"
  if [[ "$actual" == "$expected" ]]; then echo "PASS $label"; else echo "FAIL $label (got '$actual', expected '$expected')"; failures=$((failures + 1)); fi
}

printf '%s\r\n' 'PLAIN=8443' 'DOUBLE="9443"' "SINGLE='7443'" '  SPACED =1' 'SPACES=  6443  ' 'COMMENT=5443 # host port' \
  'export EXPORTED=4443' 'TWICE=1' 'TWICE=2' 'EMPTY=' 'QUOTED_COMMENT="3443" # note' '# HIDDEN=1' > "$work/test.env"

expect "plain value with CRLF" PLAIN 8443
expect "double quotes" DOUBLE 9443
expect "single quotes" SINGLE 7443
expect "surrounding spaces" SPACES 6443
expect "inline comment" COMMENT 5443
expect "export prefix" EXPORTED 4443
expect "last assignment wins" TWICE 2
expect "empty value" EMPTY ""
expect "quoted value with comment" QUOTED_COMMENT 3443
expect "commented-out key is absent" HIDDEN ""
expect "missing key is absent" MISSING ""
if [[ -z "$(env_value "$work/none.env" PLAIN)" ]]; then echo "PASS missing file"; else echo "FAIL missing file"; failures=$((failures + 1)); fi

if [[ "$failures" -gt 0 ]]; then echo "$failures check(s) failed"; exit 1; fi
echo "All env file checks passed"
