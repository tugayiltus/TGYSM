import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../../shared/routes';

import { TicketList } from './TicketList';

const LINK_BUTTON_CLASS = 'inline-flex min-h-11 items-center justify-center rounded-lg px-4 text-sm font-medium';

export function MyTicketsPage() {
  const { t } = useTranslation('tickets');
  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t('list.title')}</h1>
        <div className="flex flex-wrap gap-2">
          <Link to={ROUTES.newIncident} className={`${LINK_BUTTON_CLASS} bg-brand-600 text-white hover:bg-brand-700`}>
            {t('list.newIncident')}
          </Link>
          <Link to={ROUTES.newRequest} className={`${LINK_BUTTON_CLASS} border border-slate-300 bg-white text-slate-800 hover:bg-slate-50`}>
            {t('list.newRequest')}
          </Link>
        </div>
      </div>
      <TicketList queue="mine" showWorkColumns={false} />
    </section>
  );
}
