import CIcon from '@coreui/icons-react';
import { CCard, CCardBody } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { cilGroup, cilInbox, cilMediaPause, cilUser, cilWarning } from '../../shared/ui/icons';
import type { KpiColor } from '../../shared/ui/KpiCard';
import { useTickets, type TicketQueue } from '../tickets/api';

import { queuePath } from './queuePath';

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;

/** Icon and color per queue, in the stat widget look of the dashboards (spec §3). */
const QUEUE_LOOK: Record<TicketQueue, { icon: string[]; color: KpiColor }> = {
  mine: { icon: cilUser, color: 'primary' },
  assigned_to_me: { icon: cilUser, color: 'primary' },
  team: { icon: cilGroup, color: 'info' },
  unassigned: { icon: cilInbox, color: 'warning' },
  on_hold: { icon: cilMediaPause, color: 'success' },
  sla_risk: { icon: cilWarning, color: 'danger' },
};

/** One queue with its record count (the list endpoint's total for a one-row page). */
export function QueueCard({ queue }: { queue: TicketQueue }) {
  const { t } = useTranslation('tickets');
  const count = useTickets({ queue, status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  const look = QUEUE_LOOK[queue];
  return (
    <CCard className="shadow-sm h-100">
      <CCardBody className="d-flex align-items-center gap-3">
        <span className={`d-inline-flex align-items-center justify-content-center rounded-3 flex-shrink-0 tgysm-kpi-icon bg-${look.color}-subtle text-${look.color}-emphasis`} aria-hidden="true">
          <CIcon icon={look.icon} size="xl" />
        </span>
        {/* The count is part of the link, so the link reads "SLA Riski 3 kayıt". */}
        <Link to={queuePath(queue)} className="stretched-link text-decoration-none text-reset d-flex flex-column min-w-0">
          <span className="small text-body-secondary text-uppercase fw-semibold">{t(`workspace.${queue}`)}</span>
          <span className="fs-4 fw-semibold">{count.data ? t('workspace.count', { count: count.data.totalCount }) : '…'}</span>
        </Link>
      </CCardBody>
    </CCard>
  );
}
