import { TextInput } from '@astryxdesign/core/TextInput';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AstryxProvider } from '../../shared/ui/AstryxProvider';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { stubPhoneViewport } from '../../test/viewport';
import type { Me } from '../auth/api';
import { SessionContext } from '../auth/session';

import { SEARCH_DEBOUNCE_MS } from './api';
import { GlobalSearch } from './GlobalSearch';

const SEARCH_ROUTE = 'GET /api/v1/search';
const EMPTY = { exact: null, groups: [] };
const HITS = {
  exact: null,
  groups: [
    { type: 'ticket', hasMore: true, hits: [{ type: 'ticket', key: 'INC-2026-0001', title: 'Yazıcı bozuk', status: 'Açık' }] },
    { type: 'asset', hasMore: false, hits: [{ type: 'asset', key: 'ASSET-2026-0007', title: 'Dell Latitude', status: 'Kullanımda' }] },
  ],
};

let lastRouter: ReturnType<typeof createMemoryRouter>;
function renderSearch(me: Me = IT_ME as Me) {
  const router = createMemoryRouter([{ path: '*', element: <GlobalSearch /> }], { initialEntries: ['/start'] });
  lastRouter = router;
  render(
    <SessionContext.Provider value={me}>
      <AstryxProvider>
        <RouterProvider router={router} />
      </AstryxProvider>
    </SessionContext.Provider>,
  );
}

const searchCalls = (api: ReturnType<typeof mockApi>) => api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === '/api/v1/search');
/** Reads the router state: React does not repaint the page inside the fake-timer window, the router state is current. */
const where = () => `${lastRouter.state.location.pathname}${lastRouter.state.location.search}`;
const openBox = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole('button', { name: 'Ara' }));
  return screen.findByRole('combobox');
};

describe('GlobalSearch', () => {
  beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['setTimeout', 'clearTimeout'] }));
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('sends one request after the debounce and none for one character', async () => {
    const api = mockApi({ [SEARCH_ROUTE]: { status: 200, body: EMPTY } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'y');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    expect(searchCalls(api)).toHaveLength(0);
    await user.type(input, 'az');
    expect(searchCalls(api)).toHaveLength(0);
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await waitFor(() => expect(searchCalls(api)).toHaveLength(1));
    expect(new URL(searchCalls(api)[0]!.url).searchParams.get('q')).toBe('yaz');
  });

  it('shows groups with Turkish titles and Sonuç bulunamadı when empty', async () => {
    const api = mockApi({ [SEARCH_ROUTE]: { status: 200, body: HITS } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'dell');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    expect(await screen.findByText('Kayıtlar')).toBeInTheDocument();
    expect(screen.getByText('Envanter')).toBeInTheDocument();
    expect(screen.getByText(/Yazıcı bozuk/)).toBeInTheDocument();
    api.set(SEARCH_ROUTE, { status: 200, body: EMPTY });
    await user.clear(input);
    await user.type(input, 'zzzz');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    expect(await screen.findByText('Sonuç bulunamadı')).toBeInTheDocument();
  });

  it('navigates straight to an exact hit', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: { exact: { type: 'ticket', key: 'INC-2026-0001', title: 'x', status: 'Açık' }, groups: [] } } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'INC-2026-0001');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await waitFor(() => expect(where()).toBe('/tickets/INC-2026-0001'));
  });

  it('opens the highlighted hit with Enter and moves with the arrows', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: HITS } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'dell');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await screen.findByText(/Yazıcı bozuk/);
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}{Enter}');
    await waitFor(() => expect(where()).toBe('/assets?q=ASSET-2026-0007'));
  });

  it('opens an asset hit in the asset list filtered by its tag', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: HITS } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'dell');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await user.click(await screen.findByText(/Dell Latitude/));
    await waitFor(() => expect(where()).toBe('/assets?q=ASSET-2026-0007'));
  });

  it('opens the staff ticket list without a queue filter from Tümünü gör for IT staff', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: HITS } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'dell');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await user.click(await screen.findByText('Tümünü gör'));
    await waitFor(() => expect(where()).toBe('/workspace/queue?queue=all&q=dell'));
  });

  it('opens the own ticket list from Tümünü gör for an employee', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: HITS } });
    renderSearch(EMPLOYEE_ME as Me);
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'dell');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await user.click(await screen.findByText('Tümünü gör'));
    await waitFor(() => expect(where()).toBe('/tickets?q=dell'));
  });

  it('drops an older response that arrives after a newer query', async () => {
    let releaseOld: (() => void) | undefined;
    const OLD = { exact: null, groups: [{ type: 'ticket', hasMore: false, hits: [{ type: 'ticket', key: 'INC-OLD', title: 'Eski sonuç', status: 'Açık' }] }] };
    mockApi({
      [SEARCH_ROUTE]: async (request) => {
        if (new URL(request.url).searchParams.get('q') === 'del') {
          await new Promise<void>((resolve) => {
            releaseOld = resolve;
          });
          return { status: 200, body: OLD };
        }
        return { status: 200, body: HITS };
      },
    });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const input = await openBox(user);
    await user.type(input, 'del');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await waitFor(() => expect(releaseOld).toBeDefined());
    await user.type(input, 'l');
    await act(() => vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS * 2));
    await screen.findByText(/Yazıcı bozuk/);
    await act(async () => releaseOld?.());
    expect(screen.queryByText(/Eski sonuç/)).not.toBeInTheDocument();
    expect(screen.getByText(/Yazıcı bozuk/)).toBeInTheDocument();
  });

  it('closes on Escape', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: EMPTY } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await openBox(user);
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('combobox')).not.toBeInTheDocument());
  });

  it('opens on the / key only when no field has focus', async () => {
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: EMPTY } });
    render(
      <SessionContext.Provider value={IT_ME as Me}><AstryxProvider>
        <RouterProvider router={createMemoryRouter([{ path: '*', element: (<><GlobalSearch /><TextInput label="Diğer alan" value="" onChange={() => undefined} /></>) }])} />
      </AstryxProvider></SessionContext.Provider>,
    );
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await user.click(screen.getByLabelText('Diğer alan'));
    await user.keyboard('/');
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    await user.click(document.body);
    await user.keyboard('/');
    expect(await screen.findByRole('combobox')).toBeInTheDocument();
  });

  it('shows an icon button on a phone', async () => {
    stubPhoneViewport();
    mockApi({ [SEARCH_ROUTE]: { status: 200, body: EMPTY } });
    renderSearch();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const button = screen.getByRole('button', { name: 'Ara' });
    expect(button).toHaveTextContent('');
    expect(await openBox(user)).toBeInTheDocument();
  });
});
