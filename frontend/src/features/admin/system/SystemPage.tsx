import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../../shared/lib/format';
import { ResponsiveList, type Column } from '../../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../../shared/ui/States';

import { useWorkerHeartbeats, type WorkerHeartbeatItem } from './api';

const DEGRADED_STATUS = 'degraded';

function FreshnessBadge({ stale }: { stale: boolean }) {
  const { t } = useTranslation('admin');
  return (
    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${stale ? 'bg-red-50 text-red-800' : 'bg-emerald-50 text-emerald-800'}`}>
      {stale ? t('system.stale') : t('system.fresh')}
    </span>
  );
}

const keyOf = (heartbeat: WorkerHeartbeatItem) => `${heartbeat.job}/${heartbeat.instance}`;

export function SystemPage() {
  const { t } = useTranslation('admin');
  const workers = useWorkerHeartbeats();

  const columns: Column<WorkerHeartbeatItem>[] = [
    { key: 'job', header: t('system.columns.job'), renderCell: (heartbeat) => <span className="font-medium">{heartbeat.job}</span> },
    { key: 'instance', header: t('system.columns.instance'), renderCell: (heartbeat) => <span className="break-all">{heartbeat.instance}</span> },
    { key: 'lastRun', header: t('system.columns.lastRun'), renderCell: (heartbeat) => formatDateTime(heartbeat.lastRunAt) },
    { key: 'interval', header: t('system.columns.interval'), renderCell: (heartbeat) => t('system.intervalValue', { count: heartbeat.intervalSeconds }) },
    { key: 'status', header: t('system.columns.status'), renderCell: (heartbeat) => <FreshnessBadge stale={heartbeat.stale} /> },
    { key: 'error', header: t('system.columns.error'), renderCell: (heartbeat) => heartbeat.lastError ?? '' },
  ];

  const renderCard = (heartbeat: WorkerHeartbeatItem) => (
    <div className="flex flex-col gap-2">
      <div className="flex items-start justify-between gap-2">
        <p className="font-medium">{heartbeat.job}</p>
        <FreshnessBadge stale={heartbeat.stale} />
      </div>
      <p className="text-sm break-all text-slate-600">{heartbeat.instance}</p>
      <p className="text-sm text-slate-700">
        {formatDateTime(heartbeat.lastRunAt)} · {t('system.intervalValue', { count: heartbeat.intervalSeconds })}
      </p>
      {heartbeat.lastError && <p className="text-sm text-red-800">{heartbeat.lastError}</p>}
    </div>
  );

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('system.title')}</h1>
      <h2 className="text-lg font-medium">{t('system.workersTitle')}</h2>
      {workers.isPending && <LoadingState />}
      {workers.isError && <ErrorState error={workers.error} onRetry={() => void workers.refetch()} />}
      {workers.data && (
        <>
          <p role="status" className={`rounded-lg p-3 text-sm ${workers.data.status === DEGRADED_STATUS ? 'bg-red-50 text-red-800' : 'bg-emerald-50 text-emerald-800'}`}>
            {workers.data.status === DEGRADED_STATUS ? t('system.overall.degraded') : t('system.overall.ok')}
          </p>
          {workers.data.jobs.length === 0 ? (
            <EmptyState message={t('system.empty')} />
          ) : (
            <ResponsiveList items={workers.data.jobs} columns={columns} getKey={keyOf} renderCard={renderCard} caption={t('system.listCaption')} />
          )}
        </>
      )}
    </section>
  );
}
