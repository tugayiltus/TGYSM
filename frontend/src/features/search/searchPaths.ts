/**
 * @input A search hit or a hit type.
 * @output The route that opens the record, or the list that holds the type.
 * @position Used by the top bar search to navigate.
 */
import type { components } from '../../shared/api/schema';
import { SEARCH_PARAM } from '../../shared/list/useListParams';
import { ROUTES, articlePath, changePath, taskPath, ticketPath } from '../../shared/routes';

export type SearchHit = components['schemas']['SearchHit'];
export type SearchHitType = components['schemas']['SearchHitType'];

const LIST_ROUTES: Record<SearchHitType, string> = {
  ticket: ROUTES.tickets,
  change: ROUTES.changes,
  task: ROUTES.tasks,
  asset: ROUTES.assets,
  article: ROUTES.kb,
};

/** The list of one type with the search box filled in. */
export function listPathForType(type: SearchHitType, query: string): string {
  return `${LIST_ROUTES[type]}?${new URLSearchParams({ [SEARCH_PARAM]: query }).toString()}`;
}

/**
 * The asset route takes the asset id, but a hit carries only the tag. An asset hit therefore opens the
 * asset list filtered by its tag.
 */
export function pathForHit(hit: SearchHit): string {
  switch (hit.type) {
    case 'ticket':
      return ticketPath(hit.key);
    case 'change':
      return changePath(hit.key);
    case 'task':
      return taskPath(hit.key);
    case 'article':
      return articlePath(hit.key);
    case 'asset':
      return listPathForType('asset', hit.key);
  }
}
