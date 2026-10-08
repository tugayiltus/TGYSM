import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTES } from '../../shared/routes';

import { TicketList } from './TicketList';

export function MyTicketsPage() {
  const { t } = useTranslation('tickets');
  return (
    <>
      <PageHeader
        titleOverride={t('list.title')}
        actions={
          <>
            <Link to={ROUTES.newIncident} className="btn btn-primary tgysm-touch d-inline-flex align-items-center">
              {t('list.newIncident')}
            </Link>
            <Link to={ROUTES.newRequest} className="btn btn-outline-primary tgysm-touch d-inline-flex align-items-center">
              {t('list.newRequest')}
            </Link>
          </>
        }
      />
      <TicketList queue="mine" showWorkColumns={false} />
    </>
  );
}
