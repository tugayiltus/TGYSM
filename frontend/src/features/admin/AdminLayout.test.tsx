import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('AdminLayout', () => {
  it('has a close button inside the navigation drawer', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/users': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
    });
    const { user } = renderRoute('/admin/users');

    await user.click(await screen.findByRole('button', { name: 'Menüyü aç' }));
    const drawer = await screen.findByRole('dialog', { name: 'Yönetim menüsü' });
    await user.click(within(drawer).getByRole('button', { name: 'Kapat' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Yönetim menüsü' })).not.toBeInTheDocument());
  });

  it('links to the reports outside the admin panel', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/users': { status: 200, body: { items: [], page: 1, pageSize: 20, totalCount: 0 } },
    });
    renderRoute('/admin/users');

    const nav = (await screen.findAllByRole('navigation', { name: 'Yönetim menüsü' }))[0] as HTMLElement;
    expect(within(nav).getByRole('link', { name: 'Raporlar' })).toHaveAttribute('href', '/reports');
  });
});
