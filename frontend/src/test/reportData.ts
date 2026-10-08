import type { DrilldownResult, ExportJobItem, ReportFigure, ReportResult } from '../features/reports/api';

const SECONDS_PER_HOUR = 3_600;

export const TEAM_ID = '00000000-0000-7000-8000-0000000000b1';

export const VOLUME: ReportFigure = { metric: 'volume', shape: 'count', value: 12, numerator: null, denominator: null, buckets: null, unit: null };

export const FIRST_RESPONSE_NO_DATA: ReportFigure = {
  metric: 'first_response_time',
  shape: 'duration',
  value: null,
  numerator: null,
  denominator: 0,
  buckets: null,
  unit: 'seconds',
};

export const SLA_COMPLIANCE: ReportFigure = { metric: 'sla_compliance', shape: 'ratio', value: 0.75, numerator: 3, denominator: 4, buckets: null, unit: null };

/** Two incidents, 2 h calendar time each, 1 h SLA time each (bucket holds the SLA total). */
export const MTTR: ReportFigure = {
  metric: 'mttr',
  shape: 'ratio',
  value: 2 * SECONDS_PER_HOUR,
  numerator: 4 * SECONDS_PER_HOUR,
  denominator: 2,
  buckets: [{ key: 'mttr_sla', label: null, count: 2 * SECONDS_PER_HOUR }],
  unit: 'seconds',
};

export const BACKLOG: ReportFigure = {
  metric: 'open_backlog',
  shape: 'distribution',
  value: 3,
  numerator: null,
  denominator: null,
  buckets: [
    { key: '0-1', label: null, count: 2 },
    { key: '8+', label: null, count: 1 },
  ],
  unit: null,
};

export const TEAM_LOAD: ReportFigure = {
  metric: 'team_load',
  shape: 'distribution',
  value: 5,
  numerator: null,
  denominator: null,
  buckets: [{ key: TEAM_ID, label: 'Ağ Ekibi', count: 5 }],
  unit: null,
};

export const reportResult = (report: ReportResult['report'], figures: ReportFigure[], scopeLabel = 'all'): ReportResult => ({
  report,
  filter: {} as ReportResult['filter'],
  scopeLabel,
  figures,
});

export const INCIDENTS = reportResult('incidents', [VOLUME, FIRST_RESPONSE_NO_DATA, SLA_COMPLIANCE, MTTR, BACKLOG]);

export const ticketRow = (n: number) => ({
  number: `INC-${String(n).padStart(6, '0')}`,
  kind: 'incident' as const,
  subject: `Yazıcı arızası ${n}`,
  status: 'open' as const,
  priority: 'p2' as const,
  teamName: 'Ağ Ekibi',
  assigneeName: n % 2 === 0 ? 'Can Demir' : null,
  createdAt: '2026-10-01T09:00:00Z',
  completedAt: null,
});

export const ticketDrilldown = (page: number, count: number, totalCount: number): DrilldownResult => ({
  kind: 'tickets',
  tickets: { items: Array.from({ length: count }, (_, i) => ticketRow(i + 1 + (page - 1) * 20)), page, pageSize: 20, totalCount },
  assets: null,
});

export const ASSET_DRILLDOWN: DrilldownResult = {
  kind: 'assets',
  tickets: null,
  assets: {
    items: [
      {
        id: '00000000-0000-7000-8000-0000000000c1',
        assetTag: 'PC-0001',
        type: 'laptop',
        brand: 'Lenovo',
        model: 'T14',
        status: 'assigned',
        locationName: 'Merkez',
        warrantyEndsOn: '2026-12-31',
      },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
  },
};

export const exportJob = (id: string, status: ExportJobItem['status'], rowCount: number | null = null): ExportJobItem => ({
  id,
  report: 'incidents',
  metric: 'volume',
  status,
  rowCount,
  createdAt: '2026-10-01T09:00:00Z',
  expiresAt: status === 'ready' || status === 'expired' ? '2026-10-02T09:00:00Z' : null,
});

export const pagedJobs = (items: ExportJobItem[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

/** Reference lists the filter bar loads. */
export const REFERENCE_ROUTES = {
  'GET /api/v1/reference/categories': { status: 200, body: [{ id: 'c-1', path: 'Ağ', parentId: null }] },
  'GET /api/v1/reference/teams': { status: 200, body: [{ id: TEAM_ID, name: 'Ağ Ekibi', members: [{ id: 'u-1', fullName: 'Can Demir' }] }] },
  'GET /api/v1/reference/departments': { status: 200, body: [{ id: 'd-1', name: 'Muhasebe' }] },
  'GET /api/v1/reference/locations': { status: 200, body: [{ id: 'l-1', path: 'Merkez' }] },
} as const;
