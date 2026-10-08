import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LAPTOP, LOCATIONS, pageOf } from '../../test/assetData';
import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const ROW = {
  row: 2, assetTag: 'DT-1', type: 'laptop', brand: 'Lenovo', model: 'T14', serialNumber: null, locationId: 'l-1', locationPath: 'Merkez Kampüs',
  departmentId: null, departmentName: null, status: 'in_stock', warrantyEndsOn: null, purchasedOn: null, notes: null,
};
const JOB = { jobId: 'j-1', fileName: 'cihazlar.csv', status: 'previewed', totalRows: 1, validRows: 1, errorRows: 0, rows: [ROW], errors: [], createdAt: '2026-09-26T10:00:00Z', committedAt: null };

const routes = (extra: Record<string, unknown> = {}) => ({
  'GET /api/v1/auth/me': { status: 200, body: IT_ME },
  'GET /api/v1/assets': { status: 200, body: pageOf([LAPTOP]) },
  [`GET /api/v1/assets/${LAPTOP.id}`]: { status: 200, body: LAPTOP },
  [`GET /api/v1/assets/${LAPTOP.id}/history`]: { status: 200, body: { movements: [] } },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/departments': { status: 200, body: [] },
  ...extra,
});
const trailNav = () => screen.findByRole('navigation', { name: 'Sayfa yolu' });

describe('asset detail', () => {
  it('shows brand and model as separate fields', async () => {
    mockApi(routes() as never);
    renderRoute(`/assets/${LAPTOP.id}`);
    const brand = await screen.findByText('Marka');
    expect(brand.nextElementSibling).toHaveTextContent(/^Lenovo$/);
    expect(screen.getByText('Model').nextElementSibling).toHaveTextContent(/^T14$/);
  });

  it('names the asset tag in the breadcrumb of a direct URL and links to the inventory', async () => {
    mockApi(routes() as never);
    renderRoute(`/assets/${LAPTOP.id}`);
    const nav = await trailNav();
    expect(await within(nav).findByText(LAPTOP.assetTag)).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Envanter' })).toHaveAttribute('href', '/assets');
  });
});

describe('inventory list', () => {
  it('keeps the filters in the URL so the detail returns to them', async () => {
    mockApi(routes() as never);
    const { user, router } = renderRoute('/assets');
    await user.selectOptions(await screen.findByLabelText('Durum'), 'assigned');
    await waitFor(() => expect(new URLSearchParams(router.state.location.search).get('status')).toBe('assigned'));
    await user.click((await screen.findAllByRole('link', { name: new RegExp(LAPTOP.assetTag) }))[0] as HTMLElement);
    await waitFor(() => expect(router.state.location.pathname).toBe(`/assets/${LAPTOP.id}`));
    const nav = await trailNav();
    expect(within(nav).getByRole('link', { name: 'Envanter' })).toHaveAttribute('href', '/assets?status=assigned');
  });
});

describe('leaving with unsaved work', () => {
  it('asks before leaving a CSV preview that was not imported', async () => {
    mockApi(routes({ 'POST /api/v1/assets/import/preview': { status: 200, body: JOB } }) as never);
    const { user, router } = renderRoute('/assets/import');
    await user.upload(await screen.findByLabelText('CSV dosyası'), new File(['x'], 'cihazlar.csv', { type: 'text/csv' }));
    await user.click(screen.getByRole('button', { name: 'Önizle' }));
    await screen.findByRole('button', { name: 'İçe aktar' });
    await user.click(within(await trailNav()).getByRole('link', { name: 'Envanter' }));
    expect(await screen.findByRole('dialog', { name: 'Kaydedilmemiş değişiklikler' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/assets/import');
  });

  it('asks before leaving a changed asset form', async () => {
    mockApi(routes() as never);
    const { user, router } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Model'), 'X1');
    await router.navigate('/');
    expect(await screen.findByRole('dialog', { name: 'Kaydedilmemiş değişiklikler' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/assets');
  });
});
