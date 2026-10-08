import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ARTICLE, ARTICLE_ID, kbRoutes } from '../../test/kbData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const articleRoutes = {
  [`GET /api/v1/kb/articles/${ARTICLE_ID}`]: { status: 200, body: ARTICLE },
  [`GET /api/v1/kb/articles/${ARTICLE_ID}/revisions`]: { status: 200, body: [] },
};

describe('knowledge base list', () => {
  it('tells a first-time visitor that there are no articles yet', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes([]) });
    renderRoute('/kb');
    expect(await screen.findByText('Henüz yayınlanmış makale yok.')).toBeInTheDocument();
    expect(screen.queryByText('Aramanızla eşleşen makale bulunamadı.')).not.toBeInTheDocument();
  });

  it('says that the search found nothing when a search is set', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes([]) });
    renderRoute('/kb?q=yazıcı');
    expect(await screen.findByText('Aramanızla eşleşen makale bulunamadı.')).toBeInTheDocument();
  });
});

describe('article pages', () => {
  it('builds the edit trail with the article title linking to the article', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...kbRoutes(), ...articleRoutes });
    renderRoute(`/kb/${ARTICLE_ID}/edit`);
    const nav = await screen.findByRole('navigation', { name: 'Sayfa yolu' });
    expect(await within(nav).findByRole('link', { name: ARTICLE.title })).toHaveAttribute('href', `/kb/${ARTICLE_ID}`);
    expect(within(nav).getByRole('link', { name: 'Bilgi bankası' })).toHaveAttribute('href', '/kb');
    expect(within(nav).getByText('Düzenle')).toBeInTheDocument();
  });

  it('asks before leaving an edited article', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...kbRoutes(), ...articleRoutes });
    const { user, router } = renderRoute(`/kb/${ARTICLE_ID}/edit`);
    const title = await screen.findByLabelText('Başlık');
    await user.type(title, ' (güncel)');
    const nav = screen.getByRole('navigation', { name: 'Sayfa yolu' });
    await user.click(within(nav).getByRole('link', { name: 'Bilgi bankası' }));
    expect(await screen.findByRole('dialog', { name: 'Kaydedilmemiş değişiklikler' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`/kb/${ARTICLE_ID}/edit`);
  });
});
