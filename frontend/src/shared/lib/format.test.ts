import { describe, expect, it } from 'vitest';

import { formatDate, formatDateTime } from './format';

describe('formatDateTime', () => {
  it('formats UTC timestamps in Istanbul time with Turkish month names', () => {
    const text = formatDateTime('2026-09-26T09:05:00Z');

    expect(text).toContain('Eyl');
    expect(text).toContain('12:05');
  });
});

describe('formatDate', () => {
  it('formats a calendar date without shifting it across time zones', () => {
    expect(formatDate('2027-12-31')).toBe('31 Ara 2027');
  });
});
