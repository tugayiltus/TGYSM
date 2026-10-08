import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LOCATIONS, pageOf } from '../../test/assetData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT, LIST_ITEM } from '../../test/ticketData';

const listCalls = (api: ReturnType<typeof mockApi>) => api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === '/api/v1/tickets');
const trail = () => within(screen.getByRole('navigation', { name: 'Sayfa yolu' }));

describe('role-based home', () => {
  it('puts the two report actions first for an employee and lists the latest records', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': { status: 200, body: pageOf([LIST_ITEM]) } });
    renderRoute('/');
    const main = await screen.findByRole('main');
    const links = await within(main).findAllByRole('link');
    expect(links.slice(0, 2).map((l) => l.getAttribute('href'))).toEqual(['/tickets/new/incident', '/tickets/new/request']);
    expect(await within(main).findByText(LIST_ITEM.subject)).toBeInTheDocument();
    expect(listCalls(api).some((c) => new URL(c.url).searchParams.get('queue') === 'mine')).toBe(true);
    expect(within(main).queryByText('Bana atanan')).not.toBeInTheDocument();
  });

  it('shows IT staff their queue counts from the ticket list endpoint', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, 'GET /api/v1/tickets': { status: 200, body: { ...pageOf([]), totalCount: 3 } } });
    renderRoute('/');
    const main = await screen.findByRole('main');
    expect(await within(main).findByRole('link', { name: /Bana atanan/ })).toHaveAttribute('href', '/workspace/queue?queue=assigned_to_me');
    expect(within(main).getByRole('link', { name: /SLA Riski/ })).toBeInTheDocument();
    await waitFor(() => expect(within(main).getAllByText('3 kayıt').length).toBeGreaterThan(0));
    const queues = listCalls(api).map((c) => new URL(c.url).searchParams.get('queue'));
    expect(queues).toEqual(expect.arrayContaining(['assigned_to_me', 'team', 'sla_risk']));
  });
});

describe('my tickets', () => {
  it('keeps its filters in the URL and the ticket returns to that filtered list', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': { status: 200, body: pageOf([LIST_ITEM]) }, ...detailRoutes() });
    const { user, router } = renderRoute('/tickets');
    await user.selectOptions(await screen.findByLabelText('Durum'), 'open');
    await waitFor(() => expect(new URLSearchParams(router.state.location.search).get('status')).toBe('open'));
    await waitFor(() => expect(listCalls(api).some((c) => new URL(c.url).searchParams.get('status') === 'open')).toBe(true));

    await user.click((await screen.findAllByRole('link', { name: new RegExp(`Aç.*${LIST_ITEM.number}`) }))[0] as HTMLElement);
    await waitFor(() => expect(router.state.location.pathname).toBe(`/tickets/${INCIDENT.number}`));
    expect(await trail().findByRole('link', { name: 'Kayıtlarım' })).toHaveAttribute('href', '/tickets?status=open');
  });

  it('restores the filters from the URL', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, 'GET /api/v1/tickets': { status: 200, body: pageOf([LIST_ITEM]) } });
    renderRoute('/tickets?status=completed&q=yaz');
    expect(await screen.findByLabelText('Durum')).toHaveValue('completed');
    expect(screen.getByLabelText('Numara veya konu ara')).toHaveValue('yaz');
  });
});

describe('new ticket form', () => {
  it('asks before leaving a filled form', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/reference/locations': { status: 200, body: LOCATIONS },
      'GET /api/v1/reference/categories': { status: 200, body: [] },
      'GET /api/v1/reference/request-types': { status: 200, body: [] },
      'GET /api/v1/tickets': { status: 200, body: pageOf([]) },
    });
    const { user, router } = renderRoute('/tickets/new/incident');
    await user.type(await screen.findByLabelText('Konu'), 'Ekran');
    await user.click(trail().getByRole('link', { name: 'Kayıtlarım' }));
    expect(await screen.findByRole('dialog', { name: 'Kaydedilmemiş değişiklikler' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/tickets/new/incident');
  });
});
