import { act, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi, type MockRoutes } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const CALENDAR = {
  id: 'c-1',
  version: 2,
  timeZone: 'Europe/Istanbul',
  hours: ['monday', 'tuesday', 'wednesday', 'thursday', 'friday'].map((weekday) => ({ weekday, startsAt: '09:00:00', endsAt: '18:00:00' })),
  updatedAt: '2026-09-27T09:00:00Z',
};
const HOLIDAY = { id: 'h-1', date: '2026-10-29', name: 'Cumhuriyet Bayramı' };

function setup(extra: MockRoutes = {}) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/admin/sla/calendar': { status: 200, body: CALENDAR },
    'GET /api/v1/admin/sla/holidays': { status: 200, body: [HOLIDAY] },
    ...extra,
  });
}

describe('SlaCalendarPage', () => {
  it('shows the zone, the weekly hours and the holidays', async () => {
    setup();
    renderRoute('/admin/sla/calendar');

    expect(await screen.findByLabelText('Saat dilimi')).toHaveValue('Europe/Istanbul');
    expect(screen.getByLabelText('Pazartesi Başlangıç')).toHaveValue('09:00');
    expect(screen.getByLabelText('Cumartesi Başlangıç')).toBeDisabled();
    expect((await screen.findAllByText('Cumhuriyet Bayramı')).length).toBeGreaterThan(0);
  });

  it('refuses a start that is not before the end, without calling the API', async () => {
    const api = setup();
    const { user } = renderRoute('/admin/sla/calendar');

    const start = await screen.findByLabelText('Pazartesi Başlangıç');
    await user.clear(start);
    await user.type(start, '19:00');
    await user.click(screen.getByRole('button', { name: 'Takvimi kaydet' }));

    expect(await screen.findByText(/Çalışma saatleri geçersiz/)).toBeInTheDocument();
    expect(api.calls.some((c) => c.method === 'PUT')).toBe(false);
  });

  it('refuses a week with no working day', async () => {
    setup();
    const { user } = renderRoute('/admin/sla/calendar');

    await screen.findByLabelText('Saat dilimi');
    for (const day of ['Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma']) {
      await user.click(screen.getByRole('switch', { name: `${day} · Çalışılıyor` }));
    }
    await user.click(screen.getByRole('button', { name: 'Takvimi kaydet' }));

    expect(await screen.findByText(/en az bir çalışma günü/)).toBeInTheDocument();
  });

  it('requires a time zone', async () => {
    setup();
    const { user } = renderRoute('/admin/sla/calendar');

    await user.clear(await screen.findByLabelText('Saat dilimi'));
    await user.click(screen.getByRole('button', { name: 'Takvimi kaydet' }));

    expect(await screen.findByText(/zorunlu/i)).toBeInTheDocument();
  });

  it('saves the calendar with its version', async () => {
    let put: unknown;
    setup({
      'PUT /api/v1/admin/sla/calendar': async (request) => {
        put = await request.json();
        return { status: 200, body: { ...CALENDAR, version: 3 } };
      },
    });
    const { user } = renderRoute('/admin/sla/calendar');

    await user.click(await screen.findByRole('button', { name: 'Takvimi kaydet' }));

    expect(await screen.findByText('Takvim kaydedildi.')).toBeInTheDocument();
    expect(put).toMatchObject({ version: 2, timeZone: 'Europe/Istanbul' });
    expect((put as { hours: unknown[] }).hours).toHaveLength(5);
    expect((put as { hours: { startsAt: string }[] }).hours[0]?.startsAt).toBe('09:00:00');
  });

  it('submits the version the draft was based on, even after the calendar was refetched in the background', async () => {
    let put: unknown;
    const api = setup({
      'PUT /api/v1/admin/sla/calendar': async (request) => {
        put = await request.json();
        return { status: 409, body: { code: 'concurrency.conflict' } };
      },
    });
    const { user, queryClient } = renderRoute('/admin/sla/calendar');
    await screen.findByLabelText('Saat dilimi');

    api.set('GET /api/v1/admin/sla/calendar', { status: 200, body: { ...CALENDAR, version: 3, timeZone: 'Europe/London' } });
    await act(() => queryClient.invalidateQueries());
    await waitFor(() => expect(api.calls.filter((c) => c.method === 'GET' && c.url.endsWith('/admin/sla/calendar'))).toHaveLength(2));
    await user.click(screen.getByRole('button', { name: 'Takvimi kaydet' }));

    await waitFor(() => expect(put).toMatchObject({ version: 2, timeZone: 'Europe/Istanbul' }));
  });

  it('on a conflict, offers to reload the server copy and then saves on top of it', async () => {
    const api = setup({ 'PUT /api/v1/admin/sla/calendar': { status: 409, body: { code: 'concurrency.conflict' } } });
    const { user } = renderRoute('/admin/sla/calendar');

    await user.click(await screen.findByRole('button', { name: 'Takvimi kaydet' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('başka biri tarafından değiştirildi');

    api.set('GET /api/v1/admin/sla/calendar', { status: 200, body: { ...CALENDAR, version: 3, timeZone: 'Europe/London' } });
    let put: unknown;
    api.set('PUT /api/v1/admin/sla/calendar', async (request) => {
      put = await request.json();
      return { status: 200, body: { ...CALENDAR, version: 4, timeZone: 'Europe/London' } };
    });
    await user.click(screen.getByRole('button', { name: 'Sunucudaki takvimi yükle' }));

    expect(await screen.findByDisplayValue('Europe/London')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Takvimi kaydet' }));
    await waitFor(() => expect(put).toMatchObject({ version: 3, timeZone: 'Europe/London' }));
  });

  it('adds and removes a holiday', async () => {
    let posted: unknown;
    const api = setup({
      'POST /api/v1/admin/sla/holidays': async (request) => {
        posted = await request.json();
        return { status: 201, body: { id: 'h-2', date: '2026-12-31', name: 'Yılbaşı arifesi' } };
      },
      'DELETE /api/v1/admin/sla/holidays/h-1': { status: 204 },
    });
    const { user } = renderRoute('/admin/sla/calendar');

    await screen.findAllByText('Cumhuriyet Bayramı');
    await user.type(screen.getByLabelText('Tarih'), '2026-12-31');
    await user.type(screen.getByLabelText('Ad'), 'Yılbaşı arifesi');
    await user.click(screen.getByRole('button', { name: 'Tatil ekle' }));
    await waitFor(() => expect(posted).toEqual({ date: '2026-12-31', name: 'Yılbaşı arifesi' }));

    await user.click(screen.getAllByRole('button', { name: /^Kaldır/ })[0]!);
    await waitFor(() => expect(api.calls.some((c) => c.method === 'DELETE')).toBe(true));
  });
});
