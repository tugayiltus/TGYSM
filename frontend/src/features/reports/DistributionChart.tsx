/**
 * @input The bars of a distribution: key, label and count.
 * @output A horizontal bar chart; hidden from screen readers because the card lists the same numbers as links.
 * @position Report figure cards; loaded lazily in its own chunk.
 */
import { BarElement, CategoryScale, Chart as ChartJS, LinearScale, Tooltip } from 'chart.js';
import { Bar } from 'react-chartjs-2';

import { useChartColors } from '../../shared/ui/chartTheme';

import { chartHeight } from './chartSize';
import { formatNumber } from './formatMetric';

// Only the parts a horizontal bar chart needs, so the chart chunk stays small (ADR-0022).
ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip);

export interface ChartBar {
  key: string;
  label: string;
  count: number;
}

const BAR_RADIUS_PX = 4;

export function DistributionChart({ bars }: { bars: readonly ChartBar[] }) {
  const height = chartHeight(bars.length);
  const { colors, key } = useChartColors();
  return (
    // Chart.js sizes the canvas from its parent, which must be positioned and have a height. No Astryx layout
    // component sets both, so this one raw element keeps an inline style.
    <div aria-hidden="true" data-testid="distribution-chart" style={{ position: 'relative', height }}>
      <Bar
        key={key}
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
