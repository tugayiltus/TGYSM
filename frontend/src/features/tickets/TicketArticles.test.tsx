import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ARTICLE_ID, kbRoutes, LIST_ARTICLE } from '../../test/kbData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

const ARTICLES_ROUTE = `/api/v1/tickets/${INCIDENT.number}/articles`;
const LINKED = { articleId: ARTICLE_ID, title: 'VPN bağlantısı nasıl kurulur?', visibility: 'employees', status: 'published' };
const INTERNAL = { articleId: 'a-2', title: 'Sunucu parolaları', visibility: 'internal', status: 'published' };

const listCalls = (api: ReturnType<typeof mockApi>) => api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === '/api/v1/kb/articles');

describe('TicketArticles', () => {
  it('lists linked articles for the requester without link controls', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      ...detailRoutes(INCIDENT),
      [`GET ${ARTICLES_ROUTE}`]: { status: 200, body: [LINKED] },
    } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByRole('link', { name: LINKED.title })).toHaveAttribute('href', `/kb/${ARTICLE_ID}`);
    expect(screen.queryByRole('button', { name: 'Makale bağla' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Bağlantıyı kaldır/ })).not.toBeInTheDocument();
  });

  it('says so when no article is linked', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...detailRoutes(INCIDENT), [`GET ${ARTICLES_ROUTE}`]: { status: 200, body: [] } } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByText('Bu kayda bağlı makale yok.')).toBeInTheDocument();
  });

  it('lets IT staff link a published article and marks internal ones', async () => {
    let posted: unknown;
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes({ ...INCIDENT, canWork: true }),
      ...kbRoutes([LIST_ARTICLE]),
      [`GET ${ARTICLES_ROUTE}`]: { status: 200, body: [INTERNAL] },
      [`POST ${ARTICLES_ROUTE}`]: async (request: Request) => {
        posted = await request.json();
        return { status: 200, body: [INTERNAL, LINKED] };
      },
    } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByText('Çalışana gösterilmez')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Makale bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(await within(dialog).findByLabelText('Makale'), LIST_ARTICLE.title);
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    expect(await screen.findByRole('link', { name: LINKED.title })).toBeInTheDocument();
    expect(posted).toEqual({ articleId: ARTICLE_ID });
    expect(listCalls(api).every((c) => new URL(c.url).searchParams.get('status') === 'published')).toBe(true);
  });

  it('shows the already-linked conflict inside the dialog', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes({ ...INCIDENT, canWork: true }),
      ...kbRoutes([LIST_ARTICLE]),
      [`GET ${ARTICLES_ROUTE}`]: { status: 200, body: [] },
      [`POST ${ARTICLES_ROUTE}`]: { status: 409, body: { code: 'knowledge.article_already_linked' } },
    } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'Makale bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(await within(dialog).findByLabelText('Makale'), LIST_ARTICLE.title);
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    expect(await within(dialog).findByText('Bu makale kayda zaten bağlı.')).toBeInTheDocument();
  });

  it('unlinks an article for IT staff', async () => {
    let deleted = false;
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      ...detailRoutes({ ...INCIDENT, canWork: true }),
      [`GET ${ARTICLES_ROUTE}`]: { status: 200, body: [LINKED] },
      [`DELETE ${ARTICLES_ROUTE}/${ARTICLE_ID}`]: () => {
        deleted = true;
        return { status: 200, body: [] };
      },
    } as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: `Bağlantıyı kaldır ${LINKED.title}` }));

    await waitFor(() => expect(deleted).toBe(true));
  });
});
