import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { LIST_ITEM } from '../../test/ticketData';

const COUNTS: Record<string, number> = { assigned_to_me: 2, team: 5, unassigned: 3, on_hold: 1, sla_risk: 4 };

describe('WorkspacePage', () => {
  it('shows a card per queue with its count and opens the filtered queue', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'GET /api/v1/tickets': (request) => {
        const queue = new URL(request.url).searchParams.get('queue') ?? '';
        return { status: 200, body: { items: [LIST_ITEM], page: 1, pageSize: 20, totalCount: COUNTS[queue] ?? 0 } };
      },
    });
    const { user, router } = renderRoute('/workspace');

    expect(await screen.findByText('5 kayıt')).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: /Atanmamış/ }));

    await waitFor(() => expect(router.state.location.search).toBe('?queue=unassigned'));
    expect(await screen.findByRole('heading', { name: 'Atanmamış' })).toBeInTheDocument();
    await waitFor(() => expect(api.calls.some((c) => new URL(c.url).searchParams.get('pageSize') === '20' && new URL(c.url).searchParams.get('queue') === 'unassigned')).toBe(true));
  });

  it('opens the SLA risk queue from its card', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'GET /api/v1/tickets': (request) => {
        const queue = new URL(request.url).searchParams.get('queue') ?? '';
        return { status: 200, body: { items: [LIST_ITEM], page: 1, pageSize: 20, totalCount: COUNTS[queue] ?? 0 } };
      },
    });
    const { user, router } = renderRoute('/workspace');

    expect(await screen.findByText('4 kayıt')).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: /SLA riski/ }));

    await waitFor(() => expect(router.state.location.pathname + router.state.location.search).toBe('/workspace/queue?queue=sla_risk'));
    expect(await screen.findByRole('heading', { name: 'SLA riski' })).toBeInTheDocument();
  });
});
