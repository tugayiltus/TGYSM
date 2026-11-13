/**
 * @input Nothing; it reads the signed-in person's open work.
 * @output One list of the open tickets, tasks and changes assigned to the person, newest first, with paging; change rows show risk and window.
 * @position IT workspace (İş listem), below the queue cards.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { Pagination } from '@astryxdesign/core/Pagination';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { changePath, taskPath, ticketPath } from '../../shared/routes';
import { ChangeStatusBadge, RiskBadge } from '../changes/ChangeBadges';
import { PriorityBadge } from '../tickets/TicketBadges';

import { useWorkItems, type WorkItemRow } from './api';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SKELETON_ROWS = 3;
const TASK_KIND = 'task';
const CHANGE_KIND = 'change';
type Kind = 'incident' | 'request' | 'task' | 'change';
type ChangeStatusValue = ComponentProps<typeof ChangeStatusBadge>['status'];

const KIND_COLOR = { task: 'purple', change: 'blue' } as const;

function pathOf(row: WorkItemRow) {
  switch (row.kind) {
    case TASK_KIND:
      return taskPath(row.number);
    case CHANGE_KIND:
      return changePath(row.number);
    default:
      return ticketPath(row.number);
  }
}

function NumberLink({ row }: { row: WorkItemRow }) {
  const location = useLocation();
  return (
    <StateLink href={pathOf(row)} state={linkState(location)}>
      {row.number}
    </StateLink>
  );
}

function KindToken({ kind }: { kind: string }) {
  const { t } = useTranslation('tasks');
  return <Token size="sm" color={KIND_COLOR[kind as keyof typeof KIND_COLOR] ?? 'gray'} label={t(`workspace.kinds.${kind as Kind}`)} />;
}

/** Ticket and task statuses share their names, so one label set reads both; a change has its own status names. */
function StatusText({ row }: { row: WorkItemRow }) {
  const { t } = useTranslation('tasks');
  if (row.kind === CHANGE_KIND) {
    return <ChangeStatusBadge status={row.status as ChangeStatusValue} />;
  }

  const status = row.status;
  return <Text>{t(`statuses.${status as 'open'}`)}</Text>;
}

/** A change has a risk instead of a priority. */
function PriorityOrRisk({ row }: { row: WorkItemRow }) {
  if (row.kind === CHANGE_KIND) {
    return row.risk ? <RiskBadge risk={row.risk} /> : null;
  }

  return row.priority ? <PriorityBadge priority={row.priority} /> : null;
}

function WindowStart({ row }: { row: WorkItemRow }) {
  const { t } = useTranslation('tasks');
  const { t: tc } = useTranslation();
  return <Text type="supporting">{row.windowStart ? t('workspace.windowStart', { start: formatDateTime(row.windowStart) }) : tc('none')}</Text>;
}

/** A change has a maintenance window instead of a due date; the row carries only its start. */
function DueOrWindow({ row }: { row: WorkItemRow }) {
  const { t: tc } = useTranslation();
  if (row.kind === CHANGE_KIND) {
    return <WindowStart row={row} />;
  }

  return row.dueDate ? formatDate(row.dueDate) : tc('none');
}

export function WorkItemList() {
  const { t } = useTranslation('tasks');
  const [page, setPage] = useState(FIRST_PAGE);
  const items = useWorkItems({ queue: 'assigned_to_me', page, pageSize: PAGE_SIZE });
  const columns: DataColumn<WorkItemRow>[] = [
    { key: 'kind', header: '', renderCell: (x) => <KindToken kind={x.kind} /> },
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <NumberLink row={x} /> },
    { key: 'title', header: t('list.columns.title'), renderCell: (x) => x.title },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <StatusText row={x} /> },
    { key: 'priority', header: t('list.columns.priority'), renderCell: (x) => <PriorityOrRisk row={x} /> },
    { key: 'due', header: t('list.columns.due'), renderCell: (x) => <DueOrWindow row={x} /> },
    { key: 'team', header: t('list.columns.team'), renderCell: (x) => x.teamName },
  ];
  const renderCard = (x: WorkItemRow) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <NumberLink row={x} />
            <Text type="label" wordBreak="break-word">
              {x.title}
            </Text>
          </VStack>
        </StackItem>
        <StackItem>
          <KindToken kind={x.kind} />
        </StackItem>
      </HStack>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <PriorityOrRisk row={x} />
        <StatusText row={x} />
        {x.kind === CHANGE_KIND ? <WindowStart row={x} /> : x.dueDate && <Text type="supporting">{formatDate(x.dueDate)}</Text>}
      </HStack>
    </VStack>
  );
  return (
    <SectionCard title={t('workspace.allWork')} padding={0}>
      {items.isPending && <LoadingState rows={SKELETON_ROWS} />}
      {items.isError && <ErrorState error={items.error} onRetry={() => void items.refetch()} />}
      {items.data && items.data.items.length === 0 && <EmptyState message={t('workspace.empty')} />}
      {items.data && items.data.items.length > 0 && (
        <DataList items={items.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('workspace.allWorkCaption')} />
      )}
      {items.data && items.data.totalCount > items.data.pageSize && (
        <Pagination page={items.data.page} pageSize={items.data.pageSize} totalItems={items.data.totalCount} onChange={setPage} />
      )}
    </SectionCard>
  );
}
