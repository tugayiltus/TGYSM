import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';
import { Link, useParams, useSearchParams } from 'react-router';

import { translateOr } from '../../shared/i18n/dynamic';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { readPageParam } from '../../shared/lib/pageParam';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { assetPath, reportPath, ticketPath } from '../../shared/routes';
import { DataCard } from '../../shared/ui/DataCard';
import { cilArrowLeft } from '../../shared/ui/icons';
import { Pagination } from '../../shared/ui/Pagination';
import { ResponsiveList, type Column } from '../../shared/ui/ResponsiveList';
import { EmptyState, ErrorState, LoadingState, TABLE_SKELETON_ROWS } from '../../shared/ui/States';

import {
  DRILL_PARAM,
  DRILL_PARTS,
  DRILLDOWN_PAGE_SIZE,
  isReportName,
  useDrilldown,
  useReport,
  type DrillPart,
  type DrillSelection,
  type DrilldownAssetRow,
  type DrilldownTicketRow,
  type ReportName,
} from './api';
import { ExportButton } from './ExportButton';
import { bucketLabel, metricLabel } from './formatMetric';
import { filterQuery, readReportFilter, type ReportFilterValues } from './useReportFilter';

const DEFAULT_PART: DrillPart = 'numerator';

const readPart = (value: string | null): DrillPart => DRILL_PARTS.find((part) => part === value) ?? DEFAULT_PART;

function useTicketColumns(): Column<DrilldownTicketRow>[] {
  const { t } = useTranslation('reports');
  return [
    { key: 'number', header: t('drilldown.columns.number'), renderCell: (row) => <TicketLink row={row} /> },
    { key: 'subject', header: t('drilldown.columns.subject'), renderCell: (row) => row.subject },
    { key: 'status', header: t('drilldown.columns.status'), renderCell: (row) => translateOr(`tickets:statuses.${row.status}`, row.status) },
    { key: 'priority', header: t('drilldown.columns.priority'), renderCell: (row) => translateOr(`tickets:priorities.${row.priority}`, row.priority) },
    { key: 'team', header: t('drilldown.columns.team'), renderCell: (row) => row.teamName },
    { key: 'assignee', header: t('drilldown.columns.assignee'), renderCell: (row) => row.assigneeName ?? t('buckets.unassigned') },
    { key: 'createdAt', header: t('drilldown.columns.createdAt'), renderCell: (row) => formatDateTime(row.createdAt) },
  ];
}

function useAssetColumns(): Column<DrilldownAssetRow>[] {
  const { t } = useTranslation('reports');
  const { t: tc } = useTranslation();
  return [
    { key: 'assetTag', header: t('drilldown.columns.assetTag'), renderCell: (row) => <AssetLink row={row} /> },
    { key: 'type', header: t('drilldown.columns.type'), renderCell: (row) => translateOr(`assets:types.${row.type}`, row.type) },
    { key: 'device', header: t('drilldown.columns.device'), renderCell: (row) => `${row.brand} ${row.model}` },
    { key: 'status', header: t('drilldown.columns.status'), renderCell: (row) => translateOr(`assets:statuses.${row.status}`, row.status) },
    { key: 'location', header: t('drilldown.columns.location'), renderCell: (row) => row.locationName },
    {
      key: 'warrantyEndsOn',
      header: t('drilldown.columns.warrantyEndsOn'),
      renderCell: (row) => (row.warrantyEndsOn === null ? tc('none') : formatDate(row.warrantyEndsOn)),
    },
  ];
}

function TicketLink({ row }: { row: DrilldownTicketRow }) {
  return (
    <Link to={ticketPath(row.number)} className="fw-medium link-primary text-decoration-underline">
      {row.number}
    </Link>
  );
}

function AssetLink({ row }: { row: DrilldownAssetRow }) {
  return (
    <Link to={assetPath(row.id)} className="fw-medium link-primary text-decoration-underline">
      {row.assetTag}
    </Link>
  );
}

function TicketCard({ row }: { row: DrilldownTicketRow }) {
  const { t } = useTranslation('reports');
  return (
    <div className="d-flex flex-column gap-1 small">
      <TicketLink row={row} />
      <p className="text-break">{row.subject}</p>
      <p className="text-body-secondary">
        {translateOr(`tickets:statuses.${row.status}`, row.status)} · {translateOr(`tickets:priorities.${row.priority}`, row.priority)} · {row.teamName} ·{' '}
        {row.assigneeName ?? t('buckets.unassigned')}
      </p>
      <p className="text-body-secondary">{formatDateTime(row.createdAt)}</p>
    </div>
  );
}

function AssetCard({ row }: { row: DrilldownAssetRow }) {
  return (
    <div className="d-flex flex-column gap-1 small">
      <AssetLink row={row} />
      <p>
        {translateOr(`assets:types.${row.type}`, row.type)} · {row.brand} {row.model}
      </p>
      <p className="text-body-secondary">
        {translateOr(`assets:statuses.${row.status}`, row.status)} · {row.locationName}
        {row.warrantyEndsOn !== null && ` · ${formatDate(row.warrantyEndsOn)}`}
      </p>
    </div>
  );
}

interface RowsProps {
  report: ReportName;
  selection: DrillSelection;
  filter: ReportFilterValues;
  page: number;
  onPageChange: (page: number) => void;
}

function DrilldownRows({ report, selection, filter, page, onPageChange }: RowsProps) {
  const { t } = useTranslation('reports');
  const ticketColumns = useTicketColumns();
  const assetColumns = useAssetColumns();
  const rows = useDrilldown(report, selection, filter, page);

  if (rows.isPending) {
    return (
      <DataCard>
        <LoadingState rows={TABLE_SKELETON_ROWS} />
      </DataCard>
    );
  }
  if (rows.isError) {
    return (
      <DataCard>
        <ErrorState error={rows.error} onRetry={() => void rows.refetch()} />
      </DataCard>
    );
  }
  const paged = rows.data.tickets ?? rows.data.assets;
  if (paged === null || paged.totalCount === 0) {
    return (
      <DataCard>
        <EmptyState message={t('drilldown.empty')} />
      </DataCard>
    );
  }
  return (
    <DataCard footer={<Pagination page={paged.page} pageSize={DRILLDOWN_PAGE_SIZE} totalCount={paged.totalCount} onPageChange={onPageChange} />}>
      {rows.data.tickets !== null ? (
        <ResponsiveList
          items={rows.data.tickets.items}
          columns={ticketColumns}
          getKey={(row) => row.number}
          renderCard={(row) => <TicketCard row={row} />}
          caption={t('drilldown.caption')}
        />
      ) : (
        <ResponsiveList
          items={rows.data.assets?.items ?? []}
          columns={assetColumns}
          getKey={(row) => row.id}
          renderCard={(row) => <AssetCard row={row} />}
          caption={t('drilldown.caption')}
        />
      )}
    </DataCard>
  );
}

/** Name of the selected bucket, read from the report figures (usually already cached by the report page). */
function useBucketName(report: ReportName, selection: DrillSelection, filter: ReportFilterValues): string | null {
  const wanted = selection.bucket !== '';
  const result = useReport(report, filter, wanted);
  const bucket = result.data?.figures.find((f) => f.metric === selection.metric)?.buckets?.find((b) => b.key === selection.bucket);
  return wanted && bucket !== undefined ? bucketLabel(selection.metric, bucket) : null;
}

function Drilldown({ report, metric }: { report: ReportName; metric: string }) {
  const { t } = useTranslation('reports');
  const [params, setParams] = useSearchParams();
  const filter = readReportFilter(params, report);
  const selection: DrillSelection = { metric, bucket: params.get(DRILL_PARAM.bucket) ?? '', part: readPart(params.get(DRILL_PARAM.part)) };
  const page = readPageParam(params.get(DRILL_PARAM.page));
  const bucketName = useBucketName(report, selection, filter);
  const backQuery = filterQuery(filter).toString();

  const changePage = (next: number) =>
    setParams((current) => {
      const updated = new URLSearchParams(current);
      updated.set(DRILL_PARAM.page, String(next));
      return updated;
    });

  return (
    <>
      <PageHeader
        icon={ROUTE_META.reports.icon}
        titleOverride={metricLabel(metric)}
        description={
          bucketName !== null || selection.part !== DEFAULT_PART ? (
            <>
              {bucketName !== null && <span>{bucketName}</span>}
              {selection.part !== DEFAULT_PART && <span>{t('drilldown.denominator')}</span>}
            </>
          ) : undefined
        }
        actions={
          <>
            <Link to={backQuery === '' ? reportPath(report) : `${reportPath(report)}?${backQuery}`} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center gap-2">
              <CIcon icon={cilArrowLeft} aria-hidden="true" />
              {t('drilldown.back')}
            </Link>
            <ExportButton report={report} selection={selection} filter={filter} />
          </>
        }
      />
      <DrilldownRows report={report} selection={selection} filter={filter} page={page} onPageChange={changePage} />
    </>
  );
}

/** `/reports/:report/list?metric=…`: the rows behind one figure, bucket or ratio part, with the report's filters. */
export function DrilldownPage() {
  const { t } = useTranslation('reports');
  const { report } = useParams();
  const [params] = useSearchParams();
  const metric = params.get(DRILL_PARAM.metric) ?? '';

  if (!isReportName(report)) {
    return <EmptyState message={t('page.unknown')} />;
  }
  if (metric === '') {
    return <EmptyState message={t('drilldown.missingMetric')} />;
  }
  return <Drilldown report={report} metric={metric} />;
}
