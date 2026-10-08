import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { useSession } from '../../features/auth/session';
import { translateOr } from '../i18n/dynamic';
import { ROUTE_META, type MenuArea } from '../navigation/routeMeta';
import { useCurrentRouteId } from '../navigation/useCrumbs';
import { Button } from '../ui/Button';

import { activeMenuItem, menuFor } from './sidebarMenu';

export interface SidebarNavProps {
  area: MenuArea;
  label: string;
  onNavigate?: () => void;
}

/** Grouped menu of an area (spec §4.2); used in the desktop sidebar and the phone panel. */
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
          <li key={group.id} className="nav-group-items-wrapper">
            <p className="nav-title mb-0">{translateOr(`menuGroups.${group.id}`, group.id)}</p>
            <ul className="list-unstyled">
              {group.items.map((item) => (
                <li key={item.id} className="nav-item">
                  <Link
                    to={item.to}
                    className={`nav-link ${item.id === active ? 'active' : ''}`}
                    aria-current={item.id === active ? (item.id === current ? 'page' : 'true') : undefined}
                    onClick={onNavigate}
                  >
                    {translateOr(ROUTE_META[item.id].titleKey, item.id)}
                  </Link>
                </li>
              ))}
            </ul>
          </li>
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
        <span className="sidebar-brand fw-semibold text-truncate">{title}</span>
      </div>
      <div className="flex-grow-1 overflow-auto">
        <SidebarNav area={area} label={label} />
      </div>
      <div className="sidebar-footer border-top d-flex justify-content-end">
        <Button variant="ghost" className="text-white" aria-pressed={narrow} onClick={onToggleNarrow}>
          {narrow ? t('layout.expandSidebar') : t('layout.collapseSidebar')}
        </Button>
      </div>
    </aside>
  );
}
