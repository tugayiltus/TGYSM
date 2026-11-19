import { describe, expect, it } from 'vitest';

import { addDays, eachDay, isIsoDay, monthRange, shiftMonth, weekRange, windowDays } from './calendarRange';

describe('calendarRange', () => {
  it('covers a month with whole Monday-first weeks', () => {
    expect(monthRange('2026-11')).toEqual({ from: '2026-10-26', to: '2026-12-06' });
    expect(monthRange('2026-02')).toEqual({ from: '2026-01-26', to: '2026-03-01' });
  });

  it('gives the Monday-first week of a day', () => {
    expect(weekRange('2026-11-18')).toEqual({ from: '2026-11-16', to: '2026-11-22' });
    expect(weekRange('2026-11-22')).toEqual({ from: '2026-11-16', to: '2026-11-22' });
    expect(weekRange('2026-11-16')).toEqual({ from: '2026-11-16', to: '2026-11-22' });
  });

  it('moves days and months, cutting the day to the shorter month', () => {
    expect(addDays('2026-12-30', 3)).toBe('2027-01-02');
    expect(shiftMonth('2026-01-31', 1)).toBe('2026-02-28');
    expect(shiftMonth('2026-01-15', -1)).toBe('2025-12-15');
    expect(eachDay({ from: '2026-11-30', to: '2026-12-02' })).toEqual(['2026-11-30', '2026-12-01', '2026-12-02']);
  });

  it('accepts only real calendar days', () => {
    expect(isIsoDay('2026-11-18')).toBe(true);
    expect(isIsoDay('2026-02-30')).toBe(false);
    expect(isIsoDay('abc')).toBe(false);
  });

  it('lists company-time days of a window and ignores an end at midnight', () => {
    expect(windowDays('2026-11-18T21:00:00Z', '2026-11-19T01:00:00Z')).toEqual(['2026-11-19']);
    expect(windowDays('2026-11-18T10:00:00Z', '2026-11-20T21:00:00Z')).toEqual(['2026-11-18', '2026-11-19', '2026-11-20']);
  });
});
