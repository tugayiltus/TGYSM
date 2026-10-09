import CIcon from '@coreui/icons-react';
import { CCard, CCardBody, CListGroup, CListGroupItem } from '@coreui/react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { linkState } from '../../shared/navigation/returnTo';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ROUTES, ticketPath } from '../../shared/routes';
import { cilBook, cilLaptop, cilPlus, cilWarning } from '../../shared/ui/icons';
import type { KpiColor } from '../../shared/ui/KpiCard';
import { PageCard } from '../../shared/ui/PageCard';
import { EmptyState, ErrorState, LoadingState } from '../../shared/ui/States';
import { isItStaffOrAdmin, useSession } from '../auth/session';
import { useTickets, type TicketQueue } from '../tickets/api';
import { TicketStatusBadge } from '../tickets/TicketBadges';
import { QueueCard } from '../workspace/QueueCard';

const RECENT_COUNT = 5;
const FIRST_PAGE = 1;
const HOME_QUEUES: readonly TicketQueue[] = ['assigned_to_me', 'team', 'sla_risk'];

/** The two things everyone comes for, as large primary actions. */
interface ActionCardProps {
  to: string;
  title: string;
  description: string;
  icon: string[];
  color: KpiColor;
}

/** A start-page shortcut in the stat widget look: colored icon box, title and one line; the whole card is the link. */
function ActionCard({ to, title, description, icon, color }: ActionCardProps) {
  return (
    <CCard className="shadow-sm h-100">
      <CCardBody className="d-flex align-items-center gap-3">
        <span className={`d-inline-flex align-items-center justify-content-center rounded-3 flex-shrink-0 tgysm-kpi-icon bg-${color}-subtle text-${color}-emphasis`} aria-hidden="true">
          <CIcon icon={icon} size="xl" />
        </span>
        <Link to={to} className="stretched-link text-decoration-none text-reset min-w-0">
          <span className="d-block fw-semibold">{title}</span>
          <span className="d-block small text-body-secondary">{description}</span>
        </Link>
      </CCardBody>
    </CCard>
  );
}

function ReportActions() {
  const { t } = useTranslation('tickets');
  const actions: ActionCardProps[] = [
    { to: ROUTES.newIncident, title: t('home.incidentCard'), description: t('home.incidentCardDescription'), icon: cilWarning, color: 'danger' },
    { to: ROUTES.newRequest, title: t('home.requestCard'), description: t('home.requestCardDescription'), icon: cilPlus, color: 'primary' },
  ];
  return (
    <div className="row g-3">
      {actions.map((action) => (
        <div key={action.to} className="col-12 col-sm-6">
          <ActionCard {...action} />
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
    <PageCard title={t('home.recentTitle')} actions={<Link to={ROUTES.tickets}>{t('home.allTickets')}</Link>} flush bodyClassName="tgysm-card-body-min">
      {tickets.isPending && <LoadingState rows={RECENT_COUNT} />}
      {tickets.isError && <ErrorState error={tickets.error} onRetry={() => void tickets.refetch()} />}
      {tickets.data?.items.length === 0 && <EmptyState message={t('home.recentEmpty')} />}
      {tickets.data !== undefined && tickets.data.items.length > 0 && (
        <CListGroup flush>
          {tickets.data.items.map((ticket) => (
            <CListGroupItem key={ticket.number} className="d-flex justify-content-between align-items-start gap-2 py-3">
              <Link to={ticketPath(ticket.number)} state={linkState(location)} className="text-decoration-none min-w-0">
                <span className="d-block small text-body-secondary">{ticket.number}</span>
                <span className="d-block text-break">{ticket.subject}</span>
              </Link>
              <TicketStatusBadge status={ticket.status} />
            </CListGroupItem>
          ))}
        </CListGroup>
      )}
    </PageCard>
  );
}

function MoreLinks() {
  const { t } = useTranslation('tickets');
  const { t: tk } = useTranslation('kb');
  const { t: ta } = useTranslation('assets');
  const links: ActionCardProps[] = [
    { to: ROUTES.kb, title: tk('home.card'), description: tk('home.cardDescription'), icon: cilBook, color: 'info' },
    { to: ROUTES.myAssets, title: ta('home.mineCard'), description: ta('home.mineCardDescription'), icon: cilLaptop, color: 'success' },
  ];
  return (
    <section aria-labelledby="home-more">
      <h2 id="home-more" className="h5 mb-3">
        {t('home.moreTitle')}
      </h2>
      <div className="row g-3">
        {links.map((link) => (
          <div key={link.to} className="col-12 col-md-6">
            <ActionCard {...link} />
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
      <PageHeader icon={ROUTE_META.home.icon} titleOverride={t('home.greeting', { name: me.firstName })} />
      <div className="d-flex flex-column gap-4">
        <ReportActions />
        {isItStaffOrAdmin(me.role) && (
          <section aria-labelledby="home-queues">
            <h2 id="home-queues" className="h5 mb-3">
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
