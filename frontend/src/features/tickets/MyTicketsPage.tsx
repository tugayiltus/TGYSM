import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../shared/navigation/routeMeta';
import { ROUTES } from '../../shared/routes';
import { cilPlus, cilWarning } from '../../shared/ui/icons';

import { TicketList } from './TicketList';

export function MyTicketsPage() {
  const { t } = useTranslation('tickets');
  return (
    <>
      <PageHeader
        icon={ROUTE_META.myTickets.icon}
        titleOverride={t('list.title')}
        actions={
          <>
            <Link to={ROUTES.newIncident} className="btn btn-primary tgysm-touch d-inline-flex align-items-center gap-2">
              <CIcon icon={cilWarning} aria-hidden="true" />
              {t('list.newIncident')}
            </Link>
            <Link to={ROUTES.newRequest} className="btn btn-outline-primary tgysm-touch d-inline-flex align-items-center gap-2">
              <CIcon icon={cilPlus} aria-hidden="true" />
              {t('list.newRequest')}
            </Link>
          </>
        }
      />
      <TicketList queue="mine" showWorkColumns={false} />
    </>
  );
}
