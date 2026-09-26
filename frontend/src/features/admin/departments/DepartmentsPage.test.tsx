import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });
const APPROVER = { id: 'a-1', firstName: 'Onay', lastName: 'Veren', email: 'onay@example.com', role: 'admin', isActive: true, departmentId: null, departmentName: null, locationId: null, locationName: null, version: 1 };

describe('DepartmentsPage', () => {
  it('creates a department with an admin approver', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/departments': { status: 200, body: pageOf([]) },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([APPROVER]) },
      'POST /api/v1/admin/departments': async (request) => {
        posted = await request.json();
        return { status: 201, body: { id: 'd-1', name: 'Finans', isActive: true, approverUserId: 'a-1', approverName: 'Onay Veren', version: 1 } };
      },
    });
    const { user } = renderRoute('/admin/departments');

    await user.click(await screen.findByRole('button', { name: 'Yeni departman' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Departman adı'), 'Finans');
    await user.selectOptions(within(dialog).getByLabelText('Onaylayıcı yönetici'), await within(dialog).findByRole('option', { name: 'Onay Veren' }));
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    await screen.findByRole('heading', { name: 'Departmanlar' });
    expect(posted).toEqual({ name: 'Finans', approverUserId: 'a-1' });
  });

  it('shows a duplicate name under the name field', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/departments': { status: 200, body: pageOf([]) },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([]) },
      'POST /api/v1/admin/departments': { status: 409, body: { code: 'departments.name_taken' } },
    });
    const { user } = renderRoute('/admin/departments');

    await user.click(await screen.findByRole('button', { name: 'Yeni departman' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Departman adı'), 'insan kaynakları');
    await user.click(within(dialog).getByRole('button', { name: 'Oluştur' }));

    expect(await within(dialog).findByText('Bu adla bir departman zaten var.')).toBeInTheDocument();
  });
});
