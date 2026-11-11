/**
 * @input Nothing; entity type and page come from the URL query.
 * @output The audit event list with Turkish action names and the old and new values per event.
 * @position `/admin/audit`, inside the admin shell.
 */
import { Card } from '@astryxdesign/core/Card';
import { CodeBlock } from '@astryxdesign/core/CodeBlock';
import { Collapsible } from '@astryxdesign/core/Collapsible';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { translateOr } from '../../../shared/i18n/dynamic';
import adminTexts from '../../../shared/i18n/locales/tr/admin.json';
import { formatDateTime } from '../../../shared/lib/format';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';

import { useAuditEvents, type AuditEventItem } from './api';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const ENTITY_FILTER_KEY = 'entity';
const FILTER_KEYS = [ENTITY_FILTER_KEY] as const;
const META_SEPARATOR = ' · ';
const CODE_MAX_HEIGHT = 240;
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
    <Collapsible defaultIsOpen={false} trigger={<Text type="label">{t('audit.details')}</Text>}>
      <VStack gap={2}>
        {before && <CodeBlock title={t('audit.before')} code={before} language="json" hasLanguageLabel={false} isWrapped maxHeight={CODE_MAX_HEIGHT} />}
        {after && <CodeBlock title={t('audit.after')} code={after} language="json" hasLanguageLabel={false} isWrapped maxHeight={CODE_MAX_HEIGHT} />}
        <Text type="supporting" wordBreak="break-all">{`${t('audit.correlation')}: ${event.correlationId}`}</Text>
      </VStack>
    </Collapsible>
  );
}

export function AuditPage() {
  const { t } = useTranslation('admin');
  const list = useListParams(FILTER_KEYS);
  const entityType = list.filters.entity;
  const events = useAuditEvents({ entityType: entityType === '' ? undefined : entityType, page: list.page, pageSize: PAGE_SIZE });

  const actorName = (event: AuditEventItem) => event.actorName ?? t('audit.system');

  const columns: DataColumn<AuditEventItem>[] = [
    { key: 'time', header: t('audit.columns.time'), renderCell: (event) => formatDateTime(event.occurredAt) },
    { key: 'actor', header: t('audit.columns.actor'), renderCell: actorName },
    { key: 'action', header: t('audit.columns.action'), renderCell: (event) => <Text type="label">{actionLabel(event.action)}</Text> },
    { key: 'entity', header: t('audit.columns.entity'), renderCell: (event) => entityLabel(event.entityType) },
    { key: 'details', header: t('audit.details'), renderCell: (event) => <AuditDetails event={event} /> },
  ];

  const renderCard = (event: AuditEventItem) => (
    <VStack gap={2}>
      <VStack gap={0}>
        <Text type="label">{actionLabel(event.action)}</Text>
        <Text type="supporting">{[formatDateTime(event.occurredAt), actorName(event), entityLabel(event.entityType)].join(META_SEPARATOR)}</Text>
      </VStack>
      <AuditDetails event={event} />
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminAudit.icon} titleOverride={t('audit.title')} />
      <ListToolbar
        title={t('audit.title')}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <Selector
            label={t('audit.entityFilter')}
            placeholder={t('audit.allEntities')}
            hasSearch
            value={entityType}
            options={ENTITY_TYPES.map((type) => ({ value: type, label: entityLabel(type) }))}
            onChange={(value) => list.setFilter(ENTITY_FILTER_KEY, value)}
          />
        }
      />
      <Card padding={0}>
        {events.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {events.isError && <ErrorState error={events.error} onRetry={() => void events.refetch()} />}
        {events.data && events.data.items.length === 0 && <EmptyState />}
        {events.data && events.data.items.length > 0 && (
          <DataList items={events.data.items} columns={columns} getKey={(event) => event.id} renderCard={renderCard} caption={t('audit.listCaption')} />
        )}
      </Card>
      {events.data && events.data.items.length > 0 && (
        <Pagination page={events.data.page} pageSize={events.data.pageSize} totalItems={events.data.totalCount} onChange={list.setPage} />
      )}
    </VStack>
  );
}
