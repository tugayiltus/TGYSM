import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LOCATIONS } from '../../test/assetData';
import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { INCIDENT } from '../../test/ticketData';

const referenceRoutes = {
  'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/categories': { status: 200, body: [{ id: 'c-1', path: 'Donanım', parentId: null }] },
  'GET /api/v1/reference/request-types': {
    status: 200,
    body: [
      { id: 'rt-1', name: 'Yeni dizüstü', requiresApproval: true },
      { id: 'rt-2', name: 'Yazılım kurulumu', requiresApproval: false },
    ],
  },
};

describe('incident device', () => {
  it('offers only my assigned devices, optional, and sends the chosen one', async () => {
    let posted: { assetId?: string | null } = {};
    mockApi({
      ...referenceRoutes,
      'GET /api/v1/me/assets': { status: 200, body: [{ id: 'a-1', assetTag: 'DMR-000001', type: 'laptop', brand: 'Lenovo', model: 'T14', serialNumber: null, assignedAt: '2026-09-01T08:00:00Z' }] },
      'POST /api/v1/tickets/incidents': async (request) => {
        posted = (await request.json()) as { assetId?: string | null };
        return { status: 201, body: INCIDENT };
      },
      [`GET /api/v1/tickets/${INCIDENT.number}`]: { status: 200, body: INCIDENT },
    });
    const { user } = renderRoute('/tickets/new/incident');
    const device = await screen.findByLabelText('İlgili cihaz');
    expect(await within(device).findByRole('option', { name: 'DMR-000001 · Lenovo T14' })).toBeInTheDocument();
    await user.selectOptions(device, 'DMR-000001 · Lenovo T14');
    await user.type(screen.getByLabelText('Konu'), 'Ekran kırık');
    await user.type(screen.getByLabelText('Açıklama'), 'Açılmıyor, siyah ekran.');
    await user.click(screen.getByRole('button', { name: 'Gönder' }));
    await waitFor(() => expect(posted.assetId).toBe('a-1'));
  });

  it('does not offer a device on a request', async () => {
    mockApi({ ...referenceRoutes, 'GET /api/v1/me/assets': { status: 200, body: [] } });
    renderRoute('/tickets/new/request');
    await screen.findByLabelText('Konu');
    expect(screen.queryByLabelText('İlgili cihaz')).not.toBeInTheDocument();
  });
});

describe('NewTicketPage', () => {
  it('validates, creates an incident, uploads the files and opens the ticket', async () => {
    let posted: unknown;
    let uploadType: string | null = null;
    mockApi({
      ...referenceRoutes,
      'POST /api/v1/tickets/incidents': async (request) => {
        posted = await request.json();
        return { status: 201, body: INCIDENT };
      },
      [`POST /api/v1/tickets/${INCIDENT.number}/comments`]: (request) => {
        uploadType = request.headers.get('content-type');
        return { status: 201, body: {} };
      },
      [`GET /api/v1/tickets/${INCIDENT.number}`]: { status: 200, body: INCIDENT },
    });
    const { user, router } = renderRoute('/tickets/new/incident');

    await user.click(await screen.findByRole('button', { name: 'Gönder' }));
    expect(await screen.findAllByText('Bu alan zorunludur.')).toHaveLength(2);
    await user.type(screen.getByLabelText('Konu'), 'Yazıcı çalışmıyor');
    await user.type(screen.getByLabelText('Açıklama'), 'Kağıt sıkışıyor');
    await user.selectOptions(screen.getByLabelText('Kategori'), await screen.findByRole('option', { name: 'Donanım' }));
    await user.upload(screen.getByLabelText(/Dosya ekle/), new File([new Uint8Array([0x89, 0x50])], 'ekran.png', { type: 'image/png' }));
    await user.click(screen.getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/tickets/${INCIDENT.number}`));
    expect(posted).toEqual({ subject: 'Yazıcı çalışmıyor', description: 'Kağıt sıkışıyor', categoryId: 'c-1', locationId: null, assetId: null });
    expect(uploadType).toMatch(/^multipart\/form-data/);
  });

  it('asks for a request type and explains when approval is needed', async () => {
    let posted: unknown;
    mockApi({
      ...referenceRoutes,
      'POST /api/v1/tickets/requests': async (request) => {
        posted = await request.json();
        return { status: 201, body: { ...INCIDENT, number: 'REQ-2026-000001', kind: 'request' } };
      },
      'GET /api/v1/tickets/REQ-2026-000001': { status: 200, body: { ...INCIDENT, number: 'REQ-2026-000001', kind: 'request' } },
    });
    const { user, router } = renderRoute('/tickets/new/request');

    await user.selectOptions(await screen.findByLabelText('Talep tipi'), await screen.findByRole('option', { name: 'Yeni dizüstü' }));
    expect(screen.getByText('Bu talep yöneticinizin onayından sonra işleme alınır.')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Konu'), 'Yeni cihaz');
    await user.type(screen.getByLabelText('Açıklama'), 'Göreve başlıyorum');
    await user.click(screen.getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/tickets/REQ-2026-000001'));
    expect(posted).toEqual({ subject: 'Yeni cihaz', description: 'Göreve başlıyorum', categoryId: null, locationId: null, requestTypeId: 'rt-1' });
  });
});
