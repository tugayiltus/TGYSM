import { useTranslation } from 'react-i18next';

import type { Priority, TicketStatus } from './api';

const STATUS_CLASS: Record<TicketStatus, string> = {
  open: 'bg-brand-50 text-brand-900 ring-brand-200',
  in_progress: 'bg-indigo-50 text-indigo-900 ring-indigo-200',
  on_hold: 'bg-amber-50 text-amber-900 ring-amber-200',
  completed: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  cancelled: 'bg-slate-100 text-slate-700 ring-slate-200',
};

const PRIORITY_CLASS: Record<Priority, string> = {
  p1: 'bg-red-50 text-red-800 ring-red-200',
  p2: 'bg-orange-50 text-orange-900 ring-orange-200',
  p3: 'bg-slate-50 text-slate-800 ring-slate-200',
  p4: 'bg-slate-50 text-slate-600 ring-slate-200',
};

const BADGE_CLASS = 'inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ring-1';

export function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const { t } = useTranslation('tickets');
  return <span className={`${BADGE_CLASS} ${STATUS_CLASS[status]}`}>{t(`statuses.${status}`)}</span>;
}

export function PriorityBadge({ priority }: { priority: Priority }) {
  const { t } = useTranslation('tickets');
  return <span className={`${BADGE_CLASS} ${PRIORITY_CLASS[priority]}`}>{t(`priorities.${priority}`)}</span>;
}
