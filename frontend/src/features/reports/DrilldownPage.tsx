/**
 * @input The report from the route; metric, bucket, part and page from the URL query.
 * @output The rows behind one figure, bucket or ratio part, with the report's filters, paging and export.
 * @position `/reports/:report/list`, inside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Link } from '@astryxdesign/core/Link';
import { Pagination } from '@astryxdesign/core/Pagination';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { useParams, useSearchParams } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { translateOr } from '../../shared/i18n/dynamic';
import { formatDate, formatDateTime } from '../../shared/lib/format';
import { readPageParam } from '../../shared/lib/pageParam';
import { DataList, type DataColumn } from '../../shared/list/DataList';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { assetPath, reportPath, ticketPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

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
const SKELETON_ROWS = 5;
const META_SEPARATOR = ' · ';

const readPart = (value: string | null): DrillPart => DRILL_PARTS.find((part) => part === value) ?? DEFAULT_PART;

function useTicketColumns(): DataColumn<DrilldownTicketRow>[] {
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

function useAssetColumns(): DataColumn<DrilldownAssetRow>[] {
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
  return <Link href={ticketPath(row.number)}>{row.number}</Link>;
}

function AssetLink({ row }: { row: DrilldownAssetRow }) {
  return <Link href={assetPath(row.id)}>{row.assetTag}</Link>;
}

function TicketCard({ row }: { row: DrilldownTicketRow }) {
  const { t } = useTranslation('reports');
  const meta = [
    translateOr(`tickets:statuses.${row.status}`, row.status),
    translateOr(`tickets:priorities.${row.priority}`, row.priority),
    row.teamName,
    row.assigneeName ?? t('buckets.unassigned'),
  ].join(META_SEPARATOR);
  return (
    <VStack gap={0.5}>
      <TicketLink row={row} />
      <Text wordBreak="break-word">{row.subject}</Text>
      <Text type="supporting">{meta}</Text>
      <Text type="supporting">{formatDateTime(row.createdAt)}</Text>
    </VStack>
  );
}

function AssetCard({ row }: { row: DrilldownAssetRow }) {
  const status = [translateOr(`assets:statuses.${row.status}`, row.status), row.locationName, row.warrantyEndsOn === null ? null : formatDate(row.warrantyEndsOn)]
    .filter(Boolean)
    .join(META_SEPARATOR);
  return (
    <VStack gap={0.5}>
      <AssetLink row={row} />
      <Text>{`${translateOr(`assets:types.${row.type}`, row.type)}${META_SEPARATOR}${row.brand} ${row.model}`}</Text>
      <Text type="supporting">{status}</Text>
    </VStack>
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

  const paged = rows.data?.tickets ?? rows.data?.assets ?? null;
  return (
    <VStack gap={3}>
      <Card padding={0}>
        {rows.isPending && <LoadingState rows={SKELETON_ROWS} />}
        {rows.isError && <ErrorState error={rows.error} onRetry={() => void rows.refetch()} />}
        {rows.data !== undefined && (paged === null || paged.totalCount === 0) && <EmptyState message={t('drilldown.empty')} />}
        {rows.data?.tickets != null && rows.data.tickets.totalCount > 0 && (
          <DataList items={rows.data.tickets.items} columns={ticketColumns} getKey={(row) => row.number} renderCard={(row) => <TicketCard row={row} />} caption={t('drilldown.caption')} />
        )}
        {rows.data?.tickets == null && rows.data?.assets != null && rows.data.assets.totalCount > 0 && (
          <DataList items={rows.data.assets.items} columns={assetColumns} getKey={(row) => row.id} renderCard={(row) => <AssetCard row={row} />} caption={t('drilldown.caption')} />
        )}
      </Card>
      {paged !== null && paged.totalCount > 0 && <Pagination page={paged.page} pageSize={DRILLDOWN_PAGE_SIZE} totalItems={paged.totalCount} onChange={onPageChange} />}
    </VStack>
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

  const parts = [bucketName, selection.part !== DEFAULT_PART ? t('drilldown.denominator') : null].filter((part): part is string => part !== null);
  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.reports.icon}
        titleOverride={metricLabel(metric)}
        description={
          parts.length === 0 ? undefined : (
            <HStack gap={2} wrap="wrap">
              {parts.map((part) => (
                <Text key={part} type="supporting">
                  {part}
                </Text>
              ))}
            </HStack>
          )
        }
        actions={
          <HStack gap={2} vAlign="start" wrap="wrap">
            <Button label={t('drilldown.back')} variant="secondary" href={backQuery === '' ? reportPath(report) : `${reportPath(report)}?${backQuery}`} icon={<Icon icon={Icons.arrowLeft} />} />
            <ExportButton report={report} selection={selection} filter={filter} />
          </HStack>
        }
      />
      <DrilldownRows report={report} selection={selection} filter={filter} page={page} onPageChange={changePage} />
    </VStack>
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
