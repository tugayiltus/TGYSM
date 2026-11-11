import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CATALOG, LAPTOP, LOCATIONS, pageOf } from '../../test/assetData';
import { ADMIN_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';

const DISCARD_TITLE = 'Değişiklikler kaydedilmedi';

const assetRoutes = {
  'GET /api/v1/auth/me': { status: 200, body: IT_ME },
  'GET /api/v1/assets': { status: 200, body: pageOf([LAPTOP]) },
  'GET /api/v1/assets/catalog': { status: 200, body: CATALOG },
  'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
  'GET /api/v1/reference/departments': { status: 200, body: [] },
};
const brandRoutes = {
  'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
  'GET /api/v1/admin/brands': { status: 200, body: [] },
};

type CloseWay = 'Vazgeç' | 'Kapat' | 'Escape';
const CLOSE_WAYS: readonly CloseWay[] = ['Vazgeç', 'Kapat', 'Escape'];

async function openDirtyAssetForm() {
  mockApi(assetRoutes as never);
  const view = renderRoute('/assets');
  await view.user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
  const dialog = await screen.findByRole('dialog');
  await chooseOption(view.user, 'Tür', 'Dizüstü', dialog);
  return { ...view, dialog };
}

async function closeBy(user: Awaited<ReturnType<typeof openDirtyAssetForm>>['user'], dialog: HTMLElement, way: CloseWay) {
  if (way === 'Escape') {
    await user.keyboard('{Escape}');
  } else {
    await user.click(within(dialog).getByRole('button', { name: way }));
  }
}

describe('discarding a changed form', () => {
  for (const way of CLOSE_WAYS) {
    it(`asks before ${way} closes a changed asset form, and keeps the form on cancel`, async () => {
      const { user, dialog } = await openDirtyAssetForm();
      await closeBy(user, dialog, way);
      const confirm = await screen.findByRole('dialog', { name: DISCARD_TITLE });
      await user.click(within(confirm).getByRole('button', { name: 'Vazgeç' }));
      await waitFor(() => expect(screen.queryByRole('dialog', { name: DISCARD_TITLE })).not.toBeInTheDocument());
      expect(screen.getByRole('dialog', { name: 'Yeni cihaz' })).toBeInTheDocument();
    });
  }

  it('closes the changed form once the discard is confirmed', async () => {
    const { user, dialog } = await openDirtyAssetForm();
    await closeBy(user, dialog, 'Vazgeç');
    const confirm = await screen.findByRole('dialog', { name: DISCARD_TITLE });
    await user.click(within(confirm).getByRole('button', { name: 'Değişiklikleri at' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('closes an unchanged form at once', async () => {
    mockApi(assetRoutes as never);
    const { user } = renderRoute('/assets');
    await user.click(await screen.findByRole('button', { name: 'Yeni cihaz' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Vazgeç' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('guards the shared form dialog too (brand form)', async () => {
    mockApi(brandRoutes as never);
    const { user } = renderRoute('/admin/brands');
    await user.click(await screen.findByRole('button', { name: 'Yeni marka' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/^Ad/), 'Asus');
    await user.keyboard('{Escape}');
    expect(await screen.findByRole('dialog', { name: DISCARD_TITLE })).toBeInTheDocument();
  });
});
