/**
 * @input A ticket status or priority.
 * @output A colored token with the translated name; color is never the only cue.
 * @position Ticket lists, ticket detail and the start page.
 */
import { Token } from '@astryxdesign/core/Token';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import type { Priority, TicketStatus } from './api';

type TokenColor = NonNullable<ComponentProps<typeof Token>['color']>;

/** One fixed color per status and priority, so people learn the mapping across the app. */
const STATUS_COLOR: Record<TicketStatus, TokenColor> = {
  open: 'blue',
  in_progress: 'cyan',
  on_hold: 'yellow',
  completed: 'green',
  cancelled: 'gray',
};

const PRIORITY_COLOR: Record<Priority, TokenColor> = {
  p1: 'red',
  p2: 'orange',
  p3: 'blue',
  p4: 'gray',
};

export function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const { t } = useTranslation('tickets');
  return <Token size="sm" color={STATUS_COLOR[status]} label={t(`statuses.${status}`)} />;
}

export function PriorityBadge({ priority }: { priority: Priority }) {
  const { t } = useTranslation('tickets');
  return <Token size="sm" color={PRIORITY_COLOR[priority]} label={t(`priorities.${priority}`)} />;
}
