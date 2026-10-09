import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LAPTOP, LOCATIONS } from '../../test/assetData';
import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const HISTORY = {
  movements: [
    { id: 'm-1', kind: 'created', fromStatus: null, toStatus: 'in_stock', userId: null, userName: null, actorName: 'Can Demir', reason: null, userResponsibilityRetained: null, occurredAt: '2026-09-01T08:00:00Z' },
  ],
};
const AYSE = { id: 'u-1', fullName: 'Ayşe Kaya', email: 'ayse@example.com', departmentName: 'Muhasebe' };

function detailRoutes(asset = LAPTOP) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    'GET /api/v1/assets/a-1': { status: 200, body: asset },
    'GET /api/v1/assets/a-1/history': { status: 200, body: HISTORY },
    'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
    'GET /api/v1/reference/users': { status: 200, body: [AYSE] },
  };
}

describe('AssetDetailPage', () => {
  it('shows the card, the history and only the operations allowed in the current status', async () => {
    mockApi(detailRoutes());
    renderRoute('/assets/a-1');

    expect(await screen.findByRole('heading', { name: 'DT-0001' })).toBeInTheDocument();
    expect(await screen.findByText('Kayıt oluşturuldu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Teslim et' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Bakıma al' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'İade al' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Bulundu' })).not.toBeInTheDocument();
  });

  it('hands the asset over to a searched user with the current version', async () => {
    let posted: unknown;
    mockApi({
      ...detailRoutes(),
      'POST /api/v1/assets/a-1/assign': async (request) => {
        posted = await request.json();
        return { status: 200, body: { ...LAPTOP, status: 'assigned', assignedUserId: 'u-1', assignedUserName: 'Ayşe Kaya', version: 8 } };
      },
    });
    const { user } = renderRoute('/assets/a-1');

    await user.click(await screen.findByRole('button', { name: 'Teslim et' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Kullanıcı ara'), 'ayşe');
    await user.selectOptions(within(dialog).getByLabelText('Kullanıcı'), await within(dialog).findByRole('option', { name: 'Ayşe Kaya (ayse@example.com)' }));
    await user.type(within(dialog).getByLabelText('Not'), 'Yeni başlangıç');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await screen.findByRole('heading', { name: 'DT-0001' });
    expect(posted).toEqual({ version: 7, userId: 'u-1', requestTicketId: null, note: 'Yeni başlangıç' });
  });

  it('requires a reason to report an asset lost and shows a refused transition', async () => {
    let posted: unknown;
    mockApi({
      ...detailRoutes(),
      'POST /api/v1/assets/a-1/lost': async (request) => {
        posted = await request.json();
        return { status: 409, body: { code: 'assets.invalid_transition' } };
      },
    });
    const { user } = renderRoute('/assets/a-1');

    await user.click(await screen.findByRole('button', { name: 'Kayıp bildir' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Neden'), 'Serviste kayboldu');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    expect(await within(dialog).findByText('Bu işlem cihazın mevcut durumunda yapılamaz.')).toBeInTheDocument();
    expect(posted).toEqual({ version: 7, reason: 'Serviste kayboldu' });
  });

  it('returns an assigned asset to a chosen location', async () => {
    let posted: unknown;
    mockApi({
      ...detailRoutes({ ...LAPTOP, status: 'assigned', assignedUserId: 'u-1', assignedUserName: 'Ayşe Kaya' }),
      'POST /api/v1/assets/a-1/return': async (request) => {
        posted = await request.json();
        return { status: 200, body: LAPTOP };
      },
    });
    const { user } = renderRoute('/assets/a-1');

    expect(await screen.findByText('Ayşe Kaya')).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'İade al' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Lokasyon'), await within(dialog).findByRole('option', { name: 'Merkez Kampüs / A Blok' }));
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await screen.findByRole('heading', { name: 'DT-0001' });
    expect(posted).toEqual({ version: 7, locationId: 'l-2' });
  });

  it('keeps the user responsible during maintenance when asked', async () => {
    let posted: unknown;
    mockApi({
      ...detailRoutes({ ...LAPTOP, status: 'assigned', assignedUserId: 'u-1', assignedUserName: 'Ayşe Kaya' }),
      'POST /api/v1/assets/a-1/maintenance/start': async (request) => {
        posted = await request.json();
        return { status: 200, body: { ...LAPTOP, status: 'maintenance' } };
      },
    });
    const { user } = renderRoute('/assets/a-1');

    await user.click(await screen.findByRole('button', { name: 'Bakıma al' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByLabelText('Kullanıcının sorumluluğu devam etsin'));
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await screen.findByRole('heading', { name: 'DT-0001' });
    expect(posted).toEqual({ version: 7, retainResponsibility: true });
  });
});

describe('AssetOperationDialog user search', () => {
  it('clears the chosen user when the search changes', async () => {
    const ALI = { id: 'u-2', fullName: 'Ali Veli', email: 'ali@example.com', departmentName: null };
    mockApi({
      ...detailRoutes(),
      'GET /api/v1/reference/users': (request) =>
        ({ status: 200, body: new URL(request.url).searchParams.get('search') === 'ali' ? [ALI] : [AYSE] }),
    });
    const { user } = renderRoute('/assets/a-1');

    await user.click(await screen.findByRole('button', { name: 'Teslim et' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Kullanıcı ara'), 'ayşe');
    await user.selectOptions(within(dialog).getByLabelText('Kullanıcı'), await within(dialog).findByRole('option', { name: 'Ayşe Kaya (ayse@example.com)' }));
    await user.clear(within(dialog).getByLabelText('Kullanıcı ara'));
    await user.type(within(dialog).getByLabelText('Kullanıcı ara'), 'ali');
    await within(dialog).findByRole('option', { name: 'Ali Veli (ali@example.com)' });
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();
  });
});
