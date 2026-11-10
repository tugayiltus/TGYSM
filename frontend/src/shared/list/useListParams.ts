/**
 * @input The filter keys a list page uses.
 * @output Page, search and filter values from the URL query, and setters that write them back.
 * @position Used by every list page, so a link reopens the same filtered view.
 */
import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';

import { FIRST_PAGE, readPageParam } from '../lib/pageParam';

export const PAGE_PARAM = 'page';
export const SEARCH_PARAM = 'q';

export function useListParams<K extends string>(filterKeys: readonly K[]) {
  const [params, setParams] = useSearchParams();

  const page = readPageParam(params.get(PAGE_PARAM));
  const search = params.get(SEARCH_PARAM) ?? '';
  const filters = useMemo(() => Object.fromEntries(filterKeys.map((key) => [key, params.get(key) ?? ''])) as Record<K, string>, [filterKeys, params]);
  const activeCount = filterKeys.filter((key) => filters[key] !== '').length + (search === '' ? 0 : 1);

  // A changed search or filter starts again at the first page. An empty value leaves the URL.
  const update = useCallback(
    (key: string, value: string) =>
      setParams((current) => {
        const next = new URLSearchParams(current);
        if (value === '') {
          next.delete(key);
        } else {
          next.set(key, value);
        }
        next.delete(PAGE_PARAM);
        return next;
      }),
    [setParams],
  );

  const setPage = useCallback(
    (next: number) =>
      setParams((current) => {
        const params = new URLSearchParams(current);
        if (next === FIRST_PAGE) {
          params.delete(PAGE_PARAM);
        } else {
          params.set(PAGE_PARAM, String(next));
        }
        return params;
      }),
    [setParams],
  );

  const clear = useCallback(
    () =>
      setParams((current) => {
        const next = new URLSearchParams(current);
        [SEARCH_PARAM, PAGE_PARAM, ...filterKeys].forEach((key) => next.delete(key));
        return next;
      }),
    [filterKeys, setParams],
  );

  return {
    page,
    search,
    filters,
    activeCount,
    setPage,
    setSearch: (value: string) => update(SEARCH_PARAM, value),
    setFilter: (key: K, value: string) => update(key, value),
    clear,
  };
}
