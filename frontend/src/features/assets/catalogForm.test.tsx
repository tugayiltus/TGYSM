import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CATALOG, LAPTOP, LOCATIONS, pageOf } from '../../test/assetData';
import { ADMIN_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption, findSelect } from '../../test/select';

const routes = (me: object, extra: Record<string, unknown> = {}) => ({
  'GET /api/v1/auth/me': { status: 200, body: me },
  'GET /api/v1/assets': { status: 200, body: pageOf([LAPTOP]) },
  'GET /api/v1/assets/catalog': { status: 200, body: CATALOG },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/departments': { status: 200, body: [] },
  ...extra,
});

describe('generated asset tag', () => {
  it('shows the tag the server assigned once the asset is saved', async () => {
    mockApi(routes(IT_ME, { 'POST /api/v1/assets': { status: 201, body: { ...LAPTOP, assetTag: 'DMR-000007' } } }) as never);
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Tür', 'Dizüstü', dialog);
    await chooseOption(user, 'Marka', 'Lenovo', dialog);
    await chooseOption(user, 'Model', 'T14', dialog);
    await chooseOption(user, 'Lokasyon', LOCATIONS[0]?.path ?? '', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    expect(await screen.findByText('Cihaz kaydedildi: DMR-000007')).toBeInTheDocument();
  });
});

describe('asset form catalog', () => {
  it('offers only the brands and models of the chosen device type', async () => {
    mockApi(routes(IT_ME) as never);
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Tür', 'Masaüstü', dialog);
    await user.click(await findSelect('Marka', dialog));
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['Lenovo']);
    await user.click(screen.getByRole('option', { name: 'Lenovo' }));
    await user.click(within(dialog).getByRole('combobox', { name: 'Model' }));
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['M70q']);
  });

  it('clears the brand and model when the device type changes, so a model of the old type is never sent', async () => {
    let posted = false;
    mockApi(
      routes(IT_ME, {
        'POST /api/v1/assets': () => {
          posted = true;
          return { status: 201, body: LAPTOP };
        },
      }) as never,
    );
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Tür', 'Dizüstü', dialog);
    await chooseOption(user, 'Marka', 'Lenovo', dialog);
    await chooseOption(user, 'Model', 'T14', dialog);
    await chooseOption(user, 'Lokasyon', LOCATIONS[0]?.path ?? '', dialog);

    await chooseOption(user, 'Tür', 'Masaüstü', dialog);
    expect(await findSelect('Marka', dialog)).toHaveTextContent('Marka seçin');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect((await within(dialog).findAllByText('Bu alan zorunludur.')).length).toBeGreaterThan(0);
    expect(posted).toBe(false);
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
    expect(within(dialog).getByRole('combobox', { name: 'Model' })).toHaveTextContent('T470');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(posted.modelId).toBe('m-old'));
  });
});
