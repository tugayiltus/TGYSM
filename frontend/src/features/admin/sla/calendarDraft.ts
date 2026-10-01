import { ERROR_CODES } from '../../../shared/api/errors';

import { WEEKDAYS, type DayOfWeek, type WorkingHoursItem } from './api';

/** Error codes the form raises itself, with the same texts the server codes have. */
const ERROR_INVALID_HOURS = 'sla.invalid_hours';
const ERROR_CALENDAR_EMPTY = 'sla.calendar_empty';
const TIME_LENGTH = 5;
export const SECONDS_SUFFIX = ':00';
const DEFAULT_START = '09:00';
const DEFAULT_END = '18:00';

export interface DayDraft {
  open: boolean;
  starts: string;
  ends: string;
}

export type WeekDraft = Record<DayOfWeek, DayDraft>;

export function toWeekDraft(hours: readonly WorkingHoursItem[]): WeekDraft {
  const draft = {} as WeekDraft;
  for (const day of WEEKDAYS) {
    const found = hours.find((h) => h.weekday === day);
    draft[day] = found
      ? { open: true, starts: found.startsAt.slice(0, TIME_LENGTH), ends: found.endsAt.slice(0, TIME_LENGTH) }
      : { open: false, starts: DEFAULT_START, ends: DEFAULT_END };
  }
  return draft;
}

/** First problem of the form as an error code, or null. Times are "HH:mm", so text comparison orders them. */
export function validateCalendar(timeZone: string, week: WeekDraft): string | null {
  if (timeZone.trim() === '') {
    return ERROR_CODES.required;
  }
  const open = WEEKDAYS.filter((day) => week[day].open);
  if (open.length === 0) {
    return ERROR_CALENDAR_EMPTY;
  }
  return open.some((day) => week[day].starts === '' || week[day].ends === '' || week[day].starts >= week[day].ends) ? ERROR_INVALID_HOURS : null;
}
