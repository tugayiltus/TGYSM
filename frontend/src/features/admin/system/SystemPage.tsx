import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../../shared/lib/format';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { PageCard } from '../../../shared/ui/PageCard';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../../shared/ui/States';

import { useWorkerHeartbeats, type WorkerHeartbeatItem } from './api';

const DEGRADED_STATUS = 'degraded';
/** Mirrors the server's WorkerHealth.StaleIntervalMultiplier: silent for this many intervals = not reporting. */
const STALE_INTERVAL_MULTIPLIER = 3;
const MS_PER_SECOND = 1000;

/** The job as a whole (server-judged by its newest heartbeat across instances). */
function FreshnessBadge({ stale }: { stale: boolean }) {
  const { t } = useTranslation('admin');
  return <span className={`badge ${stale ? 'bg-danger-subtle text-danger-emphasis' : 'bg-success-subtle text-success-emphasis'}`}>{stale ? t('system.stale') : t('system.fresh')}</span>;
}

/** One instance: an old host that stopped writing heartbeats is silent even when its job runs elsewhere (UX-04). */
function InstanceBadge({ silent }: { silent: boolean }) {
  const { t } = useTranslation('admin');
  return <span className={`badge ${silent ? 'bg-warning-subtle text-warning-emphasis' : 'bg-secondary-subtle text-secondary-emphasis'}`}>{silent ? t('system.instanceSilent') : t('system.instanceFresh')}</span>;
}

/** Judged against the time the list was fetched, so rendering stays pure. */
const isSilent = (heartbeat: WorkerHeartbeatItem, fetchedAt: number) =>
  fetchedAt - Date.parse(heartbeat.lastRunAt) > heartbeat.intervalSeconds * STALE_INTERVAL_MULTIPLIER * MS_PER_SECOND;

const keyOf = (heartbeat: WorkerHeartbeatItem) => `${heartbeat.job}/${heartbeat.instance}`;

export function SystemPage() {
  const { t } = useTranslation('admin');
  const workers = useWorkerHeartbeats();

  const fetchedAt = workers.dataUpdatedAt;
  const columns: Column<WorkerHeartbeatItem>[] = [
    { key: 'job', header: t('system.columns.job'), renderCell: (heartbeat) => <span className="fw-medium">{heartbeat.job}</span> },
    { key: 'instance', header: t('system.columns.instance'), renderCell: (heartbeat) => <span className="text-break">{heartbeat.instance}</span> },
    { key: 'lastRun', header: t('system.columns.lastRun'), renderCell: (heartbeat) => formatDateTime(heartbeat.lastRunAt) },
    { key: 'interval', header: t('system.columns.interval'), renderCell: (heartbeat) => t('system.intervalValue', { count: heartbeat.intervalSeconds }) },
    { key: 'jobStatus', header: t('system.columns.jobStatus'), renderCell: (heartbeat) => <FreshnessBadge stale={heartbeat.stale} /> },
    { key: 'instanceStatus', header: t('system.columns.instanceStatus'), renderCell: (heartbeat) => <InstanceBadge silent={isSilent(heartbeat, fetchedAt)} /> },
    { key: 'error', header: t('system.columns.error'), renderCell: (heartbeat) => heartbeat.lastError ?? '' },
  ];

  const renderCard = (heartbeat: WorkerHeartbeatItem) => (
    <div className="d-flex flex-column gap-2">
      <div className="d-flex align-items-start justify-content-between gap-2">
        <p className="mb-0 fw-medium">{heartbeat.job}</p>
        <span className="d-flex flex-wrap justify-content-end gap-1">
          <FreshnessBadge stale={heartbeat.stale} />
          <InstanceBadge silent={isSilent(heartbeat, fetchedAt)} />
        </span>
      </div>
      <p className="mb-0 small text-break text-body-secondary">{heartbeat.instance}</p>
      <p className="mb-0 small">
        {formatDateTime(heartbeat.lastRunAt)} · {t('system.intervalValue', { count: heartbeat.intervalSeconds })}
      </p>
      {heartbeat.lastError && <p className="mb-0 small text-danger">{heartbeat.lastError}</p>}
    </div>
  );

  return (
    <>
      <PageHeader icon={ROUTE_META.adminSystem.icon} titleOverride={t('system.title')} />
      <PageCard title={t('system.workersTitle')} flush>
        {workers.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
        {workers.isError && <ErrorState error={workers.error} onRetry={() => void workers.refetch()} />}
        {workers.data && (
          <>
            <p role="status" className={`alert m-3 ${workers.data.status === DEGRADED_STATUS ? 'alert-danger' : 'alert-success'}`}>
              {workers.data.status === DEGRADED_STATUS ? t('system.overall.degraded') : t('system.overall.ok')}
            </p>
            {workers.data.jobs.length === 0 ? (
              <EmptyState message={t('system.empty')} />
            ) : (
              <ResponsiveList items={workers.data.jobs} columns={columns} getKey={keyOf} renderCard={renderCard} caption={t('system.listCaption')} />
            )}
          </>
        )}
      </PageCard>
    </>
  );
}
