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
        <Link to={queuePath(queue)} className="stretched-link text-decoration-none fw-semibold">
          {t(`workspace.${queue}`)}
        </Link>
        <p className="fs-4 fw-bold mb-0 mt-1">{count.data ? t('workspace.count', { count: count.data.totalCount }) : '…'}</p>
      </CCardBody>
    </CCard>
  );
}
