/**
 * @input Nothing; recipient search, status, event and page come from the URL query.
 * @output The email delivery log with a retry action for failed deliveries.
 * @position `/admin/email-deliveries`, inside the admin shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import type { ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import { describeError } from '../../../shared/api/errorMessage';
import { FormAlert } from '../../../shared/feedback/FormAlert';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { formatDateTime } from '../../../shared/lib/format';
import { useDebouncedValue } from '../../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { ListToolbar } from '../../../shared/list/ListToolbar';
import { useListParams } from '../../../shared/list/useListParams';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { recordActionLabel } from '../../../shared/ui/recordActionLabel';

import { useEmailDeliveries, useRetryEmailDelivery, type DeliveryStatus, type EmailDeliveryItem } from './api';
import { EMAIL_EVENT_TYPES, isEmailEventType } from './eventTypes';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const STATUS_FILTER_KEY = 'status';
const EVENT_FILTER_KEY = 'event';
const FILTER_KEYS = [STATUS_FILTER_KEY, EVENT_FILTER_KEY] as const;
const META_SEPARATOR = ' · ';
const DELIVERY_STATUSES = ['pending', 'sending', 'sent', 'failed'] as const satisfies readonly DeliveryStatus[];
const RETRYABLE_STATUS: DeliveryStatus = 'failed';

const STATUS_COLOR: Record<DeliveryStatus, ComponentProps<typeof Token>['color']> = {
  pending: 'gray',
  sending: 'blue',
  sent: 'green',
  failed: 'red',
};

const readStatus = (value: string): DeliveryStatus | undefined => DELIVERY_STATUSES.find((status) => status === value);

function DeliveryStatusToken({ status }: { status: DeliveryStatus }) {
  const { t } = useTranslation('admin');
  return <Token label={t(`emailDeliveries.statuses.${status}`)} color={STATUS_COLOR[status]} size="sm" />;
}

export function EmailDeliveriesPage() {
  const { t } = useTranslation('admin');
  const list = useListParams(FILTER_KEYS);
  const status = readStatus(list.filters.status);
  const eventType = list.filters.event;
  const recipient = useDebouncedValue(list.search.trim(), SEARCH_DEBOUNCE_MS);
  const deliveries = useEmailDeliveries({
    status,
    eventType: eventType === '' ? undefined : eventType,
    recipient: recipient === '' ? undefined : recipient,
    page: list.page,
    pageSize: PAGE_SIZE,
  });
  const retry = useRetryEmailDelivery();
  const eventLabel = (type: string) => (isEmailEventType(type) ? t(`emailEvents.${type}`) : type);

  const retryButton = (delivery: EmailDeliveryItem) =>
    delivery.status === RETRYABLE_STATUS ? (
      <Button
        label={t('emailDeliveries.retry')}
        aria-label={recordActionLabel(t('emailDeliveries.retry'), delivery.recipientEmail)}
        size="sm"
        variant="secondary"
        isLoading={retry.isPending && retry.variables === delivery.id}
        onClick={() => retry.mutate(delivery.id)}
      />
    ) : null;

  const columns: DataColumn<EmailDeliveryItem>[] = [
    {
      key: 'recipient',
      header: t('emailDeliveries.columns.recipient'),
      renderCell: (delivery) => (
        <Text type="body" wordBreak="break-all">
          {delivery.recipientEmail}
        </Text>
      ),
    },
    { key: 'event', header: t('emailDeliveries.columns.event'), renderCell: (delivery) => eventLabel(delivery.eventType) },
    { key: 'subject', header: t('emailDeliveries.columns.subject'), renderCell: (delivery) => delivery.subject },
    { key: 'status', header: t('emailDeliveries.columns.status'), renderCell: (delivery) => <DeliveryStatusToken status={delivery.status} /> },
    { key: 'attempts', header: t('emailDeliveries.columns.attempts'), renderCell: (delivery) => delivery.attempts },
    { key: 'error', header: t('emailDeliveries.columns.error'), renderCell: (delivery) => delivery.lastError ?? '' },
    { key: 'date', header: t('emailDeliveries.columns.date'), renderCell: (delivery) => formatDateTime(delivery.eventCreatedAt) },
    { key: 'actions', header: t('emailDeliveries.columns.actions'), renderCell: retryButton },
  ];

  const renderCard = (delivery: EmailDeliveryItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <Text type="label" wordBreak="break-all">
            {delivery.recipientEmail}
          </Text>
        </StackItem>
        <DeliveryStatusToken status={delivery.status} />
      </HStack>
      <VStack gap={0}>
        <Text type="body">{`${eventLabel(delivery.eventType)}${META_SEPARATOR}${delivery.subject}`}</Text>
        {delivery.lastError && <Text type="supporting">{`${t('emailDeliveries.columns.error')}: ${delivery.lastError}`}</Text>}
        <Text type="supporting">{`${formatDateTime(delivery.eventCreatedAt)}${META_SEPARATOR}${t('emailDeliveries.columns.attempts')}: ${delivery.attempts}`}</Text>
      </VStack>
      <HStack>{retryButton(delivery)}</HStack>
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminEmailDeliveries.icon} titleOverride={t('emailDeliveries.title')} />
      <ListToolbar
        title={t('emailDeliveries.title')}
        search={list.search}
        searchLabel={t('emailDeliveries.filters.recipient')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('emailDeliveries.filters.status')}
              placeholder={t('emailDeliveries.statuses.all')}
              value={status ?? ''}
              options={DELIVERY_STATUSES.map((value) => ({ value, label: t(`emailDeliveries.statuses.${value}`) }))}
              onChange={(value) => list.setFilter(STATUS_FILTER_KEY, value)}
            />
            <Selector
              label={t('emailDeliveries.filters.event')}
              placeholder={t('emailDeliveries.allEvents')}
              value={eventType}
              options={EMAIL_EVENT_TYPES.map((value) => ({ value, label: t(`emailEvents.${value}`) }))}
              onChange={(value) => list.setFilter(EVENT_FILTER_KEY, value)}
            />
          </>
        }
      />
      <FormAlert message={retry.error ? describeError(retry.error) : null} />
      <Card padding={0}>
        {deliveries.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {deliveries.isError && <ErrorState error={deliveries.error} onRetry={() => void deliveries.refetch()} />}
        {deliveries.data && deliveries.data.items.length === 0 && <EmptyState />}
        {deliveries.data && deliveries.data.items.length > 0 && (
          <DataList items={deliveries.data.items} columns={columns} getKey={(delivery) => delivery.id} renderCard={renderCard} caption={t('emailDeliveries.listCaption')} />
        )}
      </Card>
      {deliveries.data && deliveries.data.items.length > 0 && (
        <Pagination page={deliveries.data.page} pageSize={deliveries.data.pageSize} totalItems={deliveries.data.totalCount} onChange={list.setPage} />
      )}
    </VStack>
  );
}
