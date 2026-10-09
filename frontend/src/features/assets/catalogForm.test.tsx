import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CATALOG, LAPTOP, LOCATIONS, pageOf } from '../../test/assetData';
import { ADMIN_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const routes = (me: object, extra: Record<string, unknown> = {}) => ({
  'GET /api/v1/auth/me': { status: 200, body: me },
  'GET /api/v1/assets': { status: 200, body: pageOf([LAPTOP]) },
  'GET /api/v1/assets/catalog': { status: 200, body: CATALOG },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/departments': { status: 200, body: [] },
  ...extra,
});

describe('asset form catalog', () => {
  it('offers only the brands and models of the chosen device type', async () => {
    mockApi(routes(IT_ME) as never);
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Tür'), 'Masaüstü');
    await user.click(within(dialog).getByRole('combobox', { name: 'Marka' }));
    expect(within(within(dialog).getByRole('listbox')).getAllByRole('option').map((o) => o.textContent)).toEqual(['Lenovo']);
    await user.keyboard('{ArrowDown}{Enter}');
    const model = within(dialog).getByLabelText('Model');
    expect(within(model).getAllByRole('option').map((o) => o.textContent)).toEqual(['Seçiniz', 'M70q']);
  });

  it('tells IT staff that a missing definition is for an admin, and gives admins a link to the catalog', async () => {
    mockApi(routes(IT_ME) as never);
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Listede yoksa tanım eksik: yöneticiye bildirin.')).toBeInTheDocument();
    expect(within(dialog).queryByRole('link', { name: 'Kataloğu düzenle' })).not.toBeInTheDocument();
  });

  it('gives admins a link to the device catalog', async () => {
    mockApi(routes(ADMIN_ME) as never);
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('link', { name: 'Kataloğu düzenle' })).toHaveAttribute('href', '/admin/models');
  });

  it('keeps an asset whose model is no longer in the active catalog editable with its model', async () => {
    const retired = { ...LAPTOP, modelId: 'm-old', brand: 'Lenovo', model: 'T470' };
    let posted: { modelId?: string } = {};
    mockApi(
      routes(IT_ME, {
        [`GET /api/v1/assets/${LAPTOP.id}`]: { status: 200, body: retired },
        [`GET /api/v1/assets/${LAPTOP.id}/history`]: { status: 200, body: { movements: [] } },
        [`PUT /api/v1/assets/${LAPTOP.id}`]: async (request: Request) => {
          posted = (await request.json()) as { modelId?: string };
          return { status: 200, body: retired };
        },
      }) as never,
    );
    const { user } = renderRoute(`/assets/${LAPTOP.id}`);
    await user.click(await screen.findByRole('button', { name: 'Düzenle' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Model')).toHaveDisplayValue('T470');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(posted.modelId).toBe('m-old'));
  });
});
