import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const PENDING_ME = { ...EMPLOYEE_ME, mustChangePassword: true };

describe('ChangePasswordPage', () => {
  it('checks that the new passwords match before calling the server', async () => {
    const mock = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: PENDING_ME } });
    const { user } = renderRoute('/change-password');

    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Temp-pass-1234');
    await user.type(screen.getByLabelText('Yeni şifre'), 'Brand-new-pass-7');
    await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'Different-pass-7');
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));

    expect(await screen.findByText('Şifreler eşleşmiyor.')).toBeInTheDocument();
    expect(mock.calls.some((r) => r.url.endsWith('/change-password'))).toBe(false);
  });

  it('shows a wrong current password under that field', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: PENDING_ME },
      'POST /api/v1/auth/change-password': {
        status: 400,
        body: { code: 'validation.failed', errors: { currentPassword: ['auth.current_password_invalid'] } },
      },
    });
    const { user } = renderRoute('/change-password');

    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Wrong-pass-1234');
    await user.type(screen.getByLabelText('Yeni şifre'), 'Brand-new-pass-7');
    await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'Brand-new-pass-7');
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));

    expect(await screen.findByText('Mevcut şifre hatalı.')).toBeInTheDocument();
    expect(screen.getByLabelText('Mevcut şifre')).toHaveAttribute('aria-invalid', 'true');
  });

  it('goes home after a successful change', async () => {
    let changed = false;
    const mock = mockApi({ 'POST /api/v1/auth/change-password': () => { changed = true; return { status: 204 }; } });
    mock.set('GET /api/v1/auth/me', () => ({ status: 200, body: changed ? EMPLOYEE_ME : PENDING_ME }));
    const { user, router } = renderRoute('/change-password');

    await user.type(await screen.findByLabelText('Mevcut şifre'), 'Temp-pass-1234');
    await user.type(screen.getByLabelText('Yeni şifre'), 'Brand-new-pass-7');
    await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), 'Brand-new-pass-7');
    await user.click(screen.getByRole('button', { name: 'Şifreyi değiştir' }));

    expect(await screen.findByRole('heading', { name: 'Merhaba, Ayşe' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });
});
