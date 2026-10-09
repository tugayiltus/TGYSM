import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const HARDWARE = { id: 'c-1', name: 'Donanım', parentId: null, scope: 'both', isActive: true, version: 1 };
const PRINTER = { id: 'c-2', name: 'Yazıcı', parentId: 'c-1', scope: 'incident', isActive: true, version: 1 };

describe('Catalog admin', () => {
  it('lists categories as a tree and creates a child category', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/categories': { status: 200, body: [PRINTER, HARDWARE] },
      'POST /api/v1/admin/categories': async (request) => {
        posted = await request.json();
        return { status: 409, body: { code: 'categories.too_deep' } };
      },
    });
    const { user } = renderRoute('/admin/categories');

    expect((await screen.findAllByText('Donanım / Yazıcı')).length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', { name: 'Yeni kategori' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Kategori adı'), 'Toner');
    await user.selectOptions(within(dialog).getByLabelText('Üst kategori'), 'Donanım');
    await user.selectOptions(within(dialog).getByLabelText('Kullanım'), 'Arıza');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Kategoriler en fazla iki seviyeli olabilir.')).toBeInTheDocument();
    expect(posted).toEqual({ name: 'Toner', parentId: 'c-1', scope: 'incident' });
    expect(within(dialog).queryByRole('option', { name: 'Yazıcı' })).not.toBeInTheDocument();
  });

  it('creates a request type that needs approval', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/request-types': { status: 200, body: [] },
      'POST /api/v1/admin/request-types': async (request) => {
        posted = await request.json();
        return { status: 201, body: { id: 'rt-1', name: 'Yeni cihaz', requiresApproval: true, isActive: true, version: 1 } };
      },
    });
    const { user } = renderRoute('/admin/request-types');

    await user.click(await screen.findByRole('button', { name: 'Yeni talep tipi' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Talep tipi adı'), 'Yeni cihaz');
    await user.click(within(dialog).getByRole('switch', { name: 'Yönetici onayı gerekir' }));
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    await waitFor(() => expect(posted).toEqual({ name: 'Yeni cihaz', requiresApproval: true }));
  });

  it('sets a team as the default intake team', async () => {
    let posted: unknown;
    const team = { id: 't-1', name: 'Servis Masası', isActive: true, isDefaultIntake: false, memberCount: 0, version: 3 };
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/teams': { status: 200, body: { items: [team], page: 1, pageSize: 20, totalCount: 1 } },
      'PUT /api/v1/admin/teams/t-1': async (request) => {
        posted = await request.json();
        return { status: 200, body: { ...team, isDefaultIntake: true, members: [] } };
      },
    });
    const { user } = renderRoute('/admin/teams');

    await user.click((await screen.findAllByRole('button', { name: 'Düzenle Servis Masası' }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('switch', { name: /Yeni kayıtları bu ekip alsın/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    await waitFor(() => expect(posted).toEqual({ version: 3, name: 'Servis Masası', isDefaultIntake: true }));
  });
});
