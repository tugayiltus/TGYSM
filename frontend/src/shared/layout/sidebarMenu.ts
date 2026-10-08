import type { Role } from '../../features/auth/api';
import { ADMIN_MENU_GROUPS, APP_MENU_GROUPS, ROUTE_IDS, ROUTE_META, routeChain, type MenuArea, type MenuGroupId, type RouteId } from '../navigation/routeMeta';

export interface MenuItem {
  id: RouteId;
  to: string;
}

export interface MenuGroup {
  id: MenuGroupId;
  items: MenuItem[];
}

/** Sidebar groups of an area with the items this role may open; empty groups are left out. */
export function menuFor(area: MenuArea, role: Role): MenuGroup[] {
  const groups: readonly MenuGroupId[] = area === 'app' ? APP_MENU_GROUPS : ADMIN_MENU_GROUPS;
  return groups
    .map((group) => ({
      id: group,
      items: ROUTE_IDS.filter((id) => {
        const meta = ROUTE_META[id];
        return meta.area === area && meta.group === group && meta.menu !== undefined && (meta.menu.roles === undefined || meta.menu.roles.includes(role));
      })
        .sort((a, b) => (ROUTE_META[a].menu?.order ?? 0) - (ROUTE_META[b].menu?.order ?? 0))
        .map((id) => ({ id, to: ROUTE_META[id].path })),
    }))
    .filter((group) => group.items.length > 0);
}

/** The menu item to highlight: the deepest item on the current route's chain. */
export function activeMenuItem(current: RouteId | null, groups: MenuGroup[]): RouteId | null {
  if (current === null) {
    return null;
  }
  const itemIds = new Set(groups.flatMap((group) => group.items.map((item) => item.id)));
  const chain = routeChain(current);
  for (let index = chain.length - 1; index >= 0; index -= 1) {
    const id = chain[index];
    if (id !== undefined && itemIds.has(id)) {
      return id;
    }
  }
  return null;
}
