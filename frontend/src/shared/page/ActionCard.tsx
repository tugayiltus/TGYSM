/**
 * @input Target route, title, one-line description, icon and an optional icon color.
 * @output A whole-card link with an icon, the title and the description.
 * @position Start-page shortcuts and queue tiles.
 */
import { ClickableCard } from '@astryxdesign/core/ClickableCard';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import type { ComponentProps, ReactNode } from 'react';

import { Icons, type AppIcon } from '../ui/icons';

export interface ActionCardProps {
  to: string;
  title: string;
  /** Second line: a sentence, or a value such as a record count. */
  description: ReactNode;
  icon: AppIcon;
  /** Error color marks a fault or a risk; everything else uses the accent. */
  iconColor?: Extract<ComponentProps<typeof Icon>['color'], 'accent' | 'error'>;
  /** Accessible name. Defaults to the title plus a text description, as a screen reader reads the whole card. */
  label?: string;
}

export function ActionCard({ to, title, description, icon, iconColor = 'accent', label = typeof description === 'string' ? `${title} ${description}` : title }: ActionCardProps) {
  return (
    <ClickableCard href={to} label={label} height="100%">
      <HStack gap={3} vAlign="center">
        <Icon icon={Icons[icon]} size="lg" color={iconColor} />
        <VStack gap={0.5}>
          <Text type="label">{title}</Text>
          <Text type="supporting">{description}</Text>
        </VStack>
      </HStack>
    </ClickableCard>
  );
}
