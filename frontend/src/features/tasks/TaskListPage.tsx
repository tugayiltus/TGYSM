/**
 * @input `queue`, `status`, `priority`, `search` and `page` in the URL query.
 * @output The task list with filters, paging and a button that opens a new task.
 * @position `/tasks`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { taskPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { PRIORITIES } from '../tickets/api';
import { PriorityBadge } from '../tickets/TicketBadges';

import { TASK_QUEUES, TASK_STATUSES, useTasks, type TaskListItem } from './api';
import { OverdueBadge, TaskStatusBadge } from './TaskBadges';
import { TaskFormDialog } from './TaskFormDialog';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['queue', 'status', 'priority'] as const;

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

function OpenLink({ task }: { task: TaskListItem }) {
  const { t } = useTranslation('tasks');
  const location = useLocation();
  return (
    <StateLink href={taskPath(task.number)} state={linkState(location)} label={recordActionLabel(t('list.open'), task.number)}>
      {t('list.open')}
    </StateLink>
  );
}

function DueDate({ task }: { task: TaskListItem }) {
  const { t: tc } = useTranslation();
  if (task.dueDate === null) {
    return <>{tc('none')}</>;
  }
  return (
    <HStack gap={1} vAlign="center" wrap="wrap">
      <Text>{formatDate(task.dueDate)}</Text>
      {task.isOverdue && <OverdueBadge />}
    </HStack>
  );
}

export function TaskListPage() {
  const { t } = useTranslation('tasks');
  const { t: tt } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const list = useListParams(FILTER_KEYS);
  const queue = pick(TASK_QUEUES, list.filters.queue);
  const status = pick(TASK_STATUSES, list.filters.status);
  const priority = pick(PRIORITIES, list.filters.priority);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const tasks = useTasks({ queue, status, priority, search, page: list.page, pageSize: PAGE_SIZE });
  const columns: DataColumn<TaskListItem>[] = [
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <Text type="label">{x.number}</Text> },
    { key: 'title', header: t('list.columns.title'), renderCell: (x) => x.title },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <TaskStatusBadge status={x.status} /> },
    { key: 'priority', header: t('list.columns.priority'), renderCell: (x) => <PriorityBadge priority={x.priority} /> },
    { key: 'team', header: t('list.columns.team'), renderCell: (x) => x.teamName },
    { key: 'assignee', header: t('list.columns.assignee'), renderCell: (x) => x.assigneeName ?? tc('none') },
    { key: 'due', header: t('list.columns.due'), renderCell: (x) => <DueDate task={x} /> },
    { key: 'updated', header: t('list.columns.updated'), renderCell: (x) => formatDateTime(x.updatedAt) },
    { key: 'open', header: '', renderCell: (x) => <OpenLink task={x} /> },
  ];
  const renderCard = (x: TaskListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="supporting">{x.number}</Text>
            <Text type="label" wordBreak="break-word">
              {x.title}
            </Text>
          </VStack>
        </StackItem>
        <StackItem>
          <TaskStatusBadge status={x.status} />
        </StackItem>
      </HStack>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <PriorityBadge priority={x.priority} />
        <Text type="supporting">{x.assigneeName ?? x.teamName}</Text>
        {x.isOverdue && <OverdueBadge />}
      </HStack>
      <OpenLink task={x} />
    </VStack>
  );
  const option = (value: string | undefined) => value ?? '';
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.tasks.icon}
        titleOverride={queue ? t(`queues.${queue}`) : undefined}
        actions={<Button label={t('list.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setCreating(true)} />}
      />
      <ListToolbar
        title={t('list.title')}
        search={list.search}
        searchLabel={t('list.search')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('list.queueFilter')}
              placeholder={t('list.all')}
              options={TASK_QUEUES.map((value) => ({ value, label: t(`queues.${value}`) }))}
              value={option(queue)}
              onChange={(value) => list.setFilter('queue', value)}
            />
            <Selector
              label={t('list.statusFilter')}
              placeholder={t('list.all')}
              options={TASK_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={option(status)}
              onChange={(value) => list.setFilter('status', value)}
            />
            <Selector
              label={t('list.priorityFilter')}
              placeholder={t('list.all')}
              options={PRIORITIES.map((value) => ({ value, label: tt(`priorities.${value}`) }))}
              value={option(priority)}
              onChange={(value) => list.setFilter('priority', value)}
            />
          </>
        }
      />
      <Card padding={0}>
        {tasks.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {tasks.isError && <ErrorState error={tasks.error} onRetry={() => void tasks.refetch()} />}
        {tasks.data && tasks.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
        {tasks.data && tasks.data.items.length > 0 && (
          <DataList items={tasks.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {tasks.data && tasks.data.items.length > 0 && (
        <Pagination page={tasks.data.page} pageSize={tasks.data.pageSize} totalItems={tasks.data.totalCount} onChange={list.setPage} />
      )}
      {creating && <TaskFormDialog onClose={() => setCreating(false)} onCreated={(task) => void navigate(taskPath(task.number))} />}
    </VStack>
  );
}
