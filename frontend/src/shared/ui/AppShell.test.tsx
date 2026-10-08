import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('AppShell', () => {
  it('shows the inventory to IT staff but not the admin panel', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME } });
    renderRoute('/profile');

    const nav = await screen.findByRole('navigation', { name: 'Ana menü' });
    expect(within(nav).getByRole('link', { name: 'Envanter' })).toHaveAttribute('href', '/assets');
    expect(within(nav).getByRole('link', { name: 'Zimmetlerim' })).toHaveAttribute('href', '/assets/mine');
    expect(within(nav).queryByRole('link', { name: 'Yönetim paneli' })).not.toBeInTheDocument();
  });

  it('opens the same links in a drawer on small screens', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { user } = renderRoute('/profile');

    await user.click(await screen.findByRole('button', { name: 'Menüyü aç' }));
    const drawer = await screen.findByRole('dialog');

    expect(within(drawer).getByRole('link', { name: 'Zimmetlerim' })).toBeInTheDocument();
    expect(within(drawer).queryByRole('link', { name: 'Envanter' })).not.toBeInTheDocument();
    await user.click(within(drawer).getByRole('link', { name: 'Zimmetlerim' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it.each([
    { me: EMPLOYEE_ME, seesReports: false },
    { me: IT_ME, seesReports: true },
    { me: ADMIN_ME, seesReports: true },
  ])('shows the knowledge base to $me.role and reports only to IT staff and admins', async ({ me, seesReports }) => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: me } });
    renderRoute('/profile');

    const nav = await screen.findByRole('navigation', { name: 'Ana menü' });
    expect(within(nav).getByRole('link', { name: 'Bilgi Bankası' })).toHaveAttribute('href', '/kb');
    if (seesReports) {
      expect(within(nav).getByRole('link', { name: 'Raporlar' })).toHaveAttribute('href', '/reports');
    } else {
      expect(within(nav).queryByRole('link', { name: 'Raporlar' })).not.toBeInTheDocument();
    }
  });
});
