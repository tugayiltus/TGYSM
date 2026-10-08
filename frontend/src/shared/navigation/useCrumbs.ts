import { useTranslation } from 'react-i18next';
import { useLocation, useMatches, useParams } from 'react-router';

import { translateOr } from '../i18n/dynamic';

import { useBreadcrumbLabels } from './breadcrumbLabels';
import { buildCrumbs, type Crumb } from './crumbs';
import { parseReturn } from './returnTo';
import { isRouteId, type RouteId } from './routeMeta';

/** The deepest matched route that has metadata. */
export function useCurrentRouteId(): RouteId | null {
  const matches = useMatches();
  for (let index = matches.length - 1; index >= 0; index -= 1) {
    const id = matches[index]?.id;
    if (isRouteId(id)) {
      return id;
    }
  }
  return null;
}

const translate = (key: string) => translateOr(key, key);

export function useCrumbs(): Crumb[] {
  // Subscribes to language changes; keys are built at runtime from route metadata.
  useTranslation();
  const routeId = useCurrentRouteId();
  const params = useParams();
  const location = useLocation();
  const labels = useBreadcrumbLabels(location.pathname);
  if (routeId === null) {
    return [];
  }
  return buildCrumbs({ routeId, params, labels, from: parseReturn(location.state), t: (key) => translate(key) });
}
