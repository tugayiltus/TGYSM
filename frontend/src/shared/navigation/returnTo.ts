import { matchPath, useLocation } from 'react-router';

import { ROUTE_IDS, ROUTE_META, type RouteId } from './routeMeta';

/** Lists a detail page may return to with their filters (spec §4.5); anything else falls back to the default parent. */
export const RETURNABLE_LISTS: readonly RouteId[] = ['myTickets', 'workspace', 'workspaceQueue', 'approvals', 'assets', 'myAssets', 'kbList', 'report', 'reportList'];

export interface ReturnTarget {
  routeId: RouteId;
  href: string;
  params: Record<string, string>;
}

/** The route id whose pattern matches a pathname; static patterns win over parameterised ones. */
export function matchRouteId(pathname: string): { routeId: RouteId; params: Record<string, string> } | null {
  const ordered = [...ROUTE_IDS].sort((a, b) => Number(ROUTE_META[a].path.includes(':')) - Number(ROUTE_META[b].path.includes(':')));
  for (const routeId of ordered) {
    const match = matchPath({ path: ROUTE_META[routeId].path, end: true }, pathname);
    if (match !== null) {
      return { routeId, params: Object.fromEntries(Object.entries(match.params).filter((entry): entry is [string, string] => entry[1] !== undefined)) };
    }
  }
  return null;
}

/** Reads `state.from` left by a list link; only same-app paths of a returnable list are accepted. */
export function parseReturn(state: unknown): ReturnTarget | null {
  if (typeof state !== 'object' || state === null || !('from' in state)) {
    return null;
  }
  const from = (state as { from: unknown }).from;
  if (typeof from !== 'string' || !from.startsWith('/') || from.startsWith('//') || from.startsWith('/\\')) {
    return null;
  }
  const url = new URL(from, window.location.origin);
  if (url.origin !== window.location.origin) {
    return null;
  }
  const match = matchRouteId(url.pathname);
  if (match === null || !RETURNABLE_LISTS.includes(match.routeId)) {
    return null;
  }
  return { routeId: match.routeId, href: `${url.pathname}${url.search}`, params: match.params };
}

/** State for a link from a list to a detail, so the detail can return to the same filtered list. */
export const linkState = (location: { pathname: string; search: string }) => ({ from: `${location.pathname}${location.search}` });

/** Where a detail page's "back" goes: the list it came from, or `defaultTo`. */
export function useReturnTo(defaultTo: string): string {
  const location = useLocation();
  return parseReturn(location.state)?.href ?? defaultTo;
}
