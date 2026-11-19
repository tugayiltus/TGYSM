import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';

export type SearchResult = components['schemas']['SearchResult'];

/** Mirror the server's query limits; the server stays the authority. */
export const SEARCH_MIN_LENGTH = 2;
export const SEARCH_MAX_LENGTH = 100;
export const SEARCH_DEBOUNCE_MS = 300;

export function fetchSearch(q: string): Promise<SearchResult> {
  return unwrap(api.GET('/api/v1/search', { params: { query: { q } } }));
}
