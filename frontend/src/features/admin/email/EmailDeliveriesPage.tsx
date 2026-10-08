import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { formatDateTime } from '../../../shared/lib/format';
import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { Button } from '../../../shared/ui/Button';
import { FormAlert } from '../../../shared/ui/FormAlert';
import { Pagination } from '../../../shared/ui/Pagination';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { SelectField } from '../../../shared/ui/SelectField';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';
import { TextField } from '../../../shared/ui/TextField';

import { useEmailDeliveries, useRetryEmailDelivery, type DeliveryStatus, type EmailDeliveryItem } from './api';
import { EMAIL_EVENT_TYPES, isEmailEventType } from './eventTypes';

const PAGE_SIZE = 20;
const FIRST_PAGE = 1;
const SEARCH_DEBOUNCE_MS = 300;
const ALL = '';
const DELIVERY_STATUSES = ['pending', 'sending', 'sent', 'failed'] as const satisfies readonly DeliveryStatus[];
const RETRYABLE_STATUS: DeliveryStatus = 'failed';

const STATUS_CLASS: Record<DeliveryStatus, string> = {
  pending: 'bg-secondary-subtle text-secondary-emphasis',
  sending: 'bg-info-subtle text-info-emphasis',
  sent: 'bg-success-subtle text-success-emphasis',
  failed: 'bg-danger-subtle text-danger-emphasis',
};

function DeliveryStatusBadge({ status }: { status: DeliveryStatus }) {
  const { t } = useTranslation('admin');
  return <span className={`badge rounded-pill fw-medium ${STATUS_CLASS[status]}`}>{t(`emailDeliveries.statuses.${status}`)}</span>;
}

export function EmailDeliveriesPage() {
  const { t } = useTranslation('admin');
  const [status, setStatus] = useState<DeliveryStatus | typeof ALL>(ALL);
  const [eventType, setEventType] = useState(ALL);
  const [recipient, setRecipient] = useState('');
  const [page, setPage] = useState(FIRST_PAGE);
  const debouncedRecipient = useDebouncedValue(recipient.trim(), SEARCH_DEBOUNCE_MS);
  const deliveries = useEmailDeliveries({
    status: status === ALL ? undefined : status,
    eventType: eventType === ALL ? undefined : eventType,
    recipient: debouncedRecipient === '' ? undefined : debouncedRecipient,
    page,
    pageSize: PAGE_SIZE,
  });
  const retry = useRetryEmailDelivery();
  const eventLabel = (type: string) => (isEmailEventType(type) ? t(`emailEvents.${type}`) : type);

  const retryButton = (delivery: EmailDeliveryItem) =>
    delivery.status === RETRYABLE_STATUS ? (
      <Button
        aria-label={recordActionLabel(t('emailDeliveries.retry'), delivery.recipientEmail)}
        variant="secondary"
        isLoading={retry.isPending && retry.variables === delivery.id}
        onClick={() => retry.mutate(delivery.id)}
      >
        {t('emailDeliveries.retry')}
      </Button>
    ) : null;

  const columns: Column<EmailDeliveryItem>[] = [
    { key: 'recipient', header: t('emailDeliveries.columns.recipient'), renderCell: (delivery) => <span className="text-break">{delivery.recipientEmail}</span> },
    { key: 'event', header: t('emailDeliveries.columns.event'), renderCell: (delivery) => eventLabel(delivery.eventType) },
    { key: 'subject', header: t('emailDeliveries.columns.subject'), renderCell: (delivery) => delivery.subject },
    { key: 'status', header: t('emailDeliveries.columns.status'), renderCell: (delivery) => <DeliveryStatusBadge status={delivery.status} /> },
    { key: 'attempts', header: t('emailDeliveries.columns.attempts'), renderCell: (delivery) => delivery.attempts },
    { key: 'error', header: t('emailDeliveries.columns.error'), renderCell: (delivery) => delivery.lastError ?? '' },
    { key: 'date', header: t('emailDeliveries.columns.date'), renderCell: (delivery) => formatDateTime(delivery.eventCreatedAt) },
    { key: 'actions', header: t('emailDeliveries.columns.actions'), renderCell: retryButton },
  ];

  const renderCard = (delivery: EmailDeliveryItem) => (
    <div className="d-flex flex-column gap-2">
      <div className="d-flex align-items-start justify-content-between gap-2">
        <p className="mb-0 min-w-0 fw-medium text-break">{delivery.recipientEmail}</p>
        <DeliveryStatusBadge status={delivery.status} />
      </div>
      <p className="mb-0 small">
        {eventLabel(delivery.eventType)} · {delivery.subject}
      </p>
      {delivery.lastError && <p className="mb-0 small text-danger">{delivery.lastError}</p>}
      <p className="mb-0 small text-body-secondary">
        {formatDateTime(delivery.eventCreatedAt)} · {t('emailDeliveries.columns.attempts')}: {delivery.attempts}
      </p>
      {retryButton(delivery)}
    </div>
  );

  const resetPage = () => setPage(FIRST_PAGE);

  return (
    <section className="d-flex flex-column gap-3">
      <PageHeader titleOverride={t('emailDeliveries.title')} />

      <div className="tgysm-grid tgysm-grid-3">
        <SelectField
          label={t('emailDeliveries.filters.status')}
          value={status}
          options={DELIVERY_STATUSES.map((value) => ({ value, label: t(`emailDeliveries.statuses.${value}`) }))}
          placeholder={t('emailDeliveries.statuses.all')}
          onChange={(event) => {
            setStatus(event.target.value as DeliveryStatus | typeof ALL);
            resetPage();
          }}
        />
        <SelectField
          label={t('emailDeliveries.filters.event')}
          value={eventType}
          options={EMAIL_EVENT_TYPES.map((value) => ({ value, label: t(`emailEvents.${value}`) }))}
          placeholder={t('emailDeliveries.allEvents')}
          onChange={(event) => {
            setEventType(event.target.value);
            resetPage();
          }}
        />
        <TextField
          type="search"
          label={t('emailDeliveries.filters.recipient')}
          value={recipient}
          onChange={(event) => {
            setRecipient(event.target.value);
            resetPage();
          }}
        />
      </div>

      <FormAlert message={retry.error ? describeError(retry.error) : null} />
      {deliveries.isPending && <LoadingState />}
      {deliveries.isError && <ErrorState error={deliveries.error} onRetry={() => void deliveries.refetch()} />}
      {deliveries.data && deliveries.data.items.length === 0 && <EmptyState />}
      {deliveries.data && deliveries.data.items.length > 0 && (
        <>
          <ResponsiveList items={deliveries.data.items} columns={columns} getKey={(delivery) => delivery.id} renderCard={renderCard} caption={t('emailDeliveries.listCaption')} />
          <Pagination page={deliveries.data.page} pageSize={deliveries.data.pageSize} totalCount={deliveries.data.totalCount} onPageChange={setPage} />
        </>
      )}
    </section>
  );
}
