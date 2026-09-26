import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('LoginPage', () => {
  it('shows Turkish field errors when submitted empty', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } });
    const { user } = renderRoute('/login');

    await user.click(await screen.findByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findAllByText('Bu alan zorunludur.')).toHaveLength(2);
  });

  it('shows the uniform error for wrong credentials', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } },
      'POST /api/v1/auth/login': { status: 401, body: { code: 'auth.invalid_credentials' } },
    });
    const { user } = renderRoute('/login');

    await user.type(await screen.findByLabelText('E-posta'), 'ayse@example.com');
    await user.type(screen.getByLabelText('Şifre'), 'wrong-password-1');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya şifre hatalı.');
  });

  it('signs in and goes to the page the user asked for', async () => {
    let signedIn = false;
    const mock = mockApi({ 'POST /api/v1/auth/login': () => { signedIn = true; return { status: 200, body: { mustChangePassword: false } }; } });
    mock.set('GET /api/v1/auth/me', () => (signedIn ? { status: 200, body: EMPLOYEE_ME } : { status: 401, body: { code: 'auth.unauthenticated' } }));
    const { user, router } = renderRoute('/profile');

    await user.type(await screen.findByLabelText('E-posta'), 'ayse@example.com');
    await user.type(screen.getByLabelText('Şifre'), 'Correct-pass-1');
    await user.click(screen.getByRole('button', { name: 'Giriş yap' }));

    expect(await screen.findByRole('heading', { name: 'Profilim' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/profile');
  });
});
