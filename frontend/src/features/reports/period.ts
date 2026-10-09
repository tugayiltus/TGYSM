import { APP_TIME_ZONE } from '../../shared/lib/format';

import type { ReportName } from './api';

/** Reports over a time window; the asset report is a snapshot and warranty uses its days as an expiry window. */
export const PERIODIC_REPORTS: readonly ReportName[] = ['incidents', 'requests', 'operations', 'transfers'];

export const DEFAULT_PERIOD_DAYS = 30;
const WEEK_DAYS = 7;
const MS_PER_DAY = 86_400_000;
const ISO_DAY_LENGTH = 10;
const FIRST_DAY = '01';
const JANUARY = '01';

export const PERIOD_PRESETS = ['last7', 'last30', 'thisMonth', 'lastMonth', 'thisYear'] as const;
export type PeriodPreset = (typeof PERIOD_PRESETS)[number];

export interface Period {
  from: string;
  to: string;
}

/** Today in the company time zone as an ISO day (yyyy-MM-dd). */
export function companyToday(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: APP_TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
}

export function addDays(day: string, days: number): string {
  return new Date(Date.parse(`${day}T00:00:00Z`) + days * MS_PER_DAY).toISOString().slice(0, ISO_DAY_LENGTH);
}

/** The days a preset covers, both ends included, ending today (or the last day of last month). */
export function presetPeriod(preset: PeriodPreset, today: string): Period {
  const [year, month] = today.split('-');
  switch (preset) {
    case 'last7':
      return { from: addDays(today, 1 - WEEK_DAYS), to: today };
    case 'last30':
      return { from: addDays(today, 1 - DEFAULT_PERIOD_DAYS), to: today };
    case 'thisMonth':
      return { from: `${year}-${month}-${FIRST_DAY}`, to: today };
    case 'lastMonth': {
      const lastOfPrevious = addDays(`${year}-${month}-${FIRST_DAY}`, -1);
      return { from: `${lastOfPrevious.slice(0, ISO_DAY_LENGTH - FIRST_DAY.length)}${FIRST_DAY}`, to: lastOfPrevious };
    }
    case 'thisYear':
      return { from: `${year}-${JANUARY}-${FIRST_DAY}`, to: today };
  }
}

/** The preset a from/to pair matches, if any. */
export function matchPreset(period: Partial<Period>, today: string): PeriodPreset | null {
  return PERIOD_PRESETS.find((preset) => {
    const candidate = presetPeriod(preset, today);
    return candidate.from === period.from && candidate.to === period.to;
  }) ?? null;
}
