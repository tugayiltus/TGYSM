import CIcon from '@coreui/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { formatDateTime } from '../../shared/lib/format';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ROUTES } from '../../shared/routes';
import { DataCard } from '../../shared/ui/DataCard';
import { cilArrowLeft } from '../../shared/ui/icons';
import { Pagination } from '../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../shared/ui/States';

import { EXPORTS_PAGE_SIZE, exportDownloadUrl, FIRST_PAGE, useMyExports, type ExportJobItem } from './api';
import { formatNumber, metricLabel } from './formatMetric';

const STATUS_CLASSES: Record<ExportJobItem['status'], string> = {
  pending: 'bg-secondary-subtle text-secondary-emphasis',
  running: 'bg-info-subtle text-info-emphasis',
  ready: 'bg-success-subtle text-success-emphasis',
  failed: 'bg-danger-subtle text-danger-emphasis',
  expired: 'bg-warning-subtle text-warning-emphasis',
};

function JobStatus({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  return <span className={`badge rounded-pill fw-medium ${STATUS_CLASSES[job.status]}`}>{t(`exports.statuses.${job.status}`)}</span>;
}

/** Only a ready file can be downloaded; the API re-checks the creator's role and scope on every download. */
function DownloadLink({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  if (job.status !== 'ready') {
    return null;
  }
  return (
    <a href={exportDownloadUrl(job.id)} download className="fw-medium link-primary text-decoration-underline">
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
    <div className="d-flex flex-column gap-1 small">
      <p className="fw-medium">
        {t(`tabs.${job.report}`)} · {metricLabel(job.metric)}
      </p>
      <div className="d-flex flex-wrap align-items-center gap-2">
        <JobStatus job={job} />
        {job.rowCount !== null && <span>{t('exports.rows', { count: job.rowCount })}</span>}
      </div>
      <p className="text-body-secondary">{formatDateTime(job.createdAt)}</p>
      {job.status === 'ready' && job.expiresAt !== null && <p className="text-body-secondary">{t('exports.expiresAt', { date: formatDateTime(job.expiresAt) })}</p>}
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
    <>
      <PageHeader
        icon={ROUTE_META.reportExports.icon}
        titleOverride={t('exports.title')}
        actions={
          <Link to={ROUTES.reports} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center gap-2">
            <CIcon icon={cilArrowLeft} aria-hidden="true" />
            {t('exports.back')}
          </Link>
        }
      />
      <DataCard
        footer={
          jobs.data !== undefined && jobs.data.totalCount > 0 ? (
            <Pagination page={jobs.data.page} pageSize={EXPORTS_PAGE_SIZE} totalCount={jobs.data.totalCount} onPageChange={setPage} />
          ) : undefined
        }
      >
        {jobs.isPending && <LoadingState rows={TABLE_SKELETON_ROWS} />}
        {jobs.isError && <ErrorState error={jobs.error} onRetry={() => void jobs.refetch()} />}
        {jobs.data !== undefined &&
          (jobs.data.totalCount === 0 ? (
            <EmptyState message={t('exports.empty')} />
          ) : (
            <ResponsiveList items={jobs.data.items} columns={columns} getKey={(job) => job.id} renderCard={(job) => <JobCard job={job} />} caption={t('exports.caption')} />
          ))}
      </DataCard>
    </>
  );
}
