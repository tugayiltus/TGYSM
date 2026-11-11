/**
 * @input The report name from the route; filters from the URL query.
 * @output Report tabs, period and filters (kept in the URL), and figure cards that link to their rows.
 * @position `/reports/:report`, inside the app shell (IT staff and admins).
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Grid } from '@astryxdesign/core/Grid';
import { HStack } from '@astryxdesign/core/HStack';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { useTranslation } from 'react-i18next';
import { Navigate, useNavigate, useParams, useSearchParams } from 'react-router';

import { EmptyState, ErrorState, LoadingState } from '../../shared/feedback/States';
import { ListToolbar } from '../../shared/list/ListToolbar';
import { useIsPhone } from '../../shared/list/useIsPhone';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { SectionCard } from '../../shared/page/SectionCard';
import { ROUTES, reportPath } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { ActiveFilterChips } from './ActiveFilterChips';
import { DEFAULT_REPORT, isReportName, REPORT_NAMES, useReport, type ReportName } from './api';
import { FigureCard } from './FigureCard';
import { PeriodPicker } from './PeriodPicker';
import { ReportFilterBar } from './ReportFilterBar';
import { activeFilterKeys, filterQuery, readReportFilter, useReportFilter } from './useReportFilter';

/** Server ScopeLabel of IT staff (their teams only); admins get `all`. */
const TEAMS_SCOPE = 'teams';
/** Figure cards reflow from one column on phones to several on wide screens. */
const FIGURE_COLUMNS = { minWidth: 300 } as const;

/** Tab link of another report, carrying only the filters that report understands. */
function tabHref(target: ReportName, params: URLSearchParams): string {
  const query = filterQuery(readReportFilter(params, target)).toString();
  return query === '' ? reportPath(target) : `${reportPath(target)}?${query}`;
}

function ReportTabs({ current }: { current: string }) {
  const { t } = useTranslation('reports');
  const [params] = useSearchParams();
  const navigate = useNavigate();
  // Six link tabs do not fit a phone row (part of it scrolled sideways); one selector reaches every report.
  if (useIsPhone()) {
    return (
      <Selector
        label={t('page.reportType')}
        options={REPORT_NAMES.map((name) => ({ value: name, label: t(`tabs.${name}`) }))}
        value={current}
        onChange={(value) => isReportName(value) && void navigate(tabHref(value, params))}
      />
    );
  }
  return (
    // Link tabs: Astryx renders them as a navigation landmark and marks the current one.
    <TabList aria-label={t('page.tabs')} value={current} onChange={() => undefined} hasDivider>
      {REPORT_NAMES.map((name) => (
        <Tab key={name} value={name} label={t(`tabs.${name}`)} href={tabHref(name, params)} />
      ))}
    </TabList>
  );
}

function ReportView({ report }: { report: ReportName }) {
  const { t } = useTranslation('reports');
  const { filter, setFilter, setPeriod, clear, periodic, today, reversedRange } = useReportFilter(report);
  const result = useReport(report, filter);
  const { t: tc } = useTranslation();
  const isPhone = useIsPhone();
  const activeCount = activeFilterKeys(filter).length;
  const filterBar = <ReportFilterBar report={report} filter={filter} onChange={setFilter} />;

  return (
    <>
      <SectionCard title={t('filters.panel')}>
        {periodic && <PeriodPicker period={filter} today={today} onChange={setPeriod} />}
        {reversedRange && <Banner status="warning" title={t('page.reversedRange')} />}
        {isPhone ? (
          <ListToolbar title={t('filters.panel')} filters={filterBar} activeCount={activeCount} onClear={clear} />
        ) : (
          // Wide screens: the filters take the card's width; the toolbar row would squeeze them into one column.
          filterBar
        )}
        <HStack gap={2} wrap="wrap" vAlign="center">
          <ActiveFilterChips filter={filter} onRemove={(key) => setFilter(key, '')} />
          {!isPhone && activeCount > 0 && <Button label={tc('filters.clear')} variant="ghost" onClick={clear} />}
        </HStack>
      </SectionCard>
      {result.isPending && <LoadingState />}
      {result.isError && <ErrorState error={result.error} onRetry={() => void result.refetch()} />}
      {result.data !== undefined && (
        <>
          <Text type="supporting">{t(result.data.scopeLabel === TEAMS_SCOPE ? 'scope.teams' : 'scope.all')}</Text>
          {result.data.figures.length === 0 ? (
            <EmptyState message={t('page.empty')} />
          ) : (
            <Grid columns={FIGURE_COLUMNS} gap={4} align="start" aria-busy={result.isPlaceholderData}>
              {result.data.figures.map((figure) => (
                <FigureCard key={figure.metric} report={report} figure={figure} filter={filter} />
              ))}
            </Grid>
          )}
        </>
      )}
    </>
  );
}

export function ReportsPage() {
  const { t } = useTranslation('reports');
  const { report = DEFAULT_REPORT } = useParams();

  return (
    <VStack gap={4}>
      <PageHeader
        icon={ROUTE_META.reports.icon}
        titleOverride={t('page.title')}
        actions={<Button label={t('page.myExports')} variant="secondary" href={ROUTES.reportExports} icon={<Icon icon={Icons.download} />} />}
      />
      <ReportTabs current={report} />
      {isReportName(report) ? <ReportView key={report} report={report} /> : <EmptyState message={t('page.unknown')} />}
    </VStack>
  );
}

/** `/reports` opens the first tab. */
export function ReportsIndexRedirect() {
  return <Navigate to={reportPath(DEFAULT_REPORT)} replace />;
}
