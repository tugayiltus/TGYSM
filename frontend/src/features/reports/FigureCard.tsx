import { lazy, Suspense, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { figureListHref, type ReportFigure, type ReportName } from './api';
import { bucketLabel, describeFigure, formatNumber, metricLabel } from './formatMetric';
import type { ReportFilterValues } from './useReportFilter';

// Chart.js stays out of the page chunk: it loads with the first distribution card (vendor-charts).
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
      className="d-flex min-w-0 flex-column gap-2 rounded-3 border bg-body p-3 shadow-sm"
    >
      <h2 id={titleId} className="fs-6 fw-semibold mb-0">
        <Link to={figureListHref(report, figure.metric, filter)} className="link-primary link-underline-opacity-0 link-underline-opacity-100-hover">
          {title}
        </Link>
      </h2>
      {view.noData && <p className="fs-5 text-body-secondary mb-0">{t('figure.noData')}</p>}
      {view.headline !== null && (
        <p id={headlineId} className="fs-2 fw-semibold link-primary mb-0">
          {view.headline}
        </p>
      )}
      {view.lines.length > 0 && (
        <dl className="tgysm-pairs column-gap-2 row-gap-1 mb-0">
          {view.lines.map((line) => (
            <div key={line.label} className="d-contents">
              <dt className="small text-body-secondary">{line.label}</dt>
              <dd className="fw-semibold link-primary mb-0">{line.text}</dd>
            </div>
          ))}
        </dl>
      )}
      {view.note !== null && <p className="small text-body-secondary mb-0">{view.note}</p>}
      {figure.shape === 'ratio' && figure.denominator !== null && (
        <Link to={figureListHref(report, figure.metric, filter, '', 'denominator')} className="align-self-start small link-primary text-decoration-underline">
          {t('figure.openDenominator', { count: figure.denominator })}
        </Link>
      )}
      {bars.length > 0 && (
        <>
          <Suspense fallback={<p className="small text-body-secondary">{t('figure.chartLoading')}</p>}>
            <DistributionChart bars={bars} />
          </Suspense>
          <ul aria-label={t('figure.buckets')} className="list-unstyled d-flex flex-column gap-1 small mb-0">
            {bars.map((bar) => (
              <li key={bar.key} className="d-flex align-items-baseline justify-content-between gap-2">
                <Link to={figureListHref(report, figure.metric, filter, bar.key)} className="min-w-0 text-break link-primary text-decoration-underline">
                  {bar.label}
                </Link>
                <span className="fw-medium font-monospace">{formatNumber(bar.count)}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </article>
  );
}
