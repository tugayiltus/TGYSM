/**
 * @input A report, one of its figures and the current filter.
 * @output A card with the figure's title link, headline, detail lines, a bar chart and bucket links.
 * @position Report pages, one card per figure.
 */
import { Card } from '@astryxdesign/core/Card';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { List, ListItem } from '@astryxdesign/core/List';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { Skeleton } from '@astryxdesign/core/Skeleton';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { lazy, Suspense, useId } from 'react';
import { useTranslation } from 'react-i18next';

import { figureListHref, type ReportFigure, type ReportName } from './api';
import { chartHeight } from './chartSize';
import { bucketLabel, describeFigure, formatNumber, metricLabel } from './formatMetric';
import { companyToday, periodText } from './period';
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
  const period = periodText(filter, companyToday());
  const title = metricLabel(figure.metric);
  const titleId = useId();
  const headlineId = useId();
  const bars = figure.shape === 'distribution' ? (figure.buckets ?? []).map((b) => ({ key: b.key, label: bucketLabel(figure.metric, b), count: b.count })) : [];

  return (
    <Card padding={5}>
      {/* Named by its title and described by its headline, so the value can be read (and found) apart from the card's other numbers. */}
      <article aria-labelledby={titleId} aria-describedby={view.headline !== null ? headlineId : undefined}>
        <VStack gap={2}>
          <Heading level={2} id={titleId}>
            {view.isEmpty ? title : <Link href={figureListHref(report, figure.metric, filter)}>{title}</Link>}
          </Heading>
          {view.isEmpty && <Text type="supporting">{period === null ? t('figure.empty') : t('figure.emptyForPeriod', { period })}</Text>}
          {view.headline !== null && (
            <Text type="display-3" color="accent" id={headlineId}>
              {view.headline}
            </Text>
          )}
          {view.lines.length > 0 && (
            <MetadataList>
              {view.lines.map((line) => (
                <MetadataListItem key={line.label} label={line.label}>
                  <Text type="label" color="accent">
                    {line.text}
                  </Text>
                </MetadataListItem>
              ))}
            </MetadataList>
          )}
          {view.note !== null && <Text type="supporting">{view.note}</Text>}
          {figure.shape === 'ratio' && figure.denominator !== null && !view.isEmpty && (
            <Link href={figureListHref(report, figure.metric, filter, '', 'denominator')}>{t('figure.openDenominator', { count: figure.denominator })}</Link>
          )}
          {bars.length > 0 && (
            <>
              <Suspense fallback={<Skeleton height={chartHeight(bars.length)} aria-label={t('figure.chartLoading')} />}>
                <DistributionChart bars={bars} />
              </Suspense>
              <List density="compact" aria-label={t('figure.buckets')}>
                {bars.map((bar) => (
                  <ListItem
                    key={bar.key}
                    label={<Link href={figureListHref(report, figure.metric, filter, bar.key)}>{bar.label}</Link>}
                    endContent={
                      <HStack>
                        <Text type="code">{formatNumber(bar.count)}</Text>
                      </HStack>
                    }
                  />
                ))}
              </List>
            </>
          )}
        </VStack>
      </article>
    </Card>
  );
}
