/**
 * @input Nothing; the search text comes from the URL query.
 * @output The email template list per event, with an edit dialog.
 * @position `/admin/email-templates`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { formatDateTime } from '../../../shared/lib/format';
import { toSearchKey } from '../../../shared/lib/turkish';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import { useEmailTemplates, type EmailTemplateItem } from './api';
import { EmailTemplateEditor } from './EmailTemplateEditor';
import { isEmailEventType } from './eventTypes';

const SKELETON_ROWS = 5;
const NO_FILTERS = [] as const;

function useEventLabel() {
  const { t } = useTranslation('admin');
  return (eventType: string) => (isEmailEventType(eventType) ? t(`emailEvents.${eventType}`) : eventType);
}

export function EmailTemplatesPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const list = useListParams(NO_FILTERS);
  const templates = useEmailTemplates();
  const eventLabel = useEventLabel();
  const [editingEventType, setEditingEventType] = useState<string | null>(null);
  const editing = templates.data?.find((template) => template.eventType === editingEventType) ?? null;
  const searchKey = toSearchKey(list.search);
  const rows = templates.data?.filter((template) => searchKey === '' || toSearchKey(`${eventLabel(template.eventType)} ${template.subject}`).includes(searchKey));

  const editButton = (template: EmailTemplateItem) => (
    <Button
      label={tc('actions.edit')}
      aria-label={recordActionLabel(tc('actions.edit'), eventLabel(template.eventType))}
      size="sm"
      variant="secondary"
      onClick={() => setEditingEventType(template.eventType)}
    />
  );

  const columns: DataColumn<EmailTemplateItem>[] = [
    { key: 'event', header: t('emailTemplates.columns.event'), renderCell: (template) => <Text type="label">{eventLabel(template.eventType)}</Text> },
    { key: 'subject', header: t('emailTemplates.columns.subject'), renderCell: (template) => template.subject },
    { key: 'updated', header: t('emailTemplates.columns.updated'), renderCell: (template) => formatDateTime(template.updatedAt) },
    { key: 'actions', header: t('emailTemplates.columns.actions'), renderCell: editButton },
  ];

  const renderCard = (template: EmailTemplateItem) => (
    <VStack gap={2}>
      <VStack gap={0}>
        <Text type="label">{eventLabel(template.eventType)}</Text>
        <Text type="body" wordBreak="break-word">
          {template.subject}
        </Text>
        <Text type="supporting">{formatDateTime(template.updatedAt)}</Text>
      </VStack>
      {editButton(template)}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminEmailTemplates.icon} titleOverride={t('emailTemplates.title')} />
      <ListToolbar
        title={t('emailTemplates.title')}
        search={list.search}
        searchLabel={t('emailTemplates.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
      />
      <Card padding={0}>
        {templates.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {templates.isError && <ErrorState error={templates.error} onRetry={() => void templates.refetch()} />}
        {rows?.length === 0 && <EmptyState />}
        {rows !== undefined && rows.length > 0 && (
          <DataList items={rows} columns={columns} getKey={(template) => template.id} renderCard={renderCard} caption={t('emailTemplates.listCaption')} />
        )}
      </Card>
      {editing !== null && <EmailTemplateEditor key={`${editing.eventType}-${editing.version}`} template={editing} onClose={() => setEditingEventType(null)} />}
    </VStack>
  );
}
