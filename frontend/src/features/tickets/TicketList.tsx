/**
 * @input The queue that fixes the scope (mine, team …) and whether IT work columns show.
 * @output The ticket list with search, status and priority filters in the URL, and paging.
 * @position "Kayıtlarım" and the IT queue pages.
 */
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { linkState } from '../../shared/navigation/returnTo';
import { StateLink } from '../../shared/navigation/StateLink';
import { ticketPath } from '../../shared/routes';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';

import { PRIORITIES, TICKET_STATUSES, useTickets, type TicketListItem, type TicketQueue } from './api';
import { SlaSummary } from './SlaBadge';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['status', 'priority'] as const;

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

function OpenLink({ ticket }: { ticket: TicketListItem }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  return (
    <StateLink href={ticketPath(ticket.number)} state={linkState(location)} label={recordActionLabel(t('list.open'), ticket.number)}>
      {t('list.open')}
    </StateLink>
  );
}

export function TicketList({ queue, showWorkColumns }: { queue: TicketQueue | undefined; showWorkColumns: boolean }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const status = pick(TICKET_STATUSES, list.filters.status);
  const priority = pick(PRIORITIES, list.filters.priority);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const tickets = useTickets({ queue, status, kind: undefined, priority, search, page: list.page, pageSize: PAGE_SIZE });

  const columns: DataColumn<TicketListItem>[] = [
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <Text type="label">{x.number}</Text> },
    { key: 'subject', header: t('list.columns.subject'), renderCell: (x) => x.subject },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <TicketStatusBadge status={x.status} /> },
    ...(showWorkColumns
      ? [
          { key: 'priority', header: t('list.columns.priority'), renderCell: (x: TicketListItem) => <PriorityBadge priority={x.priority} /> },
          { key: 'assignee', header: t('list.columns.assignee'), renderCell: (x: TicketListItem) => x.assigneeName ?? tc('none') },
          { key: 'sla', header: t('sla.listColumn'), renderCell: (x: TicketListItem) => <SlaSummary sla={x.sla} /> },
        ]
      : []),
    { key: 'updated', header: t('list.columns.updated'), renderCell: (x) => formatDateTime(x.updatedAt) },
    { key: 'open', header: '', renderCell: (x) => <OpenLink ticket={x} /> },
  ];

  const renderCard = (x: TicketListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <VStack gap={0}>
          <Text type="supporting">{x.number}</Text>
          <Text type="label" wordBreak="break-word">
            {x.subject}
          </Text>
        </VStack>
        <TicketStatusBadge status={x.status} />
      </HStack>
      {showWorkColumns && (
        <HStack gap={2} vAlign="center" wrap="wrap">
          <PriorityBadge priority={x.priority} />
          <Text type="supporting">{x.assigneeName ?? x.teamName}</Text>
          <SlaSummary sla={x.sla} />
        </HStack>
      )}
      <OpenLink ticket={x} />
    </VStack>
  );

  const option = (value: string | undefined) => value ?? '';
  return (
    <VStack gap={3}>
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
              label={t('list.statusFilter')}
              placeholder={t('list.all')}
              options={TICKET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={option(status)}
              onChange={(value) => list.setFilter('status', value)}
            />
            {showWorkColumns && (
              <Selector
                label={t('list.priorityFilter')}
                placeholder={t('list.all')}
                options={PRIORITIES.map((value) => ({ value, label: t(`priorities.${value}`) }))}
                value={option(priority)}
                onChange={(value) => list.setFilter('priority', value)}
              />
            )}
          </>
        }
      />
      <Card padding={0}>
        {tickets.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
        {tickets.data && tickets.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
        {tickets.data && tickets.data.items.length > 0 && (
          <DataList items={tickets.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {tickets.data && tickets.data.items.length > 0 && <Pagination page={tickets.data.page} pageSize={tickets.data.pageSize} totalItems={tickets.data.totalCount} onChange={list.setPage} />}
    </VStack>
  );
}
