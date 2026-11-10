/**
 * @input The asset id from the route.
 * @output The asset facts and history, with lifecycle operations and edit.
 * @position `/assets/:id`, inside the app shell (IT staff and admins).
 */
import { Button } from '@astryxdesign/core/Button';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { List, ListItem } from '@astryxdesign/core/List';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { useBreadcrumbLabel } from '../../shared/navigation/breadcrumbLabels';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { useReturnTo } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { OPERATIONS_BY_STATUS, useAsset, useAssetHistory, type AssetItem, type AssetMovementItem, type AssetOperationKind } from './api';
import { AssetFormDialog } from './AssetFormDialog';
import { AssetOperationDialog } from './AssetOperationDialog';
import { AssetStatusBadge } from './AssetStatusBadge';

const SKELETON_ROWS = 5;
const META_SEPARATOR = ' · ';
/** Wide screens: content takes two of three columns, the side column one. */
const WIDE_COLUMNS = 3;
const MAIN_SPAN = 2;

function Field({ label, value }: { label: string; value: string }) {
  return (
    <MetadataListItem label={label}>
      <Text wordBreak="break-word">{value}</Text>
    </MetadataListItem>
  );
}

function AssetFacts({ asset }: { asset: AssetItem }) {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const optionalDate = (value: string | null) => (value === null ? tc('none') : formatDate(value));
  return (
    <SectionCard title={t('detail.facts')}>
      <MetadataList>
        <Field label={t('fields.type')} value={t(`types.${asset.type}`)} />
        <Field label={t('fields.brand')} value={asset.brand} />
        <Field label={t('fields.model')} value={asset.model} />
        <Field label={t('fields.serialNumber')} value={asset.serialNumber ?? tc('none')} />
        <Field label={t('fields.holder')} value={asset.assignedUserName ?? tc('none')} />
        <Field label={t('fields.location')} value={asset.locationName} />
        <Field label={t('fields.department')} value={asset.departmentName ?? tc('none')} />
        <Field label={t('fields.warrantyEndsOn')} value={optionalDate(asset.warrantyEndsOn)} />
        <Field label={t('fields.purchasedOn')} value={optionalDate(asset.purchasedOn)} />
        <Field label={t('fields.createdAt')} value={formatDateTime(asset.createdAt)} />
        {asset.notes && <Field label={t('fields.notes')} value={asset.notes} />}
      </MetadataList>
    </SectionCard>
  );
}

function History({ movements }: { movements: AssetMovementItem[] }) {
  const { t } = useTranslation('assets');
  if (movements.length === 0) {
    return <EmptyState />;
  }
  return (
    <List listStyle="decimal" hasDividers density="compact" aria-label={t('detail.history')}>
      {movements.map((movement) => (
        <ListItem
          key={movement.id}
          label={t(`movements.${movement.kind}`)}
          description={[
            formatDateTime(movement.occurredAt),
            movement.userName && t('detail.to', { name: movement.userName }),
            movement.userResponsibilityRetained && t('detail.retained'),
            movement.reason,
            movement.actorName && t('detail.by', { name: movement.actorName }),
          ]
            .filter(Boolean)
            .join(META_SEPARATOR)}
        />
      ))}
    </List>
  );
}

export function AssetDetailPage() {
  const { t } = useTranslation('assets');
  const { t: tc } = useTranslation();
  const { id = '' } = useParams();
  const asset = useAsset(id);
  const history = useAssetHistory(id);
  const [operation, setOperation] = useState<AssetOperationKind | null>(null);
  const [editing, setEditing] = useState(false);
  const backTo = useReturnTo(ROUTES.assets);
  const isPhone = useIsPhone();
  useBreadcrumbLabel('assetDetail', asset.data?.assetTag);

  if (asset.isPending) {
    return <LoadingState />;
  }
  if (asset.isError) {
    return <ErrorState error={asset.error} onRetry={() => void asset.refetch()} />;
  }

  const item = asset.data;
  const operations = OPERATIONS_BY_STATUS[item.status];

  const side = (
    <SectionCard title={t('detail.operations')}>
      {operations.length === 0 ? (
        <Text type="supporting">{t('detail.noOperations')}</Text>
      ) : (
        <HStack gap={2} wrap="wrap">
          {operations.map((kind) => (
            <Button key={kind} label={t(`actions.${kind}`)} variant={kind === 'lost' || kind === 'retire' ? 'destructive' : 'secondary'} onClick={() => setOperation(kind)} />
          ))}
        </HStack>
      )}
    </SectionCard>
  );
  const main = (
    <VStack gap={4}>
      <AssetFacts asset={item} />
      <SectionCard title={t('detail.history')}>
        {history.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {history.isError && <ErrorState error={history.error} onRetry={() => void history.refetch()} />}
        {history.data && <History movements={history.data.movements} />}
      </SectionCard>
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.assets.icon}
        titleOverride={item.assetTag}
        actions={
          <>
            <Button label={t('detail.back')} variant="secondary" href={backTo} icon={<Icon icon={Icons.arrowLeft} />} />
            <Button label={tc('actions.edit')} variant="secondary" icon={<Icon icon={Icons.pencil} />} onClick={() => setEditing(true)} />
          </>
        }
      />
      <HStack>
        <AssetStatusBadge status={item.status} />
      </HStack>
      {/* Phones show the operations before the long content; wide screens put them in a side column. */}
      {isPhone ? (
        <VStack gap={4}>
          {side}
          {main}
        </VStack>
      ) : (
        <Grid columns={WIDE_COLUMNS} gap={4} align="start">
          <GridSpan columns={MAIN_SPAN}>{main}</GridSpan>
          {side}
        </Grid>
      )}
      {operation !== null && <AssetOperationDialog key={operation} asset={item} kind={operation} onClose={() => setOperation(null)} />}
      {editing && <AssetFormDialog asset={item} onClose={() => setEditing(false)} />}
    </VStack>
  );
}
