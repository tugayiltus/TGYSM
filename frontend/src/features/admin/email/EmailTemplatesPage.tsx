import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../../shared/lib/format';
import { Button } from '../../../shared/ui/Button';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { ErrorState, LoadingState } from '../../../shared/ui/States';

import { useEmailTemplates, type EmailTemplateItem } from './api';
import { EmailTemplateEditor } from './EmailTemplateEditor';
import { isEmailEventType } from './eventTypes';

function useEventLabel() {
  const { t } = useTranslation('admin');
  return (eventType: string) => (isEmailEventType(eventType) ? t(`emailEvents.${eventType}`) : eventType);
}

export function EmailTemplatesPage() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const templates = useEmailTemplates();
  const eventLabel = useEventLabel();
  const [editingEventType, setEditingEventType] = useState<string | null>(null);
  const editing = templates.data?.find((template) => template.eventType === editingEventType) ?? null;

  const editButton = (template: EmailTemplateItem) => (
    <Button aria-label={recordActionLabel(tc('actions.edit'), eventLabel(template.eventType))} variant="secondary" onClick={() => setEditingEventType(template.eventType)}>
      {tc('actions.edit')}
    </Button>
  );

  const columns: Column<EmailTemplateItem>[] = [
    { key: 'event', header: t('emailTemplates.columns.event'), renderCell: (template) => <span className="font-medium">{eventLabel(template.eventType)}</span> },
    { key: 'subject', header: t('emailTemplates.columns.subject'), renderCell: (template) => template.subject },
    { key: 'updated', header: t('emailTemplates.columns.updated'), renderCell: (template) => formatDateTime(template.updatedAt) },
    { key: 'actions', header: t('emailTemplates.columns.actions'), renderCell: editButton },
  ];

  const renderCard = (template: EmailTemplateItem) => (
    <div className="flex flex-col gap-2">
      <p className="font-medium">{eventLabel(template.eventType)}</p>
      <p className="text-sm text-slate-700">{template.subject}</p>
      <p className="text-sm text-slate-600">{formatDateTime(template.updatedAt)}</p>
      {editButton(template)}
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('emailTemplates.title')}</h1>
      {templates.isPending && <LoadingState />}
      {templates.isError && <ErrorState error={templates.error} onRetry={() => void templates.refetch()} />}
      {templates.data && <ResponsiveList items={templates.data} columns={columns} getKey={(template) => template.id} renderCard={renderCard} caption={t('emailTemplates.listCaption')} />}
      {editing !== null && <EmailTemplateEditor key={`${editing.eventType}-${editing.version}`} template={editing} onClose={() => setEditingEventType(null)} />}
    </section>
  );
}
