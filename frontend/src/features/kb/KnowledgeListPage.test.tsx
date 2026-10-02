import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { kbRoutes } from '../../test/kbData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const articleCalls = (api: ReturnType<typeof mockApi>) => api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === '/api/v1/kb/articles');

describe('KnowledgeListPage', () => {
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
