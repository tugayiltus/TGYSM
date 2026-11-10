/**
 * @input The menu area, its label and title, and the collapsed state.
 * @output The Astryx side navigation with one section per menu group.
 * @position `sideNav` slot of the app shell; Astryx moves it into a drawer on phones.
 */
import { Icon } from '@astryxdesign/core/Icon';
import { SideNav, SideNavHeading, SideNavItem, SideNavSection } from '@astryxdesign/core/SideNav';
import { useTranslation } from 'react-i18next';

import { useSession } from '../../features/auth/session';
import { translateOr } from '../i18n/dynamic';
import { ROUTE_META, type MenuArea } from '../navigation/routeMeta';
import { useCurrentRouteId } from '../navigation/useCrumbs';
import { ROUTES } from '../routes';
import { Icons } from '../ui/icons';

import { activeMenuItem, menuFor } from './sidebarMenu';

/** Where the brand in the side navigation leads in each area. */
const AREA_HOME: Record<MenuArea, string> = { app: ROUTES.home, admin: ROUTES.admin };

export interface AppSidebarProps {
  area: MenuArea;
  label: string;
  title: string;
  isCollapsed: boolean;
  onCollapsedChange: (isCollapsed: boolean) => void;
  /** Called after a menu item is chosen, so the phone drawer can close. */
  onNavigate?: () => void;
}

export function AppSidebar({ area, label, title, isCollapsed, onCollapsedChange, onNavigate }: AppSidebarProps) {
  useTranslation();
  const me = useSession();
  const groups = menuFor(area, me.role);
  const active = activeMenuItem(useCurrentRouteId(), groups);
  return (
    <SideNav
      aria-label={label}
      collapsible={{ isCollapsed, onCollapsedChange }}
      header={<SideNavHeading heading={title} headingHref={AREA_HOME[area]} icon={<Icon icon={Icons.devices} size="sm" />} />}
    >
      {groups.map((group) => (
        <SideNavSection key={group.id} title={translateOr(`menuGroups.${group.id}`, group.id)}>
          {group.items.map((item) => (
            <SideNavItem
              key={item.id}
              label={translateOr(ROUTE_META[item.id].titleKey, item.id)}
              href={item.to}
              icon={item.icon === undefined ? undefined : Icons[item.icon]}
              isSelected={item.id === active}
              onClick={onNavigate}
            />
          ))}
        </SideNavSection>
      ))}
    </SideNav>
  );
}
