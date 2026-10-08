import { useEffect, useSyncExternalStore } from 'react';
import { useLocation } from 'react-router';

import type { RouteId } from './routeMeta';

interface LabelEntry {
  pathname: string;
  routeId: RouteId;
  label: string;
}

/**
 * Labels pages publish for the breadcrumb from data they already loaded (ticket number, article title).
 * A small external store, so publishing does not set React state inside an effect; entries are tied to the
 * pathname they were set on and ignored elsewhere.
 */
let entries: readonly LabelEntry[] = [];
const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) {
    listener();
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useBreadcrumbLabel(routeId: RouteId, label: string | undefined) {
  const { pathname } = useLocation();
  useEffect(() => {
    if (label === undefined) {
      return undefined;
    }
    const entry: LabelEntry = { pathname, routeId, label };
    entries = [...entries.filter((e) => !(e.pathname === pathname && e.routeId === routeId)), entry];
    emit();
    return () => {
      entries = entries.filter((e) => e !== entry);
      emit();
    };
  }, [pathname, routeId, label]);
}

export function useBreadcrumbLabels(pathname: string): Partial<Record<RouteId, string>> {
  const snapshot = useSyncExternalStore(subscribe, () => entries);
  const labels: Partial<Record<RouteId, string>> = {};
  for (const entry of snapshot) {
    if (entry.pathname === pathname) {
      labels[entry.routeId] = entry.label;
    }
  }
  return labels;
}
