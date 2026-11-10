/**
 * @input Optional actions, title override, icon and description.
 * @output The page title row: icon, H1 and primary actions, with an optional line below.
 * @position First element of every page inside the app shell.
 */
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useEffect, useRef, type ReactNode } from 'react';
import { useLocation } from 'react-router';

import { Icons, type AppIcon } from '../ui/icons';

import { useCrumbs } from './useCrumbs';

/** Pathname of the last mounted header. Focus moves to the new H1 only on navigation, not on first load. */
let lastPathname: string | null = null;

export interface PageHeaderProps {
  /** Primary page actions, after the title on wide screens. */
  actions?: ReactNode;
  /** Title when it differs from the breadcrumb's current item. */
  titleOverride?: string;
  /** The page's icon, usually its menu icon. */
  icon?: AppIcon;
  /** One line under the title. */
  description?: ReactNode;
}

export function PageHeader({ actions, titleOverride, icon, description }: PageHeaderProps) {
  const crumbs = useCrumbs();
  const { pathname } = useLocation();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const title = titleOverride ?? crumbs.at(-1)?.label ?? '';

  // Synchronizes keyboard focus with router navigation: a screen reader then reads the new page title.
  useEffect(() => {
    if (lastPathname !== null && lastPathname !== pathname) {
      headingRef.current?.focus();
    }
    lastPathname = pathname;
  }, [pathname]);

  return (
    <VStack gap={1}>
      <HStack gap={3} hAlign="between" vAlign="center" wrap="wrap">
        <HStack gap={2} vAlign="center">
          {icon !== undefined && <Icon icon={Icons[icon]} size="lg" color="accent" />}
          <Heading level={1} ref={headingRef} tabIndex={-1}>
            {title}
          </Heading>
        </HStack>
        {actions !== undefined && (
          <HStack gap={2} wrap="wrap">
            {actions}
          </HStack>
        )}
      </HStack>
      {description !== undefined && <Text type="supporting">{description}</Text>}
    </VStack>
  );
}
