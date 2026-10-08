import { useTranslation } from 'react-i18next';
import { Link, Navigate, NavLink, useParams, useSearchParams } from 'react-router';

import { ROUTES, reportPath } from '../../shared/routes';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';

import { DEFAULT_REPORT, isReportName, REPORT_NAMES, useReport, type ReportName } from './api';
import { FigureCard } from './FigureCard';
import { ReportFilterBar } from './ReportFilterBar';
import { filterQuery, readReportFilter, useReportFilter } from './useReportFilter';

/** Server ScopeLabel of IT staff (their teams only); admins get `all`. */
const TEAMS_SCOPE = 'teams';

const TAB_CLASS =
  'block whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 aria-[current=page]:bg-brand-50 aria-[current=page]:text-brand-900';

/** Tab link of another report, carrying only the filters that report understands. */
function tabHref(target: ReportName, params: URLSearchParams): string {
  const query = filterQuery(readReportFilter(params, target)).toString();
  return query === '' ? reportPath(target) : `${reportPath(target)}?${query}`;
}

function ReportTabs() {
  const { t } = useTranslation('reports');
  const [params] = useSearchParams();
  return (
    <nav aria-label={t('page.tabs')} className="-mx-1 overflow-x-auto">
      <ul className="flex gap-1 px-1">
        {REPORT_NAMES.map((name) => (
          <li key={name}>
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
  const { filter, setFilter, clear } = useReportFilter(report);
  const result = useReport(report, filter);

  return (
    <>
      <ReportFilterBar report={report} filter={filter} onChange={setFilter} onClear={clear} />
      {result.isPending && <LoadingState />}
      {result.isError && <ErrorState error={result.error} onRetry={() => void result.refetch()} />}
      {result.data !== undefined && (
        <>
          <p className="text-sm text-slate-600">{t(result.data.scopeLabel === TEAMS_SCOPE ? 'scope.teams' : 'scope.all')}</p>
          {result.data.figures.length === 0 ? (
            <EmptyState message={t('page.empty')} />
          ) : (
            <div
              className={`grid gap-4 transition-opacity md:grid-cols-2 xl:grid-cols-3 ${result.isPlaceholderData ? 'opacity-60' : ''}`}
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
    <section className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('page.title')}</h1>
        <Link to={ROUTES.reportExports} className="text-sm font-medium text-brand-900 underline">
          {t('page.myExports')}
        </Link>
      </div>
      <ReportTabs />
      {isReportName(report) ? <ReportView key={report} report={report} /> : <EmptyState message={t('page.unknown')} />}
    </section>
  );
}

/** `/reports` opens the first tab. */
export function ReportsIndexRedirect() {
  return <Navigate to={reportPath(DEFAULT_REPORT)} replace />;
}
