import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { INCIDENT } from '../../test/ticketData';

const ITEM = { id: 'ap-1', version: 4, ticketNumber: 'REQ-2026-000001', subject: 'Yeni dizüstü', requesterName: 'Ayşe Kaya', requestTypeName: 'Yeni cihaz', approverId: ADMIN_ME.id, createdAt: '2026-09-26T08:00:00Z' };
const UNASSIGNED = { ...ITEM, id: 'ap-2', ticketNumber: 'REQ-2026-000002', approverId: null };
const USERS = { items: [{ id: 'adm-2', firstName: 'Onay', lastName: 'Veren', email: 'o@example.com', role: 'admin', isActive: true, departmentId: null, departmentName: null, locationId: null, locationName: null, version: 1 }], page: 1, pageSize: 100, totalCount: 1 };

describe('ApprovalsPage', () => {
  it('approves with the approval version and rejects only with a reason', async () => {
    const posted: Record<string, unknown> = {};
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/approvals/mine': { status: 200, body: [ITEM] },
      'POST /api/v1/approvals/ap-1/approve': async (request) => {
        posted.approve = await request.json();
        return { status: 200, body: INCIDENT };
      },
      'POST /api/v1/approvals/ap-1/reject': async (request) => {
        posted.reject = await request.json();
        return { status: 200, body: INCIDENT };
      },
    });
    const { user } = renderRoute('/approvals');

    await user.click(await screen.findByRole('button', { name: 'Onayla REQ-2026-000001' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(posted.approve).toEqual({ version: 4, reason: null }));

    await user.click(screen.getByRole('button', { name: 'Reddet REQ-2026-000001' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Onayla' })).toBeDisabled();
    await user.type(within(dialog).getByLabelText('Neden'), 'Bütçe yok');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(posted.reject).toEqual({ version: 4, reason: 'Bütçe yok' }));
  });

  it('offers reassignment for approvals without an approver', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/approvals/mine': { status: 200, body: [UNASSIGNED] },
      'GET /api/v1/admin/users': { status: 200, body: USERS },
      'POST /api/v1/tickets/REQ-2026-000002/approval/reassign': async (request) => {
        posted = await request.json();
        return { status: 200, body: INCIDENT };
      },
    });
    const { user } = renderRoute('/approvals');

    await user.click(await screen.findByRole('button', { name: 'Onaylayıcı ata REQ-2026-000002' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Onaylayıcı'), await within(dialog).findByRole('option', { name: 'Onay Veren' }));
    await user.type(within(dialog).getByLabelText('Neden'), 'Departman onaylayıcısı yok');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ approverId: 'adm-2', reason: 'Departman onaylayıcısı yok' }));
  });
});
