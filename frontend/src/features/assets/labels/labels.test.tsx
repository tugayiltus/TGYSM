import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DEPARTMENTS, LAPTOP, LOCATIONS, pageOf, CATALOG } from '../../../test/assetData';
import { IT_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

import { labelLines, scanLink } from './scanLink';

// jsdom has no canvas; the label image is drawn in the browser and checked in E2E.
vi.mock('./drawLabel', () => ({ drawLabel: vi.fn(async () => 'data:image/png;base64,TEST') }));

const PUBLIC = 'https://itsm.example.com';
const DESKTOP = { ...LAPTOP, id: 'a-2', assetTag: 'DT-0002', type: 'desktop' as const, model: 'M70q', serialNumber: null };
const label = (a: typeof LAPTOP) => ({ id: a.id, assetTag: a.assetTag, brand: a.brand, model: a.model, serialNumber: a.serialNumber });

const routes = (extra: Record<string, unknown> = {}) => ({
  'GET /api/v1/auth/me': { status: 200, body: IT_ME },
  'GET /api/v1/assets': { status: 200, body: pageOf([LAPTOP, DESKTOP]) },
  'GET /api/v1/assets/catalog': { status: 200, body: CATALOG },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/departments': { status: 200, body: DEPARTMENTS },
  [`GET /api/v1/assets/${LAPTOP.id}`]: { status: 200, body: LAPTOP },
  [`GET /api/v1/assets/${LAPTOP.id}/history`]: { status: 200, body: { movements: [] } },
  [`GET /api/v1/assets/${LAPTOP.id}/tickets`]: { status: 200, body: [] },
  'POST /api/v1/assets/labels': async (request: Request) => {
    const { ids } = (await request.json()) as { ids: string[] };
    const all = [LAPTOP, DESKTOP];
    return { status: 200, body: { publicBaseUrl: PUBLIC, items: ids.flatMap((id) => all.filter((a) => a.id === id).map(label)) } };
  },
  ...extra,
});

describe('scan link and label text', () => {
  it('links to the scan route under the company address', () => {
    expect(scanLink(PUBLIC, 'a-1')).toBe('https://itsm.example.com/scan/asset/a-1');
  });

  it('puts the tag first, then brand and model, then the serial number only when there is one; no company name', () => {
    expect(labelLines(label(LAPTOP))).toEqual(['DT-0001', 'Lenovo T14', 'SN-1']);
    expect(labelLines(label(DESKTOP))).toEqual(['DT-0002', 'Lenovo M70q']);
  });
});

describe('asset detail QR card', () => {
  it('shows the label with download and print', async () => {
    mockApi(routes() as never);
    renderRoute(`/assets/${LAPTOP.id}`);
    const card = await screen.findByRole('region', { name: 'QR kod' });
    expect(await within(card).findByRole('img', { name: 'DT-0001 etiketi' })).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'İndir' })).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Etiketi yazdır' })).toHaveAttribute('href', `/assets/labels?ids=${LAPTOP.id}`);
  });
});

describe('label print view', () => {
  it('prints one label per asset in the order asked for', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    mockApi(routes() as never);
    renderRoute(`/assets/labels?ids=${DESKTOP.id},${LAPTOP.id}`);
    const images = await screen.findAllByRole('img', { name: /etiketi$/ });
    expect(images.map((i) => i.getAttribute('alt'))).toEqual(['DT-0002 etiketi', 'DT-0001 etiketi']);
    await waitFor(() => expect(print).toHaveBeenCalledTimes(1));
  });
});

describe('bulk label print from Envanter', () => {
  it('enables the print action once rows are selected and opens the print view with them in list order', async () => {
    mockApi(routes() as never);
    const { user, router } = renderRoute('/assets');
    const action = await screen.findByRole('button', { name: 'Seçili cihazların etiketlerini yazdır' });
    // Astryx keeps a disabled button focusable and marks it with aria-disabled.
    expect(action).toHaveAttribute('aria-disabled', 'true');
    await user.click(await screen.findByRole('checkbox', { name: 'DT-0002 öğesini seç' }));
    await user.click(screen.getByRole('checkbox', { name: 'DT-0001 öğesini seç' }));
    expect(action).not.toHaveAttribute('aria-disabled', 'true');
    await user.click(action);
    await waitFor(() => expect(router.state.location.pathname).toBe('/assets/labels'));
    expect(router.state.location.state).toEqual({ ids: [LAPTOP.id, DESKTOP.id] });
  });
});

describe('label print view without known assets', () => {
  it('says so when none of the ids exist instead of printing a blank page', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    mockApi(routes() as never);
    renderRoute('/assets/labels?ids=gone-1,gone-2');
    expect(await screen.findByText('Yazdırılacak cihaz bulunamadı.')).toBeInTheDocument();
    expect(print).not.toHaveBeenCalled();
  });
});
