import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { pageOf } from '../../test/assetData';
import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT, LIST_ITEM } from '../../test/ticketData';

describe('MyTicketsPage', () => {
  it('lists the employee tickets with Turkish status labels', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/tickets': { status: 200, body: pageOf([LIST_ITEM]) },
    });
    renderRoute('/tickets');

    expect(await screen.findByRole('heading', { name: 'Kayıtlarım' })).toBeInTheDocument();
    expect((await screen.findAllByText('INC-2026-000001')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Açık').length).toBeGreaterThan(0);
    expect(api.calls.some((c) => new URL(c.url).searchParams.get('queue') === 'mine')).toBe(true);
  });
});

describe('TicketDetailPage for employees', () => {
  it('shows the ticket, public timeline and replies without work actions or internal tab', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...detailRoutes() });
    renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByRole('heading', { name: /Yazıcı çalışmıyor/ })).toBeInTheDocument();
    expect(await screen.findByText('Kayıt oluşturuldu')).toBeInTheDocument();
    expect(await screen.findByText('Ekran görüntüsü ekte')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ekran.png' })).toHaveAttribute('href', `/api/v1/tickets/${INCIDENT.number}/attachments/f-1`);
    expect(screen.queryByRole('button', { name: 'Üstlen' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'İç not' })).not.toBeInTheDocument();
    const box = screen.getByRole('form', { name: 'Yazışmalar' });
    expect(within(box).getByRole('button', { name: 'Gönder' })).toBeInTheDocument();
  });
});
