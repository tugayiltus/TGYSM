import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

const OTHER = { ...INCIDENT, id: 't-2', number: 'REQ-2026-000002', kind: 'request' as const, subject: 'Yeni cihaz', relatedTickets: [] };
const TEAMS = [
  { id: 'team-1', name: 'Servis Masası', members: [{ id: 'u-1', fullName: 'Can Demir' }] },
  { id: 'team-2', name: 'Saha Destek', members: [{ id: 'u-2', fullName: 'Deniz Yıldız' }] },
];

describe('review fixes', () => {
  it('does not carry a draft reply over to another ticket', async () => {
    const incident = { ...INCIDENT, relatedTickets: [{ number: OTHER.number, kind: 'request' as const, subject: OTHER.subject, status: 'open' as const }] };
    const request = { ...OTHER, relatedTickets: [{ number: INCIDENT.number, kind: 'incident' as const, subject: INCIDENT.subject, status: 'open' as const }] };
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...detailRoutes(incident), ...detailRoutes(request) } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    // Visit both tickets first so both are cached and switching does not show a loading state.
    await user.click(await screen.findByRole('link', { name: `${OTHER.number} · ${OTHER.subject}` }));
    await user.click(await screen.findByRole('link', { name: `${INCIDENT.number} · ${INCIDENT.subject}` }));
    await user.type(await screen.findByLabelText('Mesajınız'), 'Taslak');
    await user.click(screen.getByRole('link', { name: `${OTHER.number} · ${OTHER.subject}` }));

    expect(await screen.findByRole('heading', { name: OTHER.subject })).toBeInTheDocument();
    expect(screen.getByLabelText('Mesajınız')).toHaveValue('');
  });

  it('clears the chosen assignee when the target team changes', async () => {
    let posted: unknown;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes({ ...INCIDENT, canWork: true }),
      'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
      [`POST /api/v1/tickets/${INCIDENT.number}/transfer`]: async (request: Request) => {
        posted = await request.json();
        return { status: 200, body: INCIDENT };
      },
    } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'Aktar' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Hedef ekip'), await within(dialog).findByRole('option', { name: 'Servis Masası' }));
    await user.selectOptions(within(dialog).getByLabelText('Atanacak kişi'), 'Can Demir');
    await user.selectOptions(within(dialog).getByLabelText('Hedef ekip'), 'Saha Destek');
    await user.selectOptions(within(dialog).getByLabelText('Aktarma nedeni'), 'İş yükü');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toMatchObject({ teamId: 'team-2', assigneeId: null }));
  });

  it('sends the version the dialog was opened with, even if the ticket reloads meanwhile', async () => {
    let posted: unknown;
    let reads = 0;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes({ ...INCIDENT, canWork: true }),
      [`GET /api/v1/tickets/${INCIDENT.number}`]: () => {
        reads++;
        return { status: 200, body: { ...INCIDENT, canWork: true, version: reads > 1 ? 99 : 11 } };
      },
      [`POST /api/v1/tickets/${INCIDENT.number}/cancel`]: async (request: Request) => {
        posted = await request.json();
        return { status: 200, body: INCIDENT };
      },
    } as never);
    const { user, queryClient } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'İptal et' }));
    const dialog = await screen.findByRole('dialog');
    await queryClient.refetchQueries({ queryKey: ['tickets', 'detail', INCIDENT.number] });
    await user.type(within(dialog).getByLabelText('Neden'), 'Mükerrer');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 11, reason: 'Mükerrer' }));
  });

  it('opens the created ticket even when the file upload fails, so a retry cannot create a duplicate', async () => {
    let creates = 0;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/reference/locations': { status: 200, body: [] },
      'GET /api/v1/reference/categories': { status: 200, body: [] },
      'GET /api/v1/reference/request-types': { status: 200, body: [] },
      'POST /api/v1/tickets/incidents': () => {
        creates++;
        return { status: 201, body: INCIDENT };
      },
      [`POST /api/v1/tickets/${INCIDENT.number}/comments`]: { status: 400, body: { code: 'validation.failed', errors: { files: ['attachments.type_not_allowed'] } } },
      ...detailRoutes(),
    } as never);
    const { user, router } = renderRoute('/tickets/new/incident');

    await user.type(await screen.findByLabelText('Konu'), 'Yazıcı');
    await user.type(screen.getByLabelText('Açıklama'), 'Bozuk');
    await user.upload(screen.getByLabelText(/Dosya ekle/), new File(['x'], 'not.pdf', { type: 'application/pdf' }));
    await user.click(screen.getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/tickets/${INCIDENT.number}`));
    expect(await screen.findByText(/dosyalar yüklenemedi/)).toBeInTheDocument();
    expect(creates).toBe(1);
  });
});
