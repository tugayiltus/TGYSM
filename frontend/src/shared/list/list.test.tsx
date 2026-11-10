import { Selector } from '@astryxdesign/core/Selector';
import { render, renderHook, screen, act, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { AstryxProvider } from '../ui/AstryxProvider';

import { DataList, type DataColumn } from './DataList';
import { ListToolbar } from './ListToolbar';
import { useListParams } from './useListParams';

const FILTER_KEYS = ['status', 'team'] as const;
const PAGE_THREE = 3;

interface Ticket {
  id: string;
  title: string;
}
const TICKETS: Ticket[] = [
  { id: 't-1', title: 'Yazıcı arızası' },
  { id: 't-2', title: 'Şifre sıfırlama' },
];
const COLUMNS: DataColumn<Ticket>[] = [
  { key: 'title', header: 'Başlık', renderCell: (ticket) => ticket.title },
  { key: 'id', header: 'Numara', renderCell: (ticket) => ticket.id },
];

function stubPhone(isPhone: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({ matches: isPhone, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })),
  );
}

const inRouter = (entry: string) =>
  function Wrapper({ children }: { children: ReactNode }) {
    return <MemoryRouter initialEntries={[entry]}>{children}</MemoryRouter>;
  };

describe('useListParams', () => {
  it('reads page, search and filters from the URL', () => {
    const { result } = renderHook(() => useListParams(FILTER_KEYS), { wrapper: inRouter('/x?page=3&q=yazıcı&status=open') });
    expect(result.current.page).toBe(PAGE_THREE);
    expect(result.current.search).toBe('yazıcı');
    expect(result.current.filters).toEqual({ status: 'open', team: '' });
    expect(result.current.activeCount).toBe(2);
  });

  it('goes back to the first page when a filter changes and drops empty values', () => {
    const { result } = renderHook(() => useListParams(FILTER_KEYS), { wrapper: inRouter('/x?page=3&status=open') });
    act(() => result.current.setFilter('status', ''));
    act(() => result.current.setFilter('team', 'network'));
    expect(result.current.page).toBe(1);
    expect(result.current.filters).toEqual({ status: '', team: 'network' });
    expect(result.current.activeCount).toBe(1);
  });

  it('clears search and filters together', () => {
    const { result } = renderHook(() => useListParams(FILTER_KEYS), { wrapper: inRouter('/x?q=a&status=open&team=b') });
    act(() => result.current.clear());
    expect(result.current.search).toBe('');
    expect(result.current.activeCount).toBe(0);
  });
});

describe('DataList', () => {
  it('shows a named table with column headers on wide screens', () => {
    stubPhone(false);
    render(
      <AstryxProvider>
        <DataList items={TICKETS} columns={COLUMNS} getKey={(ticket) => ticket.id} renderCard={(ticket) => ticket.title} caption="Talepler" />
      </AstryxProvider>,
    );
    expect(screen.getByRole('table', { name: 'Talepler' })).toBeInTheDocument();
    expect(screen.getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual(['Başlık', 'Numara']);
    expect(screen.getByRole('cell', { name: 'Şifre sıfırlama' })).toBeInTheDocument();
  });

  it('shows one list of cards on phones', () => {
    stubPhone(true);
    render(
      <AstryxProvider>
        <DataList items={TICKETS} columns={COLUMNS} getKey={(ticket) => ticket.id} renderCard={(ticket) => `Kart: ${ticket.title}`} caption="Talepler" />
      </AstryxProvider>,
    );
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Talepler' })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(TICKETS.length);
    expect(screen.getByText('Kart: Yazıcı arızası')).toBeInTheDocument();
  });
});

const STATUS_OPTIONS = [
  { value: 'open', label: 'Açık' },
  { value: 'closed', label: 'Kapalı' },
];

function ToolbarHarness() {
  const list = useListParams(FILTER_KEYS);
  return (
    <ListToolbar
      title="Talep filtreleri"
      search={list.search}
      onSearchChange={list.setSearch}
      activeCount={list.activeCount}
      onClear={list.clear}
      filters={
        <Selector label="Durum" options={STATUS_OPTIONS} value={list.filters.status} onChange={(value) => list.setFilter('status', value)} placeholder="Tümü" />
      }
    />
  );
}

function renderToolbar(entry = '/list') {
  const router = createMemoryRouter([{ path: '/list', element: <ToolbarHarness /> }], { initialEntries: [entry] });
  render(
    <AstryxProvider>
      <RouterProvider router={router} />
    </AstryxProvider>,
  );
  return router;
}

describe('ListToolbar', () => {
  it('writes the search into the URL', async () => {
    stubPhone(false);
    const user = userEvent.setup();
    const router = renderToolbar();
    await user.type(screen.getByRole('searchbox', { name: 'Ara' }), 'yazıcı');
    await waitFor(() => expect(router.state.location.search).toBe('?q=yaz%C4%B1c%C4%B1'));
  });

  it('offers Temizle only while something is filtered', async () => {
    stubPhone(false);
    const user = userEvent.setup();
    const router = renderToolbar('/list?status=open&page=2');
    await user.click(screen.getByRole('button', { name: 'Temizle' }));
    await waitFor(() => expect(router.state.location.search).toBe(''));
    expect(screen.queryByRole('button', { name: 'Temizle' })).not.toBeInTheDocument();
  });

  it('puts the filters in a drawer on phones', async () => {
    stubPhone(true);
    const user = userEvent.setup();
    renderToolbar('/list?status=open');
    expect(screen.queryByRole('combobox', { name: 'Durum' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Filtreler (1)' }));
    expect(await screen.findByRole('combobox', { name: 'Durum' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('combobox', { name: 'Durum' })).not.toBeInTheDocument());
  });
});
