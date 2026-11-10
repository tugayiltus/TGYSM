/**
 * @input A ticket queue.
 * @output A card link to the queue with its record count.
 * @position Start page and IT workspace.
 */
import { useTranslation } from 'react-i18next';

import { ActionCard } from '../../shared/page/ActionCard';
import type { AppIcon } from '../../shared/ui/icons';
import { useTickets, type TicketQueue } from '../tickets/api';

import { queuePath } from './queuePath';

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const PENDING_COUNT = '…';

const QUEUE_ICON: Record<TicketQueue, AppIcon> = {
  mine: 'user',
  assigned_to_me: 'user',
  team: 'group',
  unassigned: 'inbox',
  on_hold: 'pause',
  sla_risk: 'warning',
};

/** The count is the list endpoint's total for a one-row page. It is part of the link name, e.g. "SLA riski 3 kayıt". */
export function QueueCard({ queue }: { queue: TicketQueue }) {
  const { t } = useTranslation('tickets');
  const count = useTickets({ queue, status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const title = t(`workspace.${queue}`);
  const countText = count.data ? t('workspace.count', { count: count.data.totalCount }) : PENDING_COUNT;
  return <ActionCard to={queuePath(queue)} title={title} description={countText} label={`${title} ${countText}`} icon={QUEUE_ICON[queue]} iconColor={queue === 'sla_risk' ? 'error' : 'accent'} />;
}
