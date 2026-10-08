import { lazy, Suspense, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { figureListHref, type ReportFigure, type ReportName } from './api';
import { bucketLabel, describeFigure, formatNumber, metricLabel } from './formatMetric';
import type { ReportFilterValues } from './useReportFilter';

// Recharts stays out of the page chunk: it loads with the first distribution card (vendor-charts).
const DistributionChart = lazy(async () => ({ default: (await import('./DistributionChart')).DistributionChart }));

export interface FigureCardProps {
  report: ReportName;
  figure: ReportFigure;
  filter: ReportFilterValues;
}

export function FigureCard({ report, figure, filter }: FigureCardProps) {
  const { t } = useTranslation('reports');
  const view = describeFigure(figure);
  const title = metricLabel(figure.metric);
  const titleId = useId();
  const headlineId = useId();
  const bars = figure.shape === 'distribution' ? (figure.buckets ?? []).map((b) => ({ key: b.key, label: bucketLabel(figure.metric, b), count: b.count })) : [];

  return (
    // Named by its title and described by its headline, so the value can be read (and found) apart from the card's other numbers.
    <article
      aria-labelledby={titleId}
      aria-describedby={view.headline !== null ? headlineId : undefined}
      className="flex min-w-0 flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
    >
      <h2 id={titleId} className="font-medium">
        <Link to={figureListHref(report, figure.metric, filter)} className="text-brand-900 underline-offset-2 hover:underline">
          {title}
        </Link>
      </h2>
      {view.noData && <p className="text-lg text-slate-500">{t('figure.noData')}</p>}
      {view.headline !== null && (
        <p id={headlineId} className="text-3xl font-semibold text-brand-900">
          {view.headline}
        </p>
      )}
      {view.lines.length > 0 && (
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
          {view.lines.map((line) => (
            <div key={line.label} className="contents">
              <dt className="text-sm text-slate-600">{line.label}</dt>
              <dd className="font-semibold text-brand-900">{line.text}</dd>
            </div>
          ))}
        </dl>
      )}
      {view.note !== null && <p className="text-sm text-slate-600">{view.note}</p>}
      {figure.shape === 'ratio' && figure.denominator !== null && (
        <Link to={figureListHref(report, figure.metric, filter, '', 'denominator')} className="self-start text-sm text-brand-900 underline">
          {t('figure.openDenominator', { count: figure.denominator })}
        </Link>
      )}
      {bars.length > 0 && (
        <>
          <Suspense fallback={<p className="text-sm text-slate-500">{t('figure.chartLoading')}</p>}>
            <DistributionChart bars={bars} />
          </Suspense>
          <ul aria-label={t('figure.buckets')} className="flex flex-col gap-1 text-sm">
            {bars.map((bar) => (
              <li key={bar.key} className="flex items-baseline justify-between gap-3">
                <Link to={figureListHref(report, figure.metric, filter, bar.key)} className="min-w-0 break-words text-brand-900 underline">
                  {bar.label}
                </Link>
                <span className="font-medium tabular-nums">{formatNumber(bar.count)}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </article>
  );
}
