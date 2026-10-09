import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import packageJson from '../../../package.json';
import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { INCIDENTS, REFERENCE_ROUTES, reportResult } from '../../test/reportData';

const MS_PER_DAY = 86_400_000;
const LAST_30_SPAN_DAYS = 29;

const routes = () => ({
  'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
  'GET /api/v1/reports/incidents': { status: 200, body: INCIDENTS },
  'GET /api/v1/reports/assets': { status: 200, body: reportResult('assets', []) },
  ...REFERENCE_ROUTES,
});
const calls = (api: ReturnType<typeof mockApi>, report: string) =>
  api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === `/api/v1/reports/${report}`).map((c) => new URL(c.url).searchParams);

describe('report period', () => {
  it('opens a period report on the last 30 days', async () => {
    const api = mockApi(routes());
    renderRoute('/reports/incidents');
    expect(await screen.findByRole('button', { name: 'Son 30 gün' })).toHaveAttribute('aria-pressed', 'true');
    await waitFor(() => expect(calls(api, 'incidents').length).toBeGreaterThan(0));
    const query = calls(api, 'incidents')[0] as URLSearchParams;
    const span = (Date.parse(query.get('to') ?? '') - Date.parse(query.get('from') ?? '')) / MS_PER_DAY;
    expect(span).toBe(LAST_30_SPAN_DAYS);
  });

  it('sends no dates for the asset snapshot report', async () => {
    const api = mockApi(routes());
    renderRoute('/reports/assets');
    await waitFor(() => expect(calls(api, 'assets').length).toBeGreaterThan(0));
    expect(calls(api, 'assets')[0]?.has('from')).toBe(false);
    expect(screen.queryByRole('button', { name: 'Son 30 gün' })).not.toBeInTheDocument();
  });

  it('switches to a quick period through the URL', async () => {
    mockApi(routes());
    const { user, router } = renderRoute('/reports/incidents');
    await user.click(await screen.findByRole('button', { name: 'Son 7 gün' }));
    const params = new URLSearchParams(router.state.location.search);
    expect((Date.parse(params.get('to') ?? '') - Date.parse(params.get('from') ?? '')) / MS_PER_DAY).toBe(6);
    expect(screen.getByRole('button', { name: 'Son 7 gün' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('warns when a date range in the URL ends before it starts (UX-12)', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents?from=2026-05-10&to=2026-05-01');
    expect(await screen.findByText('Başlangıç tarihi bitişten sonra olduğu için tarih aralığı yok sayıldı.')).toBeInTheDocument();
  });
});

describe('report filters', () => {
  it('puts the filters behind a counted button on phones and lists active ones as removable chips', async () => {
    mockApi(routes());
    const { user, router } = renderRoute('/reports/incidents?priority=p1');
    expect(await screen.findByRole('button', { name: 'Filtreler (1)' })).toBeInTheDocument();
    const chips = screen.getByRole('list', { name: 'Etkin filtreler' });
    await user.click(within(chips).getByRole('button', { name: /Öncelik/ }));
    await waitFor(() => expect(new URLSearchParams(router.state.location.search).has('priority')).toBe(false));
  });
});

describe('charts', () => {
  it('uses Chart.js through CoreUI; Recharts is gone', () => {
    const dependencies: Record<string, string> = { ...packageJson.dependencies };
    expect(dependencies).not.toHaveProperty('recharts');
    expect(dependencies).toHaveProperty('@coreui/react-chartjs');
  });
});
