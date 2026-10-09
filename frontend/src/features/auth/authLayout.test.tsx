import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const SIGNED_OUT = { 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } };
const trail = () => within(screen.getByRole('navigation', { name: 'Sayfa yolu' }));

describe('auth screens', () => {
  it.each(['/forgot-password', '/set-password?token=abc'])('%s is a card without app menu or breadcrumb, with a way back to login', async (path) => {
    mockApi(SIGNED_OUT);
    renderRoute(path);
    expect(await screen.findByRole('link', { name: 'Giriş sayfasına dön' })).toHaveAttribute('href', '/login');
    expect(screen.queryByRole('navigation', { name: 'Ana menü' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Sayfa yolu' })).not.toBeInTheDocument();
  });

  it('keeps a forced password change out of the app: no menu, no breadcrumb, no profile link, logout offered', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: { ...EMPLOYEE_ME, mustChangePassword: true } } });
    renderRoute('/change-password');
    expect(await screen.findByRole('heading', { level: 1, name: 'Şifre değiştir' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Ana menü' })).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Sayfa yolu' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Profilim' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Çıkış yap' })).toBeInTheDocument();
  });

  it('shows a voluntary password change inside the app under Profilim', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/change-password');
    expect(await screen.findByRole('heading', { level: 1, name: 'Şifre değiştir' })).toBeInTheDocument();
    expect(trail().getByRole('link', { name: 'Profilim' })).toHaveAttribute('href', '/profile');
    expect(trail().getByRole('link', { name: 'Ana sayfa' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('navigation', { name: 'Ana menü' })).toBeInTheDocument();
  });
});

describe('profile and error pages', () => {
  it('shows the profile under Ana sayfa', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/profile');
    expect(await screen.findByRole('heading', { level: 1, name: 'Profilim' })).toBeInTheDocument();
    expect(trail().getByRole('link', { name: 'Ana sayfa' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Şifre değiştir' })).toHaveAttribute('href', '/change-password');
  });

  it('shows the 403 page inside the app layout with a way home', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/reports/incidents');
    const main = await screen.findByRole('main');
    expect(await within(main).findByRole('heading', { level: 1, name: 'Bu sayfayı görme yetkiniz yok' })).toBeInTheDocument();
    expect(within(main).getByRole('link', { name: 'Ana sayfaya dön' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('navigation', { name: 'Ana menü' })).toBeInTheDocument();
  });

  it('shows the 404 page as a card with a way home', async () => {
    mockApi(SIGNED_OUT);
    renderRoute('/no-such-page');
    expect(await screen.findByRole('heading', { level: 1, name: 'Sayfa bulunamadı' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ana sayfaya dön' })).toHaveClass('btn');
  });
});

describe('sign-in look', () => {
  it('shows the sign-in fields with leading icons and the product brand', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } });
    renderRoute('/login');
    await screen.findByRole('heading', { level: 1 });
    expect(document.querySelectorAll('main .input-group .input-group-text svg')).toHaveLength(2);
    expect(document.querySelector('main .tgysm-auth-brand svg')).not.toBeNull();
  });
});
