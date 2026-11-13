/**
 * @input `queue`, `status`, `risk`, `search` and `page` in the URL query.
 * @output The change list with filters, paging and a button that opens a new change.
 * @position `/changes`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Selector } from '@astryxdesign/core/Selector';
import { StackItem } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { changePath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';

import { CHANGE_QUEUES, CHANGE_STATUSES, LEVELS, useChanges, type ChangeListItem } from './api';
import { ChangeOutcomeBadge, ChangeStatusBadge, RiskBadge } from './ChangeBadges';
import { ChangeFormDialog } from './ChangeFormDialog';
import { WindowText } from './WindowText';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['queue', 'status', 'risk'] as const;

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

function OpenLink({ change }: { change: ChangeListItem }) {
  const { t } = useTranslation('changes');
  const location = useLocation();
  return (
    <StateLink href={changePath(change.number)} state={linkState(location)} label={recordActionLabel(t('list.open'), change.number)}>
      {t('list.open')}
    </StateLink>
  );
}

function StatusCell({ change }: { change: ChangeListItem }) {
  return (
    <HStack gap={1} vAlign="center" wrap="wrap">
      <ChangeStatusBadge status={change.status} />
      <ChangeOutcomeBadge outcome={change.outcome} />
    </HStack>
  );
}

export function ChangeListPage() {
  const { t } = useTranslation('changes');
  const { t: tc } = useTranslation();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const list = useListParams(FILTER_KEYS);
  const queue = pick(CHANGE_QUEUES, list.filters.queue);
  const status = pick(CHANGE_STATUSES, list.filters.status);
  const risk = pick(LEVELS, list.filters.risk);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const changes = useChanges({ queue, status, risk, search, page: list.page, pageSize: PAGE_SIZE });
  const columns: DataColumn<ChangeListItem>[] = [
    { key: 'number', header: t('list.columns.number'), renderCell: (x) => <Text type="label">{x.number}</Text> },
    { key: 'title', header: t('list.columns.title'), renderCell: (x) => x.title },
    { key: 'status', header: t('list.columns.status'), renderCell: (x) => <StatusCell change={x} /> },
    { key: 'risk', header: t('list.columns.risk'), renderCell: (x) => <RiskBadge risk={x.risk} /> },
    { key: 'window', header: t('list.columns.window'), renderCell: (x) => <WindowText start={x.windowStart} end={x.windowEnd} /> },
    { key: 'team', header: t('list.columns.team'), renderCell: (x) => x.ownerTeamName ?? tc('none') },
    { key: 'owner', header: t('list.columns.owner'), renderCell: (x) => x.ownerName ?? tc('none') },
    { key: 'updated', header: t('list.columns.updated'), renderCell: (x) => formatDateTime(x.updatedAt) },
    { key: 'open', header: '', renderCell: (x) => <OpenLink change={x} /> },
  ];
  const renderCard = (x: ChangeListItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="supporting">{x.number}</Text>
            <Text type="label" wordBreak="break-word">
              {x.title}
            </Text>
          </VStack>
        </StackItem>
        <StackItem>
          <StatusCell change={x} />
        </StackItem>
      </HStack>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <RiskBadge risk={x.risk} />
        <WindowText start={x.windowStart} end={x.windowEnd} />
      </HStack>
      <OpenLink change={x} />
    </VStack>
  );
  const option = (value: string | undefined) => value ?? '';
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.changes.icon}
        titleOverride={queue ? t(`queues.${queue}`) : undefined}
        actions={<Button label={t('list.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setCreating(true)} />}
      />
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
              label={t('list.queueFilter')}
              placeholder={t('list.all')}
              options={CHANGE_QUEUES.map((value) => ({ value, label: t(`queues.${value}`) }))}
              value={option(queue)}
              onChange={(value) => list.setFilter('queue', value)}
            />
            <Selector
              label={t('list.statusFilter')}
              placeholder={t('list.all')}
              options={CHANGE_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={option(status)}
              onChange={(value) => list.setFilter('status', value)}
            />
            <Selector
              label={t('list.riskFilter')}
              placeholder={t('list.all')}
              options={LEVELS.map((value) => ({ value, label: t(`levels.${value}`) }))}
              value={option(risk)}
              onChange={(value) => list.setFilter('risk', value)}
            />
          </>
        }
      />
      <Card padding={0}>
        {changes.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {changes.isError && <ErrorState error={changes.error} onRetry={() => void changes.refetch()} />}
        {changes.data && changes.data.items.length === 0 && <EmptyState message={t('list.empty')} />}
        {changes.data && changes.data.items.length > 0 && (
          <DataList items={changes.data.items} columns={columns} getKey={(x) => x.number} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {changes.data && changes.data.items.length > 0 && (
        <Pagination page={changes.data.page} pageSize={changes.data.pageSize} totalItems={changes.data.totalCount} onChange={list.setPage} />
      )}
      {creating && <ChangeFormDialog onClose={() => setCreating(false)} onSaved={(change) => void navigate(changePath(change.number))} />}
    </VStack>
  );
}
