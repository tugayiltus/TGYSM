import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { createMemoryRouter, Link, RouterProvider } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { toSearchKey } from '../lib/turkish';

import { ComboboxField } from './ComboboxField';
import { FilterPanel } from './FilterPanel';
import { KpiCard } from './KpiCard';
import { UnsavedChangesGuard } from './UnsavedChangesGuard';

const BRANDS = [
  { value: 'b1', label: 'Lenovo' },
  { value: 'b2', label: 'Işık Bilgisayar' },
  { value: 'b3', label: 'HP' },
];

function ComboHarness({ onChange = vi.fn() }: { onChange?: (value: string) => void }) {
  const [value, setValue] = useState('');
  return (
    <ComboboxField
      label="Marka"
      options={BRANDS}
      value={value}
      emptyText="Tanım eksik"
      onChange={(next) => {
        setValue(next);
        onChange(next);
      }}
    />
  );
}

describe('toSearchKey', () => {
  it('matches the server search key: trimmed, Turkish lower case, dotless i as i', () => {
    expect(toSearchKey('  IŞIK ')).toBe('işik');
    expect(toSearchKey('Işık')).toBe(toSearchKey('IŞIK'));
  });
});

describe('ComboboxField', () => {
  it('filters with Turkish rules and selects with the keyboard', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<ComboHarness onChange={onChange} />);
    const input = screen.getByRole('combobox', { name: 'Marka' });

    await user.type(input, 'ışı');
    const listbox = screen.getByRole('listbox');
    expect(within(listbox).getAllByRole('option').map((o) => o.textContent)).toEqual(['Işık Bilgisayar']);
    await user.keyboard('{ArrowDown}{Enter}');

    expect(onChange).toHaveBeenLastCalledWith('b2');
    expect(input).toHaveValue('Işık Bilgisayar');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('marks the active option and closes with Escape', async () => {
    const user = userEvent.setup();
    render(<ComboHarness />);
    const input = screen.getByRole('combobox', { name: 'Marka' });
    await user.click(input);
    await user.keyboard('{ArrowDown}{ArrowDown}');
    const active = screen.getByRole('option', { name: 'Işık Bilgisayar' });
    expect(input).toHaveAttribute('aria-activedescendant', active.id);
    expect(active).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{Escape}');
    expect(input).toHaveAttribute('aria-expanded', 'false');
  });

  it('says so when nothing matches', async () => {
    const user = userEvent.setup();
    render(<ComboHarness />);
    await user.type(screen.getByRole('combobox', { name: 'Marka' }), 'xyz');
    expect(screen.getByText('Tanım eksik')).toBeInTheDocument();
  });
});

function PanelHarness({ onApply, onClear }: { onApply: (v: string) => void; onClear: () => void }) {
  const [applied, setApplied] = useState('p1');
  const [draft, setDraft] = useState(applied);
  return (
    <FilterPanel
      title="Filtreler"
      activeCount={applied === '' ? 0 : 1}
      onOpen={() => setDraft(applied)}
      onApply={() => {
        setApplied(draft);
        onApply(draft);
      }}
      onClear={() => {
        setApplied('');
        setDraft('');
        onClear();
      }}
      primary={<input aria-label="Öncelik" value={draft} onChange={(e) => setDraft(e.target.value)} />}
    />
  );
}

describe('FilterPanel', () => {
  it('on phones opens a sheet; Vazgeç drops the draft, Uygula applies once, Temizle clears', async () => {
    const onApply = vi.fn();
    const onClear = vi.fn();
    const user = userEvent.setup();
    render(<PanelHarness onApply={onApply} onClear={onClear} />);

    await user.click(screen.getByRole('button', { name: 'Filtreler (1)' }));
    const sheet = await screen.findByRole('dialog', { name: 'Filtreler' });
    await user.clear(within(sheet).getByLabelText('Öncelik'));
    await user.type(within(sheet).getByLabelText('Öncelik'), 'p2');
    await user.click(within(sheet).getByRole('button', { name: 'Vazgeç' }));
    expect(onApply).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Filtreler (1)' }));
    const again = await screen.findByRole('dialog', { name: 'Filtreler' });
    expect(within(again).getByLabelText('Öncelik')).toHaveValue('p1');
    await user.clear(within(again).getByLabelText('Öncelik'));
    await user.type(within(again).getByLabelText('Öncelik'), 'p3');
    await user.click(within(again).getByRole('button', { name: 'Uygula' }));
    expect(onApply).toHaveBeenCalledExactlyOnceWith('p3');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Filtreler (1)' }));
    await user.click(within(await screen.findByRole('dialog', { name: 'Filtreler' })).getByRole('button', { name: 'Temizle' }));
    expect(onClear).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: 'Filtreler (0)' })).toBeInTheDocument();
  });
});

describe('KpiCard', () => {
  it('shows value, unit and period and links when given a target', () => {
    render(
      <RouterProvider
        router={createMemoryRouter([{ path: '/', element: <KpiCard title="Açılan kayıt" value="12" unit="adet" period="Son 30 gün" href="/reports/incidents/list" /> }])}
      />,
    );
    const card = screen.getByRole('article', { name: 'Açılan kayıt' });
    expect(card).toHaveTextContent('12');
    expect(card).toHaveTextContent('adet');
    expect(card).toHaveTextContent('Son 30 gün');
    expect(within(card).getByRole('link', { name: 'Açılan kayıt' })).toHaveAttribute('href', '/reports/incidents/list');
  });
});

function GuardedForm() {
  const [dirty, setDirty] = useState(false);
  return (
    <>
      <UnsavedChangesGuard when={dirty} />
      <input aria-label="Başlık" onChange={() => setDirty(true)} />
      <Link to="/elsewhere">Başka sayfa</Link>
    </>
  );
}

function renderGuard() {
  const router = createMemoryRouter(
    [
      { path: '/', element: <GuardedForm /> },
      { path: '/elsewhere', element: <p>Başka sayfa içeriği</p> },
    ],
    { initialEntries: ['/'] },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe('UnsavedChangesGuard', () => {
  it('lets a clean form leave without asking', async () => {
    const user = userEvent.setup();
    const router = renderGuard();
    await user.click(screen.getByRole('link', { name: 'Başka sayfa' }));
    expect(router.state.location.pathname).toBe('/elsewhere');
  });

  it('asks before leaving a changed form and stays on Vazgeç', async () => {
    const user = userEvent.setup();
    const router = renderGuard();
    await user.type(screen.getByLabelText('Başlık'), 'a');
    await user.click(screen.getByRole('link', { name: 'Başka sayfa' }));
    const dialog = await screen.findByRole('dialog', { name: 'Kaydedilmemiş değişiklikler' });
    await user.click(within(dialog).getByRole('button', { name: 'Vazgeç' }));
    expect(router.state.location.pathname).toBe('/');

    await user.click(screen.getByRole('link', { name: 'Başka sayfa' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Sayfadan çık' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/elsewhere'));
  });
});
