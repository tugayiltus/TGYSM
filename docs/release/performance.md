# Performance measurement

Run by `tools/perf-measure.sh` on 2026-10-10T08:08:22Z at commit `cc2b2a6`. Spec 5.2; acceptance criterion 18 of `tgysm.md` section 20.

- Target: local Release API on http://localhost:5090, database tgysm_pilot.
- Machine: Apple M1, 8 logical cores, 16 GiB RAM, macOS 26.7.
- Data volume before the run (counts the IT staff account sees): tickets=2000 assets=500.
- Runs per operation: 5; times are wall-clock milliseconds from request to the last byte of the body, over HTTP on the same machine (no network, no browser rendering).
- The CSV import commit writes 5,000 assets, so each run grows the database by that many; it is timed once per run (one sample), the preview 5 times.
- No numeric targets are set (criterion 18); outliers are reported to the owner, not tuned.

| Operation | Runs | Median (ms) | p95 (ms) |
|---|---:|---:|---:|
| Ticket list (page of 50) | 5 | 10 | 12 |
| Ticket search | 5 | 14 | 25 |
| Workspace queue (unassigned) | 5 | 11 | 26 |
| Asset list (page of 50) | 5 | 8 | 8 |
| Asset search (brand name) | 5 | 11 | 25 |
| Report: incidents | 5 | 56 | 147 |
| Report: requests | 5 | 33 | 79 |
| Report: operations | 5 | 26 | 130 |
| Report: transfers | 5 | 9 | 25 |
| Report: assets | 5 | 6 | 35 |
| Report: warranty | 5 | 4 | 8 |
| Drill-down page (incidents, mttr) | 5 | 8 | 24 |
| Synchronous Excel export (incidents, mttr) | 5 | 86 | 396 |
| CSV import preview (5000 rows) | 5 | 127 | 197 |
| CSV import commit (5000 rows, once per run) | 1 | 3153 | 3153 |
