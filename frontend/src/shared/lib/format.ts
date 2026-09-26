import { TURKISH_LOCALE } from './turkish';

/** Display time zone of the institution (spec assumption; stored times are UTC). */
export const APP_TIME_ZONE = 'Europe/Istanbul';

const dateTimeFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: APP_TIME_ZONE,
});

export const formatDateTime = (iso: string): string => dateTimeFormatter.format(new Date(iso));

const UTC = 'UTC';
const MIDNIGHT_UTC = 'T00:00:00Z';
const dateFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, { dateStyle: 'medium', timeZone: UTC });

/** Formats an ISO calendar date ("2027-12-31"); read as UTC midnight so no time zone moves it to another day. */
export const formatDate = (isoDate: string): string => dateFormatter.format(new Date(`${isoDate}${MIDNIGHT_UTC}`));
