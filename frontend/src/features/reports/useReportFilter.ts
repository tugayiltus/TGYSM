import { useState } from 'react';
import { useSearchParams } from 'react-router';

import type { components } from '../../shared/api/schema';

import type { ReportName } from './api';
import { companyToday, PERIODIC_REPORTS, presetPeriod, type Period } from './period';

type Schemas = components['schemas'];

/** Query names of the API's ReportFilter (ReportFilterKeys on the server), in the server's order. */
export const REPORT_FILTER_KEYS = [
  'from',
  'to',
  'kind',
  'priority',
  'categoryId',
  'teamId',
  'assigneeId',
  'departmentId',
  'locationId',
  'status',
  'slaState',
  'assetType',
  'assetStatus',
] as const;

export type ReportFilterKey = (typeof REPORT_FILTER_KEYS)[number];

/** Set filters only, as their query values (ISO day, snake_case enum, guid). */
export type ReportFilterValues = Partial<Record<ReportFilterKey, string>>;

export const TICKET_KINDS: readonly Schemas['TicketKind'][] = ['incident', 'request'];
export const PRIORITIES: readonly Schemas['Priority'][] = ['p1', 'p2', 'p3', 'p4'];
export const TICKET_STATUSES: readonly Schemas['TicketStatus'][] = ['open', 'in_progress', 'on_hold', 'completed', 'cancelled'];
export const SLA_STATES: readonly NonNullable<Schemas['SlaStateFilter']>[] = ['met', 'breached', 'open_breached', 'at_risk'];
export const ASSET_TYPES: readonly Schemas['AssetType'][] = ['desktop', 'laptop', 'phone', 'tablet'];
export const ASSET_STATUSES: readonly Schemas['AssetStatus'][] = ['in_stock', 'assigned', 'maintenance', 'lost', 'retired'];

/** Enum filters and their allowed values; the select fields use the same lists. */
export const ENUM_FILTERS = {
  kind: TICKET_KINDS,
  priority: PRIORITIES,
  status: TICKET_STATUSES,
  slaState: SLA_STATES,
  assetType: ASSET_TYPES,
  assetStatus: ASSET_STATUSES,
} as const satisfies Partial<Record<ReportFilterKey, readonly string[]>>;

/** Where each enum filter finds its value names (`<namespace>:<object>.<value>`); shared by the filter bar and the chips. */
export const ENUM_FILTER_LABELS: Readonly<Record<keyof typeof ENUM_FILTERS, string>> = {
  kind: 'tickets:kinds',
  priority: 'tickets:priorities',
  status: 'tickets:statuses',
  slaState: 'reports:slaStates',
  assetType: 'assets:types',
  assetStatus: 'assets:statuses',
};

const TICKET_FIELDS: readonly ReportFilterKey[] = ['from', 'to', 'priority', 'categoryId', 'teamId', 'assigneeId', 'departmentId', 'locationId', 'status', 'slaState'];
const ASSET_FIELDS: readonly ReportFilterKey[] = ['assetType', 'assetStatus', 'departmentId', 'locationId'];

/**
 * Filters each report applies. Incident and request reports force their own kind on the server, so they offer none;
 * the asset report is a snapshot (no days); the warranty report uses the days as its expiry window.
 */
export const REPORT_FILTER_FIELDS: Record<ReportName, readonly ReportFilterKey[]> = {
  incidents: TICKET_FIELDS,
  requests: TICKET_FIELDS,
  operations: ['kind', ...TICKET_FIELDS],
  transfers: ['kind', ...TICKET_FIELDS],
  assets: ASSET_FIELDS,
  warranty: ['from', 'to', ...ASSET_FIELDS],
};

const ISO_DAY = /^\d{4}-\d{2}-\d{2}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const ISO_DAY_LENGTH = 10;
/** The server's ReportFilter.MinDay / MaxDay; days outside are refused there. */
const MIN_DAY = '2000-01-01';
const MAX_DAY = '2100-12-31';
export const DAY_BOUNDS = { min: MIN_DAY, max: MAX_DAY } as const;

/** A real calendar day in the server's accepted range ("2026-02-30" is not one). */
const isDay = (value: string) =>
  ISO_DAY.test(value) && !Number.isNaN(Date.parse(value)) && new Date(value).toISOString().slice(0, ISO_DAY_LENGTH) === value && value >= MIN_DAY && value <= MAX_DAY;

const isEnumKey = (key: ReportFilterKey): key is keyof typeof ENUM_FILTERS => key in ENUM_FILTERS;

export function isValidFilterValue(key: ReportFilterKey, value: string): boolean {
  if (key === 'from' || key === 'to') {
    return isDay(value);
  }
  if (isEnumKey(key)) {
    return (ENUM_FILTERS[key] as readonly string[]).includes(value);
  }
  return GUID.test(value);
}

/** The report's filters from a query string; unknown, irrelevant and invalid values are dropped. */
export function readReportFilter(params: URLSearchParams, report: ReportName): ReportFilterValues {
  const filter: ReportFilterValues = {};
  for (const key of REPORT_FILTER_FIELDS[report]) {
    const value = params.get(key);
    if (value !== null && isValidFilterValue(key, value)) {
      filter[key] = value;
    }
  }
  // A hand-edited range that ends before it starts is refused by the server; drop both days instead.
  if (filter.from !== undefined && filter.to !== undefined && filter.from > filter.to) {
    delete filter.from;
    delete filter.to;
  }
  return filter;
}

const DATE_KEYS: readonly ReportFilterKey[] = ['from', 'to'];

/** Set filters other than the period (shown as chips and counted on the phone filter button). */
export function activeFilterKeys(filter: ReportFilterValues): ReportFilterKey[] {
  return (Object.keys(filter) as ReportFilterKey[]).filter((key) => !DATE_KEYS.includes(key) && filter[key] !== undefined);
}

/** True when the URL has both days and the range ends before it starts (dropped by readReportFilter, UX-12). */
export function hasReversedRange(params: URLSearchParams): boolean {
  const from = params.get('from');
  const to = params.get('to');
  return from !== null && to !== null && isValidFilterValue('from', from) && isValidFilterValue('to', to) && from > to;
}

/** The set filters as query parameters, in the server's key order. */
export function filterQuery(filter: ReportFilterValues): URLSearchParams {
  const query = new URLSearchParams();
  for (const key of REPORT_FILTER_KEYS) {
    const value = filter[key];
    if (value !== undefined && value !== '') {
      query.set(key, value);
    }
  }
  return query;
}

/** Changing a filter shows its first page again. */
const PAGE_PARAM = 'page';

/** Reads and writes the report filter in the URL, so a filtered view can be bookmarked, shared and restored. */
export function useReportFilter(report: ReportName) {
  const [params, setParams] = useSearchParams();
  // Read once per mount: "today" for the default period must not change mid-session on its own.
  const [today] = useState(companyToday);
  const read = readReportFilter(params, report);
  const periodic = PERIODIC_REPORTS.includes(report);
  // Period reports open on the last 30 days unless the URL names days (spec §6.2); the URL stays clean.
  const filter = periodic && read.from === undefined && read.to === undefined ? { ...read, ...presetPeriod('last30', today) } : read;

  const setFilter = (key: ReportFilterKey, value: string) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value === '' || !isValidFilterValue(key, value)) {
          next.delete(key);
        } else {
          next.set(key, value);
        }
        next.delete(PAGE_PARAM);
        return next;
      },
      { replace: true },
    );

  const clear = () =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        for (const key of REPORT_FILTER_KEYS) {
          next.delete(key);
        }
        next.delete(PAGE_PARAM);
        return next;
      },
      { replace: true },
    );

  const setPeriod = (period: Period) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set('from', period.from);
        next.set('to', period.to);
        next.delete(PAGE_PARAM);
        return next;
      },
      { replace: true },
    );

  return { filter, setFilter, setPeriod, clear, periodic, today, reversedRange: hasReversedRange(params) };
}
