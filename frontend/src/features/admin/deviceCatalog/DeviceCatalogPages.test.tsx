import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const BRANDS = [
  { id: 'b-1', name: 'Lenovo', isActive: true, version: 3 },
  { id: 'b-2', name: 'Apple', isActive: false, version: 1 },
];
const MODELS = [
  { id: 'm-1', brandId: 'b-1', brandName: 'Lenovo', type: 'laptop', name: 'T14', isActive: true, version: 2 },
  { id: 'm-2', brandId: 'b-2', brandName: 'Apple', type: 'phone', name: 'iPhone 15', isActive: true, version: 1 },
];

describe('BrandsPage', () => {
  it('lists brands, shows a duplicate name under the field and deactivates with the version', async () => {
    let deactivated: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/brands': { status: 200, body: BRANDS },
      'POST /api/v1/admin/brands': { status: 409, body: { code: 'assets.catalog_duplicate' } },
      'POST /api/v1/admin/brands/b-1/deactivate': async (request) => {
        deactivated = await request.json();
        return { status: 200, body: { ...BRANDS[0], isActive: false } };
      },
    });
    const { user } = renderRoute('/admin/brands');
    const table = await screen.findByRole('table');
    expect(within(table).getByText('Lenovo')).toBeInTheDocument();
    expect(within(table).getByText('Pasif')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Yeni marka' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Ad'), ' lenovo ');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    expect(await within(dialog).findByText('Bu ad zaten kayıtlı. Mevcut tanımı kullanın.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Vazgeç' }));

    await user.click(within(table).getByRole('button', { name: /Pasifleştir.*Lenovo/ }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(deactivated).toEqual({ version: 3 }));
  });
});

describe('ModelsPage', () => {
  it('filters models by brand and creates a model under a brand and type', async () => {
    let created: unknown;
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/brands': { status: 200, body: BRANDS },
      'GET /api/v1/admin/models': { status: 200, body: MODELS },
      'POST /api/v1/admin/models': async (request) => {
        created = await request.json();
        return { status: 201, body: MODELS[0] };
      },
    });
    const { user } = renderRoute('/admin/models');
    const filter = await screen.findByLabelText('Marka filtresi');
    await within(filter).findByRole('option', { name: 'Lenovo' });
    await user.selectOptions(filter, 'Lenovo');
    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).searchParams.get('brandId') === 'b-1')).toBe(true));

    await user.click(screen.getByRole('button', { name: 'Yeni model' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Marka'), 'Lenovo');
    await user.selectOptions(within(dialog).getByLabelText('Tür'), 'Dizüstü');
    await user.type(within(dialog).getByLabelText('Ad'), 'ThinkPad T14s');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    await waitFor(() => expect(created).toEqual({ brandId: 'b-1', type: 'laptop', name: 'ThinkPad T14s' }));
  });

  it('is in the device catalog group of the admin menu and breadcrumb', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/brands': { status: 200, body: BRANDS }, 'GET /api/v1/admin/models': { status: 200, body: MODELS } });
    renderRoute('/admin/models');
    const nav = await screen.findByRole('navigation', { name: 'Yönetim menüsü' });
    expect(within(nav).getByRole('link', { name: 'Modeller' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'Markalar' })).toHaveAttribute('href', '/admin/brands');
    expect(within(screen.getByRole('navigation', { name: 'Sayfa yolu' })).getByText('Cihaz kataloğu')).toBeInTheDocument();
  });
});
