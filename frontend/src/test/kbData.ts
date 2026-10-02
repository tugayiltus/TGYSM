import type { ArticleDetail, ArticleListItem } from '../features/kb/api';

export const ARTICLE_ID = '00000000-0000-7000-8000-0000000000a1';

export const ARTICLE: ArticleDetail = {
  id: ARTICLE_ID,
  title: 'VPN bağlantısı nasıl kurulur?',
  summary: 'Uzaktan çalışmak için VPN kurulumu.',
  body: '## Adımlar\n\nÖnce istemciyi kurun.',
  categoryId: null,
  categoryName: null,
  visibility: 'employees',
  status: 'draft',
  authorName: 'Can Demir',
  publishedAt: null,
  updatedAt: '2026-10-01T09:00:00Z',
  version: 3,
};

export const LIST_ARTICLE: ArticleListItem = {
  id: ARTICLE_ID,
  title: ARTICLE.title,
  summary: ARTICLE.summary,
  categoryId: null,
  categoryName: 'Ağ',
  visibility: 'employees',
  status: 'published',
  updatedAt: ARTICLE.updatedAt,
};

export const paged = (items: ArticleListItem[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length });

/** Default KB routes; `items` is what the list endpoint returns. */
export function kbRoutes(items: ArticleListItem[] = [LIST_ARTICLE]) {
  return {
    'GET /api/v1/kb/articles': { status: 200, body: paged(items) },
    'GET /api/v1/reference/categories': { status: 200, body: [{ id: 'c-1', path: 'Ağ', parentId: null }] },
  } as const;
}
