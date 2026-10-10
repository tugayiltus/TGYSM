#!/usr/bin/env bash
# Builds docs/user-guides/combined.md and combined.pdf from the pages listed in docs/user-guides/README.md.
# Checks anchors and images (frontend/guide/build.ts), prints the PDF page size and fails unless it is A4.
#
#   tools/build-user-guide.sh
#
# Needs pdfinfo (brew install poppler) and the Playwright Chromium (cd frontend && npx playwright install chromium).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly PDF_FILE="docs/user-guides/combined.pdf"
readonly A4_WIDTH_PTS=595
readonly A4_HEIGHT_PTS=842
readonly SIZE_TOLERANCE_PTS=2

if ! command -v pdfinfo >/dev/null 2>&1; then
  echo "pdfinfo not found: install poppler (brew install poppler) to check the PDF page size." >&2
  exit 1
fi

(cd frontend && node guide/build.ts)

info="$(pdfinfo "$PDF_FILE")"
size_line="$(grep '^Page size:' <<<"$info")"
pages="$(awk -F': *' '/^Pages:/ { print $2 }' <<<"$info")"
width="$(awk '/^Page size:/ { print $3 }' <<<"$info")"
height="$(awk '/^Page size:/ { print $5 }' <<<"$info")"
echo "$size_line; Pages: $pages"
if ! awk -v w="$width" -v h="$height" -v ew="$A4_WIDTH_PTS" -v eh="$A4_HEIGHT_PTS" -v t="$SIZE_TOLERANCE_PTS" \
  'function abs(x) { return x < 0 ? -x : x } BEGIN { exit !(abs(w - ew) <= t && abs(h - eh) <= t) }'; then
  echo "combined.pdf is not A4 (~${A4_WIDTH_PTS} x ${A4_HEIGHT_PTS} pts): got ${width} x ${height}." >&2
  exit 1
fi
