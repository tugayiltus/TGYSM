import { cleanup, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const CONFIRMATION = 'Bu e-posta adresi kayıtlıysa şifre belirleme bağlantısı gönderildi. Gelen kutunuzu kontrol edin.';

describe('ForgotPasswordPage', () => {
  it('is reachable from the login page', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } } });
    const { user, router } = renderRoute('/login');

    await user.click(await screen.findByRole('link', { name: 'Şifremi unuttum' }));

    expect(router.state.location.pathname).toBe('/forgot-password');
  });

  it('asks for an email before posting', async () => {
    const mock = mockApi({});
    const { user } = renderRoute('/forgot-password');

    await user.click(await screen.findByRole('button', { name: 'Bağlantı iste' }));

    expect(await screen.findByText('Bu alan zorunludur.')).toBeInTheDocument();
    expect(mock.calls.some((request) => request.url.includes('forgot-password'))).toBe(false);
  });

  it('shows the same confirmation for any address', async () => {
    const posted: unknown[] = [];
    mockApi({
      'POST /api/v1/auth/forgot-password': async (request) => {
        posted.push(await request.json());
        return { status: 202 };
      },
    });

    for (const email of ['known@example.com', 'nobody@example.com']) {
      const { user } = renderRoute('/forgot-password');
      await user.type(await screen.findByLabelText('E-posta'), email);
      await user.click(screen.getByRole('button', { name: 'Bağlantı iste' }));
      expect(await screen.findByText(CONFIRMATION)).toBeInTheDocument();
      cleanup();
    }

    expect(posted).toEqual([{ email: 'known@example.com' }, { email: 'nobody@example.com' }]);
  });

  it('shows the rate-limit message', async () => {
    mockApi({ 'POST /api/v1/auth/forgot-password': { status: 429, body: { code: 'auth.rate_limited' } } });
    const { user } = renderRoute('/forgot-password');

    await user.type(await screen.findByLabelText('E-posta'), 'a@example.com');
    await user.click(screen.getByRole('button', { name: 'Bağlantı iste' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Çok fazla deneme yapıldı.');
  });
});
