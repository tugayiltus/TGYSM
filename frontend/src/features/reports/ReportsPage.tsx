import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';
import { Link, Navigate, NavLink, useParams, useSearchParams } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ROUTES, reportPath } from '../../shared/routes';
import { FilterPanel } from '../../shared/ui/FilterPanel';
import { cilCloudDownload } from '../../shared/ui/icons';
import { PageCard } from '../../shared/ui/PageCard';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';

import { ActiveFilterChips } from './ActiveFilterChips';
import { DEFAULT_REPORT, isReportName, REPORT_NAMES, useReport, type ReportName } from './api';
import { FigureCard } from './FigureCard';
import { PeriodPicker } from './PeriodPicker';
import { ReportFilterBar } from './ReportFilterBar';
import { activeFilterKeys, filterQuery, readReportFilter, useReportFilter } from './useReportFilter';

/** Server ScopeLabel of IT staff (their teams only); admins get `all`. */
const TEAMS_SCOPE = 'teams';

const TAB_CLASS = 'nav-link text-nowrap';

/** Tab link of another report, carrying only the filters that report understands. */
function tabHref(target: ReportName, params: URLSearchParams): string {
  const query = filterQuery(readReportFilter(params, target)).toString();
  return query === '' ? reportPath(target) : `${reportPath(target)}?${query}`;
}

function ReportTabs() {
  const { t } = useTranslation('reports');
  const [params] = useSearchParams();
  return (
    <nav aria-label={t('page.tabs')} className="overflow-x-auto">
      <ul className="nav nav-pills flex-nowrap gap-1">
        {REPORT_NAMES.map((name) => (
          <li key={name} className="nav-item">
            <NavLink to={tabHref(name, params)} end className={TAB_CLASS}>
              {t(`tabs.${name}`)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}

function ReportView({ report }: { report: ReportName }) {
  const { t } = useTranslation('reports');
  const { filter, setFilter, setPeriod, clear, periodic, today, reversedRange } = useReportFilter(report);
  const result = useReport(report, filter);

  return (
    <>
      <PageCard bodyClassName="d-flex flex-column gap-3">
        {periodic && <PeriodPicker period={filter} today={today} onChange={setPeriod} />}
        {reversedRange && <p className="alert alert-warning mb-0">{t('page.reversedRange')}</p>}
        <FilterPanel
          title={t('filters.panel')}
          activeCount={activeFilterKeys(filter).length}
          primary={<ReportFilterBar report={report} filter={filter} onChange={setFilter} />}
          onApply={() => undefined}
          onClear={clear}
          live
        />
        <ActiveFilterChips filter={filter} onRemove={(key) => setFilter(key, '')} />
      </PageCard>
      {result.isPending && <LoadingState />}
      {result.isError && <ErrorState error={result.error} onRetry={() => void result.refetch()} />}
      {result.data !== undefined && (
        <>
          <p className="small text-body-secondary mb-0">{t(result.data.scopeLabel === TEAMS_SCOPE ? 'scope.teams' : 'scope.all')}</p>
          {result.data.figures.length === 0 ? (
            <EmptyState message={t('page.empty')} />
          ) : (
            <div
              className={`tgysm-grid tgysm-grid-figures ${result.isPlaceholderData ? 'opacity-50' : ''}`}
              aria-busy={result.isPlaceholderData}
            >
              {result.data.figures.map((figure) => (
                <FigureCard key={figure.metric} report={report} figure={figure} filter={filter} />
              ))}
            </div>
          )}
        </>
      )}
    </>
  );
}

/** `/reports/:report` (IT staff and admins): report tabs, filters kept in the URL, figure cards linking to their rows. */
export function ReportsPage() {
  const { t } = useTranslation('reports');
  const { report = DEFAULT_REPORT } = useParams();

  return (
    <section className="d-flex flex-column gap-3">
      <PageHeader
        icon={ROUTE_META.reports.icon}
        titleOverride={t('page.title')}
        actions={
          <Link to={ROUTES.reportExports} className="btn btn-outline-secondary tgysm-touch d-inline-flex align-items-center gap-2">
            <CIcon icon={cilCloudDownload} aria-hidden="true" />
            {t('page.myExports')}
          </Link>
        }
      />
      <ReportTabs />
      {isReportName(report) ? <ReportView key={report} report={report} /> : <EmptyState message={t('page.unknown')} />}
    </section>
  );
}

/** `/reports` opens the first tab. */
export function ReportsIndexRedirect() {
  return <Navigate to={reportPath(DEFAULT_REPORT)} replace />;
}
