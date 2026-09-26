import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const pageOf = (items: unknown[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });
const TEAM = { id: 't-1', name: 'Saha Destek', isActive: true, memberCount: 1, version: 3 };
const MEMBER = { userId: 'm-1', firstName: 'Mert', lastName: 'Can', email: 'mert@example.com', role: 'it_staff', isActive: true };
const CANDIDATE = { id: 'm-2', firstName: 'Elif', lastName: 'Su', email: 'elif@example.com', role: 'it_staff', isActive: true, departmentId: null, departmentName: null, locationId: null, locationName: null, version: 1 };
const EMPLOYEE = { ...CANDIDATE, id: 'e-1', firstName: 'Çalışan', lastName: 'Kişi', email: 'calisan@example.com', role: 'employee' };

describe('TeamsPage', () => {
  it('lists members, offers only IT staff and admins, and shows server errors', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/teams': { status: 200, body: pageOf([TEAM]) },
      'GET /api/v1/admin/teams/t-1': { status: 200, body: { id: 't-1', name: 'Saha Destek', isActive: true, version: 3, members: [MEMBER] } },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([CANDIDATE, EMPLOYEE]) },
      'POST /api/v1/admin/teams/t-1/members': { status: 409, body: { code: 'teams.member_inactive' } },
    });
    const { user } = renderRoute('/admin/teams');

    await user.click((await screen.findAllByRole('button', { name: 'Üyeleri yönet' }))[0]!);
    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText('Mert Can')).toBeInTheDocument();
    const select = within(dialog).getByLabelText('Eklenecek kişi');
    expect(within(select).queryByRole('option', { name: /Çalışan Kişi/ })).not.toBeInTheDocument();

    await user.selectOptions(select, await within(select).findByRole('option', { name: /Elif Su/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Üye ekle' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Pasif kullanıcı ekibe eklenemez.');
  });
});
