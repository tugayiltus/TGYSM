/**
 * @input The area link (if any) for the end of the bar.
 * @output The Astryx top bar with the area link, theme and account menus at the end.
 * @position `topNav` slot of the app shell; on phones Astryx adds the menu toggle to it.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { TopNav } from '@astryxdesign/core/TopNav';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { AccountMenu } from './AccountMenu';
import { ThemeMenu } from './ThemeMenu';

export interface AppHeaderProps {
  /** Link to the other area: "Yönetim" for admins in the app, "Uygulamaya dön" in /admin. */
  areaSwitch?: ReactNode;
}

export function AppHeader({ areaSwitch }: AppHeaderProps) {
  const { t } = useTranslation();
  return (
    <TopNav
      label={t('layout.topBar')}
      endContent={
        <HStack gap={1} vAlign="center">
          {areaSwitch}
          <ThemeMenu />
          <AccountMenu />
        </HStack>
      }
    />
  );
}
