/**
 * @input A task queue.
 * @output A card link to the task list in that queue, with its task count.
 * @position IT workspace (İş listem).
 */
import { useTranslation } from 'react-i18next';

import { ActionCard } from '../../shared/page/ActionCard';
import type { AppIcon } from '../../shared/ui/icons';

import { taskQueuePath, useTasks, type TaskQueue } from './api';

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;
const PENDING_COUNT = '…';

const QUEUE_ICON: Record<TaskQueue, AppIcon> = {
  mine: 'user',
  team: 'group',
  unassigned: 'inbox',
  overdue: 'warning',
};

/** The count is the list endpoint's total for a one-row page; it is part of the link name. */
export function TaskQueueCard({ queue }: { queue: TaskQueue }) {
  const { t } = useTranslation('tasks');
  const count = useTasks({ queue, status: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const title = t(`queues.${queue}`);
  const countText = count.data ? t('workspace.count', { count: count.data.totalCount }) : PENDING_COUNT;
  return <ActionCard to={taskQueuePath(queue)} title={title} description={countText} label={`${title} ${countText}`} icon={QUEUE_ICON[queue]} iconColor={queue === 'overdue' ? 'error' : 'accent'} />;
}
