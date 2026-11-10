/**
 * @input The menu area, the app or area title, the menu label, and an optional area link.
 * @output The Astryx app shell: side navigation, top bar, then the page trail and the routed page.
 * @position Root element of `AppLayout` and `AdminLayout`.
 */
import { AppShell } from '@astryxdesign/core/AppShell';
import { VStack } from '@astryxdesign/core/VStack';
import type { ReactNode } from 'react';
import { Outlet } from 'react-router';

import { Breadcrumbs } from '../navigation/Breadcrumbs';
import type { MenuArea } from '../navigation/routeMeta';

import { AppHeader } from './AppHeader';
import { AppSidebar } from './AppSidebar';
import { useSidebar } from './useSidebar';

/** Pages hold forms, lists and text, so the content area gets standard padding. */
const CONTENT_PADDING = 4;

export interface ShellFrameProps {
  area: MenuArea;
  title: string;
  navLabel: string;
  areaSwitch?: ReactNode;
}

export function ShellFrame({ area, title, navLabel, areaSwitch }: ShellFrameProps) {
  const sidebar = useSidebar();
  return (
    <AppShell
      height="auto"
      contentPadding={CONTENT_PADDING}
      topNav={<AppHeader areaSwitch={areaSwitch} />}
      sideNav={
        <AppSidebar
          area={area}
          label={navLabel}
          title={title}
          isCollapsed={sidebar.narrow}
          onCollapsedChange={sidebar.setNarrow}
          onNavigate={() => sidebar.setMobileOpen(false)}
        />
      }
      mobileNav={{ isOpen: sidebar.mobileOpen, onOpenChange: sidebar.setMobileOpen }}
    >
      {/* The trail sits above the page, not in the top bar: on phones Astryx moves top-bar content into the menu drawer. */}
      <VStack gap={4}>
        <Breadcrumbs />
        <Outlet />
      </VStack>
    </AppShell>
  );
}
