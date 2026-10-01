import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { ticketPath } from '../../shared/routes';
import { Pagination } from '../../shared/ui/Pagination';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { TextField } from '../../shared/ui/TextField';

import { PRIORITIES, TICKET_STATUSES, useTickets, type Priority, type TicketListItem, type TicketQueue, type TicketStatus } from './api';
import { SlaSummary } from './SlaBadge';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;
const ALL = '';

const LINK_BUTTON_CLASS =
  'inline-flex min-h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-800 hover:bg-slate-50';

function OpenLink({ ticket }: { ticket: TicketListItem }) {
  const { t } = useTranslation('tickets');
  return (
    <Link to={ticketPath(ticket.number)} className={LINK_BUTTON_CLASS} aria-label={recordActionLabel(t('list.open'), ticket.number)}>
      {t('list.open')}
    </Link>
  );
}

/** Ticket list with search, status and priority filters; the queue fixes the scope (mine, team, …). */
export function TicketList({ queue, showWorkColumns }: { queue: TicketQueue | undefined; showWorkColumns: boolean }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<TicketStatus | typeof ALL>(ALL);
  const [priority, setPriority] = useState<Priority | typeof ALL>(ALL);
  const [page, setPage] = useState(FIRST_PAGE);
  const debouncedSearch = useDebouncedValue(search, SEARCH_DEBOUNCE_MS);
  const tickets = useTickets({
    queue,
    status: status === ALL ? undefined : status,
    kind: undefined,
    priority: priority === ALL ? undefined : priority,
    search: debouncedSearch,
    page,
    pageSize: PAGE_SIZE,
  });

  const columns: Column<TicketListItem>[] = [
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <span className="font-medium whitespace-nowrap">{x.number}</span> },
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
    <div className="flex flex-col gap-2">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm text-slate-600">{x.number}</p>
          <p className="font-medium break-words">{x.subject}</p>
        </div>
        <TicketStatusBadge status={x.status} />
      </div>
      {showWorkColumns && (
        <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
          <PriorityBadge priority={x.priority} />
          <span>{x.assigneeName ?? x.teamName}</span>
          <SlaSummary sla={x.sla} />
        </div>
      )}
      <OpenLink ticket={x} />
    </div>
  );

  const reset = () => setPage(FIRST_PAGE);

  return (
    <div className="flex flex-col gap-4">
      <div className={`grid gap-3 ${showWorkColumns ? 'sm:grid-cols-3' : 'sm:grid-cols-2'}`}>
        <TextField
          type="search"
          label={t('list.search')}
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            reset();
          }}
        />
        <SelectField
          label={t('list.statusFilter')}
          placeholder={t('list.all')}
          options={TICKET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
          value={status}
          onChange={(event) => {
            setStatus(event.target.value as TicketStatus | typeof ALL);
            reset();
          }}
        />
        {showWorkColumns && (
          <SelectField
            label={t('list.priorityFilter')}
            placeholder={t('list.all')}
            options={PRIORITIES.map((value) => ({ value, label: t(`priorities.${value}`) }))}
            value={priority}
            onChange={(event) => {
              setPriority(event.target.value as Priority | typeof ALL);
              reset();
            }}
          />
        )}
      </div>
      {tickets.isPending && <LoadingState />}
      {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
      {tickets.data && tickets.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
      {tickets.data && tickets.data.items.length > 0 && (
        <>
          <ResponsiveList items={tickets.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('list.caption')} />
          <Pagination page={tickets.data.page} pageSize={tickets.data.pageSize} totalCount={tickets.data.totalCount} onPageChange={setPage} />
        </>
      )}
    </div>
  );
}
