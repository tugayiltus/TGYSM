import { screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const PAGE = { items: [], totalCount: 0, page: 1, pageSize: 100 };
const ADMIN_DATA = {
  'GET /api/v1/admin/departments': { status: 200, body: { ...PAGE, items: [{ id: 'd1', name: 'Muhasebe', approverUserId: null, approverName: null, isActive: true, version: 1 }], totalCount: 1 } },
  'GET /api/v1/admin/teams': { status: 200, body: { ...PAGE, items: [{ id: 't1', name: 'Ağ', isDefaultIntake: false, isActive: true, memberCount: 0, version: 1 }], totalCount: 1 } },
  'GET /api/v1/admin/system/workers': { status: 200, body: { status: 'degraded', jobs: [] } },
};

const mainNav = () => screen.findByRole('navigation', { name: 'Ana menü' });
const linkNames = (nav: HTMLElement) => within(nav).getAllByRole('link').map((link) => link.textContent);

describe('AppLayout sidebar', () => {
  it('shows an employee only the personal group', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/profile');
    const nav = await mainNav();
    expect(linkNames(nav)).toEqual(['Ana sayfa', 'Kayıtlarım', 'Zimmetlerim', 'Bilgi bankası']);
    expect(within(nav).queryByText('IT operasyonu')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Yönetim' })).not.toBeInTheDocument();
  });

  it('shows IT staff the IT and analysis groups without approvals or admin', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME } });
    renderRoute('/profile');
    const nav = await mainNav();
    expect(linkNames(nav)).toEqual(['Ana sayfa', 'Kayıtlarım', 'Zimmetlerim', 'Bilgi bankası', 'İş listem', 'Kuyruk', 'Envanter', 'Raporlar', 'Dışa aktarımlarım']);
    expect(screen.queryByRole('link', { name: 'Yönetim' })).not.toBeInTheDocument();
  });

  it('shows admins approvals and the Yönetim link', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME } });
    renderRoute('/profile');
    const nav = await mainNav();
    expect(within(nav).getByRole('link', { name: 'Onay yönetimi' })).toHaveAttribute('href', '/approvals');
    expect(screen.getByRole('link', { name: 'Yönetim' })).toHaveAttribute('href', '/admin');
  });

  it('marks the menu item of a deep page as current', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': { status: 200, body: { items: [], totalCount: 0, page: 1, pageSize: 20 } } });
    renderRoute('/tickets/new/incident');
    const nav = await mainNav();
    expect(within(nav).getByRole('link', { name: 'Kayıtlarım' })).toHaveAttribute('aria-current', 'true');
    expect(within(nav).getByRole('link', { name: 'Ana sayfa' })).not.toHaveAttribute('aria-current');
  });

  it('opens the menu in a panel on phones and closes it on navigation, moving focus to the new page title', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { user, router } = renderRoute('/profile');
    const hamburger = await screen.findByRole('button', { name: 'Menüyü aç' });
    await user.click(hamburger);
    const panel = await screen.findByRole('dialog', { name: 'Ana menü' });
    await user.click(within(panel).getByRole('link', { name: 'Zimmetlerim' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Ana menü' })).not.toBeInTheDocument());
    // The target page is lazy: the location changes once its module has loaded.
    await waitFor(() => expect(router.state.location.pathname).toBe('/assets/mine'));
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'Zimmetlerim' })).toHaveFocus());
  });

  it('gives focus back to the menu button when the panel closes without navigating', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { user } = renderRoute('/profile');
    const hamburger = await screen.findByRole('button', { name: 'Menüyü aç' });
    await user.click(hamburger);
    const panel = await screen.findByRole('dialog', { name: 'Ana menü' });
    await user.click(within(panel).getByRole('button', { name: 'Kapat' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Ana menü' })).not.toBeInTheDocument());
    await waitFor(() => expect(hamburger).toHaveFocus());
  });
});

describe('sidebar width', () => {
  afterEach(() => vi.restoreAllMocks());

  it('remembers the narrow sidebar across reloads', async () => {
    window.localStorage.clear();
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { user } = renderRoute('/profile');
    await user.click(await screen.findByRole('button', { name: 'Menüyü daralt' }));
    expect(window.localStorage.getItem('tgysm.sidebar.narrow')).toBe('true');
    expect(screen.getByRole('button', { name: 'Menüyü genişlet' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('still renders when storage is unavailable', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/profile');
    expect(await screen.findByRole('button', { name: 'Menüyü daralt' })).toBeInTheDocument();
  });
});

describe('account menu', () => {
  it('holds profile, password change and logout; logout goes to login', async () => {
    const mock = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'POST /api/v1/auth/logout': { status: 204, body: null } });
    const { user, router } = renderRoute('/profile');
    await user.click(await screen.findByRole('button', { name: /Hesap menüsü/ }));
    const menu = screen.getByRole('menu');
    expect(within(menu).getByRole('menuitem', { name: 'Profilim' })).toHaveAttribute('href', '/profile');
    expect(within(menu).getByRole('menuitem', { name: 'Şifre değiştir' })).toHaveAttribute('href', '/change-password');
    mock.set('GET /api/v1/auth/me', () => ({ status: 401, body: { code: 'auth.unauthenticated' } }));
    await user.click(within(menu).getByRole('menuitem', { name: 'Çıkış yap' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });

  it('truncates a very long name in the header', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: { ...EMPLOYEE_ME, firstName: 'Ayşe'.repeat(30) } } });
    renderRoute('/profile');
    const header = await screen.findByRole('banner');
    const name = within(header).getByText(new RegExp(`^${'Ayşe'.repeat(30)}`));
    expect(name).toHaveClass('text-truncate');
  });
});

describe('AdminLayout', () => {
  it('shows the admin groups, Uygulamaya dön and the overview with setup gaps', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, ...ADMIN_DATA });
    renderRoute('/admin');
    const nav = await screen.findByRole('navigation', { name: 'Yönetim menüsü' });
    for (const group of ['Organizasyon', 'Hizmet kataloğu', 'SLA', 'Bildirimler', 'Sistem ve denetim']) {
      expect(within(nav).getByText(group)).toBeInTheDocument();
    }
    expect(screen.getByRole('link', { name: 'Uygulamaya dön' })).toHaveAttribute('href', '/');
    expect(await screen.findByRole('heading', { level: 1, name: 'Genel bakış' })).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Muhasebe' })).toHaveAttribute('href', '/admin/departments');
    expect(screen.getByText('Varsayılan karşılama ekibi seçilmemiş.')).toBeInTheDocument();
    expect(screen.getByText('Arka plan işlerinden en az biri sorunlu.')).toBeInTheDocument();
  });

  it('marks the admin page of a deep route as current', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/sla/policy': { status: 404, body: { code: 'common.not_found' } } });
    renderRoute('/admin/sla/policy');
    const nav = await screen.findByRole('navigation', { name: 'Yönetim menüsü' });
    expect(within(nav).getByRole('link', { name: 'SLA politikası' })).toHaveAttribute('aria-current', 'page');
  });

  it.each([
    ['employee', EMPLOYEE_ME],
    ['IT staff', IT_ME],
  ])('keeps %s out of admin pages without rendering the admin menu', async (_who, me) => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: me } });
    const { router } = renderRoute('/admin/users');
    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(screen.queryByRole('navigation', { name: 'Yönetim menüsü' })).not.toBeInTheDocument();
  });
});
