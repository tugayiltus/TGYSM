import { fireEvent, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ARTICLE, ARTICLE_ID, kbRoutes } from '../../test/kbData';
import { EMPLOYEE_ME, IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

import { ARTICLE_LIMITS } from './api';

const EDIT_PATH = `/kb/${ARTICLE_ID}/edit`;
const ITEM_ROUTE = `/api/v1/kb/articles/${ARTICLE_ID}`;

const setup = (extra: Record<string, never> = {}) =>
  mockApi({ 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...kbRoutes(), [`GET ${ITEM_ROUTE}`]: { status: 200, body: ARTICLE }, ...extra } as never);

describe('ArticleEditorPage', () => {
  it('is not available to employees', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME }, ...kbRoutes() });
    const { router } = renderRoute('/kb/new');

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(screen.queryByLabelText('Başlık')).not.toBeInTheDocument();
  });

  it('refuses an empty title and body without calling the API', async () => {
    const api = setup();
    const { user } = renderRoute('/kb/new');

    await user.click(await screen.findByRole('button', { name: 'Kaydet' }));

    expect(await screen.findAllByText('Bu alan zorunludur.')).toHaveLength(2);
    expect(api.calls.some((c) => c.method === 'POST')).toBe(false);
  });

  it('refuses a title, summary and body over their limits', async () => {
    const api = setup();
    const { user } = renderRoute('/kb/new');

    fireEvent.change(await screen.findByLabelText('Başlık'), { target: { value: 'a'.repeat(ARTICLE_LIMITS.title + 1) } });
    fireEvent.change(screen.getByLabelText('Özet'), { target: { value: 'a'.repeat(ARTICLE_LIMITS.summary + 1) } });
    fireEvent.change(screen.getByLabelText('İçerik (Markdown)'), { target: { value: 'a'.repeat(ARTICLE_LIMITS.body + 1) } });
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));

    expect(await screen.findAllByText('Bu alan çok uzun.')).toHaveLength(3);
    expect(api.calls.some((c) => c.method === 'POST')).toBe(false);
  });

  it('creates an article and opens it', async () => {
    let posted: unknown;
    setup({
      'POST /api/v1/kb/articles': (async (request: Request) => {
        posted = await request.json();
        return { status: 201, body: ARTICLE };
      }) as never,
    });
    const { user, router } = renderRoute('/kb/new');

    await user.type(await screen.findByLabelText('Başlık'), 'Yeni makale');
    await user.type(screen.getByLabelText('Özet'), 'Kısa');
    await user.type(screen.getByLabelText('İçerik (Markdown)'), 'Metin');
    await user.selectOptions(screen.getByLabelText('Görünürlük'), 'Yalnızca BT personeli');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/kb/${ARTICLE_ID}`));
    expect(posted).toEqual({ title: 'Yeni makale', summary: 'Kısa', body: 'Metin', categoryId: null, visibility: 'internal' });
  });

  it('previews the body as sanitized Markdown', async () => {
    setup();
    const { user } = renderRoute('/kb/new');

    await user.type(await screen.findByLabelText('İçerik (Markdown)'), '## Başlık adımı');
    const write = screen.getByRole('button', { name: 'Yaz' });
    const preview = screen.getByRole('button', { name: 'Önizleme' });
    expect(write).toHaveAttribute('aria-pressed', 'true');
    expect(preview).toHaveAttribute('aria-pressed', 'false');
    expect(screen.queryByRole('tab')).toBeNull();

    await user.click(preview);

    expect(preview).toHaveAttribute('aria-pressed', 'true');
    expect(write).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('heading', { level: 3, name: 'Başlık adımı' })).toBeInTheDocument();
  });

  it('loads the article and saves with the version it was loaded at', async () => {
    let put: { version?: number; title?: string } = {};
    setup({
      [`PUT ${ITEM_ROUTE}`]: (async (request: Request) => {
        put = (await request.json()) as typeof put;
        return { status: 200, body: { ...ARTICLE, version: 4 } };
      }) as never,
    });
    const { user, router } = renderRoute(EDIT_PATH);

    const title = await screen.findByLabelText('Başlık');
    expect(title).toHaveValue(ARTICLE.title);
    await user.type(title, ' (güncel)');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/kb/${ARTICLE_ID}`));
    expect(put.version).toBe(ARTICLE.version);
    expect(put.title).toBe(`${ARTICLE.title} (güncel)`);
  });

  it('shows the conflict message and reloads the server version on request', async () => {
    const api = setup({ [`PUT ${ITEM_ROUTE}`]: { status: 409, body: { code: 'concurrency.conflict' } } as never });
    const { user } = renderRoute(EDIT_PATH);

    const title = await screen.findByLabelText('Başlık');
    await user.type(title, ' yerel');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));

    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
    api.set(`GET ${ITEM_ROUTE}`, { status: 200, body: { ...ARTICLE, title: 'Sunucudaki başlık', version: 9 } });
    await user.click(screen.getByRole('button', { name: 'Sunucudaki sürümü yükle' }));

    await waitFor(() => expect(screen.getByLabelText('Başlık')).toHaveValue('Sunucudaki başlık'));
    expect(screen.queryByRole('button', { name: 'Sunucudaki sürümü yükle' })).not.toBeInTheDocument();
  });

  it('shows the transition error for an archived article', async () => {
    setup({ [`PUT ${ITEM_ROUTE}`]: { status: 409, body: { code: 'knowledge.invalid_transition' } } as never });
    const { user } = renderRoute(EDIT_PATH);

    await user.click(await screen.findByRole('button', { name: 'Kaydet' }));

    expect(await screen.findByText('Makalenin mevcut durumunda bu işlem yapılamaz.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sunucudaki sürümü yükle' })).not.toBeInTheDocument();
  });
});
