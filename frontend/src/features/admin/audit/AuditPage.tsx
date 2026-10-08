import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { translateOr } from '../../../shared/i18n/dynamic';
import adminTexts from '../../../shared/i18n/locales/tr/admin.json';
import { formatDateTime } from '../../../shared/lib/format';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { Pagination } from '../../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';

import { useAuditEvents, type AuditEventItem } from './api';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const ALL_ENTITIES = '';
const JSON_INDENT = 2;
/** Every audited entity type: TranslationCoverageTests guarantees each backend AuditEntityTypes value has a text here. */
const ENTITY_TYPES = Object.keys(adminTexts.entityTypes);

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
    <details className="small">
      <summary className="link-primary">{t('audit.details')}</summary>
      <div className="mt-2 d-flex flex-column gap-2">
        {before && (
          <div>
            <p className="mb-0 fw-medium">{t('audit.before')}</p>
            <pre className="overflow-x-auto rounded bg-body-secondary p-2 small">{before}</pre>
          </div>
        )}
        {after && (
          <div>
            <p className="mb-0 fw-medium">{t('audit.after')}</p>
            <pre className="overflow-x-auto rounded bg-body-secondary p-2 small">{after}</pre>
          </div>
        )}
        <p className="mb-0 small text-break text-body-secondary">
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
    { key: 'time', header: t('audit.columns.time'), renderCell: (event) => <span className="text-nowrap">{formatDateTime(event.occurredAt)}</span> },
    { key: 'actor', header: t('audit.columns.actor'), renderCell: actorName },
    { key: 'action', header: t('audit.columns.action'), renderCell: (event) => actionLabel(event.action) },
    { key: 'entity', header: t('audit.columns.entity'), renderCell: (event) => entityLabel(event.entityType) },
    { key: 'details', header: t('audit.details'), renderCell: (event) => <AuditDetails event={event} /> },
  ];

  const renderCard = (event: AuditEventItem) => (
    <div className="d-flex flex-column gap-1">
      <p className="mb-0 fw-medium">{actionLabel(event.action)}</p>
      <p className="mb-0 small text-body-secondary">
        {formatDateTime(event.occurredAt)} · {actorName(event)} · {entityLabel(event.entityType)}
      </p>
      <AuditDetails event={event} />
    </div>
  );

  return (
    <section className="d-flex flex-column gap-3">
      <PageHeader titleOverride={t('audit.title')} />
      <SelectField
       
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
