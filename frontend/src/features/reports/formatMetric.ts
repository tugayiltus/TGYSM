import i18n from '../../shared/i18n';
import { translateOr } from '../../shared/i18n/dynamic';
import { TURKISH_LOCALE } from '../../shared/lib/turkish';

import type { ReportBucket, ReportFigure } from './api';

const SECONDS_PER_MINUTE = 60;
const MINUTES_PER_HOUR = 60;
const HOURS_PER_DAY = 24;
const PERCENT_FRACTION_DIGITS = 1;

/** Server unit of durations (MetricKeys.UnitSeconds); a ratio in seconds (MTTR) is an average time, not a percentage. */
const UNIT_SECONDS = 'seconds';
/** Bucket keys of duration figures (MetricKeys.Sla) and of MTTR (IncidentMetrics.MttrSla, a total over the denominator). */
const SLA_AVERAGE_BUCKET = 'sla';
const MTTR_SLA_TOTAL_BUCKET = 'mttr_sla';

const numberFormat = new Intl.NumberFormat(TURKISH_LOCALE);
const percentFormat = new Intl.NumberFormat(TURKISH_LOCALE, { style: 'percent', maximumFractionDigits: PERCENT_FRACTION_DIGITS });

export const formatNumber = (value: number): string => numberFormat.format(value);

export const formatPercent = (ratio: number): string => percentFormat.format(ratio);

/** "2 gün 3 sa", "3 sa 15 dk" or "15 dk"; seconds are dropped. */
export function formatDuration(seconds: number): string {
  const totalMinutes = Math.floor(Math.abs(seconds) / SECONDS_PER_MINUTE);
  const totalHours = Math.floor(totalMinutes / MINUTES_PER_HOUR);
  const days = Math.floor(totalHours / HOURS_PER_DAY);
  if (days > 0) {
    return i18n.t('reports:duration.daysHours', { days, hours: totalHours % HOURS_PER_DAY });
  }
  if (totalHours > 0) {
    return i18n.t('reports:duration.hoursMinutes', { hours: totalHours, minutes: totalMinutes % MINUTES_PER_HOUR });
  }
  return i18n.t('reports:duration.minutes', { minutes: totalMinutes });
}

export const metricLabel = (metric: string): string => translateOr(`reports:metrics.${metric}`, metric);

/** Null-label bucket keys whose names live with their domain (hold reasons, asset types and statuses). */
const BUCKET_NAMESPACES: Readonly<Record<string, string>> = {
  hold_reasons: 'tickets:holdReasons',
  by_type: 'assets:types',
  by_status: 'assets:statuses',
  maintenance_lost_retired: 'assets:statuses',
};

/** The server's label (team, person, place) or the Turkish name of a key (ageing band, enum value, "none"). */
export function bucketLabel(metric: string, bucket: Pick<ReportBucket, 'key' | 'label'>): string {
  if (bucket.label !== null) {
    return bucket.label;
  }
  const common = translateOr(`reports:buckets.${bucket.key}`, bucket.key);
  const namespace = BUCKET_NAMESPACES[metric];
  return namespace === undefined ? common : translateOr(`${namespace}.${bucket.key}`, common);
}

export interface FigureLine {
  label: string;
  text: string;
}

/** What a figure card shows: a headline number, or labeled lines (calendar and SLA time), plus a note. */
export interface FigureView {
  noData: boolean;
  headline: string | null;
  lines: FigureLine[];
  note: string | null;
}

const bucketCount = (figure: ReportFigure, key: string) => figure.buckets?.find((b) => b.key === key)?.count;

const sampleNote = (figure: ReportFigure) => (figure.denominator === null ? null : i18n.t('reports:figure.sample', { count: figure.denominator }));

function durationView(figure: ReportFigure, calendar: number, sla: number | undefined): FigureView {
  const lines = [{ label: i18n.t('reports:figure.calendar'), text: formatDuration(calendar) }];
  if (sla !== undefined) {
    lines.push({ label: i18n.t('reports:figure.sla'), text: formatDuration(sla) });
  }
  return { noData: false, headline: null, lines, note: sampleNote(figure) };
}

export function describeFigure(figure: ReportFigure): FigureView {
  if (figure.value === null) {
    return { noData: true, headline: null, lines: [], note: null };
  }
  if (figure.shape === 'duration') {
    return durationView(figure, figure.value, bucketCount(figure, SLA_AVERAGE_BUCKET));
  }
  if (figure.shape === 'ratio' && figure.unit === UNIT_SECONDS) {
    const slaTotal = bucketCount(figure, MTTR_SLA_TOTAL_BUCKET);
    const denominator = figure.denominator ?? 0;
    return durationView(figure, figure.value, slaTotal === undefined || denominator === 0 ? undefined : slaTotal / denominator);
  }
  if (figure.shape === 'ratio') {
    const note =
      figure.numerator === null || figure.denominator === null
        ? null
        : i18n.t('reports:figure.ratio', { numerator: formatNumber(figure.numerator), denominator: formatNumber(figure.denominator) });
    return { noData: false, headline: formatPercent(figure.value), lines: [], note };
  }
  return { noData: false, headline: formatNumber(figure.value), lines: [], note: null };
}
