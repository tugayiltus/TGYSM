import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CHANGE, CHANGE_SUMMARY, changeRoutes, full } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';

const PATH = `/changes/${CHANGE.number}`;
const BASE = `/api/v1/changes/${CHANGE.number}`;

describe('ChangeDetailPage', () => {
  it('shows the sections, the tasks grouped by phase and the timeline', async () => {
    mockApi(
      changeRoutes(CHANGE, {
        [`GET ${BASE}/timeline`]: {
          status: 200,
          body: [
            { id: 'e-1', kind: 'created', actorName: 'Can Demir', data: {}, occurredAt: '2026-11-13T08:00:00Z' },
            { id: 'e-2', kind: 'rejected', actorName: 'Tugay İltuş', data: { reason: 'Pencere uygun değil' }, occurredAt: '2026-11-13T09:00:00Z' },
          ],
        },
      }) as never,
    );
    renderRoute(PATH);

    for (const name of ['Özet', 'Plan', 'Etkilenen varlıklar', 'Görevler', 'Onay', 'Zaman çizgisi']) {
      expect(await screen.findByRole('region', { name })).toBeInTheDocument();
    }
    expect(screen.getByText('DMR-000001 · Dell Latitude')).toBeInTheDocument();
    const tasks = screen.getByRole('region', { name: 'Görevler' });
    for (const phase of ['Hazırlık', 'Uygulama', 'Doğrulama', 'Geri dönüş']) {
      expect(within(tasks).getByRole('heading', { name: phase })).toBeInTheDocument();
    }
    expect(within(tasks).getByText(/TSK-2026-000001/)).toBeInTheDocument();
    const history = await screen.findByRole('list', { name: 'Zaman çizgisi' });
    expect(within(history).getByText('Pencere uygun değil')).toBeInTheDocument();
  });

  it('warns about window conflicts and links the visible ones', async () => {
    const conflicts = [
      { number: 'CHG-2026-000009', title: 'Ağ bakımı', windowStart: '2026-12-01T22:00:00Z', windowEnd: '2026-12-02T01:00:00Z', reason: 'asset' as const },
      { number: null, title: null, windowStart: '2026-12-01T23:00:00Z', windowEnd: '2026-12-02T03:00:00Z', reason: 'service' as const },
    ];
    mockApi(changeRoutes({ ...CHANGE, conflicts }) as never);
    renderRoute(PATH);

    expect(await screen.findByText('Bakım penceresi 2 değişiklikle çakışıyor')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /CHG-2026-000009/ })).toHaveAttribute('href', '/changes/CHG-2026-000009');
    expect(screen.getByText(/Ortak varlık/)).toBeInTheDocument();
    expect(screen.getByText(/Aynı hizmet/)).toBeInTheDocument();
    expect(screen.getByText('Görmediğiniz bir değişiklik')).toBeInTheDocument();
  });

  it('shows no conflict banner when nothing conflicts', async () => {
    mockApi(changeRoutes(CHANGE) as never);
    renderRoute(PATH);

    await screen.findByRole('region', { name: 'Plan' });
    expect(screen.queryByText(/çakışıyor/)).not.toBeInTheDocument();
  });

  it('adds a task with the phase of its group', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(CHANGE, {
        'GET /api/v1/tasks': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
        'POST /api/v1/tasks': async (request: Request) => {
          posted = await request.json();
          return { status: 201, body: { number: 'TSK-2026-000002' } };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Görev ekle Hazırlık' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Başlık'), 'Yedek al');
    await chooseOption(user, 'Ekip', 'Saha Destek', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Görevi aç' }));

    await waitFor(() => expect(posted).toMatchObject({ title: 'Yedek al', teamId: 'team-2', changeNumber: CHANGE.number, phase: 'preparation' }));
  });

  it('warns when no other admin can approve', async () => {
    mockApi(changeRoutes({ ...CHANGE, hasEligibleApprover: false }) as never);
    renderRoute(PATH);

    expect(await screen.findByText('Onaylayabilecek başka yönetici yok')).toBeInTheDocument();
  });

  it('shows the approval box with decision buttons to an eligible admin', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(
        {
          ...CHANGE,
          status: 'pending_approval',
          approval: { status: 'pending', decidedById: null, decidedByName: null, reason: null, createdAt: '2026-11-13T09:00:00Z', decidedAt: null, canDecide: true, version: 5 },
        },
        {
          [`POST ${BASE}/approval/reject`]: async (request: Request) => {
            posted = await request.json();
            return { status: 200, body: { ...CHANGE, status: 'draft' } };
          },
        },
      ) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Reddet' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Gerekçe'), 'Pencere uygun değil');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ approvalVersion: 5, reason: 'Pencere uygun değil' }));
  });

  it('renders the working summary without actions, approvals or history', async () => {
    mockApi({
      ...changeRoutes(CHANGE),
      [`GET ${BASE}`]: { status: 200, body: { access: 'summary', detail: null, summary: CHANGE_SUMMARY } },
    } as never);
    renderRoute(PATH);

    expect(await screen.findByText('Bu değişikliğin çalışma özetini görüyorsunuz.')).toBeInTheDocument();
    expect(screen.getByText('Anahtarı sırayla güncelle')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Onay' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Zaman çizgisi' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Uygulamaya başla' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Görev ekle Hazırlık' })).not.toBeInTheDocument();
  });

  it('shows the stale-version message and loads the change again on 409', async () => {
    let loads = 0;
    mockApi(
      changeRoutes(CHANGE, {
        [`GET ${BASE}`]: () => {
          loads++;
          return { status: 200, body: full(CHANGE) };
        },
        [`POST ${BASE}/submit`]: { status: 409, body: { code: 'concurrency.conflict' } },
      }) as never,
    );
    const { user } = renderRoute(PATH);

    await user.click(await screen.findByRole('button', { name: 'Onaya gönder' }));

    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
    await waitFor(() => expect(loads).toBeGreaterThan(1));
  });
});
