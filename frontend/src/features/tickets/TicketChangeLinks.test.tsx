import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { components } from '../../shared/api/schema';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

type LinkedChangeItem = components['schemas']['LinkedChangeItem'];

const URL = `/api/v1/tickets/${INCIDENT.number}/changes`;
const CHANGE_ROW: LinkedChangeItem = {
  number: 'CHG-2026-000001',
  title: 'Anahtar güncellemesi',
  status: 'scheduled',
  windowStart: '2026-12-01T22:00:00Z',
  windowEnd: '2026-12-02T01:00:00Z',
  ownerTeamName: 'Ağ Ekibi',
  linkKind: 'caused',
  linkedAt: '2026-11-13T08:00:00Z',
  canOpen: true,
};

describe('Ticket İlgili değişiklikler panel', () => {
  it('lists the linked changes for IT staff', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes(INCIDENT),
      [`GET ${URL}`]: { status: 200, body: [CHANGE_ROW, { ...CHANGE_ROW, number: 'CHG-2026-000002', title: 'Gizli', canOpen: false, ownerTeamName: 'Güvenlik' }] },
    } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    const region = await screen.findByRole('region', { name: 'İlgili değişiklikler' });
    expect(await within(region).findByRole('link', { name: /CHG-2026-000001/ })).toHaveAttribute('href', '/changes/CHG-2026-000001');
    expect(within(region).getAllByText(/Değişiklik sonucu oluştu/)).toHaveLength(2);
    expect(within(region).getAllByText('Planlandı')).toHaveLength(2);
    expect(within(region).queryByRole('link', { name: /CHG-2026-000002/ })).not.toBeInTheDocument();
    expect(within(region).getByText(/Güvenlik ekibinin kaydı/)).toBeInTheDocument();
  });

  it('shows an error with a retry when the list fails', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes(INCIDENT),
      [`GET ${URL}`]: { status: 500, body: { title: 'x', status: 500 } },
    } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    const region = await screen.findByRole('region', { name: 'İlgili değişiklikler' });
    expect(await within(region).findByRole('button', { name: 'Tekrar dene' })).toBeInTheDocument();
  });

  it('shows no panel to an employee and makes no request', async () => {
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      ...detailRoutes(INCIDENT),
      [`GET ${URL}`]: { status: 200, body: [CHANGE_ROW] },
    } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByRole('heading', { name: INCIDENT.subject })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'İlgili değişiklikler' })).not.toBeInTheDocument();
    expect(mock.calls.some((call) => call.url.endsWith('/changes'))).toBe(false);
  });
});
