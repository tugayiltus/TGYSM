import { CCard, CCardBody, CListGroup, CListGroupItem } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTES, ticketPath } from '../../shared/routes';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { isItStaffOrAdmin, useSession } from '../auth/session';
import { useTickets, type TicketQueue } from '../tickets/api';
import { TicketStatusBadge } from '../tickets/TicketBadges';
import { QueueCard } from '../workspace/QueueCard';

const RECENT_COUNT = 5;
const FIRST_PAGE = 1;
const HOME_QUEUES: readonly TicketQueue[] = ['assigned_to_me', 'team', 'sla_risk'];

/** The two things everyone comes for, as large primary actions. */
function ReportActions() {
  const { t } = useTranslation('tickets');
  const actions = [
    { to: ROUTES.newIncident, title: t('home.incidentCard'), description: t('home.incidentCardDescription') },
    { to: ROUTES.newRequest, title: t('home.requestCard'), description: t('home.requestCardDescription') },
  ];
  return (
    <div className="row g-3">
      {actions.map((action, index) => (
        <div key={action.to} className="col-12 col-sm-6">
          <Link to={action.to} className={`btn ${index === 0 ? 'btn-primary' : 'btn-outline-primary'} w-100 h-100 text-start p-3 d-flex flex-column`}>
            <span className="fs-5 fw-semibold">{action.title}</span>
            <span className="small">{action.description}</span>
          </Link>
        </div>
      ))}
    </div>
  );
}

function RecentTickets() {
  const { t } = useTranslation('tickets');
  const location = useLocation();
  const tickets = useTickets({ queue: 'mine', status: undefined, kind: undefined, priority: undefined, search: '', page: FIRST_PAGE, pageSize: RECENT_COUNT });
  return (
    <section aria-labelledby="home-recent">
      <div className="d-flex justify-content-between align-items-center mb-2">
        <h2 id="home-recent" className="h5 mb-0">
          {t('home.recentTitle')}
        </h2>
        <Link to={ROUTES.tickets}>{t('home.allTickets')}</Link>
      </div>
      {tickets.isPending && <LoadingState />}
      {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
      {tickets.data?.items.length === 0 && <EmptyState message={t('home.recentEmpty')} />}
      {tickets.data !== undefined && tickets.data.items.length > 0 && (
        <CCard>
          <CListGroup flush>
            {tickets.data.items.map((ticket) => (
              <CListGroupItem key={ticket.number} className="d-flex justify-content-between align-items-start gap-2">
                <Link to={ticketPath(ticket.number)} state={linkState(location)} className="text-decoration-none min-w-0">
                  <span className="d-block small text-body-secondary">{ticket.number}</span>
                  <span className="d-block text-break">{ticket.subject}</span>
                </Link>
                <TicketStatusBadge status={ticket.status} />
              </CListGroupItem>
            ))}
          </CListGroup>
        </CCard>
      )}
    </section>
  );
}

function MoreLinks() {
  const { t } = useTranslation('tickets');
  const { t: tk } = useTranslation('kb');
  const { t: ta } = useTranslation('assets');
  const links = [
    { to: ROUTES.kb, title: tk('home.card'), description: tk('home.cardDescription') },
    { to: ROUTES.myAssets, title: ta('home.mineCard'), description: ta('home.mineCardDescription') },
  ];
  return (
    <section aria-labelledby="home-more">
      <h2 id="home-more" className="h5">
        {t('home.moreTitle')}
      </h2>
      <div className="row g-3">
        {links.map((link) => (
          <div key={link.to} className="col-12 col-md-6">
            <CCard className="h-100">
              <CCardBody>
                <Link to={link.to} className="stretched-link text-decoration-none">
                  <span className="d-block fw-semibold">{link.title}</span>
                  <span className="d-block small text-body-secondary mt-1">{link.description}</span>
                </Link>
              </CCardBody>
            </CCard>
          </div>
        ))}
      </div>
    </section>
  );
}

/** Role-based start page (spec §7): employees get the report actions and their latest records; IT staff also their queues. */
export function HomePage() {
  const { t } = useTranslation('auth');
  const { t: tt } = useTranslation('tickets');
  const me = useSession();
  return (
    <>
      <PageHeader titleOverride={t('home.greeting', { name: me.firstName })} />
      <div className="d-flex flex-column gap-4">
        <ReportActions />
        {isItStaffOrAdmin(me.role) && (
          <section aria-labelledby="home-queues">
            <h2 id="home-queues" className="h5">
              {tt('home.queuesTitle')}
            </h2>
            <div className="row g-3">
              {HOME_QUEUES.map((queue) => (
                <div key={queue} className="col-12 col-md-4">
                  <QueueCard queue={queue} />
                </div>
              ))}
            </div>
          </section>
        )}
        <RecentTickets />
        <MoreLinks />
      </div>
    </>
  );
}
