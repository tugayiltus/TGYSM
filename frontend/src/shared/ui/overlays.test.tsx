import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { Button } from './Button';
import { Dialog } from './Dialog';
import { Sheet } from './Sheet';
import { ToastProvider } from './Toast';
import { TOAST_TIMEOUT_MS, useToast } from './useToast';

function DialogHarness() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setOpen(true)}>Aç</Button>
      <Dialog open={open} onOpenChange={setOpen} title="Onay">
        <input aria-label="Not" />
      </Dialog>
    </>
  );
}

function NestedHarness() {
  const [sheetOpen, setSheetOpen] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setSheetOpen(true)}>Filtreler</Button>
      <Sheet open={sheetOpen} onOpenChange={setSheetOpen} title="Filtre paneli">
        <Button onClick={() => setDialogOpen(true)}>Ayrıntı</Button>
      </Sheet>
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen} title="Ayrıntı penceresi">
        <p>İçerik</p>
      </Dialog>
    </>
  );
}

describe('Dialog', () => {
  it('is labelled by its title, closes with Escape and gives focus back to the trigger', async () => {
    const user = userEvent.setup();
    render(<DialogHarness />);
    const trigger = screen.getByRole('button', { name: 'Aç' });
    await user.click(trigger);
    expect(await screen.findByRole('dialog', { name: 'Onay' })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Onay' })).not.toBeInTheDocument());
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('closes from its close button', async () => {
    const user = userEvent.setup();
    render(<DialogHarness />);
    await user.click(screen.getByRole('button', { name: 'Aç' }));
    await user.click(await screen.findByRole('button', { name: 'Kapat' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

describe('Sheet', () => {
  it('keeps the sheet open and returns focus inside it when a dialog opened from it closes', async () => {
    const user = userEvent.setup();
    render(<NestedHarness />);
    await user.click(screen.getByRole('button', { name: 'Filtreler' }));
    const inner = await screen.findByRole('button', { name: 'Ayrıntı' });
    await user.click(inner);
    expect(await screen.findByRole('dialog', { name: 'Ayrıntı penceresi' })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Ayrıntı penceresi' })).not.toBeInTheDocument());
    expect(screen.getByRole('dialog', { name: 'Filtre paneli' })).toBeInTheDocument();
    await waitFor(() => expect(inner).toHaveFocus());
  });
});

function ToastHarness() {
  const toast = useToast();
  return <Button onClick={() => toast.show('Kaydedildi: DMR-000001', 'success')}>Kaydet</Button>;
}

describe('Toast', () => {
  afterEach(() => vi.useRealTimers());

  it('announces a message politely and removes it after the timeout', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
    render(
      <ToastProvider>
        <ToastHarness />
      </ToastProvider>,
    );
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    const region = screen.getByRole('status', { name: 'Bildirimler' });
    expect(region).toHaveTextContent('Kaydedildi: DMR-000001');

    act(() => {
      vi.advanceTimersByTime(TOAST_TIMEOUT_MS + 1000);
    });
    await waitFor(() => expect(screen.getByRole('status', { name: 'Bildirimler' })).not.toHaveTextContent('Kaydedildi'));
  });
});
