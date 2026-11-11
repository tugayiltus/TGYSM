import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import packageJson from '../../../package.json';
import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { INCIDENTS, REFERENCE_ROUTES, reportResult } from '../../test/reportData';
import { chooseOption } from '../../test/select';
import { stubPhoneViewport } from '../../test/viewport';

import { splitFilterFields } from './useReportFilter';

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

  it('offers the quick periods in a selector on phones', async () => {
    stubPhoneViewport();
    mockApi(routes());
    const { user, router } = renderRoute('/reports/incidents');
    await chooseOption(user, 'Dönem', 'Son 7 gün');
    const params = new URLSearchParams(router.state.location.search);
    expect((Date.parse(params.get('to') ?? '') - Date.parse(params.get('from') ?? '')) / MS_PER_DAY).toBe(6);
    expect(screen.queryByRole('button', { name: 'Son 7 gün' })).not.toBeInTheDocument();
  });

  it('warns when a date range in the URL ends before it starts (UX-12)', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents?from=2026-05-10&to=2026-05-01');
    expect(await screen.findByText('Başlangıç tarihi bitişten sonra olduğu için tarih aralığı yok sayıldı.')).toBeInTheDocument();
  });
});

describe('report filters', () => {
  it('applies filters as they change, so wide screens get one clear button and no apply button', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents?priority=p1');
    const filters = await screen.findByRole('region', { name: 'Filtreler' });
    expect(within(filters).getAllByRole('button', { name: /temizle/i })).toHaveLength(1);
    expect(within(filters).queryByRole('button', { name: 'Uygula' })).not.toBeInTheDocument();
  });

  it('puts the filters behind a counted button on phones and lists active ones as removable chips', async () => {
    stubPhoneViewport();
    mockApi(routes());
    const { user, router } = renderRoute('/reports/incidents?priority=p1');
    expect(await screen.findByRole('button', { name: 'Filtreler (1)' })).toBeInTheDocument();
    const chips = screen.getByRole('list', { name: 'Etkin filtreler' });
    await user.click(within(chips).getByRole('button', { name: /Öncelik/ }));
    await waitFor(() => expect(new URLSearchParams(router.state.location.search).has('priority')).toBe(false));
  });
});

describe('report filter layout', () => {
  const CATEGORY = '00000000-0000-7000-8000-00000000ca71';

  it('puts period, priority, status and team first and keeps the rest in report order', () => {
    expect(splitFilterFields('incidents')).toEqual({
      main: ['from', 'to', 'priority', 'status', 'teamId'],
      other: ['categoryId', 'assigneeId', 'departmentId', 'locationId', 'slaState'],
    });
    expect(splitFilterFields('operations').other[0]).toBe('kind');
    expect(splitFilterFields('assets')).toEqual({ main: ['assetType', 'assetStatus'], other: ['departmentId', 'locationId'] });
  });

  it('keeps minor filters under a closed "Diğer filtreler" that counts an active one from the URL', async () => {
    mockApi(routes());
    const { user } = renderRoute(`/reports/incidents?categoryId=${CATEGORY}`);
    const more = await screen.findByRole('button', { name: 'Diğer filtreler (1)' });
    expect(more).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('combobox', { name: 'Öncelik' })).toBeVisible();
    expect(within(screen.getByRole('list', { name: 'Etkin filtreler' })).getByRole('button', { name: /Kategori/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Temizle' })).toBeVisible();

    // jsdom loads no StyleX CSS, so the collapsed state is checked on the trigger; E2E checks the height.
    await user.click(more);
    expect(more).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('combobox', { name: 'Kategori' })).toBeInTheDocument();
  });
});

describe('report navigation', () => {
  it('lets phones pick the report from one selector and keeps only the filters the target understands', async () => {
    stubPhoneViewport();
    mockApi({ ...routes(), 'GET /api/v1/reports/requests': { status: 200, body: reportResult('requests', []) } });
    const { user, router } = renderRoute('/reports/operations?kind=incident&priority=p1');
    const selector = await screen.findByRole('combobox', { name: 'Rapor türü' });
    expect(screen.queryByRole('navigation', { name: 'Rapor türleri' })).not.toBeInTheDocument();
    expect(screen.queryAllByRole('tab')).toHaveLength(0);
    expect(selector).toHaveTextContent('Operasyon');
    await chooseOption(user, 'Rapor türü', 'Talepler');
    await waitFor(() => expect(router.state.location.pathname).toBe('/reports/requests'));
    expect(router.state.location.search).toBe('?priority=p1');
  });

  it('marks wide-screen report links as navigation with the current page, and no tab lacks its panel', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents');
    const nav = await screen.findByRole('navigation', { name: 'Rapor türleri' });
    expect(within(nav).getByRole('link', { name: 'Olaylar' })).toHaveAttribute('aria-current');
    for (const tab of document.querySelectorAll('[role="tab"]')) {
      const controls = tab.getAttribute('aria-controls');
      expect(controls !== null && document.getElementById(controls) !== null).toBe(true);
    }
  });
});

describe('charts', () => {
  it('uses Chart.js through react-chartjs-2; Recharts and the CoreUI chart wrapper are gone', () => {
    const dependencies: Record<string, string> = { ...packageJson.dependencies };
    expect(dependencies).not.toHaveProperty('recharts');
    expect(dependencies).not.toHaveProperty('@coreui/react-chartjs');
    expect(dependencies).toHaveProperty('react-chartjs-2');
  });
});
