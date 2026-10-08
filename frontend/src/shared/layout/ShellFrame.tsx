import { useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';

import { Breadcrumbs } from '../navigation/Breadcrumbs';
import type { MenuArea } from '../navigation/routeMeta';
import { Sheet } from '../ui/Sheet';

import { AppHeader } from './AppHeader';
import { AppSidebar, SidebarNav } from './AppSidebar';
import { useSidebar } from './useSidebar';

export interface ShellFrameProps {
  area: MenuArea;
  title: string;
  navLabel: string;
  areaSwitch?: ReactNode;
}

/** CoreUI frame shared by the app and admin layouts: sidebar, header, breadcrumb, content. */
export function ShellFrame({ area, title, navLabel, areaSwitch }: ShellFrameProps) {
  const { t } = useTranslation();
  const sidebar = useSidebar();
  const trigger = useRef<HTMLButtonElement | null>(null);
  const closePanel = () => {
    sidebar.setMobileOpen(false);
    trigger.current?.focus();
  };
  return (
    <div className="d-flex min-vh-100 bg-body-tertiary">
      <AppSidebar area={area} label={navLabel} title={title} narrow={sidebar.narrow} onToggleNarrow={sidebar.toggleNarrow} />
      <Sheet open={sidebar.mobileOpen} onOpenChange={(open) => (open ? sidebar.setMobileOpen(true) : closePanel())} title={navLabel}>
        <SidebarNav area={area} label={navLabel} onNavigate={closePanel} />
      </Sheet>
      <div className="wrapper d-flex flex-column flex-grow-1 min-w-0">
        <a className="visually-hidden-focusable" href="#main">
          {t('layout.skipToContent')}
        </a>
        <AppHeader
          title={title}
          areaSwitch={areaSwitch}
          onOpenMenu={(button) => {
            trigger.current = button;
            sidebar.setMobileOpen(true);
          }}
        />
        <main id="main" className="body flex-grow-1 px-3 py-3 min-w-0">
          <div className="container-lg px-0">
            <Breadcrumbs />
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
