import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const VALID_PASSWORD = 'Correct-pass-12';
const TOKEN = 'abc-token-123';

async function fill(user: ReturnType<typeof renderRoute>['user'], password: string, confirm = password) {
  await user.type(await screen.findByLabelText('Yeni şifre'), password);
  await user.type(screen.getByLabelText('Yeni şifre (tekrar)'), confirm);
  await user.click(screen.getByRole('button', { name: 'Şifreyi kaydet' }));
}

describe('SetPasswordPage', () => {
  it('removes the token from the address bar and posts it with the new password', async () => {
    let posted: unknown;
    mockApi({
      'POST /api/v1/auth/set-password': async (request) => {
        posted = await request.json();
        return { status: 204 };
      },
    });
    const { user, router } = renderRoute(`/set-password?token=${TOKEN}`);

    await waitFor(() => expect(router.state.location.search).toBe(''));
    await fill(user, VALID_PASSWORD);

    expect(await screen.findByText('Şifreniz belirlendi. Şimdi giriş yapabilirsiniz.')).toBeInTheDocument();
    expect(posted).toEqual({ token: TOKEN, newPassword: VALID_PASSWORD });
    await user.click(screen.getByRole('button', { name: 'Giriş sayfasına git' }));
    expect(router.state.location.pathname).toBe('/login');
  });

  it('shows the token_invalid text and a link to request a new one', async () => {
    mockApi({ 'POST /api/v1/auth/set-password': { status: 400, body: { code: 'auth.token_invalid' } } });
    const { user } = renderRoute(`/set-password?token=${TOKEN}`);

    await fill(user, VALID_PASSWORD);

    expect(await screen.findByRole('alert')).toHaveTextContent('Bağlantı geçersiz veya süresi dolmuş.');
    expect(screen.getByRole('link', { name: 'Yeni bağlantı iste' })).toHaveAttribute('href', '/forgot-password');
  });

  it('treats a missing token as an invalid link without calling the API', async () => {
    const mock = mockApi({});
    renderRoute('/set-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Bağlantı geçersiz veya süresi dolmuş.');
    expect(screen.getByRole('button', { name: 'Şifreyi kaydet' })).toBeDisabled();
    expect(mock.calls.some((request) => request.url.includes('set-password'))).toBe(false);
  });

  it('validates the password rules and the confirmation before posting', async () => {
    const mock = mockApi({});
    const { user } = renderRoute(`/set-password?token=${TOKEN}`);

    await fill(user, 'short1');
    expect(await screen.findByText('Şifre en az 12 karakter olmalı, harf ve rakam içermelidir.')).toBeInTheDocument();

    await user.clear(screen.getByLabelText('Yeni şifre'));
    await user.clear(screen.getByLabelText('Yeni şifre (tekrar)'));
    await fill(user, VALID_PASSWORD, 'Different-pass-12');
    expect(await screen.findByText('Şifreler eşleşmiyor.')).toBeInTheDocument();
    expect(mock.calls.some((request) => request.url.includes('set-password'))).toBe(false);
  });

  it('shows the rate-limit message', async () => {
    mockApi({ 'POST /api/v1/auth/set-password': { status: 429, body: { code: 'auth.rate_limited' } } });
    const { user } = renderRoute(`/set-password?token=${TOKEN}`);

    await fill(user, VALID_PASSWORD);

    expect(await screen.findByRole('alert')).toHaveTextContent('Çok fazla deneme yapıldı.');
  });
});
