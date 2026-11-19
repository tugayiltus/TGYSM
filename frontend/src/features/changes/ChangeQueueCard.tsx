/**
 * @input The queue to show: "mine" or "this_week".
 * @output A card link to the change list in that queue (Değişikliklerim or Bu hafta uygulanacak), with the change count.
 * @position IT workspace (İş listem), beside the ticket and task queue cards.
 */
import { useTranslation } from 'react-i18next';

import { ActionCard } from '../../shared/page/ActionCard';
import { ROUTES } from '../../shared/routes';

import { useChanges, type ChangeQueue } from './api';

export type ChangeCardQueue = Extract<ChangeQueue, 'mine' | 'this_week'>;

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const PENDING_COUNT = '…';
const QUEUE_PARAM = 'queue';
const queuePath = (queue: ChangeCardQueue) => `${ROUTES.changes}?${new URLSearchParams({ [QUEUE_PARAM]: queue }).toString()}`;

/** The count is the list endpoint's total for a one-row page; it is part of the link name. */
export function ChangeQueueCard({ queue }: { queue: ChangeCardQueue }) {
  const { t } = useTranslation('changes');
  const count = useChanges({ queue, status: undefined, risk: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const title = queue === 'mine' ? t('list.mineCard') : t('list.thisWeekCard');
  const countText = count.data ? t('list.cardCount', { count: count.data.totalCount }) : PENDING_COUNT;
  return <ActionCard to={queuePath(queue)} title={title} description={countText} label={`${title} ${countText}`} icon="changes" iconColor="accent" />;
}
