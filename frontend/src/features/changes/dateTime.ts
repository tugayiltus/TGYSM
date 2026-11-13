import { APP_TIME_ZONE } from '../../shared/lib/format';

const LOCALE = 'en-CA';
const MS_PER_MINUTE = 60_000;
type PartType = 'year' | 'month' | 'day' | 'hour' | 'minute';
const MIDNIGHT_HOUR_24 = '24';
const MIDNIGHT_HOUR = '00';

const formatter = new Intl.DateTimeFormat(LOCALE, {
  timeZone: APP_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

function parts(date: Date): Record<PartType, string> {
  const found = formatter.formatToParts(date);
  const get = (type: PartType) => found.find((part) => part.type === type)?.value ?? '';
  const hour = get('hour') === MIDNIGHT_HOUR_24 ? MIDNIGHT_HOUR : get('hour');
  return { year: get('year'), month: get('month'), day: get('day'), hour, minute: get('minute') };
}

/** A stored UTC time as the institution's local `YYYY-MM-DDTHH:mm`, the form the date-time input edits. */
export function toLocalInput(iso: string | null): string {
  if (iso === null) {
    return '';
  }

  const p = parts(new Date(iso));
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`;
}

/** The inverse of `toLocalInput`: the institution's local time back to a UTC instant. */
export function fromLocalInput(local: string): string | null {
  if (local === '') {
    return null;
  }

  const asUtc = new Date(`${local}:00Z`);
  const shown = parts(asUtc);
  const shownAsUtc = new Date(`${shown.year}-${shown.month}-${shown.day}T${shown.hour}:${shown.minute}:00Z`);
  const offsetMinutes = (shownAsUtc.getTime() - asUtc.getTime()) / MS_PER_MINUTE;
  return new Date(asUtc.getTime() - offsetMinutes * MS_PER_MINUTE).toISOString();
}

/** The current moment in the local input form. */
export const nowLocalInput = (): string => toLocalInput(new Date().toISOString());
