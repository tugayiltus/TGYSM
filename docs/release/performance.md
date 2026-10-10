# Performance measurement

Run by `tools/perf-measure.sh` on 2026-10-10T17:22:49Z at commit `b1412b7`. Spec 5.2; acceptance criterion 18 of `tgysm.md` section 20.

- Target: local Release API on http://localhost:5090, database tgysm_pilot.
- Machine: Apple M1, 8 logical cores, 16 GiB RAM, macOS 26.7.
- Data volume before the run (counts the IT staff account sees): tickets=2000 assets=500.
- Runs per operation: 5; times are wall-clock milliseconds from request to the last byte of the body, over HTTP on the same machine (no network, no browser rendering).
- The CSV import commit writes 5,000 assets, so each run grows the database by that many; it is timed once per run (one sample), the preview 5 times.
- No numeric targets are set (criterion 18); outliers are reported to the owner, not tuned.

| Operation | Runs | Median (ms) | p95 (ms) |
|---|---:|---:|---:|
| Ticket list (page of 50) | 5 | 9 | 19 |
| Ticket search | 5 | 8 | 8 |
| Workspace queue (unassigned) | 5 | 7 | 19 |
| Asset list (page of 50) | 5 | 5 | 6 |
| Asset search (brand name) | 5 | 9 | 14 |
| Report: incidents | 5 | 23 | 30 |
| Report: requests | 5 | 16 | 22 |
| Report: operations | 5 | 14 | 14 |
| Report: transfers | 5 | 5 | 7 |
| Report: assets | 5 | 5 | 5 |
| Report: warranty | 5 | 3 | 3 |
| Drill-down page (incidents, mttr) | 5 | 4 | 5 |
| Synchronous Excel export (incidents, mttr) | 5 | 77 | 115 |
| CSV import preview (5000 rows) | 5 | 119 | 150 |
| CSV import commit (5000 rows, once per run) | 1 | 3130 | 3130 |
