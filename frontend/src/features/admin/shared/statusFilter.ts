/**
 * @input A list filter value from the URL query and the rows of a small, unpaged admin list.
 * @output The parsed status filter and the rows that match the search text and status.
 * @position Client-side filters of the catalog lists (brands, models, categories, request types).
 */
import { toSearchKey } from '../../../shared/lib/turkish';

export type StatusFilter = 'active' | 'inactive';
export const STATUS_FILTERS: readonly StatusFilter[] = ['active', 'inactive'];
export const STATUS_FILTER_KEY = 'status';

export const readStatusFilter = (value: string): StatusFilter | undefined => STATUS_FILTERS.find((status) => status === value);

/** Keeps the rows whose text contains the search (Turkish case rules) and whose state matches the status. */
export function filterRows<T extends { isActive: boolean }>(rows: readonly T[], search: string, status: StatusFilter | undefined, text: (row: T) => string): T[] {
  const key = toSearchKey(search);
  return rows.filter(
    (row) => (status === undefined || row.isActive === (status === 'active')) && (key === '' || toSearchKey(text(row)).includes(key)),
  );
}
