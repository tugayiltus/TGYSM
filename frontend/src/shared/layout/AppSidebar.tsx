import CIcon from '@coreui/icons-react';
import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { useSession } from '../../features/auth/session';
import { translateOr } from '../i18n/dynamic';
import { ROUTE_META, type MenuArea } from '../navigation/routeMeta';
import { useCurrentRouteId } from '../navigation/useCrumbs';
import { ROUTES } from '../routes';
import { cilDevices } from '../ui/icons';

import { activeMenuItem, menuFor } from './sidebarMenu';

/** Where the brand in the sidebar header leads in each area. */
const AREA_HOME: Record<MenuArea, string> = { app: ROUTES.home, admin: ROUTES.admin };

export interface SidebarNavProps {
  area: MenuArea;
  label: string;
  onNavigate?: () => void;
}

/** Grouped menu of an area with an icon per item (spec §2.3); used in the desktop sidebar and the phone panel. */
export function SidebarNav({ area, label, onNavigate }: SidebarNavProps) {
  useTranslation();
  const me = useSession();
  const groups = menuFor(area, me.role);
  const current = useCurrentRouteId();
  const active = activeMenuItem(current, groups);
  return (
    <nav aria-label={label}>
      <ul className="sidebar-nav">
        {groups.map((group) => (
          <Fragment key={group.id}>
            <li className="nav-title">{translateOr(`menuGroups.${group.id}`, group.id)}</li>
            {group.items.map((item) => {
              const title = translateOr(ROUTE_META[item.id].titleKey, item.id);
              return (
                <li key={item.id} className="nav-item">
                  <Link
                    to={item.to}
                    title={title}
                    className={`nav-link ${item.id === active ? 'active' : ''}`}
                    aria-current={item.id === active ? (item.id === current ? 'page' : 'true') : undefined}
                    onClick={onNavigate}
                  >
                    <CIcon icon={item.icon} customClassName="nav-icon" aria-hidden="true" />
                    <span className="nav-label">{title}</span>
                  </Link>
                </li>
              );
            })}
          </Fragment>
        ))}
      </ul>
    </nav>
  );
}

export interface AppSidebarProps {
  area: MenuArea;
  label: string;
  title: string;
  narrow: boolean;
  onToggleNarrow: () => void;
}

/** Fixed left sidebar from 992 px; below that the header's menu button opens the same nav in a panel. */
export function AppSidebar({ area, label, title, narrow, onToggleNarrow }: AppSidebarProps) {
  const { t } = useTranslation();
  return (
    <aside className={`sidebar sidebar-dark border-end d-none d-lg-flex flex-column flex-shrink-0 ${narrow ? 'sidebar-narrow' : ''}`}>
      <div className="sidebar-header border-bottom">
        <Link to={AREA_HOME[area]} className="sidebar-brand d-flex align-items-center gap-2 text-decoration-none text-white min-w-0">
          <CIcon icon={cilDevices} size="xl" aria-hidden="true" className="flex-shrink-0" />
          <span className="sidebar-brand-full fw-semibold text-truncate">{title}</span>
        </Link>
      </div>
      <div className="sidebar-body flex-grow-1 overflow-auto">
        <SidebarNav area={area} label={label} />
      </div>
      <div className="sidebar-footer border-top d-flex justify-content-end">
        <button
          type="button"
          className="sidebar-toggler"
          aria-pressed={narrow}
          aria-label={narrow ? t('layout.expandSidebar') : t('layout.collapseSidebar')}
          onClick={onToggleNarrow}
        />
      </div>
    </aside>
  );
}
