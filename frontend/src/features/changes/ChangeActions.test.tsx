import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CHANGE, changeRoutes, full } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';

import type { ChangeDetail } from './api';

const PATH = `/changes/${CHANGE.number}`;
const BASE = `/api/v1/changes/${CHANGE.number}`;

const inState = (status: ChangeDetail['status'], extra: Partial<ChangeDetail> = {}): ChangeDetail => ({ ...CHANGE, status, ...extra });

describe('ChangeActions', () => {
  it.each([
    ['draft', 'submit', 'Onaya gönder'],
    ['scheduled', 'start-implementation', 'Uygulamaya başla'],
    ['implementing', 'finish-implementation', 'Uygulama bitti'],
  ] as const)('runs the primary step of %s with the version the person saw', async (status, path, label) => {
    let posted: unknown;
    mockApi(
      changeRoutes(inState(status), {
        [`POST ${BASE}/${path}`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: inState(status, { version: 4 }) };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: label }));

    await waitFor(() => expect(posted).toEqual({ version: CHANGE.version }));
  });

  it('offers Kapat as the step of a change in verification', async () => {
    mockApi(changeRoutes(inState('verifying')) as never);
    renderRoute(PATH);

    expect(await screen.findByRole('button', { name: 'Kapat' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Uygulamaya başla' })).not.toBeInTheDocument();
  });

  it('shows no actions to someone who cannot work on the change', async () => {
    mockApi(changeRoutes(inState('draft', { canWork: false })) as never);
    renderRoute(PATH);

    expect(await screen.findByRole('region', { name: 'Özet' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Onaya gönder' })).not.toBeInTheDocument();
  });

  it('locks the other buttons while an action runs', async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    let current = CHANGE;
    mockApi(
      changeRoutes(CHANGE, {
        [`GET ${BASE}`]: () => ({ status: 200, body: full(current) }),
        [`POST ${BASE}/submit`]: async () => {
          await gate;
          current = inState('pending_approval', { version: 4 });
          return { status: 200, body: current };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Onaya gönder' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'İptal et' })).toHaveAttribute('aria-disabled', 'true'));
    release();
    expect(await screen.findByRole('button', { name: 'Geri çek' })).toBeInTheDocument();
  });

  it('closes successfully only with a verification note', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(inState('verifying', { actualStart: '2026-12-05T21:05:00Z' }), {
        [`POST ${BASE}/close`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: inState('closed', { outcome: 'successful' }) };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Kapat' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Sonuç', 'Başarılı', dialog);
    expect(within(dialog).queryByLabelText('Geri dönüş notu')).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();
    expect(posted).toBeUndefined();

    await user.type(within(dialog).getByLabelText('Doğrulama notu'), 'Tüm bağlantılar çalışıyor');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toMatchObject({ version: CHANGE.version, outcome: 'successful', verificationNote: 'Tüm bağlantılar çalışıyor', backoutNote: null }));
  });

  it('asks for a backout note on a rolled back change', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(inState('implementing', { backoutStartedAt: '2026-12-05T22:00:00Z' }), {
        [`POST ${BASE}/close`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: inState('closed', { outcome: 'rolled_back' }) };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Kapat' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Sonuç', 'Geri alındı', dialog);
    expect(within(dialog).queryByLabelText('Doğrulama notu')).not.toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Geri dönüş notu'), 'Eski imaj yüklendi');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toMatchObject({ outcome: 'rolled_back', backoutNote: 'Eski imaj yüklendi', verificationNote: null }));
  });

  it('offers Arıza kaydı aç for a failed close and opens nothing by itself', async () => {
    const api = mockApi(changeRoutes(inState('verifying', { backoutStartedAt: '2026-12-05T22:00:00Z' })) as never);
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Kapat' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByRole('link', { name: 'Arıza kaydı aç' })).not.toBeInTheDocument();
    await chooseOption(user, 'Sonuç', 'Başarısız', dialog);

    expect(await within(dialog).findByRole('link', { name: 'Arıza kaydı aç' })).toHaveAttribute('href', '/tickets/new/incident');
    expect(api.calls.some((c) => c.method === 'POST')).toBe(false);
  });

  it('cancels a change with a reason', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(CHANGE, {
        [`POST ${BASE}/cancel`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: inState('cancelled') };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'İptal et' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('İptal nedeni'), 'Vazgeçildi');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: CHANGE.version, reason: 'Vazgeçildi' }));
  });
});
