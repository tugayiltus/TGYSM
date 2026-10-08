import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { ASSET_DRILLDOWN, INCIDENTS, reportResult, TEAM_ID, TEAM_LOAD, ticketDrilldown } from '../../test/reportData';

const PAGE_SIZE = 20;
const TOTAL = 25;

const drillCalls = (api: ReturnType<typeof mockApi>, report = 'incidents') =>
  api.calls.filter((c) => new URL(c.url).pathname === `/api/v1/reports/${report}/drilldown`).map((c) => new URL(c.url).searchParams);

const pagedRoute = (request: Request) => {
  const page = Number(new URL(request.url).searchParams.get('page'));
  return { status: 200, body: ticketDrilldown(page, page === 1 ? PAGE_SIZE : TOTAL - PAGE_SIZE, TOTAL) };
};

describe('DrilldownPage', () => {
  it('lists the rows of a figure with its filters and pages through them', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/reports/incidents': { status: 200, body: INCIDENTS },
      'GET /api/v1/reports/incidents/drilldown': pagedRoute,
    });
    const { user, router } = renderRoute('/reports/incidents/list?metric=volume&priority=p1');

    expect(await screen.findByRole('heading', { name: 'Açılan kayıt' })).toBeInTheDocument();
    expect((await screen.findAllByRole('link', { name: 'INC-000001' }))[0]).toHaveAttribute('href', '/tickets/INC-000001');
    expect(screen.getByText('1–20 / 25 kayıt')).toBeInTheDocument();
    const first = drillCalls(api)[0];
    expect(first?.get('metric')).toBe('volume');
    expect(first?.get('priority')).toBe('p1');
    expect(first?.get('page')).toBe('1');
    expect(first?.get('pageSize')).toBe(String(PAGE_SIZE));

    await user.click(screen.getByRole('button', { name: 'Sonraki' }));

    expect((await screen.findAllByRole('link', { name: 'INC-000021' }))[0]).toBeInTheDocument();
    expect(new URLSearchParams(router.state.location.search).get('page')).toBe('2');
    expect(drillCalls(api).at(-1)?.get('page')).toBe('2');
    expect(screen.getByRole('link', { name: 'Rapora dön' })).toHaveAttribute('href', '/reports/incidents?priority=p1');
  });

  it('names the bucket and the denominator part', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/reports/operations': { status: 200, body: reportResult('operations', [TEAM_LOAD]) },
      'GET /api/v1/reports/operations/drilldown': { status: 200, body: ticketDrilldown(1, 1, 1) },
    });
    renderRoute(`/reports/operations/list?metric=team_load&bucket=${TEAM_ID}&part=denominator&page=x`);

    expect(await screen.findByText('Ağ Ekibi')).toBeInTheDocument();
    expect(screen.getByText('tümü')).toBeInTheDocument();
    await waitFor(() => expect(drillCalls(api, 'operations')).not.toHaveLength(0));
    const call = drillCalls(api, 'operations')[0];
    expect(call?.get('bucket')).toBe(TEAM_ID);
    expect(call?.get('part')).toBe('denominator');
    expect(call?.get('page')).toBe('1');
  });

  it('starts at the first page when the page parameter is not a plain number', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/reports/operations/drilldown': { status: 200, body: ticketDrilldown(1, 1, 1) },
    });
    renderRoute('/reports/operations/list?metric=team_load&page=1e21');

    await waitFor(() => expect(drillCalls(api, 'operations')).not.toHaveLength(0));
    expect(drillCalls(api, 'operations')[0]?.get('page')).toBe('1');
  });

  it('lists assets with a link to each asset', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/reports/warranty': { status: 200, body: reportResult('warranty', []) },
      'GET /api/v1/reports/warranty/drilldown': { status: 200, body: ASSET_DRILLDOWN },
    });
    renderRoute('/reports/warranty/list?metric=expiring');

    const links = await screen.findAllByRole('link', { name: 'PC-0001' });
    expect(links[0]).toHaveAttribute('href', `/assets/${ASSET_DRILLDOWN.assets?.items[0]?.id}`);
    const table = screen.getByRole('table');
    expect(within(table).getByText('Lenovo T14')).toBeInTheDocument();
    expect(within(table).getByText('Dizüstü')).toBeInTheDocument();
  });

  it('shows an empty list', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/reports/incidents': { status: 200, body: INCIDENTS },
      'GET /api/v1/reports/incidents/drilldown': { status: 200, body: ticketDrilldown(1, 0, 0) },
    });
    renderRoute('/reports/incidents/list?metric=volume');

    expect(await screen.findByText('Bu seçimde kayıt yok.')).toBeInTheDocument();
  });

  it('asks for a metric when none is given and refuses unknown reports', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME } });
    const { router } = renderRoute('/reports/incidents/list');

    expect(await screen.findByText('Listelenecek ölçü seçilmedi.')).toBeInTheDocument();
    await router.navigate('/reports/payroll/list?metric=volume');
    expect(await screen.findByText('Böyle bir rapor yok.')).toBeInTheDocument();
  });
});
