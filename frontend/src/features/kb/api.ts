import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';

type Schemas = components['schemas'];
export type ArticleDetail = Schemas['ArticleDetail'];
export type ArticleListItem = Schemas['ArticleListItem'];
export type ArticleRevisionItem = Schemas['ArticleRevisionItem'];
export type ArticleStatus = Schemas['ArticleStatus'];
export type ArticleVisibility = Schemas['ArticleVisibility'];
export type TicketArticleItem = Schemas['TicketArticleItem'];

export const ARTICLE_STATUSES: readonly ArticleStatus[] = ['draft', 'published', 'archived'];
export const ARTICLE_VISIBILITIES: readonly ArticleVisibility[] = ['employees', 'internal'];

/** Mirror the server's ArticleLimits; the server stays the authority. */
export const ARTICLE_LIMITS = { title: 200, summary: 500, body: 50_000 } as const;

export const ARTICLE_PAGE_SIZE = 20;
export const PICKER_PAGE_SIZE = 20;
export const FIRST_PAGE = 1;
const PICKER_SEARCH_DEBOUNCE_MS = 300;

export const kbKeys = {
  all: ['kb'],
  list: (params: ArticleListParams) => ['kb', 'list', params],
  detail: (id: string) => ['kb', 'detail', id],
  revisions: (id: string) => ['kb', 'revisions', id],
  categories: ['kb', 'categories'],
};

/** Under the tickets root so that every ticket invalidation also refreshes the linked articles. */
const ticketArticlesKey = (number: string) => ['tickets', 'articles', number];

export interface ArticleListParams {
  search: string;
  categoryId: string;
  status: ArticleStatus | '';
  visibility: ArticleVisibility | '';
  page: number;
  pageSize: number;
}

export function useArticles(params: ArticleListParams, enabled = true) {
  return useQuery({
    queryKey: kbKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/kb/articles', {
          params: {
            query: {
              search: params.search || undefined,
              categoryId: params.categoryId || undefined,
              status: params.status || undefined,
              visibility: params.visibility || undefined,
              page: params.page,
              pageSize: params.pageSize,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
    enabled,
  });
}


/** Published articles by title search. `visibility` limits the choice (reply picker: employee-visible only). */
export function usePublishedArticleOptions(search: string, visibility: 'employees' | '') {
  const debounced = useDebouncedValue(search.trim(), PICKER_SEARCH_DEBOUNCE_MS);
  const articles = useArticles({ search: debounced, categoryId: '', status: 'published', visibility, page: FIRST_PAGE, pageSize: PICKER_PAGE_SIZE });
  return (articles.data?.items ?? []).map((a) => ({ value: a.id, label: a.title }));
}

export function useArticle(id: string) {
  return useQuery({ queryKey: kbKeys.detail(id), queryFn: () => unwrap(api.GET('/api/v1/kb/articles/{id}', { params: { path: { id } } })) });
}

export function useArticleRevisions(id: string) {
  return useQuery({ queryKey: kbKeys.revisions(id), queryFn: () => unwrap(api.GET('/api/v1/kb/articles/{id}/revisions', { params: { path: { id } } })) });
}

export function useArticleCategories() {
  return useQuery({ queryKey: kbKeys.categories, queryFn: () => unwrap(api.GET('/api/v1/reference/categories', { params: { query: {} } })) });
}

export interface ArticleInput {
  title: string;
  summary: string;
  body: string;
  categoryId: string | null;
  visibility: ArticleVisibility;
}

function useInvalidateArticles() {
  const queryClient = useQueryClient();
  return (id?: string) => {
    void queryClient.invalidateQueries({ queryKey: kbKeys.all });
    if (id !== undefined) {
      void queryClient.invalidateQueries({ queryKey: ['tickets', 'articles'] });
    }
  };
}

export function useCreateArticle() {
  const invalidate = useInvalidateArticles();
  return useMutation({
    mutationFn: (body: ArticleInput) => unwrap(api.POST('/api/v1/kb/articles', { body })),
    onSuccess: () => invalidate(),
  });
}

export function useUpdateArticle(id: string) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateArticles();
  return useMutation({
    mutationFn: (body: ArticleInput & { version: number }) => unwrap(api.PUT('/api/v1/kb/articles/{id}', { params: { path: { id } }, body })),
    onSuccess: (article) => {
      queryClient.setQueryData(kbKeys.detail(id), article);
      invalidate(id);
    },
  });
}

export type ArticleAction = 'publish' | 'archive' | 'restore';

export function useArticleAction(id: string) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateArticles();
  return useMutation({
    mutationFn: ({ action, version }: { action: ArticleAction; version: number }) => {
      const options = { params: { path: { id } }, body: { version } };
      switch (action) {
        case 'publish':
          return unwrap(api.POST('/api/v1/kb/articles/{id}/publish', options));
        case 'archive':
          return unwrap(api.POST('/api/v1/kb/articles/{id}/archive', options));
        case 'restore':
          return unwrap(api.POST('/api/v1/kb/articles/{id}/restore', options));
      }
    },
    onSuccess: (article) => {
      queryClient.setQueryData(kbKeys.detail(id), article);
      invalidate(id);
    },
  });
}

export function useTicketArticles(number: string) {
  return useQuery({
    queryKey: ticketArticlesKey(number),
    queryFn: () => unwrap(api.GET('/api/v1/tickets/{number}/articles', { params: { path: { number } } })),
  });
}

export function useLinkArticle(number: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (articleId: string) => unwrap(api.POST('/api/v1/tickets/{number}/articles', { params: { path: { number } }, body: { articleId } })),
    onSuccess: (items) => queryClient.setQueryData(ticketArticlesKey(number), items),
  });
}

export function useUnlinkArticle(number: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (articleId: string) =>
      unwrap(api.DELETE('/api/v1/tickets/{number}/articles/{articleId}', { params: { path: { number, articleId } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ticketArticlesKey(number) }),
  });
}
