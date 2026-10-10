import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { performance } from 'node:perf_hooks';

import { request, type APIRequestContext } from '@playwright/test';

/**
 * Timing run for docs/release/performance.md (spec 5.2), started by tools/perf-measure.sh against a seeded stack.
 * Signs in as IT staff and as the admin, calls every endpoint PERF_RUNS times and prints a Markdown table of
 * median and 95th percentile. Any non-2xx answer (or an import preview with row errors) aborts with a non-zero exit.
 */

const CSRF_PATH = '/api/v1/auth/csrf';
const LOGIN_PATH = '/api/v1/auth/login';
const CSRF_HEADER = 'X-CSRF-TOKEN';
/** The API marks its antiforgery cookie Secure; this header makes it treat the plain-HTTP local call as HTTPS. */
const FORWARDED_PROTO_HEADER = 'X-Forwarded-Proto';
const FORWARDED_PROTO = 'https';
const DEFAULT_RUNS = 5;
const PERCENTILE_95 = 0.95;
const MEDIAN = 0.5;
const LIST_PAGE_SIZE = 50;
const IMPORT_ROWS = 5000;
const MIN_SEARCH_TERM_LENGTH = 4;
const CSV_BOM = '﻿';
const CSV_LINE_BREAK = '\r\n';
const CSV_HEADER = ['Demirbaş No', 'Tür', 'Marka', 'Model', 'Seri No', 'Konum', 'Departman', 'Durum', 'Garanti Bitiş', 'Satın Alma', 'Notlar'];
const IMPORT_STATUS = 'in_stock';
const IMPORT_FILE_NAME = 'perf-import.csv';
const IMPORT_MIME = 'text/csv';
const REPORTS = ['incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty'] as const;
const DRILLDOWN_REPORT = 'incidents';
const MS_DIGITS = 0;
/** The CSV matches a location by its trailing path segments split on this; a name that itself holds it cannot be addressed. */
const LOCATION_SEPARATOR = '/';

interface Sample {
  label: string;
  runs: number;
  medianMs: number;
  p95Ms: number;
}
interface ReportFigure {
  metric: string;
  numerator: number | null;
}
interface CatalogBrand {
  name: string;
  models: { type: string; name: string }[];
}

function env(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set; run this through tools/perf-measure.sh.`);
  }
  return value;
}

function quantile(sorted: number[], fraction: number): number {
  const index = Math.min(sorted.length - 1, Math.ceil(sorted.length * fraction) - 1);
  return sorted[Math.max(0, index)] ?? 0;
}

function summarize(label: string, durations: number[]): Sample {
  const sorted = [...durations].sort((a, b) => a - b);
  return { label, runs: sorted.length, medianMs: quantile(sorted, MEDIAN), p95Ms: quantile(sorted, PERCENTILE_95) };
}

async function signIn(baseURL: string, email: string, password: string): Promise<APIRequestContext> {
  const context = await request.newContext({ baseURL, extraHTTPHeaders: { [FORWARDED_PROTO_HEADER]: FORWARDED_PROTO } });
  const csrf = (await (await context.get(CSRF_PATH)).json()) as { token: string };
  const response = await context.post(LOGIN_PATH, { data: { email, password }, headers: { [CSRF_HEADER]: csrf.token } });
  if (!response.ok()) {
    throw new Error(`Sign-in of ${email} failed: ${response.status()}`);
  }
  return context;
}

async function getJson<T>(context: APIRequestContext, path: string): Promise<T> {
  const response = await context.get(path);
  if (!response.ok()) {
    throw new Error(`GET ${path} -> ${response.status()}`);
  }
  return (await response.json()) as T;
}

/** One timed GET: the clock stops once the whole body has arrived. */
async function timedGet(context: APIRequestContext, path: string): Promise<number> {
  const started = performance.now();
  const response = await context.get(path);
  await response.body();
  const elapsed = performance.now() - started;
  if (!response.ok()) {
    throw new Error(`GET ${path} -> ${response.status()}`);
  }
  return elapsed;
}

async function measureGet(context: APIRequestContext, label: string, path: string, runs: number): Promise<Sample> {
  const durations: number[] = [];
  for (let run = 0; run < runs; run++) {
    durations.push(await timedGet(context, path));
  }
  return summarize(label, durations);
}

function csvCell(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}

/** A 5,000-row import file with catalog brand/model names and seeded locations; an empty tag is generated at commit. */
function buildImportCsv(catalog: CatalogBrand[], locations: string[]): string {
  const models = catalog.flatMap((brand) => brand.models.map((model) => ({ brand: brand.name, ...model })));
  if (models.length === 0 || locations.length === 0) {
    throw new Error('The seeded database has no catalog models or locations to build the import file from.');
  }
  const rows = [CSV_HEADER.map(csvCell).join(',')];
  for (let index = 0; index < IMPORT_ROWS; index++) {
    const model = models[index % models.length];
    const location = locations[index % locations.length];
    if (!model || !location) {
      throw new Error('Import row source ran out.');
    }
    const cells = ['', model.type, model.brand, model.name, '', location, '', IMPORT_STATUS, '', '', ''];
    rows.push(cells.map(csvCell).join(','));
  }
  return CSV_BOM + rows.join(CSV_LINE_BREAK) + CSV_LINE_BREAK;
}

async function postPreview(context: APIRequestContext, csv: Buffer): Promise<{ elapsed: number; jobId: string }> {
  const csrf = (await (await context.get(CSRF_PATH)).json()) as { token: string };
  const started = performance.now();
  const response = await context.post('/api/v1/assets/import/preview', {
    multipart: { file: { name: IMPORT_FILE_NAME, mimeType: IMPORT_MIME, buffer: csv } },
    headers: { [CSRF_HEADER]: csrf.token },
  });
  const body = (await response.json()) as { jobId: string; totalRows: number; errorRows: number; errors: { row: number; field: string; code: string }[] };
  const elapsed = performance.now() - started;
  if (!response.ok()) {
    throw new Error(`Import preview -> ${response.status()}`);
  }
  if (body.errorRows !== 0 || body.totalRows !== IMPORT_ROWS) {
    const first = body.errors[0];
    const example = first ? ` First: row ${first.row}, ${first.field}, ${first.code}.` : '';
    throw new Error(`Import preview found ${body.errorRows} error rows in ${body.totalRows} rows.${example}`);
  }
  return { elapsed, jobId: body.jobId };
}

async function postCommit(context: APIRequestContext, jobId: string): Promise<number> {
  const csrf = (await (await context.get(CSRF_PATH)).json()) as { token: string };
  const started = performance.now();
  const response = await context.post(`/api/v1/assets/import/${jobId}/commit`, { headers: { [CSRF_HEADER]: csrf.token } });
  await response.body();
  const elapsed = performance.now() - started;
  if (!response.ok()) {
    throw new Error(`Import commit -> ${response.status()}`);
  }
  return elapsed;
}

function searchTerm(subject: string): string {
  const word = subject.split(/\s+/).find((candidate) => candidate.length >= MIN_SEARCH_TERM_LENGTH);
  return word ?? subject;
}

function table(samples: Sample[]): string {
  const lines = ['| Operation | Runs | Median (ms) | p95 (ms) |', '|---|---:|---:|---:|'];
  for (const sample of samples) {
    lines.push(`| ${sample.label} | ${sample.runs} | ${sample.medianMs.toFixed(MS_DIGITS)} | ${sample.p95Ms.toFixed(MS_DIGITS)} |`);
  }
  return lines.join('\n');
}

async function main(): Promise<void> {
  const baseURL = env('PERF_BASE_URL');
  const password = env('PERF_PASSWORD');
  const runs = Number(process.env['PERF_RUNS'] ?? DEFAULT_RUNS);
  const it = await signIn(baseURL, env('PERF_IT_EMAIL'), password);
  const admin = await signIn(baseURL, env('PERF_ADMIN_EMAIL'), password);
  const samples: Sample[] = [];

  const firstTickets = await getJson<{ items: { subject: string }[]; totalCount: number }>(it, `/api/v1/tickets?pageSize=${LIST_PAGE_SIZE}`);
  const firstAssets = await getJson<{ totalCount: number }>(it, `/api/v1/assets?pageSize=${LIST_PAGE_SIZE}`);
  const catalog = await getJson<CatalogBrand[]>(it, '/api/v1/assets/catalog');
  const locations = (await getJson<{ path: string }[]>(it, '/api/v1/reference/locations'))
    .map((location) => location.path)
    .filter((path) => !path.includes(LOCATION_SEPARATOR));
  const ticketTerm = encodeURIComponent(searchTerm(firstTickets.items[0]?.subject ?? ''));
  const assetTerm = encodeURIComponent(catalog[0]?.name ?? '');

  samples.push(await measureGet(it, 'Ticket list (page of 50)', `/api/v1/tickets?pageSize=${LIST_PAGE_SIZE}`, runs));
  samples.push(await measureGet(it, 'Ticket search', `/api/v1/tickets?search=${ticketTerm}&pageSize=${LIST_PAGE_SIZE}`, runs));
  samples.push(await measureGet(it, 'Workspace queue (unassigned)', `/api/v1/tickets?queue=unassigned&pageSize=${LIST_PAGE_SIZE}`, runs));
  samples.push(await measureGet(it, 'Asset list (page of 50)', `/api/v1/assets?pageSize=${LIST_PAGE_SIZE}`, runs));
  samples.push(await measureGet(it, 'Asset search (brand name)', `/api/v1/assets?search=${assetTerm}&pageSize=${LIST_PAGE_SIZE}`, runs));
  for (const report of REPORTS) {
    samples.push(await measureGet(admin, `Report: ${report}`, `/api/v1/reports/${report}`, runs));
  }

  const incidents = await getJson<{ figures: ReportFigure[] }>(admin, `/api/v1/reports/${DRILLDOWN_REPORT}`);
  const metric = incidents.figures.find((figure) => figure.numerator !== null)?.metric;
  if (!metric) {
    throw new Error(`Report ${DRILLDOWN_REPORT} has no figure to drill into.`);
  }
  const metricQuery = `metric=${encodeURIComponent(metric)}`;
  samples.push(await measureGet(admin, `Drill-down page (${DRILLDOWN_REPORT}, ${metric})`, `/api/v1/reports/${DRILLDOWN_REPORT}/drilldown?${metricQuery}&pageSize=${LIST_PAGE_SIZE}`, runs));
  samples.push(await measureGet(admin, `Synchronous Excel export (${DRILLDOWN_REPORT}, ${metric})`, `/api/v1/reports/${DRILLDOWN_REPORT}/export?${metricQuery}`, runs));

  // The file lives in the script's temp folder (removed by the script), so a failed run can be inspected.
  const csvPath = join(env('PERF_TEMP_DIR'), IMPORT_FILE_NAME);
  await writeFile(csvPath, buildImportCsv(catalog, locations), 'utf8');
  const csv = await readFile(csvPath);
  const previews: number[] = [];
  let jobId = '';
  for (let run = 0; run < runs; run++) {
    const preview = await postPreview(admin, csv);
    previews.push(preview.elapsed);
    jobId = preview.jobId;
  }
  samples.push(summarize(`CSV import preview (${IMPORT_ROWS} rows)`, previews));
  samples.push(summarize(`CSV import commit (${IMPORT_ROWS} rows, once per run)`, [await postCommit(admin, jobId)]));

  await it.dispose();
  await admin.dispose();
  console.log(`VOLUME tickets=${firstTickets.totalCount} assets=${firstAssets.totalCount}`);
  console.log(table(samples));
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
