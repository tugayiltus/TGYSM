/**
 * @input Nothing; filters come from the URL query.
 * @output The inventory list with search, type and status filters, paging, import and create.
 * @position `/assets`, inside the app shell (IT staff and admins).
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
import { useLocation } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { useDebouncedValue } from '../../shared/lib/useDebouncedValue';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useListParams } from '../../shared/list/useListParams';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { StateLink } from '../../shared/navigation/StateLink';
import { assetPath, ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';
import { recordActionLabel } from '../../shared/ui/recordActionLabel';

import { ASSET_STATUSES, ASSET_TYPES, useAssets, type AssetItem } from './api';
import { AssetFormDialog } from './AssetFormDialog';
import { AssetStatusBadge } from './AssetStatusBadge';

const PAGE_SIZE = 20;
const SKELETON_ROWS = 5;
const SEARCH_DEBOUNCE_MS = 300;
const FILTER_KEYS = ['type', 'status'] as const;
const META_SEPARATOR = ' · ';

const pick = <T extends string>(allowed: readonly T[], value: string): T | undefined => allowed.find((item) => item === value);

function OpenLink({ asset }: { asset: AssetItem }) {
  const { t } = useTranslation('assets');
  const location = useLocation();
  return (
    <StateLink href={assetPath(asset.id)} state={linkState(location)} label={recordActionLabel(t('list.open'), asset.assetTag)}>
      {t('list.open')}
    </StateLink>
  );
}

export function AssetsPage() {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const list = useListParams(FILTER_KEYS);
  const type = pick(ASSET_TYPES, list.filters.type);
  const status = pick(ASSET_STATUSES, list.filters.status);
  const search = useDebouncedValue(list.search, SEARCH_DEBOUNCE_MS);
  const [creating, setCreating] = useState(false);
  const assets = useAssets({ search, type, status, page: list.page, pageSize: PAGE_SIZE });

  const device = (a: AssetItem) => `${t(`types.${a.type}`)}${META_SEPARATOR}${a.brand} ${a.model}`;
  const columns: DataColumn<AssetItem>[] = [
    { key: 'tag', header: t('list.columns.tag'), renderCell: (a) => <Text type="label">{a.assetTag}</Text> },
    { key: 'device', header: t('list.columns.device'), renderCell: device },
    { key: 'status', header: t('list.columns.status'), renderCell: (a) => <AssetStatusBadge status={a.status} /> },
    { key: 'location', header: t('list.columns.location'), renderCell: (a) => a.locationName },
    { key: 'holder', header: t('list.columns.holder'), renderCell: (a) => a.assignedUserName ?? tc('none') },
    { key: 'actions', header: t('list.columns.actions'), renderCell: (a) => <OpenLink asset={a} /> },
  ];

  const renderCard = (a: AssetItem) => (
    <VStack gap={2}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <StackItem size="fill">
          <VStack gap={0}>
            <Text type="label" wordBreak="break-word">
              {a.assetTag}
            </Text>
            <Text type="supporting">{device(a)}</Text>
            <Text type="supporting">{a.assignedUserName ?? a.locationName}</Text>
          </VStack>
        </StackItem>
        <StackItem><AssetStatusBadge status={a.status} /></StackItem>
      </HStack>
      <OpenLink asset={a} />
    </VStack>
  );

  const option = (value: string | undefined) => value ?? '';
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.assets.icon}
        titleOverride={t('list.title')}
        actions={
          <>
            <Button label={t('list.import')} variant="secondary" href={ROUTES.assetImport} icon={<Icon icon={Icons.upload} />} />
            <Button label={t('list.new')} variant="primary" icon={<Icon icon={Icons.plus} />} onClick={() => setCreating(true)} />
          </>
        }
      />
      <ListToolbar
        title={t('list.title')}
        search={list.search}
        searchLabel={t('list.searchLabel')}
        onSearchChange={list.setSearch}
        activeCount={list.activeCount}
        onClear={list.clear}
        filters={
          <>
            <Selector
              label={t('list.typeFilter')}
              placeholder={t('list.allTypes')}
              options={ASSET_TYPES.map((value) => ({ value, label: t(`types.${value}`) }))}
              value={option(type)}
              onChange={(value) => list.setFilter('type', value)}
            />
            <Selector
              label={t('list.statusFilter')}
              placeholder={t('list.allStatuses')}
              options={ASSET_STATUSES.map((value) => ({ value, label: t(`statuses.${value}`) }))}
              value={option(status)}
              onChange={(value) => list.setFilter('status', value)}
            />
          </>
        }
      />
      <Card padding={0}>
        {assets.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
        {assets.data && assets.data.items.length === 0 && <EmptyState />}
        {assets.data && assets.data.items.length > 0 && (
          <DataList items={assets.data.items} columns={columns} getKey={(a) => a.id} renderCard={renderCard} caption={t('list.caption')} />
        )}
      </Card>
      {assets.data && assets.data.items.length > 0 && <Pagination page={assets.data.page} pageSize={assets.data.pageSize} totalItems={assets.data.totalCount} onChange={list.setPage} />}
      {creating && <AssetFormDialog asset={null} onClose={() => setCreating(false)} />}
    </VStack>
  );
}
