import { CChartBar } from '@coreui/react-chartjs';

import { useChartColors } from '../../shared/ui/chartTheme';

import { chartHeight } from './chartSize';
import { formatNumber } from './formatMetric';

export interface ChartBar {
  key: string;
  label: string;
  count: number;
}

const BAR_RADIUS_PX = 4;

/**
 * Horizontal bars of a distribution (Chart.js through CoreUI, ADR-0022). Loaded lazily in its own chunk; the card lists the
 * same numbers as links, so the canvas is hidden from screen readers.
 */
export function DistributionChart({ bars }: { bars: readonly ChartBar[] }) {
  const height = chartHeight(bars.length);
  // Colors follow the color mode; the key re-mounts the chart when the mode changes (Chart.js keeps its first colors).
  const { colors, key } = useChartColors();
  return (
    // Chart.js sizes the canvas from its parent, which must be relatively positioned and sized; CChart's own wrapper div
    // has no height, so the canvas would keep its default 150 px and spill over the bucket list below a short chart.
    <div aria-hidden="true" data-testid="distribution-chart" className="position-relative" style={{ height }}>
      <CChartBar
        key={key}
        wrapper={false}
        data={{ labels: bars.map((bar) => bar.label), datasets: [{ data: bars.map((bar) => bar.count), backgroundColor: colors.bar, borderRadius: BAR_RADIUS_PX }] }}
        options={{
          indexAxis: 'y',
          maintainAspectRatio: false,
          animation: false,
          plugins: { legend: { display: false }, tooltip: { callbacks: { label: (item) => formatNumber(Number(item.raw)) } } },
          scales: {
            x: { ticks: { precision: 0, color: colors.text }, grid: { color: colors.grid } },
            y: { ticks: { color: colors.text }, grid: { color: colors.grid } },
          },
        }}
      />
    </div>
  );
}
