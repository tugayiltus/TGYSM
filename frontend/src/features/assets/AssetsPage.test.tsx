import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DEPARTMENTS, LAPTOP, LOCATIONS, pageOf, CATALOG } from '../../test/assetData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const referenceRoutes = {
  'GET /api/v1/assets/catalog': { status: 200, body: CATALOG },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/departments': { status: 200, body: DEPARTMENTS },
};

describe('AssetsPage', () => {
  it('lists assets and filters by type', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'GET /api/v1/assets': { status: 200, body: pageOf([LAPTOP]) },
    });
    const { user } = renderRoute('/assets');

    expect(await screen.findAllByText('DT-0001')).not.toHaveLength(0);
    expect(screen.getAllByText('Stokta')).not.toHaveLength(0);
    await user.selectOptions(screen.getByLabelText('Tür'), 'Tablet');

    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).searchParams.get('type') === 'tablet')).toBe(true));
  });

  it('creates an asset and shows a taken tag under the tag field', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'GET /api/v1/assets': { status: 200, body: pageOf([]) },
      ...referenceRoutes,
      'POST /api/v1/assets': async (request) => {
        posted = await request.json();
        return { status: 409, body: { code: 'assets.tag_taken' } };
      },
    });
    const { user } = renderRoute('/assets');

    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Demirbaş numarası'), 'DT-0001');
    await user.selectOptions(within(dialog).getByLabelText('Tür'), 'Dizüstü');
    await user.type(within(dialog).getByRole('combobox', { name: 'Marka' }), 'len');
    await user.keyboard('{ArrowDown}{Enter}');
    await user.selectOptions(within(dialog).getByLabelText('Model'), 'T14');
    await user.selectOptions(within(dialog).getByLabelText('Konum'), await within(dialog).findByRole('option', { name: 'Merkez Kampüs / A Blok' }));
    await user.type(within(dialog).getByLabelText('Garanti bitiş tarihi'), '2027-12-31');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Bu demirbaş numarası başka bir cihazda kayıtlı.')).toBeInTheDocument();
    expect(posted).toEqual({
      assetTag: 'DT-0001',
      modelId: 'm-1',
      serialNumber: null,
      locationId: 'l-2',
      departmentId: null,
      warrantyEndsOn: '2027-12-31',
      purchasedOn: null,
      notes: null,
    });
  });

  it('sends employees back to the home page', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { router } = renderRoute('/assets');

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
  });
});
