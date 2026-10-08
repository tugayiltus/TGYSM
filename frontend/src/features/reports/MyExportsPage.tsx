import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { ROUTES } from '../../shared/routes';
import { Pagination } from '../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';

import { EXPORTS_PAGE_SIZE, exportDownloadUrl, FIRST_PAGE, useMyExports, type ExportJobItem } from './api';
import { formatNumber, metricLabel } from './formatMetric';

const STATUS_CLASSES: Record<ExportJobItem['status'], string> = {
  pending: 'bg-slate-100 text-slate-700',
  running: 'bg-sky-50 text-sky-800',
  ready: 'bg-emerald-50 text-emerald-800',
  failed: 'bg-red-50 text-red-800',
  expired: 'bg-amber-50 text-amber-900',
};

function JobStatus({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  return <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_CLASSES[job.status]}`}>{t(`exports.statuses.${job.status}`)}</span>;
}

/** Only a ready file can be downloaded; the API re-checks the creator's role and scope on every download. */
function DownloadLink({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  if (job.status !== 'ready') {
    return null;
  }
  return (
    <a href={exportDownloadUrl(job.id)} download className="font-medium text-brand-900 underline">
      {t('exports.download')}
    </a>
  );
}

const rowsText = (job: ExportJobItem) => (job.rowCount === null ? '' : formatNumber(job.rowCount));

function useColumns(): Column<ExportJobItem>[] {
  const { t } = useTranslation('reports');
  return [
    { key: 'report', header: t('exports.columns.report'), renderCell: (job) => t(`tabs.${job.report}`) },
    { key: 'metric', header: t('exports.columns.metric'), renderCell: (job) => metricLabel(job.metric) },
    { key: 'status', header: t('exports.columns.status'), renderCell: (job) => <JobStatus job={job} /> },
    { key: 'rows', header: t('exports.columns.rows'), renderCell: rowsText },
    { key: 'createdAt', header: t('exports.columns.createdAt'), renderCell: (job) => formatDateTime(job.createdAt) },
    { key: 'expiresAt', header: t('exports.columns.expiresAt'), renderCell: (job) => (job.expiresAt === null ? '' : formatDateTime(job.expiresAt)) },
    { key: 'actions', header: t('exports.columns.actions'), renderCell: (job) => <DownloadLink job={job} /> },
  ];
}

function JobCard({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  return (
    <div className="flex flex-col gap-1 text-sm">
      <p className="font-medium">
        {t(`tabs.${job.report}`)} · {metricLabel(job.metric)}
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <JobStatus job={job} />
        {job.rowCount !== null && <span>{t('exports.rows', { count: job.rowCount })}</span>}
      </div>
      <p className="text-slate-600">{formatDateTime(job.createdAt)}</p>
      {job.status === 'ready' && job.expiresAt !== null && <p className="text-slate-600">{t('exports.expiresAt', { date: formatDateTime(job.expiresAt) })}</p>}
      <DownloadLink job={job} />
    </div>
  );
}

/** `/reports/exports`: the signed-in user's background exports; pending and running jobs refresh until done. */
export function MyExportsPage() {
  const { t } = useTranslation('reports');
  const [page, setPage] = useState(FIRST_PAGE);
  const jobs = useMyExports(page);
  const columns = useColumns();

  return (
    <section className="flex flex-col gap-5">
      <Link to={ROUTES.reports} className="self-start text-sm text-brand-900 underline">
        {t('exports.back')}
      </Link>
      <h1 className="text-2xl font-semibold">{t('exports.title')}</h1>
      {jobs.isPending && <LoadingState />}
      {jobs.isError && <ErrorState error={jobs.error} onRetry={() => void jobs.refetch()} />}
      {jobs.data !== undefined &&
        (jobs.data.totalCount === 0 ? (
          <EmptyState message={t('exports.empty')} />
        ) : (
          <>
            <ResponsiveList items={jobs.data.items} columns={columns} getKey={(job) => job.id} renderCard={(job) => <JobCard job={job} />} caption={t('exports.caption')} />
            <Pagination page={jobs.data.page} pageSize={EXPORTS_PAGE_SIZE} totalCount={jobs.data.totalCount} onPageChange={setPage} />
          </>
        ))}
    </section>
  );
}
