import { Bar, BarChart, Tooltip, XAxis, YAxis } from 'recharts';

import { formatNumber } from './formatMetric';

export interface ChartBar {
  key: string;
  label: string;
  count: number;
}

const BAR_ROW_PX = 32;
const CHART_PADDING_PX = 24;
const LABEL_WIDTH_PX = 128;
const BAR_RADIUS_PX = 4;
const BAR_FILL = 'var(--color-brand-600)';
const CHART_MARGIN = { top: 4, right: 16, bottom: 4, left: 4 };

/**
 * Horizontal bars of a distribution. Loaded lazily (own `vendor-charts` chunk); the card lists the same numbers as links,
 * so the chart is hidden from screen readers.
 */
export function DistributionChart({ bars }: { bars: readonly ChartBar[] }) {
  const height = bars.length * BAR_ROW_PX + CHART_PADDING_PX;
  return (
    <div aria-hidden="true" data-testid="distribution-chart">
      <BarChart responsive layout="vertical" data={[...bars]} style={{ width: '100%', height }} margin={CHART_MARGIN} accessibilityLayer={false}>
        <XAxis type="number" allowDecimals={false} hide />
        <YAxis type="category" dataKey="label" width={LABEL_WIDTH_PX} tickLine={false} axisLine={false} />
        <Tooltip formatter={(value) => formatNumber(Number(value))} />
        <Bar dataKey="count" fill={BAR_FILL} radius={BAR_RADIUS_PX} isAnimationActive={false} />
      </BarChart>
    </div>
  );
}
