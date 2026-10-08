import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useSearchParams } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { readPageParam } from '../../shared/lib/pageParam';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { linkState } from '../../shared/navigation/returnTo';
import { ticketPath } from '../../shared/routes';
import { Pagination } from '../../shared/ui/Pagination';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { SelectField } from '../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { TextField } from '../../shared/ui/TextField';

import { PRIORITIES, TICKET_STATUSES, useTickets, type TicketListItem, type TicketQueue } from './api';
import { SlaSummary } from './SlaBadge';
import { PriorityBadge, TicketStatusBadge } from './TicketBadges';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;
const ALL = '';
/** Filters live in the URL so a ticket opened from the list can return to the same filtered view (spec §4.5). */
const PARAM = { search: 'q', status: 'status', priority: 'priority', page: 'page' } as const;

function OpenLink({ ticket }: { ticket: TicketListItem }) {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  return (
    <Link
      to={ticketPath(ticket.number)}
      state={linkState(location)}
      className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center"
      aria-label={recordActionLabel(t('list.open'), ticket.number)}
    >
      {t('list.open')}
    </Link>
  );
}

/** Ticket list with search, status and priority filters; the queue fixes the scope (mine, team, …). */
export function TicketList({ queue, showWorkColumns }: { queue: TicketQueue | undefined; showWorkColumns: boolean }) {
  const { t } = useTranslation('tickets');
  const { t: tc } = useTranslation();
  const [params, setParams] = useSearchParams();
  const status = TICKET_STATUSES.find((value) => value === params.get(PARAM.status)) ?? ALL;
  const priority = PRIORITIES.find((value) => value === params.get(PARAM.priority)) ?? ALL;
  const page = readPageParam(params.get(PARAM.page));
  // The search box is typed into locally and written to the URL; it starts from the URL value.
  const [search, setSearchText] = useState(params.get(PARAM.search) ?? '');
  const setFilter = (key: string, value: string) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value === '') {
          next.delete(key);
        } else {
          next.set(key, value);
        }
        next.delete(PARAM.page);
        return next;
      },
      { replace: true },
    );
  const setPage = (next: number) =>
    setParams(
      (current) => {
        const updated = new URLSearchParams(current);
        updated.set(PARAM.page, String(next));
        return updated;
      },
      { replace: true },
    );
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
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <span className="fw-semibold text-nowrap">{x.number}</span> },
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
    <div className="d-flex flex-column gap-2">
      <div className="d-flex align-items-start justify-content-between gap-2">
        <div className="min-w-0">
          <p className="small text-body-secondary mb-0">{x.number}</p>
          <p className="fw-semibold text-break mb-0">{x.subject}</p>
        </div>
        <TicketStatusBadge status={x.status} />
      </div>
      {showWorkColumns && (
        <div className="d-flex flex-wrap align-items-center gap-2 small text-body-secondary">
          <PriorityBadge priority={x.priority} />
          <span>{x.assigneeName ?? x.teamName}</span>
          <SlaSummary sla={x.sla} />
        </div>
      )}
      <OpenLink ticket={x} />
    </div>
  );

  return (
    <div className="d-flex flex-column gap-3">
      <div className="row g-3">
        <TextField
          className={showWorkColumns ? 'col-12 col-md-4' : 'col-12 col-md-6'}
          type="search"
          label={t('list.search')}
          value={search}
          onChange={(event) => {
            setSearchText(event.target.value);
            setFilter(PARAM.search, event.target.value);
          }}
        />
        <SelectField
          label={t('list.statusFilter')}
          placeholder={t('list.all')}
          options={TICKET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
          className={showWorkColumns ? 'col-12 col-md-4' : 'col-12 col-md-6'}
          value={status}
          onChange={(event) => setFilter(PARAM.status, event.target.value)}
        />
        {showWorkColumns && (
          <SelectField
            label={t('list.priorityFilter')}
            placeholder={t('list.all')}
            options={PRIORITIES.map((value) => ({ value, label: t(`priorities.${value}`) }))}
            className="col-12 col-md-4"
            value={priority}
            onChange={(event) => setFilter(PARAM.priority, event.target.value)}
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
