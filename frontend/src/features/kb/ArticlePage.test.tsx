import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ARTICLE, ARTICLE_ID, kbRoutes } from '../../test/kbData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const ITEM_ROUTE = `/api/v1/kb/articles/${ARTICLE_ID}`;
const REVISIONS = [
  { revision: 2, title: 'VPN bağlantısı nasıl kurulur?', visibility: 'employees', editedByName: 'Can Demir', editedAt: '2026-10-01T09:00:00Z' },
  { revision: 1, title: 'VPN', visibility: 'internal', editedByName: 'Deniz Yıldız', editedAt: '2026-09-30T09:00:00Z' },
];

function setup(me: unknown, article = ARTICLE, extra = {}) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: me },
    ...kbRoutes(),
    [`GET ${ITEM_ROUTE}`]: { status: 200, body: article },
    [`GET ${ITEM_ROUTE}/revisions`]: { status: 200, body: REVISIONS },
    ...extra,
  } as never);
}

describe('ArticlePage', () => {
  it('shows the article to an employee without any editing controls or revisions', async () => {
    const api = setup(EMPLOYEE_ME, { ...ARTICLE, status: 'published' });
    renderRoute(`/kb/${ARTICLE_ID}`);

    expect(await screen.findByRole('heading', { level: 1, name: ARTICLE.title })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Adımlar' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Düzenle' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Yayınla' })).not.toBeInTheDocument();
    expect(screen.queryByText('Sürüm geçmişi')).not.toBeInTheDocument();
    expect(api.calls.some((c) => c.url.endsWith('/revisions'))).toBe(false);
  });

  it('gives IT staff edit and publish for a draft, and the revision table', async () => {
    setup(IT_ME);
    renderRoute(`/kb/${ARTICLE_ID}`);

    expect(await screen.findByRole('link', { name: 'Düzenle' })).toHaveAttribute('href', `/kb/${ARTICLE_ID}/edit`);
    expect(screen.getByRole('button', { name: 'Yayınla' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Arşivle' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Taslağa geri al' })).not.toBeInTheDocument();
    expect((await screen.findAllByText('Deniz Yıldız')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Can Demir').length).toBeGreaterThan(0);
  });

  it('publishes with the loaded version', async () => {
    let body: unknown;
    const published = { ...ARTICLE, status: 'published', version: 4 };
    const api = setup(IT_ME, ARTICLE, {
      [`POST ${ITEM_ROUTE}/publish`]: async (request: Request) => {
        body = await request.json();
        api.set(`GET ${ITEM_ROUTE}`, { status: 200, body: published });
        return { status: 200, body: published };
      },
    });
    const { user } = renderRoute(`/kb/${ARTICLE_ID}`);

    await user.click(await screen.findByRole('button', { name: 'Yayınla' }));

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Yayınla' })).not.toBeInTheDocument());
    expect(body).toEqual({ version: ARTICLE.version });
  });

  it('offers restore, not edit, for an archived article', async () => {
    setup(IT_ME, { ...ARTICLE, status: 'archived' });
    renderRoute(`/kb/${ARTICLE_ID}`);

    expect(await screen.findByRole('button', { name: 'Taslağa geri al' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Düzenle' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Arşivle' })).not.toBeInTheDocument();
  });

  it('shows a transition conflict as Turkish text', async () => {
    setup(IT_ME, ARTICLE, { [`POST ${ITEM_ROUTE}/publish`]: { status: 409, body: { code: 'knowledge.invalid_transition' } } });
    const { user } = renderRoute(`/kb/${ARTICLE_ID}`);

    await user.click(await screen.findByRole('button', { name: 'Yayınla' }));

    expect(await screen.findByText('Makalenin mevcut durumunda bu işlem yapılamaz.')).toBeInTheDocument();
  });

  it('shows not-found text for an unknown article', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, [`GET ${ITEM_ROUTE}`]: { status: 404, body: { code: 'common.not_found' } } });
    renderRoute(`/kb/${ARTICLE_ID}`);

    expect(await screen.findByText('Kayıt bulunamadı.')).toBeInTheDocument();
  });
});
