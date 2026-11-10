/**
 * @input Nothing; loads the background job heartbeats.
 * @output The overall worker status and one row per job instance with freshness, last run, interval and error.
 * @position `/admin/system`, inside the admin shell.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../../shared/feedback/States';
import { formatDateTime } from '../../../shared/lib/format';
import { DataList, type DataColumn } from '../../../shared/list/DataList';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';

import { useWorkerHeartbeats, type WorkerHeartbeatItem } from './api';

const DEGRADED_STATUS = 'degraded';
/** Mirrors the server's WorkerHealth.StaleIntervalMultiplier: silent for this many intervals = not reporting. */
const STALE_INTERVAL_MULTIPLIER = 3;
const MS_PER_SECOND = 1000;
const SKELETON_ROWS = 5;
const META_SEPARATOR = ' · ';

/** The job as a whole (server-judged by its newest heartbeat across instances). */
function FreshnessBadge({ stale }: { stale: boolean }) {
  const { t } = useTranslation('admin');
  return <Token size="sm" color={stale ? 'red' : 'green'} label={stale ? t('system.stale') : t('system.fresh')} />;
}

/** One instance: an old host that stopped writing heartbeats is silent even when its job runs elsewhere (UX-04). */
function InstanceBadge({ silent }: { silent: boolean }) {
  const { t } = useTranslation('admin');
  return <Token size="sm" color={silent ? 'yellow' : 'gray'} label={silent ? t('system.instanceSilent') : t('system.instanceFresh')} />;
}

/** Judged against the time the list was fetched, so rendering stays pure. */
const isSilent = (heartbeat: WorkerHeartbeatItem, fetchedAt: number) =>
  fetchedAt - Date.parse(heartbeat.lastRunAt) > heartbeat.intervalSeconds * STALE_INTERVAL_MULTIPLIER * MS_PER_SECOND;

const keyOf = (heartbeat: WorkerHeartbeatItem) => `${heartbeat.job}/${heartbeat.instance}`;

export function SystemPage() {
  const { t } = useTranslation('admin');
  const workers = useWorkerHeartbeats();

  const fetchedAt = workers.dataUpdatedAt;
  const columns: DataColumn<WorkerHeartbeatItem>[] = [
    { key: 'job', header: t('system.columns.job'), renderCell: (heartbeat) => <Text type="label">{heartbeat.job}</Text> },
    { key: 'instance', header: t('system.columns.instance'), renderCell: (heartbeat) => <Text wordBreak="break-word">{heartbeat.instance}</Text> },
    { key: 'lastRun', header: t('system.columns.lastRun'), renderCell: (heartbeat) => formatDateTime(heartbeat.lastRunAt) },
    { key: 'interval', header: t('system.columns.interval'), renderCell: (heartbeat) => t('system.intervalValue', { count: heartbeat.intervalSeconds }) },
    { key: 'jobStatus', header: t('system.columns.jobStatus'), renderCell: (heartbeat) => <FreshnessBadge stale={heartbeat.stale} /> },
    { key: 'instanceStatus', header: t('system.columns.instanceStatus'), renderCell: (heartbeat) => <InstanceBadge silent={isSilent(heartbeat, fetchedAt)} /> },
    { key: 'error', header: t('system.columns.error'), renderCell: (heartbeat) => heartbeat.lastError ?? '' },
  ];

  const renderCard = (heartbeat: WorkerHeartbeatItem) => (
    <VStack gap={1}>
      <HStack gap={2} hAlign="between" vAlign="start">
        <Text type="label">{heartbeat.job}</Text>
        <HStack gap={1} wrap="wrap">
          <FreshnessBadge stale={heartbeat.stale} />
          <InstanceBadge silent={isSilent(heartbeat, fetchedAt)} />
        </HStack>
      </HStack>
      <Text type="supporting" wordBreak="break-word">
        {heartbeat.instance}
      </Text>
      <Text type="supporting">{`${formatDateTime(heartbeat.lastRunAt)}${META_SEPARATOR}${t('system.intervalValue', { count: heartbeat.intervalSeconds })}`}</Text>
      {heartbeat.lastError && <Text type="supporting">{heartbeat.lastError}</Text>}
    </VStack>
  );

  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminSystem.icon} titleOverride={t('system.title')} />
      <SectionCard title={t('system.workersTitle')}>
        {workers.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {workers.isError && <ErrorState error={workers.error} onRetry={() => void workers.refetch()} />}
        {workers.data && (
          <>
            <VStack role="status">
              <Banner
                status={workers.data.status === DEGRADED_STATUS ? 'error' : 'success'}
                title={workers.data.status === DEGRADED_STATUS ? t('system.overall.degraded') : t('system.overall.ok')}
              />
            </VStack>
            {workers.data.jobs.length === 0 ? (
              <EmptyState message={t('system.empty')} />
            ) : (
              <DataList items={workers.data.jobs} columns={columns} getKey={keyOf} renderCard={renderCard} caption={t('system.listCaption')} />
            )}
          </>
        )}
      </SectionCard>
    </VStack>
  );
}
