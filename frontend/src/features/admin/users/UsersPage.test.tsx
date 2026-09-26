import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, mockApi, type MockRoutes } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const USER = {
  id: 'u-1',
  firstName: 'İlker',
  lastName: 'Işık',
  email: 'ilker@example.com',
  role: 'employee',
  isActive: true,
  departmentId: null,
  departmentName: null,
  locationId: null,
  locationName: null,
  version: 7,
};

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

function setup(extra: MockRoutes = {}) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/admin/users': { status: 200, body: pageOf([USER]) },
    'GET /api/v1/admin/departments': { status: 200, body: pageOf([]) },
    'GET /api/v1/admin/locations': { status: 200, body: [] },
    ...extra,
  });
}

describe('UsersPage', () => {
  it('lists users', async () => {
    setup();
    renderRoute('/admin/users');

    expect((await screen.findAllByText('İlker Işık')).length).toBeGreaterThan(0);
  });

  it('sends the search term to the server', async () => {
    const mock = setup();
    const { user } = renderRoute('/admin/users');

    await user.type(await screen.findByLabelText('Kullanıcı ara'), 'ilker');

    await waitFor(() => expect(mock.calls.some((r) => new URL(r.url).searchParams.get('search') === 'ilker')).toBe(true));
  });

  it('creates a user and shows the temporary password once', async () => {
    let posted: unknown;
    setup({
      'POST /api/v1/admin/users': async (request) => {
        posted = await request.json();
        return { status: 201, body: { userId: 'u-2', temporaryPassword: 'Temp-pass-1234' } };
      },
    });
    const { user } = renderRoute('/admin/users');

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Ad'), 'Deniz');
    await user.type(within(dialog).getByLabelText('Soyad'), 'Aydın');
    await user.type(within(dialog).getByLabelText('E-posta'), 'deniz@example.com');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await screen.findByTestId('temporary-password')).toHaveTextContent('Temp-pass-1234');
    expect(posted).toEqual({ firstName: 'Deniz', lastName: 'Aydın', email: 'deniz@example.com', role: 'employee', departmentId: null, locationId: null });
  });

  it('shows a taken email under the email field', async () => {
    setup({ 'POST /api/v1/admin/users': { status: 409, body: { code: 'users.email_taken' } } });
    const { user } = renderRoute('/admin/users');

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Ad'), 'Ali');
    await user.type(within(dialog).getByLabelText('Soyad'), 'Veli');
    await user.type(within(dialog).getByLabelText('E-posta'), 'ALI@example.com');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Bu e-posta adresi başka bir hesapta kullanılıyor.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('E-posta')).toHaveAttribute('aria-invalid', 'true');
    expect(within(dialog).getByLabelText('E-posta')).toHaveFocus();
  });

  it('shows conflict message when the user was changed elsewhere and reloads the list', async () => {
    const mock = setup({ 'PUT /api/v1/admin/users/u-1': { status: 409, body: { code: 'concurrency.conflict' } } });
    const { user } = renderRoute('/admin/users');
    const listCalls = () => mock.calls.filter((r) => r.method === 'GET' && new URL(r.url).pathname === '/api/v1/admin/users').length;

    await user.click((await screen.findAllByRole('button', { name: /^Düzenle/ }))[0]!);
    const dialog = await screen.findByRole('dialog');
    const before = listCalls();
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('başka biri tarafından değiştirildi');
    await waitFor(() => expect(listCalls()).toBeGreaterThan(before));
  });

  it('keeps the create dialog open while saving so the temporary password is not lost', async () => {
    let release: () => void = () => undefined;
    const saved = new Promise<void>((resolve) => {
      release = resolve;
    });
    setup({
      'POST /api/v1/admin/users': async () => {
        await saved;
        return { status: 201, body: { userId: 'u-3', temporaryPassword: 'Temp-pass-5678' } };
      },
    });
    const { user } = renderRoute('/admin/users');

    await user.click(await screen.findByRole('button', { name: 'Yeni kullanıcı' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Ad'), 'Ece');
    await user.type(within(dialog).getByLabelText('Soyad'), 'Nur');
    await user.type(within(dialog).getByLabelText('E-posta'), 'ece@example.com');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));
    await user.keyboard('{Escape}');
    release();

    expect(await screen.findByTestId('temporary-password')).toHaveTextContent('Temp-pass-5678');
  });

  it('deactivates after confirmation and sends the row version', async () => {
    let posted: unknown;
    setup({
      'POST /api/v1/admin/users/u-1/deactivate': async (request) => {
        posted = await request.json();
        return { status: 200, body: { ...USER, isActive: false, mustChangePassword: false, createdAt: '2026-09-26T09:00:00Z', version: 8 } };
      },
    });
    const { user } = renderRoute('/admin/users');

    await user.click((await screen.findAllByRole('button', { name: /^Pasifleştir/ }))[0]!);
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('İlker Işık pasifleştirilsin mi?');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 7 }));
  });

  it('keeps employees out of the admin area', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    const { router } = renderRoute('/admin/users');

    expect(await screen.findByRole('heading', { name: 'Merhaba, Ayşe' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });

  it('names the user in row action buttons for screen readers', async () => {
    setup();
    renderRoute('/admin/users');

    expect((await screen.findAllByRole('button', { name: 'Düzenle İlker Işık' })).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: 'Pasifleştir İlker Işık' }).length).toBeGreaterThan(0);
  });

  it('goes back a page when the last user on it is deactivated under the active filter', async () => {
    const OTHER = { ...USER, id: 'u-9', firstName: 'Son', lastName: 'Kişi', email: 'son@example.com' };
    const mock = setup({
      'GET /api/v1/admin/users': (request) => {
        const page = Number(new URL(request.url).searchParams.get('page') ?? '1');
        return { status: 200, body: page === 2 ? { items: [OTHER], page: 2, pageSize: 20, totalCount: 21 } : { items: [USER], page: 1, pageSize: 20, totalCount: 21 } };
      },
      'POST /api/v1/admin/users/u-9/deactivate': { status: 200, body: { ...OTHER, isActive: false, mustChangePassword: false, createdAt: '2026-09-26T09:00:00Z', version: 8 } },
    });
    const { user } = renderRoute('/admin/users');

    await user.selectOptions(await screen.findByLabelText('Durum'), 'active');
    await user.click(await screen.findByRole('button', { name: 'Sonraki' }));
    await user.click((await screen.findAllByRole('button', { name: /^Pasifleştir Son Kişi/ }))[0]!);
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => {
      const last = mock.calls.filter((r) => r.method === 'GET' && new URL(r.url).pathname === '/api/v1/admin/users').at(-1);
      expect(new URL(last!.url).searchParams.get('page')).toBe('1');
    });
  });
});
