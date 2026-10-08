import { generatePath } from 'react-router';

import type { ReturnTarget } from './returnTo';
import { ROUTE_META, routeChain, type RouteId } from './routeMeta';

export interface Crumb {
  label: string;
  /** Absent for the current page and for menu groups without a page. */
  href?: string;
}

export interface BuildCrumbsInput {
  routeId: RouteId;
  params: Record<string, string | undefined>;
  /** Labels set by pages from loaded data, keyed by route id. */
  labels: Partial<Record<RouteId, string>>;
  from: ReturnTarget | null;
  t: (key: string) => string;
}

const ADMIN_ROOT = 'adminOverview' satisfies RouteId;


function hrefOf(routeId: RouteId, params: Record<string, string | undefined>): string | undefined {
  try {
    return generatePath(ROUTE_META[routeId].path, params);
  } catch {
    // A parent needing a param the current page does not have gets no link.
    return undefined;
  }
}

function labelOf(routeId: RouteId, input: BuildCrumbsInput): string {
  const meta = ROUTE_META[routeId];
  const loaded = input.labels[routeId];
  if (loaded !== undefined) {
    return loaded;
  }
  if (meta.titleParam !== undefined) {
    const value = input.params[meta.titleParam.param];
    if (value !== undefined) {
      return input.t(`${meta.titleParam.keyPrefix}${value}`);
    }
  }
  return meta.dynamic === true ? input.t('breadcrumb.loading') : input.t(meta.titleKey);
}

/** The breadcrumb trail of a page: structural parents, or the filtered list it was opened from (spec §4.4–§4.5). */
export function buildCrumbs(input: BuildCrumbsInput): Crumb[] {
  const { routeId, params, from, t } = input;
  const meta = ROUTE_META[routeId];
  const current: Crumb = { label: labelOf(routeId, input) };

  if (meta.area === 'admin') {
    const root: Crumb = { label: t('routes.admin'), href: hrefOf(ADMIN_ROOT, {}) };
    if (routeId === ADMIN_ROOT) {
      return [root, current];
    }
    return meta.group === undefined ? [root, current] : [root, { label: t(`menuGroups.${meta.group}`) }, current];
  }

  const useFrom = from !== null && from.routeId !== routeId;
  const parents = useFrom ? routeChain(from.routeId) : routeChain(routeId).slice(0, -1);
  const parentParams = useFrom ? { ...params, ...from.params } : params;
  const crumbs = parents.map((id, index): Crumb => {
    const isReturnTarget = useFrom && index === parents.length - 1;
    return { label: labelOf(id, { ...input, params: parentParams }), href: isReturnTarget ? from.href : hrefOf(id, parentParams) };
  });
  return [...crumbs, current];
}
