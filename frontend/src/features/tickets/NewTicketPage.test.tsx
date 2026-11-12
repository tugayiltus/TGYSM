import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LOCATIONS } from '../../test/assetData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
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
    await screen.findByRole('combobox', { name: 'İlgili cihaz' });
    await chooseOption(user, 'İlgili cihaz', 'DMR-000001 · Lenovo T14');
    await user.type(screen.getByLabelText(/^Konu/), 'Ekran kırık');
    await user.type(screen.getByLabelText(/^Açıklama/), 'Açılmıyor, siyah ekran.');
    await user.click(screen.getByRole('button', { name: 'Gönder' }));
    await waitFor(() => expect(posted.assetId).toBe('a-1'));
  });

  it('does not offer a device on a request', async () => {
    mockApi({ ...referenceRoutes, 'GET /api/v1/me/assets': { status: 200, body: [] } });
    renderRoute('/tickets/new/request');
    await screen.findByLabelText(/^Konu/);
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
    await user.type(screen.getByLabelText(/^Konu/), 'Yazıcı çalışmıyor');
    await user.type(screen.getByLabelText(/^Açıklama/), 'Kağıt sıkışıyor');
    await chooseOption(user, 'Kategori', 'Donanım');
    await user.upload(screen.getByLabelText(/Dosya ekle/, { selector: 'input[type="file"]' }), new File([new Uint8Array([0x89, 0x50])], 'ekran.png', { type: 'image/png' }));
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

    await chooseOption(user, /^Talep tipi/, 'Yeni dizüstü');
    expect(screen.getByText('Bu talep yöneticinizin onayından sonra işleme alınır.')).toBeInTheDocument();
    await user.type(screen.getByLabelText(/^Konu/), 'Yeni cihaz');
    await user.type(screen.getByLabelText(/^Açıklama/), 'Göreve başlıyorum');
    await user.click(screen.getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/tickets/REQ-2026-000001'));
    expect(posted).toEqual({ subject: 'Yeni cihaz', description: 'Göreve başlıyorum', categoryId: null, locationId: null, requestTypeId: 'rt-1' });
  });
});

describe('incident from a scanned device label', () => {
  const SCANNED = { id: 'a-9', assetTag: 'DMR-000009', type: 'desktop', brand: 'HP', model: 'ProDesk 400', canReport: true };
  const scanRoutes = (scan: unknown, status = 200) => ({
    ...referenceRoutes,
    'GET /api/v1/me/assets': { status: 200, body: [] },
    [`GET /api/v1/scan/assets/${SCANNED.id}`]: { status, body: scan },
  });

  it('chooses the scanned device even when it is not mine and sends it', async () => {
    let posted: { assetId?: string | null } = {};
    mockApi({
      ...scanRoutes(SCANNED),
      'POST /api/v1/tickets/incidents': async (request) => {
        posted = (await request.json()) as { assetId?: string | null };
        return { status: 201, body: INCIDENT };
      },
      [`GET /api/v1/tickets/${INCIDENT.number}`]: { status: 200, body: INCIDENT },
    });
    const { user } = renderRoute(`/tickets/new/incident?asset=${SCANNED.id}`);
    const device = await screen.findByRole('combobox', { name: 'İlgili cihaz' });
    // The option appears once the scanned device has loaded.
    await waitFor(() => expect(device).toHaveTextContent('DMR-000009 · HP ProDesk 400'));
    await user.type(screen.getByLabelText(/^Konu/), 'Yazıcı');
    await user.type(screen.getByLabelText(/^Açıklama/), 'Kağıt sıkışıyor.');
    await user.click(screen.getByRole('button', { name: 'Gönder' }));
    await waitFor(() => expect(posted.assetId).toBe(SCANNED.id));
  });

  it('says a lost or retired device cannot be reported and sends no device', async () => {
    let posted: { assetId?: string | null } = {};
    mockApi({
      ...scanRoutes({ ...SCANNED, canReport: false }),
      'POST /api/v1/tickets/incidents': async (request) => {
        posted = (await request.json()) as { assetId?: string | null };
        return { status: 201, body: INCIDENT };
      },
      [`GET /api/v1/tickets/${INCIDENT.number}`]: { status: 200, body: INCIDENT },
    });
    const { user } = renderRoute(`/tickets/new/incident?asset=${SCANNED.id}`);
    expect(await screen.findByText('Bu cihaz için arıza kaydı açılamaz.')).toBeInTheDocument();
    await user.type(screen.getByLabelText(/^Konu/), 'Yazıcı');
    await user.type(screen.getByLabelText(/^Açıklama/), 'Kağıt sıkışıyor.');
    await user.click(screen.getByRole('button', { name: 'Gönder' }));
    await waitFor(() => expect(posted.assetId).toBeNull());
  });

  it('says so when the scanned device does not exist', async () => {
    mockApi(scanRoutes({ code: 'not_found' }, 404));
    renderRoute(`/tickets/new/incident?asset=${SCANNED.id}`);
    expect(await screen.findByText('Cihaz bulunamadı.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Konu/)).toBeEnabled();
  });
});

describe('scan route', () => {
  it('opens the device for IT staff', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME } } as never);
    const { router } = renderRoute('/scan/asset/a-9');
    await waitFor(() => expect(router.state.location.pathname).toBe('/assets/a-9'));
  });

  it('opens a prefilled incident for employees', async () => {
    mockApi({ ...referenceRoutes, 'GET /api/v1/me/assets': { status: 200, body: [] } } as never);
    const { router } = renderRoute('/scan/asset/a-9');
    await waitFor(() => expect(router.state.location.pathname).toBe('/tickets/new/incident'));
    expect(router.state.location.search).toBe('?asset=a-9');
  });
});
