import { TURKISH_LOCALE } from './turkish';

/** Display time zone of the institution (spec assumption; stored times are UTC). */
export const APP_TIME_ZONE = 'Europe/Istanbul';

const dateTimeFormatter = new Intl.DateTimeFormat(TURKISH_LOCALE, {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: APP_TIME_ZONE,
});

export const formatDateTime = (iso: string): string => dateTimeFormatter.format(new Date(iso));
