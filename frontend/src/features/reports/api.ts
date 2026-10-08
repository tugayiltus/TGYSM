import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { TURKISH_LOCALE } from '../../shared/lib/turkish';
import { reportListPath } from '../../shared/routes';

import { filterQuery, type ReportFilterValues } from './useReportFilter';

type Schemas = components['schemas'];
export type ReportName = Schemas['ReportName'];
export type ReportFigure = Schemas['ReportFigure'];
export type ReportBucket = Schemas['ReportBucket'];
export type ReportResult = Schemas['ReportResult'];
export type DrilldownResult = Schemas['DrilldownResult'];
export type DrilldownTicketRow = Schemas['DrilldownTicketRow'];
export type DrilldownAssetRow = Schemas['DrilldownAssetRow'];
export type ExportJobItem = Schemas['ExportJobItem'];
export type ExportJobStatus = Schemas['ExportJobStatus'];

/** Tab order of the reports page (spec §7.2). */
export const REPORT_NAMES: readonly ReportName[] = ['incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty'];
export const DEFAULT_REPORT: ReportName = 'incidents';

export const isReportName = (value: string | undefined): value is ReportName => REPORT_NAMES.some((name) => name === value);

/** Which rows of a ratio a drill-down lists; the API defaults to the numerator. */
export type DrillPart = 'numerator' | 'denominator';
export const DRILL_PARTS: readonly DrillPart[] = ['numerator', 'denominator'];

export const FIRST_PAGE = 1;
export const DRILLDOWN_PAGE_SIZE = 20;
export const EXPORTS_PAGE_SIZE = 20;
/** Pending and running jobs are polled until the worker finishes them. */
export const EXPORTS_POLL_MS = 5_000;

/** Query parameter names shared by the drill-down URL, its API call and the export calls. */
export const DRILL_PARAM = { metric: 'metric', bucket: 'bucket', part: 'part', page: 'page', pageSize: 'pageSize', report: 'report' } as const;

export const reportKeys = {
  all: ['reports'],
  figures: (report: ReportName, filter: ReportFilterValues) => ['reports', 'figures', report, filter],
  drilldown: (report: ReportName, selection: DrillSelection, filter: ReportFilterValues, page: number) => ['reports', 'drilldown', report, selection, filter, page],
  exports: (page: number) => ['reports', 'exports', page],
  exportsRoot: ['reports', 'exports'],
};

/** One figure (and maybe one of its buckets or ratio parts) whose rows are listed or exported. */
export interface DrillSelection {
  metric: string;
  bucket: string;
  part: DrillPart;
}

/** The query string the API reads: selection first, then the filter; empty values are left out. */
export function selectionQuery(selection: DrillSelection, filter: ReportFilterValues): URLSearchParams {
  const query = new URLSearchParams();
  query.set(DRILL_PARAM.metric, selection.metric);
  if (selection.bucket !== '') {
    query.set(DRILL_PARAM.bucket, selection.bucket);
  }
  if (selection.part !== 'numerator') {
    query.set(DRILL_PARAM.part, selection.part);
  }
  for (const [key, value] of filterQuery(filter)) {
    query.set(key, value);
  }
  return query;
}

/** Drill-down URL of a figure, one of its buckets or its ratio denominator, carrying the report's filters. */
export function figureListHref(report: ReportName, metric: string, filter: ReportFilterValues, bucket = '', part: DrillPart = 'numerator'): string {
  return `${reportListPath(report)}?${selectionQuery({ metric, bucket, part }, filter).toString()}`;
}

/** The report endpoints bind their filter from the raw query string (ReportFilterBinder), which the OpenAPI schema does not list. */
const rawQuery = (query: URLSearchParams) => ({ querySerializer: () => query.toString() });

export function useReport(report: ReportName, filter: ReportFilterValues, enabled = true) {
  return useQuery({
    enabled,
    queryKey: reportKeys.figures(report, filter),
    queryFn: () => unwrap(api.GET('/api/v1/reports/{report}', { params: { path: { report } }, ...rawQuery(filterQuery(filter)) })),
    placeholderData: keepPreviousData,
  });
}

export function useDrilldown(report: ReportName, selection: DrillSelection, filter: ReportFilterValues, page: number) {
  return useQuery({
    queryKey: reportKeys.drilldown(report, selection, filter, page),
    queryFn: () => {
      const query = selectionQuery(selection, filter);
      query.set(DRILL_PARAM.page, String(page));
      query.set(DRILL_PARAM.pageSize, String(DRILLDOWN_PAGE_SIZE));
      return unwrap(api.GET('/api/v1/reports/{report}/drilldown', { params: { path: { report }, query: { metric: selection.metric } }, ...rawQuery(query) }));
    },
    placeholderData: keepPreviousData,
  });
}

const FILE_NAME_STAR = /filename\*=UTF-8''([^;]+)/i;
const FILE_NAME_PLAIN = /filename="?([^";]+)"?/i;
const EXPORT_EXTENSION = '.xlsx';

/** The download name from Content-Disposition (RFC 5987 form first), else `<report>.xlsx`. */
export function exportFileName(disposition: string | null, report: ReportName): string {
  const encoded = disposition?.match(FILE_NAME_STAR)?.[1];
  if (encoded !== undefined) {
    try {
      return decodeURIComponent(encoded);
    } catch {
      // A malformed RFC 5987 name must not lose the file already downloaded; fall back to the plain name.
    }
  }
  return disposition?.match(FILE_NAME_PLAIN)?.[1] ?? `${report}${EXPORT_EXTENSION}`;
}

function saveFile(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  // Revoke after the click has been handed to the browser; revoking at once can cancel the download in some browsers.
  setTimeout(() => URL.revokeObjectURL(url));
}

/** Synchronous Excel export; the API answers 409 `reports.export_too_large` above its row limit. */
export function useSyncExport(report: ReportName) {
  return useMutation({
    mutationFn: async ({ selection, filter }: { selection: DrillSelection; filter: ReportFilterValues }) => {
      const result = await api.GET('/api/v1/reports/{report}/export', {
        params: { path: { report }, query: { metric: selection.metric } },
        parseAs: 'blob',
        ...rawQuery(selectionQuery(selection, filter)),
      });
      const blob = await unwrap(Promise.resolve(result));
      saveFile(blob as Blob, exportFileName(result.response.headers.get('Content-Disposition'), report));
    },
  });
}

/** Queues a background export with the same selection and filter as the synchronous one. */
export function useCreateExportJob(report: ReportName) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ selection, filter }: { selection: DrillSelection; filter: ReportFilterValues }) => {
      const query = selectionQuery(selection, filter);
      query.set(DRILL_PARAM.report, report);
      return unwrap(api.POST('/api/v1/reports/exports', { params: { query: { report, metric: selection.metric } }, ...rawQuery(query) }));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: reportKeys.exportsRoot }),
  });
}

const isWorking = (status: ExportJobStatus) => status === 'pending' || status === 'running';

export function useMyExports(page: number) {
  return useQuery({
    queryKey: reportKeys.exports(page),
    queryFn: () => unwrap(api.GET('/api/v1/reports/exports', { params: { query: { page, pageSize: EXPORTS_PAGE_SIZE } } })),
    placeholderData: keepPreviousData,
    refetchInterval: (query) => (query.state.data?.items.some((job) => isWorking(job.status)) ? EXPORTS_POLL_MS : false),
  });
}

/** A plain link: the browser downloads with the session cookie, and the API re-checks the creator's scope. */
export const exportDownloadUrl = (id: string) => `/api/v1/reports/exports/${id}/download`;

/** Choices of the id filters; each list loads only when the report offers that filter. */
export function useFilterChoices(fields: readonly string[]) {
  const wants = (key: string) => fields.includes(key);
  const categories = useQuery({
    queryKey: [...reportKeys.all, 'reference', 'categories'],
    queryFn: () => unwrap(api.GET('/api/v1/reference/categories', { params: { query: {} } })),
    enabled: wants('categoryId'),
  });
  const teams = useQuery({
    queryKey: [...reportKeys.all, 'reference', 'teams'],
    queryFn: () => unwrap(api.GET('/api/v1/reference/teams')),
    enabled: wants('teamId') || wants('assigneeId'),
  });
  const departments = useQuery({
    queryKey: [...reportKeys.all, 'reference', 'departments'],
    queryFn: () => unwrap(api.GET('/api/v1/reference/departments')),
    enabled: wants('departmentId'),
  });
  const locations = useQuery({
    queryKey: [...reportKeys.all, 'reference', 'locations'],
    queryFn: () => unwrap(api.GET('/api/v1/reference/locations')),
    enabled: wants('locationId'),
  });

  const members = new Map<string, string>();
  for (const team of teams.data ?? []) {
    for (const member of team.members) {
      members.set(member.id, member.fullName);
    }
  }

  return {
    categoryId: (categories.data ?? []).map((c) => ({ value: c.id, label: c.path })),
    teamId: (teams.data ?? []).map((team) => ({ value: team.id, label: team.name })),
    assigneeId: [...members].map(([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label, TURKISH_LOCALE)),
    departmentId: (departments.data ?? []).map((d) => ({ value: d.id, label: d.name })),
    locationId: (locations.data ?? []).map((l) => ({ value: l.id, label: l.path })),
  };
}
