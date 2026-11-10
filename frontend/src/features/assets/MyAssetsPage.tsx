/**
 * @input Nothing; loads the devices assigned to the signed-in person.
 * @output A list of those devices with tag, model, type, serial number and assignment date.
 * @position `/assets/mine`, inside the app shell.
 */
import { Card } from '@astryxdesign/core/Card';
import { List, ListItem } from '@astryxdesign/core/List';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';

import { useMyAssets } from './api';

const SKELETON_ROWS = 5;
const META_SEPARATOR = ' · ';

export function MyAssetsPage() {
  const { t } = useTranslation('assets');
  const assets = useMyAssets();

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.myAssets.icon} titleOverride={t('mine.title')} />
      <Card padding={0}>
        {assets.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {assets.isError && <ErrorState error={assets.error} onRetry={() => void assets.refetch()} />}
        {assets.data && assets.data.length === 0 && <EmptyState message={t('mine.empty')} />}
        {assets.data && assets.data.length > 0 && (
          <List hasDividers aria-label={t('mine.caption')}>
            {assets.data.map((asset) => (
              <ListItem
                key={asset.id}
                label={asset.assetTag}
                description={
                  <VStack gap={0}>
                    <Text type="supporting" color="primary">{`${asset.brand} ${asset.model}`}</Text>
                    <Text type="supporting">{[t(`types.${asset.type}`), asset.serialNumber, t('mine.assignedAt', { date: formatDateTime(asset.assignedAt) })].filter(Boolean).join(META_SEPARATOR)}</Text>
                  </VStack>
                }
              />
            ))}
          </List>
        )}
      </Card>
    </VStack>
  );
}
