/**
 * @input Nothing; it reads the signed-in person's open work.
 * @output One list of the open tickets and tasks assigned to the person, newest first, with paging.
 * @position IT workspace (İş listem), below the queue cards.
 */
import { HStack } from '@astryxdesign/core/HStack';
import { Pagination } from '@astryxdesign/core/Pagination';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate } from '../../shared/lib/format';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { SectionCard } from '../../shared/page/SectionCard';
import { taskPath, ticketPath } from '../../shared/routes';
import { PriorityBadge } from '../tickets/TicketBadges';

import { useWorkItems, type WorkItemRow } from './api';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SKELETON_ROWS = 3;
const TASK_KIND = 'task';
type Kind = 'incident' | 'request' | 'task';

const pathOf = (row: WorkItemRow) => (row.kind === TASK_KIND ? taskPath(row.number) : ticketPath(row.number));

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
  return <Token size="sm" color={kind === TASK_KIND ? 'purple' : 'gray'} label={t(`workspace.kinds.${kind as Kind}`)} />;
}

/** Ticket and task statuses share their names, so one label set reads both. */
function StatusText({ status }: { status: string }) {
  const { t } = useTranslation('tasks');
  return <Text>{t(`statuses.${status as 'open'}`)}</Text>;
}

export function WorkItemList() {
  const { t } = useTranslation('tasks');
  const { t: tc } = useTranslation();
  const [page, setPage] = useState(FIRST_PAGE);
  const items = useWorkItems({ queue: 'assigned_to_me', page, pageSize: PAGE_SIZE });
  const columns: DataColumn<WorkItemRow>[] = [
    { key: 'kind', header: '', renderCell: (x) => <KindToken kind={x.kind} /> },
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <NumberLink row={x} /> },
    { key: 'title', header: t('list.columns.title'), renderCell: (x) => x.title },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <StatusText status={x.status} /> },
    { key: 'priority', header: t('list.columns.priority'), renderCell: (x) => <PriorityBadge priority={x.priority} /> },
    { key: 'due', header: t('list.columns.due'), renderCell: (x) => (x.dueDate ? formatDate(x.dueDate) : tc('none')) },
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
        <PriorityBadge priority={x.priority} />
        <StatusText status={x.status} />
        {x.dueDate && <Text type="supporting">{formatDate(x.dueDate)}</Text>}
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
