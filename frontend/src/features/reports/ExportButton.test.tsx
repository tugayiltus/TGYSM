import { screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { INCIDENTS, ticketDrilldown } from '../../test/reportData';

const LIST_URL = '/reports/incidents/list?metric=open_backlog&bucket=8%2B&priority=p1';

const routes = {
  'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
  'GET /api/v1/reports/incidents': { status: 200, body: INCIDENTS },
  'GET /api/v1/reports/incidents/drilldown': { status: 200, body: ticketDrilldown(1, 1, 1) },
};

const callsTo = (api: ReturnType<typeof mockApi>, method: string, path: string) => api.calls.filter((c) => c.method === method && new URL(c.url).pathname === path);

describe('ExportButton', () => {
  const createObjectURL = vi.fn(() => 'blob:report');
  const revokeObjectURL = vi.fn();
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

  beforeEach(() => {
    Object.assign(URL, { createObjectURL, revokeObjectURL });
  });

  afterEach(() => {
    createObjectURL.mockClear();
    click.mockClear();
  });

  it('downloads the selection with its filters at once', async () => {
    const api = mockApi({ ...routes, 'GET /api/v1/reports/incidents/export': { status: 200, body: {} } });
    const { user } = renderRoute(LIST_URL);

    await user.click(await screen.findByRole('button', { name: "Excel'e aktar" }));

    await waitFor(() => expect(click).toHaveBeenCalled());
    const query = new URL(callsTo(api, 'GET', '/api/v1/reports/incidents/export')[0]?.url ?? '').searchParams;
    expect(query.get('metric')).toBe('open_backlog');
    expect(query.get('bucket')).toBe('8+');
    expect(query.get('priority')).toBe('p1');
    expect(createObjectURL).toHaveBeenCalledTimes(1);
  });

  it('offers a background export when the list is too large, then links to my exports', async () => {
    const api = mockApi({
      ...routes,
      'GET /api/v1/reports/incidents/export': { status: 409, body: { code: 'reports.export_too_large' } },
      'POST /api/v1/reports/exports': { status: 202, body: { id: '00000000-0000-7000-8000-0000000000e1' } },
    });
    const { user } = renderRoute(LIST_URL);

    await user.click(await screen.findByRole('button', { name: "Excel'e aktar" }));
    expect(await screen.findByText(/anında indirilemeyecek kadar büyük/)).toBeInTheDocument();
    expect(click).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Arka planda hazırla' }));

    // The app-wide toast region is also a status; the export's own status is the one with this text.
    const status = (await screen.findByText(/Dışa aktarım arka planda hazırlanıyor\./)).closest('[role="status"]');
    expect(status).not.toBeNull();
    // The sidebar has the same link; this one is the page's own.
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'Dışa aktarımlarım' })).toHaveAttribute('href', '/reports/exports');
    expect(screen.queryByRole('button', { name: 'Arka planda hazırla' })).not.toBeInTheDocument();
    const query = new URL(callsTo(api, 'POST', '/api/v1/reports/exports')[0]?.url ?? '').searchParams;
    expect(query.get('report')).toBe('incidents');
    expect(query.get('metric')).toBe('open_backlog');
    expect(query.get('bucket')).toBe('8+');
    expect(query.get('priority')).toBe('p1');
  });

  it('shows other export errors as they are', async () => {
    mockApi({ ...routes, 'GET /api/v1/reports/incidents/export': { status: 400, body: { code: 'reports.invalid_filter' } } });
    const { user } = renderRoute(LIST_URL);

    await user.click(await screen.findByRole('button', { name: "Excel'e aktar" }));

    expect(await screen.findByText(/Rapor filtresi geçersiz/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Arka planda hazırla' })).not.toBeInTheDocument();
  });

  it('shows a failed background request', async () => {
    mockApi({
      ...routes,
      'GET /api/v1/reports/incidents/export': { status: 409, body: { code: 'reports.export_too_large' } },
      'POST /api/v1/reports/exports': { status: 409, body: { code: 'reports.export_too_large' } },
    });
    const { user } = renderRoute(LIST_URL);

    await user.click(await screen.findByRole('button', { name: "Excel'e aktar" }));
    await user.click(await screen.findByRole('button', { name: 'Arka planda hazırla' }));

    expect(await screen.findByText(/en fazla 100.000 satır/)).toBeInTheDocument();
  });
});
