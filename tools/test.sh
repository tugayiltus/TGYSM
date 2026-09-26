#!/usr/bin/env bash
# Test gate: backend + frontend tests with line-coverage gates.
# Backend: Domain + Application >= MIN_LINE_COVERAGE. Frontend: threshold in vite.config.ts (same value).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly MIN_LINE_COVERAGE=80
readonly RESULTS_DIR="backend/TestResults"
readonly SUMMARY_FILE="$RESULTS_DIR/report/Summary.txt"
readonly COVERED_ASSEMBLIES="+Tgysm.Domain;+Tgysm.Application"

rm -rf "$RESULTS_DIR"
dotnet test --solution backend/Tgysm.slnx --results-directory "$PWD/$RESULTS_DIR" --coverlet --coverlet-output-format cobertura
(cd backend && dotnet tool run reportgenerator \
  "-reports:TestResults/coverage.cobertura.*.xml" \
  "-targetdir:TestResults/report" \
  "-reporttypes:TextSummary" \
  "-assemblyfilters:$COVERED_ASSEMBLIES" >/dev/null)

line_coverage="$(awk -F': ' '/^ *Line coverage:/ { sub(/%/, "", $2); print $2; exit }' "$SUMMARY_FILE")"
echo "Backend Domain + Application line coverage: ${line_coverage}%"
if ! awk -v actual="$line_coverage" -v minimum="$MIN_LINE_COVERAGE" 'BEGIN { exit !(actual >= minimum) }'; then
  echo "Backend line coverage ${line_coverage}% is below ${MIN_LINE_COVERAGE}%." >&2
  exit 1
fi

(cd frontend && npm run test:coverage)
