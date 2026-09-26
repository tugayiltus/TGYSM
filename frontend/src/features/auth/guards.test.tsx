import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('route guards', () => {
  it('sends visitors without a session to the login page', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } });

    const { router } = renderRoute('/profile');

    expect(await screen.findByRole('heading', { name: 'Giriş yap' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });

  it('forces a pending password change before anything else', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: { ...EMPLOYEE_ME, mustChangePassword: true } } });

    const { router } = renderRoute('/');

    expect(await screen.findByText('İlk girişte şifrenizi değiştirmeniz gerekiyor.')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/change-password');
  });

  it('greets a signed-in user on the home page without an admin card for employees', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });

    renderRoute('/');

    expect(await screen.findByRole('heading', { name: 'Merhaba, Ayşe' })).toBeInTheDocument();
    expect(screen.queryByText('Yönetim paneli')).not.toBeInTheDocument();
  });
});
