import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const EVENT = {
  id: 'e-1',
  occurredAt: '2026-09-26T09:05:00Z',
  actorUserId: 'a-1',
  actorName: 'Tugay İltuş',
  action: 'users.role_changed',
  entityType: 'user',
  entityId: 'u-1',
  reason: null,
  oldValues: '{"role":"employee"}',
  newValues: '{"role":"it_staff"}',
  correlationId: 'corr-1',
};

describe('AuditPage', () => {
  it('shows Turkish labels, local time and change details, and filters by entity type', async () => {
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/audit-events': { status: 200, body: { items: [EVENT], page: 1, pageSize: 20, totalCount: 1 } },
    });
    const { user } = renderRoute('/admin/audit');

    expect((await screen.findAllByText('Rol değiştirildi')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Tugay İltuş').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/12:05/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/"it_staff"/).length).toBeGreaterThan(0);

    await user.selectOptions(screen.getByLabelText('Kayıt türü'), 'team');

    await waitFor(() => expect(mock.calls.some((r) => new URL(r.url).searchParams.get('entityType') === 'team')).toBe(true));
  });
});
