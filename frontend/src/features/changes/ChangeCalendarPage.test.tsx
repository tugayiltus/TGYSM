import { screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { CHANGE, CHANGE_ROW, changeRoutes } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { stubPhoneViewport } from '../../test/viewport';

import type { ChangeCalendarItem } from './api';

const ITEM: ChangeCalendarItem = {
  number: 'CHG-2026-000001',
  title: 'Çekirdek anahtar yükseltmesi',
  status: 'scheduled',
  risk: 'high',
  windowStart: '2026-11-18T10:00:00Z',
  windowEnd: '2026-11-19T21:00:00Z',
  hasConflict: false,
};
const CONFLICTING: ChangeCalendarItem = { ...ITEM, number: 'CHG-2026-000002', title: 'Mail sunucusu bakımı', hasConflict: true };
const CALENDAR_ROUTE = 'GET /api/v1/changes/calendar';

const calendar = (items: ChangeCalendarItem[]) => mockApi(changeRoutes(CHANGE, { [CALENDAR_ROUTE]: { status: 200, body: items } }) as never);
const lastQuery = (api: ReturnType<typeof calendar>) => new URL(api.calls.filter((c) => c.url.includes('/changes/calendar')).at(-1)?.url ?? 'http://x').searchParams;

describe('ChangeCalendarPage', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('asks for whole weeks of the month and shows a change on each day of its window', async () => {
    const api = calendar([ITEM, CONFLICTING]);
    renderRoute('/changes/calendar?date=2026-11-18');

    expect(await screen.findByText('Kasım 2026')).toBeInTheDocument();
    expect(await screen.findAllByRole('link', { name: `${ITEM.number} ${ITEM.title}` })).toHaveLength(2);
    expect(lastQuery(api).get('from')).toBe('2026-10-26');
    expect(lastQuery(api).get('to')).toBe('2026-12-06');
  });

  it('marks a conflicting change with the "Çakışma" token', async () => {
    calendar([ITEM, CONFLICTING]);
    renderRoute('/changes/calendar?date=2026-11-18');

    const links = await screen.findAllByRole('link', { name: `${CONFLICTING.number} ${CONFLICTING.title}` });
    expect(links).not.toHaveLength(0);
    expect(screen.getAllByText('Çakışma')).toHaveLength(2);
  });

  it('moves by one month and returns to today', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-18T09:00:00Z'));
    const api = calendar([]);
    const { user, router } = renderRoute('/changes/calendar?date=2026-11-30');

    await screen.findByText('Bu ayda planlı değişiklik yok.');
    await user.click(screen.getByRole('button', { name: 'Sonraki' }));
    await waitFor(() => expect(router.state.location.search).toContain('date=2026-12-30'));
    expect(await screen.findByText('Aralık 2026')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Önceki' }));
    await user.click(screen.getByRole('button', { name: 'Önceki' }));
    await waitFor(() => expect(router.state.location.search).toContain('date=2026-10-30'));
    await user.click(screen.getByRole('button', { name: 'Bugün' }));
    await waitFor(() => expect(router.state.location.search).toContain('date=2026-11-18'));
    expect(lastQuery(api).get('from')).toBe('2026-10-26');
  });

  it('shows one week and moves by seven days', async () => {
    const api = calendar([ITEM]);
    const { user, router } = renderRoute('/changes/calendar?view=week&date=2026-11-18');

    expect(await screen.findAllByRole('link', { name: `${ITEM.number} ${ITEM.title}` })).toHaveLength(2);
    expect(lastQuery(api).get('from')).toBe('2026-11-16');
    expect(lastQuery(api).get('to')).toBe('2026-11-22');
    await user.click(screen.getByRole('button', { name: 'Sonraki' }));
    await waitFor(() => expect(router.state.location.search).toContain('date=2026-11-25'));
    expect(router.state.location.search).toContain('view=week');
    await waitFor(() => expect(lastQuery(api).get('from')).toBe('2026-11-23'));
  });

  it('shows a loading cue while the next range loads', async () => {
    const api = calendar([ITEM]);
    const { user } = renderRoute('/changes/calendar?view=week&date=2026-11-18');
    await screen.findAllByRole('link', { name: `${ITEM.number} ${ITEM.title}` });
    expect(screen.queryByRole('progressbar', { name: 'Yükleniyor…' })).not.toBeInTheDocument();

    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    api.set(CALENDAR_ROUTE, async () => {
      await gate;
      return { status: 200, body: [] };
    });
    await user.click(screen.getByRole('button', { name: 'Sonraki' }));

    expect(await screen.findByRole('progressbar', { name: 'Yükleniyor…' })).toBeInTheDocument();
    release();
    await waitFor(() => expect(screen.queryByRole('progressbar', { name: 'Yükleniyor…' })).not.toBeInTheDocument());
  });

  it('switches the view from the segmented control', async () => {
    calendar([]);
    const { user, router } = renderRoute('/changes/calendar?date=2026-11-18');

    await screen.findByText('Bu ayda planlı değişiklik yok.');
    await user.click(screen.getByRole('radio', { name: 'Hafta' }));
    await waitFor(() => expect(router.state.location.search).toContain('view=week'));
    expect(await screen.findByText('Bu haftada planlı değişiklik yok.')).toBeInTheDocument();
  });

  it('lists each day once in the agenda on a phone', async () => {
    stubPhoneViewport();
    calendar([ITEM]);
    renderRoute('/changes/calendar?date=2026-11-18');

    const agenda = await screen.findByRole('group', { name: 'Gün gün planlı değişiklikler' });
    expect(within(agenda).getAllByRole('heading', { level: 3 }).map((h) => h.textContent)).toEqual(['18 Kasım Çarşamba', '19 Kasım Perşembe']);
    expect(within(agenda).getAllByRole('link', { name: `${ITEM.number} ${ITEM.title}` })).toHaveLength(2);
  });

  it('opens a change with the calendar as the return target', async () => {
    calendar([ITEM]);
    const { user, router } = renderRoute('/changes/calendar?date=2026-11-18');

    await user.click((await screen.findAllByRole('link', { name: `${ITEM.number} ${ITEM.title}` }))[0]!);
    await waitFor(() => expect(router.state.location.pathname).toBe('/changes/CHG-2026-000001'));
    expect(router.state.location.state).toEqual({ from: '/changes/calendar?date=2026-11-18' });
  });

  it('shows the empty message for a month without changes', async () => {
    calendar([]);
    renderRoute('/changes/calendar?date=2026-11-18');

    expect(await screen.findByText('Bu ayda planlı değişiklik yok.')).toBeInTheDocument();
  });

  it('opens from the change list button', async () => {
    mockApi(
      changeRoutes(CHANGE, {
        'GET /api/v1/changes': { status: 200, body: { items: [CHANGE_ROW], page: 1, pageSize: 20, totalCount: 1 } },
        [CALENDAR_ROUTE]: { status: 200, body: [] },
      }) as never,
    );
    const { user, router } = renderRoute('/changes');

    await user.click(await screen.findByRole('link', { name: 'Bakım takvimi' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/changes/calendar'));
  });
});
