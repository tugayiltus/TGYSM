/**
 * @input Nothing; loads the signed-in user's background exports.
 * @output The export jobs with status, rows, dates and a download link; pending jobs refresh until done.
 * @position `/reports/exports`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { VStack } from '@astryxdesign/core/VStack';
import { useState, type ComponentProps } from 'react';
import { useTranslation } from 'react-i18next';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { formatDateTime } from '../../shared/lib/format';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { EXPORTS_PAGE_SIZE, exportDownloadUrl, FIRST_PAGE, useMyExports, type ExportJobItem } from './api';
import { formatNumber, metricLabel } from './formatMetric';

const SKELETON_ROWS = 5;
const META_SEPARATOR = ' · ';

const STATUS_COLOR: Record<ExportJobItem['status'], NonNullable<ComponentProps<typeof Token>['color']>> = {
  pending: 'gray',
  running: 'cyan',
  ready: 'green',
  failed: 'red',
  expired: 'yellow',
};

function JobStatus({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  return <Token size="sm" color={STATUS_COLOR[job.status]} label={t(`exports.statuses.${job.status}`)} />;
}

/** Only a ready file can be downloaded; the API re-checks the creator's role and scope on every download. */
function DownloadLink({ job }: { job: ExportJobItem }) {
  const { t } = useTranslation('reports');
  if (job.status !== 'ready') {
    return null;
  }
  return (
    // A native download link: the router must not handle a file URL.
    <a href={exportDownloadUrl(job.id)} download>
      <Text type="label" color="accent">
        {t('exports.download')}
      </Text>
    </a>
  );
}

const rowsText = (job: ExportJobItem) => (job.rowCount === null ? '' : formatNumber(job.rowCount));

function useColumns(): DataColumn<ExportJobItem>[] {
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
    <VStack gap={1}>
      <Text type="label">{`${t(`tabs.${job.report}`)}${META_SEPARATOR}${metricLabel(job.metric)}`}</Text>
      <HStack gap={2} vAlign="center" wrap="wrap">
        <JobStatus job={job} />
        {job.rowCount !== null && <Text type="supporting">{t('exports.rows', { count: job.rowCount })}</Text>}
      </HStack>
      <Text type="supporting">{formatDateTime(job.createdAt)}</Text>
      {job.status === 'ready' && job.expiresAt !== null && <Text type="supporting">{t('exports.expiresAt', { date: formatDateTime(job.expiresAt) })}</Text>}
      <DownloadLink job={job} />
    </VStack>
  );
}

/** `/reports/exports`: the signed-in user's background exports; pending and running jobs refresh until done. */
export function MyExportsPage() {
  const { t } = useTranslation('reports');
  const [page, setPage] = useState(FIRST_PAGE);
  const jobs = useMyExports(page);
  const columns = useColumns();

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.reportExports.icon}
        titleOverride={t('exports.title')}
        actions={<Button label={t('exports.back')} variant="secondary" href={ROUTES.reports} icon={<Icon icon={Icons.arrowLeft} />} />}
      />
      <Card padding={0}>
        {jobs.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {jobs.isError && <ErrorState error={jobs.error} onRetry={() => void jobs.refetch()} />}
        {jobs.data !== undefined &&
          (jobs.data.totalCount === 0 ? (
            <EmptyState message={t('exports.empty')} />
          ) : (
            <DataList items={jobs.data.items} columns={columns} getKey={(job) => job.id} renderCard={(job) => <JobCard job={job} />} caption={t('exports.caption')} />
          ))}
      </Card>
      {jobs.data !== undefined && jobs.data.totalCount > 0 && <Pagination page={jobs.data.page} pageSize={EXPORTS_PAGE_SIZE} totalItems={jobs.data.totalCount} onChange={setPage} />}
    </VStack>
  );
}
