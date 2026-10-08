export const FIRST_PAGE = 1;

/** Digits only and at most nine of them: `Number()` would accept `1.5`, `Infinity` and `1e21`, which no page list can serve. */
const PAGE_DIGITS = /^\d{1,9}$/;

/** Reads a `?page=` value from the URL; anything that is not a whole page number from 1 up falls back to the first page. */
export function readPageParam(value: string | null): number {
  const page = value !== null && PAGE_DIGITS.test(value) ? Number(value) : FIRST_PAGE;
  return page >= FIRST_PAGE ? page : FIRST_PAGE;
}
