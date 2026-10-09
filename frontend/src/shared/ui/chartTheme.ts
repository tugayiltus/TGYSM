import { useColorMode } from './colorMode';

/** Used only when CoreUI's CSS variables are not available (tests, very early render). The only hex colors allowed in TS/TSX. */
export const CHART_FALLBACK = { bar: '#5856d6', text: '#252b36', grid: 'rgba(8, 10, 12, 0.175)' } as const;

export interface ChartColors {
  bar: string;
  text: string;
  grid: string;
}

const cssVar = (name: string) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

/** Bar, text and grid colors of the current color mode, read from CoreUI's variables (ADR-0026). */
export function readChartColors(): ChartColors {
  return {
    bar: cssVar('--cui-primary') || CHART_FALLBACK.bar,
    text: cssVar('--cui-body-color') || CHART_FALLBACK.text,
    grid: cssVar('--cui-border-color-translucent') || CHART_FALLBACK.grid,
  };
}

/** Colors for the current color mode; `key` changes with the mode so the chart re-mounts with the new colors. */
export function useChartColors() {
  const { resolved } = useColorMode();
  return { colors: readChartColors(), key: resolved };
}
