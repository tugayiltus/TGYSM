# Restore drill

Run by `tools/restore-drill.sh` on 2026-10-09T08:36:25Z at commit `5492af2` (images `5492af2`).
Archive: `tgysm-20261009-083528.tar.gz` (32K). Acceptance criterion 19 of `tgysm.md` §20; ADR-0025.

| Check | Expected | Actual | Result |
|---|---|---|---|
| archive from a newer build is refused (exit code) | 3 | 3 | pass |
| refused restore wrote no table | 0 | 0 | pass |
| restore with row counts equal to the manifest | exit 0 | exit 0 | pass |
| restore into a non-empty database without --force is refused (exit code) | 4 | 4 | pass |
| health | 200 | 200 | pass |
| session from before the backup is still valid | 200 | 200 | pass |
| attachment downloads with the same size | 1984 | 1984 | pass |
| export file downloads with the same size | 7723 | 7723 | pass |
| deactivated user cannot sign in | refused | refused | pass |

Result: **all checks passed**.
