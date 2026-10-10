# Restore drill

Run by `tools/restore-drill.sh` on 2026-10-10T16:45:10Z at commit `9190068` (images `9190068`).
Archive: `tgysm-20261010-164342.tar.gz` (32K). Acceptance criterion 19 of `tgysm.md` §20; ADR-0025.

| Check | Expected | Actual | Result |
|---|---|---|---|
| archive from a newer build is refused (exit code) | 3 | 3 | pass |
| archive whose db.dump differs from the manifest checksum is refused (exit code) | 2 | 2 | pass |
| refused restores wrote no table | 0 | 0 | pass |
| restore with row counts equal to the manifest | exit 0 | exit 0 | pass |
| restore into a non-empty database without --force is refused (exit code) | 4 | 4 | pass |
| forced restore over a non-empty database (exit code) | 0 | 0 | pass |
| forced restore took a safety backup first (archives in the target's backups volume) | 1 | 1 | pass |
| health | 200 | 200 | pass |
| session from before the backup is still valid | 200 | 200 | pass |
| attachment downloads with the same size | 1984 | 1984 | pass |
| export file downloads with the same size | 7722 | 7722 | pass |
| deactivated user cannot sign in | HTTP 401 auth.invalid_credentials | HTTP 401 auth.invalid_credentials | pass |
| post-restore checks reported | 5 | 5 | pass |
| post-restore check script (exit code) | 0 | 0 | pass |

Result: **all checks passed**.
