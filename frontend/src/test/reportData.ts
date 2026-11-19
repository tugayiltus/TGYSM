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
  changes: null,
  tasks: null,
});

export const ASSET_DRILLDOWN: DrilldownResult = {
  kind: 'assets',
  tickets: null,
  changes: null,
  tasks: null,
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

const bucketsOf = (...entries: [string, number][]) => entries.map(([key, count]) => ({ key, label: null, count }));
const baseFigure = { numerator: null, denominator: null, buckets: null, unit: null } as const;

export const CHANGE_REPORT = reportResult('changes', [
  { ...baseFigure, metric: 'by_outcome', shape: 'distribution', value: 4, buckets: bucketsOf(['successful', 3], ['rolled_back', 1], ['failed', 0]) },
  { ...baseFigure, metric: 'success_rate', shape: 'ratio', value: 0.75, numerator: 3, denominator: 4 },
  { ...baseFigure, metric: 'by_risk', shape: 'distribution', value: 5, buckets: bucketsOf(['low', 2], ['medium', 2], ['high', 1]) },
  { ...baseFigure, metric: 'cancelled', shape: 'count', value: 2 },
  { ...baseFigure, metric: 'caused_incidents', shape: 'count', value: 1 },
]);

/** Two tasks closed in 2 h and 4 h: average 3 h, median 2 h. */
export const TASK_COMPLETION: ReportFigure = {
  ...baseFigure,
  metric: 'completion_time',
  shape: 'duration',
  value: 3 * SECONDS_PER_HOUR,
  denominator: 2,
  buckets: bucketsOf(['median', 2 * SECONDS_PER_HOUR]),
  unit: 'seconds',
};

export const TASK_REPORT = reportResult('tasks', [
  { ...baseFigure, metric: 'overdue', shape: 'count', value: 3 },
  { ...baseFigure, metric: 'by_status', shape: 'distribution', value: 5, buckets: bucketsOf(['open', 2], ['in_progress', 1], ['completed', 2]) },
  TASK_COMPLETION,
  { ...baseFigure, metric: 'by_team', shape: 'distribution', value: 5, buckets: [{ key: TEAM_ID, label: 'Ağ Ekibi', count: 5 }] },
]);

export const CHANGE_DRILLDOWN: DrilldownResult = {
  kind: 'changes',
  tickets: null,
  assets: null,
  tasks: null,
  changes: {
    items: [
      { number: 'CHG-000001', title: 'Güvenlik duvarı güncellemesi', status: 'closed', outcome: 'rolled_back', risk: 'high', ownerTeamName: 'Ağ Ekibi', windowStart: '2026-09-10T20:00:00Z', actualEnd: '2026-09-10T22:00:00Z' },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
  },
};

export const TASK_DRILLDOWN: DrilldownResult = {
  kind: 'tasks',
  tickets: null,
  assets: null,
  changes: null,
  tasks: {
    items: [
      { number: 'TSK-000001', title: 'Kablo çek', status: 'in_progress', priority: 'p2', teamName: 'Ağ Ekibi', assigneeName: 'Can Demir', dueDate: '2026-09-12', createdAt: '2026-09-01T08:00:00Z', closedAt: null },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
  },
};
