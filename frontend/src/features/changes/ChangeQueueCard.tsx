/**
 * @input Nothing; it reads the signed-in person's own changes.
 * @output A card link to the change list in the "mine" queue (Değişikliklerim), with the change count.
 * @position IT workspace (İş listem), beside the ticket and task queue cards.
 */
import { useTranslation } from 'react-i18next';

import { ActionCard } from '../../shared/page/ActionCard';
import { ROUTES } from '../../shared/routes';

import { useChanges } from './api';

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const PENDING_COUNT = '…';
const QUEUE_PARAM = 'queue';
const MINE_QUEUE = 'mine';

const MINE_PATH = `${ROUTES.changes}?${new URLSearchParams({ [QUEUE_PARAM]: MINE_QUEUE }).toString()}`;

/** The count is the list endpoint's total for a one-row page; it is part of the link name. */
export function ChangeQueueCard() {
  const { t } = useTranslation('changes');
  const count = useChanges({ queue: MINE_QUEUE, status: undefined, risk: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const title = t('list.mineCard');
  const countText = count.data ? t('list.cardCount', { count: count.data.totalCount }) : PENDING_COUNT;
  return <ActionCard to={MINE_PATH} title={title} description={countText} label={`${title} ${countText}`} icon="changes" iconColor="accent" />;
}
