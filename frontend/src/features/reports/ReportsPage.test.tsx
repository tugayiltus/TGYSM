import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { INCIDENTS, REFERENCE_ROUTES, reportResult, TEAM_LOAD } from '../../test/reportData';

const reportCalls = (api: ReturnType<typeof mockApi>, report: string) =>
  api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === `/api/v1/reports/${report}`);

const routes = (me: object = ADMIN_ME) => ({
  'GET /api/v1/auth/me': { status: 200, body: me },
  'GET /api/v1/reports/incidents': { status: 200, body: INCIDENTS },
  'GET /api/v1/reports/operations': { status: 200, body: reportResult('operations', [TEAM_LOAD], 'teams') },
  'GET /api/v1/reports/assets': { status: 200, body: reportResult('assets', []) },
  ...REFERENCE_ROUTES,
});

describe('ReportsPage', () => {
  it('opens the incident report from /reports and shows its tabs and figures', async () => {
    mockApi(routes());
    const { router } = renderRoute('/reports');

    expect(await screen.findByRole('link', { name: 'Açılan kayıt' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/reports/incidents');
    const tabs = screen.getByRole('navigation', { name: 'Rapor türleri' });
    expect(within(tabs).getAllByRole('link').map((l) => l.textContent)).toEqual(['Olaylar', 'Talepler', 'Operasyon', 'Transferler', 'Varlıklar', 'Garanti']);
    expect(within(tabs).getByRole('link', { name: 'Olaylar' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByText('Kapsam: tüm kayıtlar')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Dışa aktarımlarım' })).toHaveAttribute('href', '/reports/exports');
  });

  it('shows "Veri yok" for a figure without data and the ratio parts', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents');

    const card = (await screen.findByRole('link', { name: 'İlk yanıt süresi' })).closest('article');
    expect(within(card as HTMLElement).getByText('Veri yok')).toBeInTheDocument();
    const ratio = screen.getByRole('link', { name: 'SLA uyumu' }).closest('article') as HTMLElement;
    expect(within(ratio).getByText('%75')).toBeInTheDocument();
    expect(within(ratio).getByText('3 / 4')).toBeInTheDocument();
  });

  it('writes the filter bar to the URL and restores it on reload', async () => {
    const api = mockApi(routes());
    const { user, router } = renderRoute('/reports/incidents');

    await user.selectOptions(await screen.findByLabelText('Öncelik'), 'P1 · Kritik');
    await user.selectOptions(screen.getByLabelText('Durum'), 'Açık');

    expect(new URLSearchParams(router.state.location.search).get('priority')).toBe('p1');
    await waitFor(() => expect(reportCalls(api, 'incidents').some((c) => new URL(c.url).search === '?priority=p1&status=open')).toBe(true));

    await router.navigate('/reports/incidents?priority=p1&status=open&teamId=bad');
    expect(await screen.findByLabelText('Öncelik')).toHaveValue('p1');
    expect(screen.getByLabelText('Durum')).toHaveValue('open');
    expect(screen.getByLabelText('Ekip')).toHaveValue('');

    await user.click(screen.getByRole('button', { name: 'Filtreleri temizle' }));
    expect(router.state.location.search).toBe('');
  });

  it('keeps a half-typed day in the field and writes only a valid day to the URL', async () => {
    mockApi(routes());
    const { router } = renderRoute('/reports/incidents?priority=p1');
    const from = await screen.findByLabelText('Başlangıç tarihi');
    const fromParam = () => new URLSearchParams(router.state.location.search).get('from');

    fireEvent.change(from, { target: { value: '0002-05-10' } });
    expect(from).toHaveValue('0002-05-10');
    expect(fromParam()).toBeNull();

    fireEvent.change(from, { target: { value: '2026-05-10' } });
    await waitFor(() => expect(fromParam()).toBe('2026-05-10'));
    expect(from).toHaveValue('2026-05-10');

    fireEvent.change(from, { target: { value: '0020-05-10' } });
    expect(from).toHaveValue('0020-05-10');
    expect(fromParam()).toBe('2026-05-10');

    fireEvent.click(screen.getByRole('button', { name: 'Filtreleri temizle' }));
    await waitFor(() => expect(router.state.location.search).toBe(''));
    expect(from).toHaveValue('');
  });

  it('builds the drill-down URL of a card with the same filters', async () => {
    mockApi(routes());
    const { user, router } = renderRoute('/reports/incidents?priority=p1');

    await user.click(await screen.findByRole('link', { name: 'Açılan kayıt' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/reports/incidents/list'));
    expect(router.state.location.search).toBe('?metric=volume&priority=p1');
  });

  it('links each bucket and a ratio denominator to its own drill-down', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents?priority=p1');

    expect(await screen.findByRole('link', { name: '8 gün ve üzeri' })).toHaveAttribute('href', '/reports/incidents/list?metric=open_backlog&bucket=8%2B&priority=p1');
    expect(screen.getByRole('link', { name: 'Tümünü listele (4)' })).toHaveAttribute('href', '/reports/incidents/list?metric=sla_compliance&part=denominator&priority=p1');
    expect(await screen.findAllByTestId('distribution-chart')).not.toHaveLength(0);
  });

  it('keeps only the filters the next tab understands', async () => {
    mockApi(routes());
    renderRoute('/reports/incidents?priority=p1&status=open');

    const tabs = await screen.findByRole('navigation', { name: 'Rapor türleri' });
    expect(within(tabs).getByRole('link', { name: 'Operasyon' })).toHaveAttribute('href', '/reports/operations?priority=p1&status=open');
    expect(within(tabs).getByRole('link', { name: 'Varlıklar' })).toHaveAttribute('href', '/reports/assets');
  });

  it('labels the team scope of IT staff and shows bucket names from the server', async () => {
    mockApi(routes(IT_ME));
    renderRoute('/reports/operations');

    expect(await screen.findByText('Kapsam: yalnızca ekiplerinizin kayıtları')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ağ Ekibi' })).toBeInTheDocument();
  });

  it('shows an empty message when a report has no figures', async () => {
    mockApi(routes());
    renderRoute('/reports/assets');

    expect(await screen.findByText('Bu rapor için gösterilecek sonuç yok.')).toBeInTheDocument();
  });

  it('says so for an unknown report', async () => {
    mockApi(routes());
    renderRoute('/reports/payroll');

    expect(await screen.findByText('Böyle bir rapor yok.')).toBeInTheDocument();
  });

  it('shows the 403 page to an employee on a direct URL', async () => {
    const api = mockApi(routes(EMPLOYEE_ME));
    renderRoute('/reports/incidents');

    expect(await screen.findByRole('heading', { name: 'Bu sayfayı görme yetkiniz yok' })).toBeInTheDocument();
    expect(reportCalls(api, 'incidents')).toHaveLength(0);
  });

  it('shows the server error', async () => {
    mockApi({ ...routes(), 'GET /api/v1/reports/incidents': { status: 400, body: { code: 'reports.invalid_filter' } } });
    renderRoute('/reports/incidents');

    expect(await screen.findByText(/Rapor filtresi geçersiz/)).toBeInTheDocument();
  });
});
