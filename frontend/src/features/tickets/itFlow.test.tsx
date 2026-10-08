import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { pageOf } from '../../test/assetData';
import { ADMIN_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT, LIST_ITEM } from '../../test/ticketData';

const WORKABLE = { ...INCIDENT, canWork: true };
const trail = () => within(screen.getByRole('navigation', { name: 'Sayfa yolu' }));

function itRoutes(extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    ...detailRoutes(WORKABLE),
    'GET /api/v1/reference/teams': { status: 200, body: [] },
    'GET /api/v1/tickets': { status: 200, body: pageOf([LIST_ITEM]) },
    ...extra,
  };
}

describe('ticket detail layout', () => {
  it('puts the work actions before the description and in a sticky side column', async () => {
    mockApi(itRoutes() as never);
    renderRoute(`/tickets/${INCIDENT.number}`);
    const actions = await screen.findByRole('region', { name: 'İşlemler' });
    const description = screen.getByText(INCIDENT.description);
    expect(actions.compareDocumentPosition(description) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(actions.closest('.tgysm-sticky-aside')).not.toBeNull();
  });

  it('names the ticket in the breadcrumb from the loaded data', async () => {
    mockApi(itRoutes() as never);
    renderRoute(`/tickets/${INCIDENT.number}`);
    const nav = await screen.findByRole('navigation', { name: 'Sayfa yolu' });
    expect(await within(nav).findByText(INCIDENT.number)).toBeInTheDocument();
  });

  it('shows no ticket number from the URL as title when the ticket is not found', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, 'GET /api/v1/tickets/INC-2026-999999': { status: 404, body: { code: 'common.not_found' } } });
    renderRoute('/tickets/INC-2026-999999');
    await screen.findByRole('alert');
    expect(screen.queryByText('INC-2026-999999')).not.toBeInTheDocument();
  });

  it('returns to the filtered queue it was opened from', async () => {
    mockApi(itRoutes() as never);
    const { user, router } = renderRoute('/workspace/queue?queue=team&priority=p1');
    await user.click((await screen.findAllByRole('link', { name: new RegExp(`Aç.*${LIST_ITEM.number}`) }))[0] as HTMLElement);
    await waitFor(() => expect(router.state.location.pathname).toBe(`/tickets/${INCIDENT.number}`));
    const queueCrumb = await trail().findByRole('link', { name: 'Kuyruk' });
    expect(queueCrumb).toHaveAttribute('href', '/workspace/queue?queue=team&priority=p1');
    expect(trail().getByRole('link', { name: 'İş listem' })).toHaveAttribute('href', '/workspace');
  });

  it('truncates a very long subject in the queue cards', async () => {
    mockApi(itRoutes({ 'GET /api/v1/tickets': { status: 200, body: pageOf([{ ...LIST_ITEM, subject: 'Yazıcı '.repeat(20) }]) } }) as never);
    renderRoute('/workspace/queue?queue=team');
    const subjects = await screen.findAllByText(/^(Yazıcı )+/);
    expect(subjects.some((element) => element.classList.contains('text-break'))).toBe(true);
  });
});

describe('approvals', () => {
  it('confirms an approver assignment with "Onaylayıcıyı ata", not "Onayla"', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/approvals/mine': {
        status: 200,
        body: [{ id: 'a-1', ticketNumber: 'REQ-2026-000001', subject: 'Yeni dizüstü', requesterName: 'Ayşe', requestTypeName: null, createdAt: '2026-10-01T08:00:00Z', approverId: null, version: 1 }],
      },
      'GET /api/v1/admin/users': { status: 200, body: pageOf([]) },
    });
    const { user } = renderRoute('/approvals');
    await user.click(await screen.findByRole('button', { name: /Onaylayıcı ata/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Onaylayıcıyı ata' })).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Onayla' })).not.toBeInTheDocument();
  });
});
