/**
 * @input Nothing; reads the Astryx theme and the color mode.
 * @output Bar, text and grid colors for Chart.js, and a key that changes with the color mode.
 * @position Report charts.
 */
import { useTheme } from '@astryxdesign/core/theme';

/** Astryx data tokens (ADR-0027): one categorical color per series, a neutral for labels, the border for grid lines. */
export const CHART_TOKENS = {
  bar: '--color-data-categorical-blue',
  text: '--color-data-neutral',
  grid: '--color-border',
} as const;

export interface ChartColors {
  bar: string;
  text: string;
  grid: string;
}

/** Chart.js draws on a canvas, which cannot read CSS variables, so the colors are resolved values for the current mode. */
export function useChartColors() {
  const { mode, token } = useTheme();
  const colors: ChartColors = { bar: token(CHART_TOKENS.bar), text: token(CHART_TOKENS.text), grid: token(CHART_TOKENS.grid) };
  // Chart.js keeps its first colors, so the key re-mounts the chart when the mode changes.
  return { colors, key: mode };
}
