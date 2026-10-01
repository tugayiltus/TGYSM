import { useTranslation } from 'react-i18next';

import type { TicketSla } from './api';

const SECONDS_PER_MINUTE = 60;
const MINUTES_PER_HOUR = 60;

export type SlaState = 'running' | 'paused' | 'warned' | 'breached' | 'met' | 'metBreached' | 'cancelled';

/** A stopped timer shows its result; a running one shows how close it is to the target. */
export function slaState(sla: TicketSla): SlaState {
  if (sla.result === 'met') {
    return 'met';
  }
  if (sla.result === 'breached') {
    return 'metBreached';
  }
  if (sla.result === 'cancelled') {
    return 'cancelled';
  }
  if (sla.isBreached || (sla.remainingSeconds ?? 0) < 0) {
    return 'breached';
  }
  if (sla.isPaused) {
    return 'paused';
  }
  return sla.isWarned ? 'warned' : 'running';
}

export function useFormatBusinessDuration() {
  const { t } = useTranslation('tickets');
  return (seconds: number) => {
    const totalMinutes = Math.floor(Math.abs(seconds) / SECONDS_PER_MINUTE);
    const hours = Math.floor(totalMinutes / MINUTES_PER_HOUR);
    const minutes = totalMinutes % MINUTES_PER_HOUR;
    return hours > 0 ? t('sla.duration.hoursMinutes', { hours, minutes }) : t('sla.duration.minutes', { minutes });
  };
}
