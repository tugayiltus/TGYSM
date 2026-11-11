#!/usr/bin/env bash
# Browser QA sweep: screenshots every screen in light and dark mode on the phone (360 px) and desktop viewports,
# on a fresh E2E database, then joins them into contact sheets. Results in tmp/ui-qa/<viewport>/ (problems.txt lists
# sideways overflow and console errors) and tmp/ui-qa/sheets/<viewport>/.
# Stops the dev services for the run (tools/e2e.sh needs the ports) and starts them again afterwards.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly OUT="$PWD/tmp/ui-qa"
readonly PHONE_PER_SHEET=6
readonly PHONE_COLUMN_PX=300
readonly PHONE_MAX_HEIGHT_PX=900
readonly DESKTOP_PER_SHEET=3
readonly DESKTOP_COLUMN_PX=600
readonly DESKTOP_MAX_HEIGHT_PX=700

restart_services() { tools/dev-service.sh start >/dev/null 2>&1 || echo "Start the dev services again: tools/dev-service.sh start" >&2; }
trap restart_services EXIT
tools/dev-service.sh stop >/dev/null 2>&1 || true

rm -rf "$OUT"
UI_QA=1 UI_QA_OUT="$OUT" tools/e2e.sh e2e/ui-qa.spec.ts

node tools/ui-qa-sheets.mjs "$OUT/mobile-360" "$OUT/sheets/mobile-360" "$PHONE_PER_SHEET" "$PHONE_COLUMN_PX" "$PHONE_MAX_HEIGHT_PX"
node tools/ui-qa-sheets.mjs "$OUT/desktop" "$OUT/sheets/desktop" "$DESKTOP_PER_SHEET" "$DESKTOP_COLUMN_PX" "$DESKTOP_MAX_HEIGHT_PX"

for viewport in mobile-360 desktop; do
  echo "== $viewport problems"
  if [[ -s "$OUT/$viewport/problems.txt" ]]; then cat "$OUT/$viewport/problems.txt"; else echo "none"; fi
done
