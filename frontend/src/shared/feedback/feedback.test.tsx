import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { createMemoryRouter, Link, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AstryxProvider } from '../ui/AstryxProvider';

import { ConfirmDialog } from './ConfirmDialog';
import { EmptyState, ErrorState, LoadingState } from './States';
import { UnsavedChangesGuard } from './UnsavedChangesGuard';

const LOADING = 'Yükleniyor…';
const UNEXPECTED = 'Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.';
const SKELETON_ROWS = 3;

const withAstryx = (ui: React.ReactNode) => render(<AstryxProvider>{ui}</AstryxProvider>);

afterEach(() => vi.restoreAllMocks());

describe('states', () => {
  it('announces loading without invalid nesting', () => {
    const consoleError = vi.spyOn(console, 'error');
    withAstryx(<LoadingState />);
    expect(screen.getByRole('progressbar', { name: LOADING })).toBeInTheDocument();
    expect(consoleError).not.toHaveBeenCalled();
  });

  it('keeps the space of a list while it loads', () => {
    withAstryx(<LoadingState rows={SKELETON_ROWS} />);
    const status = screen.getByRole('status', { name: LOADING });
    expect(status.querySelectorAll('.astryx-skeleton')).toHaveLength(SKELETON_ROWS);
  });

  it('says there is nothing to show and offers the action', () => {
    withAstryx(<EmptyState action={<button type="button">Yeni kayıt</button>} />);
    expect(screen.getByText('Kayıt bulunamadı.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Yeni kayıt' })).toBeInTheDocument();
  });

  it('describes the error and retries', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    withAstryx(<ErrorState error={new Error('boom')} onRetry={onRetry} />);
    expect(screen.getByText(UNEXPECTED)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Tekrar dene' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});

function ConfirmHarness({ onConfirm, error }: { onConfirm: () => void; error?: unknown }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        Sil
      </button>
      {open && (
        <ConfirmDialog
          title="Kaydı sil"
          message="Bu kayıt silinecek."
          confirmLabel="Sil ve devam et"
          variant="danger"
          isPending={false}
          error={error ?? null}
          onConfirm={onConfirm}
          onCancel={() => setOpen(false)}
        />
      )}
    </>
  );
}

describe('ConfirmDialog', () => {
  it('confirms from a named dialog', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    withAstryx(<ConfirmHarness onConfirm={onConfirm} />);
    await user.click(screen.getByRole('button', { name: 'Sil' }));
    const dialog = await screen.findByRole('dialog', { name: 'Kaydı sil' });
    expect(dialog).toHaveTextContent('Bu kayıt silinecek.');
    await user.click(screen.getByRole('button', { name: 'Sil ve devam et' }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it('shows the failure inside the dialog', async () => {
    const user = userEvent.setup();
    withAstryx(<ConfirmHarness onConfirm={vi.fn()} error={new Error('boom')} />);
    await user.click(screen.getByRole('button', { name: 'Sil' }));
    expect(await screen.findByRole('dialog', { name: 'Kaydı sil' })).toHaveTextContent(UNEXPECTED);
  });

  it('closes on Vazgeç and returns focus to the opener', async () => {
    const user = userEvent.setup();
    withAstryx(<ConfirmHarness onConfirm={vi.fn()} />);
    const opener = screen.getByRole('button', { name: 'Sil' });
    await user.click(opener);
    await user.click(await screen.findByRole('button', { name: 'Vazgeç' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(opener).toHaveFocus());
  });

  it('closes on Escape', async () => {
    const user = userEvent.setup();
    withAstryx(<ConfirmHarness onConfirm={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Sil' }));
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

function renderGuarded() {
  const router = createMemoryRouter(
    [
      {
        path: '/form',
        element: (
          <>
            <UnsavedChangesGuard when />
            <Link to="/other">Başka sayfa</Link>
          </>
        ),
      },
      { path: '/other', element: <p>Başka sayfa içeriği</p> },
    ],
    { initialEntries: ['/form'] },
  );
  withAstryx(<RouterProvider router={router} />);
  return router;
}

describe('UnsavedChangesGuard', () => {
  it('stays on the page when the person chooses Vazgeç', async () => {
    const user = userEvent.setup();
    const router = renderGuarded();
    await user.click(screen.getByRole('link', { name: 'Başka sayfa' }));
    await screen.findByRole('dialog', { name: 'Kaydedilmemiş değişiklikler' });
    await user.click(screen.getByRole('button', { name: 'Vazgeç' }));
    expect(router.state.location.pathname).toBe('/form');
  });

  it('leaves when the person confirms', async () => {
    const user = userEvent.setup();
    const router = renderGuarded();
    await user.click(screen.getByRole('link', { name: 'Başka sayfa' }));
    await user.click(await screen.findByRole('button', { name: 'Sayfadan çık' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/other'));
  });
});
