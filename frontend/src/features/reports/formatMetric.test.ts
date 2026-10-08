import { describe, expect, it } from 'vitest';

import { BACKLOG, FIRST_RESPONSE_NO_DATA, MTTR, SLA_COMPLIANCE, TEAM_LOAD, VOLUME } from '../../test/reportData';

import { bucketLabel, describeFigure, formatDuration, metricLabel } from './formatMetric';

const SECONDS_PER_MINUTE = 60;
const SECONDS_PER_HOUR = 3_600;
const SECONDS_PER_DAY = 86_400;

describe('formatMetric', () => {
  it('shows "Veri yok" data state for a null value', () => {
    expect(describeFigure(FIRST_RESPONSE_NO_DATA)).toEqual({ noData: true, headline: null, lines: [], note: null });
  });

  it('formats a ratio as a percent with numerator / denominator', () => {
    const view = describeFigure(SLA_COMPLIANCE);
    expect(view.headline).toBe('%75');
    expect(view.note).toBe('3 / 4');
  });

  it('formats a ratio without parts as percent only', () => {
    expect(describeFigure({ ...SLA_COMPLIANCE, numerator: null }).note).toBeNull();
  });

  it('shows MTTR as calendar and SLA time, labeled separately', () => {
    const view = describeFigure(MTTR);
    expect(view.headline).toBeNull();
    expect(view.lines).toEqual([
      { label: 'Takvim süresi', text: '2 sa 0 dk' },
      { label: 'SLA süresi (mesai)', text: '1 sa 0 dk' },
    ]);
    expect(view.note).toBe('2 kayıt üzerinden');
  });

  it('drops the SLA line of MTTR when the SLA total is missing', () => {
    expect(describeFigure({ ...MTTR, buckets: null }).lines).toHaveLength(1);
  });

  it('shows a duration with its SLA bucket', () => {
    const view = describeFigure({ ...FIRST_RESPONSE_NO_DATA, value: SECONDS_PER_DAY + SECONDS_PER_HOUR, denominator: 3, buckets: [{ key: 'sla', label: null, count: 5 * SECONDS_PER_MINUTE }] });
    expect(view.lines.map((l) => l.text)).toEqual(['1 gün 1 sa', '5 dk']);
  });

  it('formats counts with Turkish grouping', () => {
    expect(describeFigure({ ...VOLUME, value: 12_345 }).headline).toBe('12.345');
  });

  it('formats durations in days, hours or minutes', () => {
    expect(formatDuration(2 * SECONDS_PER_DAY + 3 * SECONDS_PER_HOUR)).toBe('2 gün 3 sa');
    expect(formatDuration(3 * SECONDS_PER_HOUR + 15 * SECONDS_PER_MINUTE)).toBe('3 sa 15 dk');
    expect(formatDuration(59)).toBe('0 dk');
  });

  it('names metrics and buckets in Turkish, falling back to the key', () => {
    expect(metricLabel('volume')).toBe('Açılan kayıt');
    expect(metricLabel('unknown_metric')).toBe('unknown_metric');
    expect(bucketLabel(BACKLOG.metric, { key: '8+', label: null })).toBe('8 gün ve üzeri');
    expect(bucketLabel(TEAM_LOAD.metric, { key: 'x', label: 'Ağ Ekibi' })).toBe('Ağ Ekibi');
    expect(bucketLabel('by_status', { key: 'retired', label: null })).toBe('Kullanım dışı');
    expect(bucketLabel('hold_reasons', { key: 'none', label: null })).toBe('Belirtilmemiş');
  });
});
