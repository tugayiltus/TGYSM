import { useTranslation } from 'react-i18next';

import type { Priority, TicketStatus } from './api';

/** CoreUI subtle badges: colour plus the status text, never colour alone. */
const STATUS_CLASS: Record<TicketStatus, string> = {
  open: 'bg-primary-subtle text-primary-emphasis border-primary-subtle',
  in_progress: 'bg-info-subtle text-info-emphasis border-info-subtle',
  on_hold: 'bg-warning-subtle text-warning-emphasis border-warning-subtle',
  completed: 'bg-success-subtle text-success-emphasis border-success-subtle',
  cancelled: 'bg-secondary-subtle text-secondary-emphasis border-secondary-subtle',
};

const PRIORITY_CLASS: Record<Priority, string> = {
  p1: 'bg-danger-subtle text-danger-emphasis border-danger-subtle',
  p2: 'bg-warning-subtle text-warning-emphasis border-warning-subtle',
  p3: 'bg-info-subtle text-info-emphasis border-info-subtle',
  p4: 'bg-secondary-subtle text-secondary-emphasis border-secondary-subtle',
};

const BADGE_CLASS = 'badge rounded-pill border fw-medium';

export function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const { t } = useTranslation('tickets');
  return <span className={`${BADGE_CLASS} ${STATUS_CLASS[status]}`}>{t(`statuses.${status}`)}</span>;
}

export function PriorityBadge({ priority }: { priority: Priority }) {
  const { t } = useTranslation('tickets');
  return <span className={`${BADGE_CLASS} ${PRIORITY_CLASS[priority]}`}>{t(`priorities.${priority}`)}</span>;
}
