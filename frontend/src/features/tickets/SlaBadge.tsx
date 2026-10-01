import { useTranslation } from 'react-i18next';

import { formatDateTime } from '../../shared/lib/format';

import type { TicketSla } from './api';
import { slaState, useFormatBusinessDuration, type SlaState } from './slaState';

const BADGE_CLASS = 'inline-flex flex-wrap items-center gap-x-1 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1';

const STATE_CLASS: Record<SlaState, string> = {
  running: 'bg-slate-50 text-slate-800 ring-slate-200',
  paused: 'bg-slate-100 text-slate-700 ring-slate-300',
  warned: 'bg-amber-50 text-amber-900 ring-amber-200',
  breached: 'bg-red-50 text-red-800 ring-red-200',
  met: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  metBreached: 'bg-red-50 text-red-800 ring-red-200',
  cancelled: 'bg-slate-100 text-slate-700 ring-slate-200',
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
    return <span className="text-sm text-slate-600">{t('sla.none')}</span>;
  }
  return (
    <ul className="flex flex-col items-start gap-1">
      {sla.map((item) => (
        <li key={item.metric} className="flex flex-col gap-0.5">
          <SlaBadge sla={item} />
          {showDue && item.dueAt !== null && <span className="text-xs text-slate-600">{t('sla.due', { date: formatDateTime(item.dueAt) })}</span>}
        </li>
      ))}
    </ul>
  );
}
