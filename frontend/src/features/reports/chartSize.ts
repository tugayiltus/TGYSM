const BAR_ROW_PX = 32;
const CHART_PADDING_PX = 24;

/** Height of a distribution chart with this many bars; the loading placeholder takes the same height, so nothing jumps. */
export const chartHeight = (barCount: number) => barCount * BAR_ROW_PX + CHART_PADDING_PX;
