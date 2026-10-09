import { CChartBar } from '@coreui/react-chartjs';

import { formatNumber } from './formatMetric';

export interface ChartBar {
  key: string;
  label: string;
  count: number;
}

const BAR_ROW_PX = 32;
const CHART_PADDING_PX = 24;
const BAR_COLOR = '#1d4ed8';

/**
 * Horizontal bars of a distribution (Chart.js through CoreUI, ADR-0022). Loaded lazily in its own chunk; the card lists the
 * same numbers as links, so the canvas is hidden from screen readers.
 */
export function DistributionChart({ bars }: { bars: readonly ChartBar[] }) {
  const height = bars.length * BAR_ROW_PX + CHART_PADDING_PX;
  return (
    <div aria-hidden="true" data-testid="distribution-chart" style={{ height }}>
      <CChartBar
        data={{ labels: bars.map((bar) => bar.label), datasets: [{ data: bars.map((bar) => bar.count), backgroundColor: BAR_COLOR, borderRadius: 4 }] }}
        options={{
          indexAxis: 'y',
          maintainAspectRatio: false,
          animation: false,
          plugins: { legend: { display: false }, tooltip: { callbacks: { label: (item) => formatNumber(Number(item.raw)) } } },
          scales: { x: { ticks: { precision: 0 } } },
        }}
      />
    </div>
  );
}
