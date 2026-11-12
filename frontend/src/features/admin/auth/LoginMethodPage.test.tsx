import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const SETTINGS = {
  method: 'local',
  entra: { tenantId: '6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f', clientId: 'istemci-1', hasClientSecret: true, allowedDomains: ['sirket.com'], isTested: false },
  ldap: { host: 'dc1.sirket.local', port: 636, security: 'ldaps', baseDn: 'DC=sirket,DC=local', bindDn: 'CN=svc,DC=sirket,DC=local', hasBindPassword: true, userFilter: null, isTested: true },
  redirectUri: 'https://itsm.sirket.com/api/v1/auth/oidc/callback',
  version: 7,
};

const routes = (extra: Record<string, unknown> = {}) => ({
  'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
  'GET /api/v1/admin/auth-settings': { status: 200, body: SETTINGS },
  ...extra,
});

describe('LoginMethodPage', () => {
  it('never shows a stored secret and saves without one when nothing is typed', async () => {
    let saved: Record<string, unknown> = {};
    mockApi(routes({
      'PUT /api/v1/admin/auth-settings/entra': async (request: Request) => {
        saved = (await request.json()) as Record<string, unknown>;
        return { status: 200, body: SETTINGS };
      },
    }) as never);
    const { user } = renderRoute('/admin/login-method');
    const entra = await screen.findByRole('region', { name: 'Microsoft (Entra ID)' });
    expect(within(entra).getByLabelText(/^İstemci sırrı/)).toHaveValue('');
    expect(within(entra).getByText('Kayıtlı. Değiştirmek için yeni değeri yazın.')).toBeInTheDocument();
    expect(within(entra).getByDisplayValue(SETTINGS.redirectUri)).toBeInTheDocument();

    await user.clear(within(entra).getByLabelText(/^Uygulama \(Client\) ID/));
    await user.type(within(entra).getByLabelText(/^Uygulama \(Client\) ID/), 'istemci-2');
    await user.click(within(entra).getByRole('button', { name: 'Kaydet' }));

    await waitFor(() => expect(saved.clientId).toBe('istemci-2'));
    expect(saved.clientSecret ?? null).toBeNull();
    expect(saved.version).toBe(7);
  });

  it('shows the connection test result in Turkish', async () => {
    mockApi(routes({ 'POST /api/v1/admin/auth-settings/test': { status: 200, body: { ok: false, errorCode: 'auth.ldap_bind_failed' } } }) as never);
    const { user } = renderRoute('/admin/login-method');
    const ldap = await screen.findByRole('region', { name: 'Active Directory (LDAP)' });
    await user.click(within(ldap).getByRole('button', { name: 'Bağlantıyı test et' }));
    expect(await within(ldap).findByText("Servis hesabı ile bağlanılamadı. Bağlanma DN'ini ve şifresini kontrol edin.")).toBeInTheDocument();
  });

  it('switches a method on only after its settings passed a test', async () => {
    let switched: unknown;
    mockApi(routes({
      'POST /api/v1/admin/auth-settings/method': async (request: Request) => {
        switched = await request.json();
        return { status: 200, body: { ...SETTINGS, method: 'ldap' } };
      },
    }) as never);
    const { user } = renderRoute('/admin/login-method');
    const entra = await screen.findByRole('region', { name: 'Microsoft (Entra ID)' });
    expect(within(entra).getByRole('button', { name: 'Bu yöntemi aç' })).toHaveAttribute('aria-disabled', 'true');

    const ldap = screen.getByRole('region', { name: 'Active Directory (LDAP)' });
    await user.click(within(ldap).getByRole('button', { name: 'Bu yöntemi aç' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(switched).toEqual({ method: 'ldap', version: 7 }));
  });

  it('names how many people get a set-password email before going back to Yerel', async () => {
    mockApi(routes({
      'GET /api/v1/admin/auth-settings': { status: 200, body: { ...SETTINGS, method: 'ldap' } },
      'GET /api/v1/admin/auth-settings/switch-preview': { status: 200, body: { usersWithoutPassword: 12 } },
    }) as never);
    const { user } = renderRoute('/admin/login-method');
    await user.click(await screen.findByRole('button', { name: 'Yerel girişe dön' }));
    expect(await within(await screen.findByRole('dialog')).findByText(/12 kişiye şifre belirleme e-postası gider/)).toBeInTheDocument();
  });

  it('is an admin page in the system group of the admin menu', async () => {
    mockApi(routes() as never);
    renderRoute('/admin/login-method');
    expect(await screen.findByRole('link', { name: 'Giriş yöntemi' })).toHaveAttribute('href', '/admin/login-method');
  });

  it('is closed to employees', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } } as never);
    const { router } = renderRoute('/admin/login-method');
    await waitFor(() => expect(router.state.location.pathname).not.toBe('/admin/login-method'));
  });
});
