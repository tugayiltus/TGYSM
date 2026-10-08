import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { kbRoutes, LIST_ARTICLE, paged } from '../../test/kbData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const articleCalls = (api: ReturnType<typeof mockApi>) => api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === '/api/v1/kb/articles');

describe('KnowledgeListPage', () => {
  it.each(['1.5', 'Infinity', '1e21'])('asks for the first page when the URL page is %s', async (value) => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes() });
    renderRoute(`/kb?page=${value}`);

    await screen.findByRole('link', { name: LIST_ARTICLE.title });
    for (const call of articleCalls(api)) {
      expect(new URL(call.url).searchParams.get('page')).toBe('1');
    }
  });

  it('marks the list busy while the next result loads', async () => {
    let release = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      ...kbRoutes(),
      'GET /api/v1/kb/articles': (async (request: Request) => {
        if (new URL(request.url).searchParams.get('search')) {
          await gate;
        }
        return { status: 200, body: paged([LIST_ARTICLE]) };
      }) as never,
    });
    const { user } = renderRoute('/kb');

    const list = await screen.findByRole('list', { name: 'Makaleler' });
    expect(list).toHaveAttribute('aria-busy', 'false');
    await user.type(screen.getByLabelText('Makalelerde ara'), 'vpn');

    await waitFor(() => expect(screen.getByRole('list', { name: 'Makaleler' })).toHaveAttribute('aria-busy', 'true'));
    release();
    await waitFor(() => expect(screen.getByRole('list', { name: 'Makaleler' })).toHaveAttribute('aria-busy', 'false'));
  });

  it('shows articles to an employee without status filter or the new-article button', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes() });
    renderRoute('/kb');

    expect(await screen.findByRole('link', { name: 'VPN bağlantısı nasıl kurulur?' })).toHaveAttribute('href', expect.stringMatching(/^\/kb\//));
    expect(screen.getByLabelText('Kategori')).toBeInTheDocument();
    expect(screen.queryByLabelText('Durum')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Görünürlük')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Yeni makale' })).not.toBeInTheDocument();
  });

  it('gives IT staff the status and visibility filters and the new-article link', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...kbRoutes() });
    renderRoute('/kb');

    expect(await screen.findByLabelText('Durum')).toBeInTheDocument();
    expect(screen.getByLabelText('Görünürlük')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Yeni makale' })).toHaveAttribute('href', '/kb/new');
  });

  it('keeps the search text in the URL and sends it to the API', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes() });
    const { user, router } = renderRoute('/kb?q=vpn');

    const box = await screen.findByLabelText('Makalelerde ara');
    expect(box).toHaveValue('vpn');
    await user.type(box, 'x');

    expect(router.state.location.search).toContain('q=vpnx');
    await waitFor(() => expect(articleCalls(api).some((c) => new URL(c.url).searchParams.get('search') === 'vpnx')).toBe(true));
  });

  it('sends the chosen filters for IT staff', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...kbRoutes() });
    const { user } = renderRoute('/kb');

    await user.selectOptions(await screen.findByLabelText('Durum'), 'Taslak');

    await waitFor(() => expect(articleCalls(api).some((c) => new URL(c.url).searchParams.get('status') === 'draft')).toBe(true));
  });

  it('sends no status or visibility filter for an employee', async () => {
    const api = mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes() });
    renderRoute('/kb');

    await screen.findByRole('link', { name: 'VPN bağlantısı nasıl kurulur?' });
    for (const call of articleCalls(api)) {
      expect(new URL(call.url).searchParams.has('status')).toBe(false);
      expect(new URL(call.url).searchParams.has('visibility')).toBe(false);
    }
  });

  it('shows an empty state when nothing matches', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes([]) });
    renderRoute('/kb?q=yok');

    expect(await screen.findByText('Aramanızla eşleşen makale bulunamadı.')).toBeInTheDocument();
  });
});
