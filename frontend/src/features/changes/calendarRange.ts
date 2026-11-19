/**
 * @input ISO calendar days (`yyyy-MM-dd`) and months (`yyyy-MM`).
 * @output Monday-first ranges, day arithmetic and company-time day keys for the maintenance calendar.
 * @position Used by `ChangeCalendarPage` and its tests.
 */
import { APP_TIME_ZONE } from '../../shared/lib/format';

export interface DateRange {
  from: string;
  to: string;
}

const MS_PER_DAY = 86_400_000;
const DAYS_PER_WEEK = 7;
const MIDNIGHT_UTC = 'T00:00:00Z';
const MONTH_LENGTH = 7;
const DAY_LENGTH = 10;
const DAY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const DAY_KEY_LOCALE = 'en-CA';

const dayKeyFormatter = new Intl.DateTimeFormat(DAY_KEY_LOCALE, { timeZone: APP_TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit' });

const toDate = (day: string): Date => new Date(`${day}${MIDNIGHT_UTC}`);
const toDay = (date: Date): string => date.toISOString().slice(0, DAY_LENGTH);

/** True when the text is a real calendar day such as `2026-11-18`. */
export function isIsoDay(value: string): boolean {
  return DAY_PATTERN.test(value) && !Number.isNaN(toDate(value).getTime()) && toDay(toDate(value)) === value;
}

export const addDays = (day: string, count: number): string => toDay(new Date(toDate(day).getTime() + count * MS_PER_DAY));

/** Same day number in the month `delta` months away; the day is cut back when that month is shorter. */
export function shiftMonth(day: string, delta: number): string {
  const date = toDate(day);
  const target = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + delta, 1));
  const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
  return toDay(new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth(), Math.min(date.getUTCDate(), lastDay))));
}

/** The Monday on or before the day. */
export function mondayOf(day: string): string {
  const sinceMonday = (toDate(day).getUTCDay() + DAYS_PER_WEEK - 1) % DAYS_PER_WEEK;
  return addDays(day, -sinceMonday);
}

/** The whole Monday-first weeks that cover a month (`yyyy-MM`). */
export function monthRange(month: string): DateRange {
  const first = `${month.slice(0, MONTH_LENGTH)}-01`;
  const last = addDays(shiftMonth(first, 1), -1);
  return { from: mondayOf(first), to: addDays(mondayOf(last), DAYS_PER_WEEK - 1) };
}

/** The Monday-first week that holds the day. */
export function weekRange(day: string): DateRange {
  const from = mondayOf(day);
  return { from, to: addDays(from, DAYS_PER_WEEK - 1) };
}

/** Every day from `from` to `to`, both included. */
export function eachDay({ from, to }: DateRange): string[] {
  const days: string[] = [];
  for (let day = from; day <= to; day = addDays(day, 1)) {
    days.push(day);
  }
  return days;
}

/** The company-time calendar day of a stored instant. */
export const dayOfInstant = (iso: string): string => dayKeyFormatter.format(new Date(iso));

export const todayInCompanyZone = (): string => dayKeyFormatter.format(new Date());

/** Company-time days a half-open window `[start, end)` touches; an end at midnight does not add that day. */
export function windowDays(startIso: string, endIso: string): string[] {
  const first = dayOfInstant(startIso);
  const lastInstant = new Date(Math.max(new Date(startIso).getTime(), new Date(endIso).getTime() - 1));
  return eachDay({ from: first, to: dayOfInstant(lastInstant.toISOString()) });
}
