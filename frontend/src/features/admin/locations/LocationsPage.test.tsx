import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const LOCATIONS = [
  { id: 'c', name: 'Merkez Kampüs', parentId: null, isActive: true, version: 1 },
  { id: 'b', name: 'A Blok', parentId: 'c', isActive: true, version: 1 },
];

describe('LocationsPage', () => {
  it('shows the tree and adds a child under the chosen parent', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/locations': { status: 200, body: LOCATIONS },
      'POST /api/v1/admin/locations': async (request) => {
        posted = await request.json();
        return { status: 201, body: { id: 'f', name: 'Kat 1', parentId: 'b', isActive: true, version: 1 } };
      },
    });
    const { user } = renderRoute('/admin/locations');

    const blockRow = (await screen.findByText('A Blok')).closest('li')!;
    await user.click(within(blockRow).getByRole('button', { name: /^Alt lokasyon ekle/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Üst lokasyon')).toHaveValue('b');
    await user.type(within(dialog).getByLabelText('Lokasyon adı'), 'Kat 1');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    await screen.findByRole('heading', { name: 'Lokasyonlar' });
    expect(posted).toEqual({ name: 'Kat 1', parentId: 'b' });
  });

  it('does not offer the location itself or its children as a new parent', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/locations': { status: 200, body: LOCATIONS },
    });
    const { user } = renderRoute('/admin/locations');

    const campusRow = (await screen.findByText('Merkez Kampüs')).closest('li')!;
    await user.click(within(campusRow).getAllByRole('button', { name: /^Düzenle/ })[0]!);
    const parentSelect = within(await screen.findByRole('dialog')).getByLabelText('Üst lokasyon');

    expect(within(parentSelect).queryByRole('option', { name: /A Blok/ })).not.toBeInTheDocument();
    expect(within(parentSelect).queryByRole('option', { name: 'Merkez Kampüs' })).not.toBeInTheDocument();
  });
});
