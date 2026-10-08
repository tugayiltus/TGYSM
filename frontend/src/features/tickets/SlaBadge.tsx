import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';

import type { TicketSla } from './api';
import { slaState, useFormatBusinessDuration, type SlaState } from './slaState';

const BADGE_CLASS = 'badge rounded-pill border fw-medium d-inline-flex flex-wrap align-items-center gap-1 text-wrap text-start';

const STATE_CLASS: Record<SlaState, string> = {
  running: 'bg-light text-body',
  paused: 'bg-secondary-subtle text-secondary-emphasis border-secondary-subtle',
  warned: 'bg-warning-subtle text-warning-emphasis border-warning-subtle',
  breached: 'bg-danger-subtle text-danger-emphasis border-danger-subtle',
  met: 'bg-success-subtle text-success-emphasis border-success-subtle',
  metBreached: 'bg-danger-subtle text-danger-emphasis border-danger-subtle',
  cancelled: 'bg-secondary-subtle text-secondary-emphasis border-secondary-subtle',
};

/** One SLA timer of a ticket: metric name plus remaining or overdue business time, or the final result. */
export function SlaBadge({ sla }: { sla: TicketSla }) {
  const { t } = useTranslation('tickets');
  const formatDuration = useFormatBusinessDuration();
  const state = slaState(sla);
  const duration = formatDuration(sla.remainingSeconds ?? 0);
  return (
    <span className={`${BADGE_CLASS} ${STATE_CLASS[state]}`} data-sla-state={state}>
      <span>{`${t(`sla.metrics.${sla.metric}`)}:`}</span>
      <span>{t(`sla.states.${state}`, { duration })}</span>
    </span>
  );
}

/** Both timers of a ticket, with the due time when the timer is running. */
export function SlaSummary({ sla, showDue = false }: { sla: readonly TicketSla[]; showDue?: boolean }) {
  const { t } = useTranslation('tickets');
  if (sla.length === 0) {
    return <span className="small text-body-secondary">{t('sla.none')}</span>;
  }
  return (
    <ul className="list-unstyled mb-0 d-flex flex-column align-items-start gap-1">
      {sla.map((item) => (
        <li key={item.metric} className="d-flex flex-column">
          <SlaBadge sla={item} />
          {showDue && item.dueAt !== null && <span className="small text-body-secondary">{t('sla.due', { date: formatDateTime(item.dueAt) })}</span>}
        </li>
      ))}
    </ul>
  );
}
