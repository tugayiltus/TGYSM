import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { translateOr } from '../../../shared/i18n/dynamic';
import { formatDateTime } from '../../../shared/lib/format';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';

import { useAuditEvents, type AuditEventItem } from './api';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const ALL_ENTITIES = '';
const JSON_INDENT = 2;
/** Mirrors backend AuditEntityTypes. */
const ENTITY_TYPES = ['user', 'department', 'location', 'team'] as const;

const actionLabel = (action: string) => translateOr(`admin:auditActions.${action}`, action);
const entityLabel = (entityType: string) => translateOr(`admin:entityTypes.${entityType}`, entityType);

function prettyJson(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  try {
    return JSON.stringify(JSON.parse(value), null, JSON_INDENT);
  } catch {
    return value;
  }
}

function AuditDetails({ event }: { event: AuditEventItem }) {
  const { t } = useTranslation('admin');
  const before = prettyJson(event.oldValues);
  const after = prettyJson(event.newValues);
  return (
    <details className="text-sm">
      <summary className="cursor-pointer text-brand-700">{t('audit.details')}</summary>
      <div className="mt-2 flex flex-col gap-2">
        {before && (
          <div>
            <p className="font-medium">{t('audit.before')}</p>
            <pre className="overflow-x-auto rounded-lg bg-slate-100 p-2 text-xs">{before}</pre>
          </div>
        )}
        {after && (
          <div>
            <p className="font-medium">{t('audit.after')}</p>
            <pre className="overflow-x-auto rounded-lg bg-slate-100 p-2 text-xs">{after}</pre>
          </div>
        )}
        <p className="text-xs break-all text-slate-600">
          {t('audit.correlation')}: {event.correlationId}
        </p>
      </div>
    </details>
  );
}

export function AuditPage() {
  const { t } = useTranslation('admin');
  const [entityType, setEntityType] = useState<string>(ALL_ENTITIES);
  const [page, setPage] = useState(FIRST_PAGE);
  const events = useAuditEvents({ entityType: entityType === ALL_ENTITIES ? undefined : entityType, page, pageSize: PAGE_SIZE });

  const actorName = (event: AuditEventItem) => event.actorName ?? t('audit.system');

  const columns: Column<AuditEventItem>[] = [
    { key: 'time', header: t('audit.columns.time'), renderCell: (event) => <span className="whitespace-nowrap">{formatDateTime(event.occurredAt)}</span> },
    { key: 'actor', header: t('audit.columns.actor'), renderCell: actorName },
    { key: 'action', header: t('audit.columns.action'), renderCell: (event) => actionLabel(event.action) },
    { key: 'entity', header: t('audit.columns.entity'), renderCell: (event) => entityLabel(event.entityType) },
    { key: 'details', header: t('audit.details'), renderCell: (event) => <AuditDetails event={event} /> },
  ];

  const renderCard = (event: AuditEventItem) => (
    <div className="flex flex-col gap-1">
      <p className="font-medium">{actionLabel(event.action)}</p>
      <p className="text-sm text-slate-600">
        {formatDateTime(event.occurredAt)} · {actorName(event)} · {entityLabel(event.entityType)}
      </p>
      <AuditDetails event={event} />
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('audit.title')}</h1>
      <SelectField
        className="sm:max-w-xs"
        label={t('audit.entityFilter')}
        value={entityType}
        options={[{ value: ALL_ENTITIES, label: t('audit.allEntities') }, ...ENTITY_TYPES.map((type) => ({ value: type, label: entityLabel(type) }))]}
        onChange={(event) => {
          setEntityType(event.target.value);
          setPage(FIRST_PAGE);
        }}
      />

      {events.isPending && <LoadingState />}
      {events.isError && <ErrorState error={events.error} onRetry={() => void events.refetch()} />}
      {events.data && events.data.items.length === 0 && <EmptyState />}
      {events.data && events.data.items.length > 0 && (
        <>
          <ResponsiveList items={events.data.items} columns={columns} getKey={(event) => event.id} renderCard={renderCard} caption={t('audit.listCaption')} />
          <Pagination page={events.data.page} pageSize={events.data.pageSize} totalCount={events.data.totalCount} onPageChange={setPage} />
        </>
      )}
    </section>
  );
}
