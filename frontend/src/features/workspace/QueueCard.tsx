import { CCard, CCardBody } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { useTickets, type TicketQueue } from '../tickets/api';

import { queuePath } from './queuePath';

const COUNT_PAGE_SIZE = 1;
const FIRST_PAGE = 1;

/** One queue with its record count (the list endpoint's total for a one-row page). */
export function QueueCard({ queue }: { queue: TicketQueue }) {
  const { t } = useTranslation('tickets');
  const count = useTickets({ queue, status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: COUNT_PAGE_SIZE });
  return (
    <CCard className="h-100">
      <CCardBody>
        {/* The count is part of the link, so the link reads "SLA Riski 3 kayıt". */}
        <Link to={queuePath(queue)} className="stretched-link text-decoration-none d-flex flex-column">
          <span className="fw-semibold">{t(`workspace.${queue}`)}</span>
          <span className="fs-4 fw-bold mt-1">{count.data ? t('workspace.count', { count: count.data.totalCount }) : '…'}</span>
        </Link>
      </CCardBody>
    </CCard>
  );
}
