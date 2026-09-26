import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { isItStaffOrAdmin, useSession } from '../auth/session';

const CARD_CLASS = 'block rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-300';

export function HomePage() {
  const { t } = useTranslation('auth');
  const { t: ta } = useTranslation('assets');
  const { t: tt } = useTranslation('tickets');
  const me = useSession();

  return (
    <section className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('home.greeting', { name: me.firstName })}</h1>
        <p className="mt-1 text-slate-600">{t('home.intro')}</p>
      </div>
      <ul className="grid gap-3 sm:grid-cols-2">
        <li>
          <Link to={ROUTES.newIncident} className={CARD_CLASS}>
            <span className="block font-medium">{tt('home.incidentCard')}</span>
            <span className="mt-1 block text-sm text-slate-600">{tt('home.incidentCardDescription')}</span>
          </Link>
        </li>
        <li>
          <Link to={ROUTES.newRequest} className={CARD_CLASS}>
            <span className="block font-medium">{tt('home.requestCard')}</span>
            <span className="mt-1 block text-sm text-slate-600">{tt('home.requestCardDescription')}</span>
          </Link>
        </li>
        <li>
          <Link to={ROUTES.tickets} className={CARD_CLASS}>
            <span className="block font-medium">{tt('home.ticketsCard')}</span>
            <span className="mt-1 block text-sm text-slate-600">{tt('home.ticketsCardDescription')}</span>
          </Link>
        </li>
        {isItStaffOrAdmin(me.role) && (
          <li>
            <Link to={ROUTES.workspace} className={CARD_CLASS}>
              <span className="block font-medium">{tt('home.workspaceCard')}</span>
              <span className="mt-1 block text-sm text-slate-600">{tt('home.workspaceCardDescription')}</span>
            </Link>
          </li>
        )}
        {me.role === 'admin' && (
          <li>
            <Link to={ROUTES.approvals} className={CARD_CLASS}>
              <span className="block font-medium">{tt('home.approvalsCard')}</span>
              <span className="mt-1 block text-sm text-slate-600">{tt('home.approvalsCardDescription')}</span>
            </Link>
          </li>
        )}
        <li>
          <Link to={ROUTES.profile} className={CARD_CLASS}>
            <span className="block font-medium">{t('home.profileCard')}</span>
            <span className="mt-1 block text-sm text-slate-600">{t('home.profileCardDescription')}</span>
          </Link>
        </li>
        <li>
          <Link to={ROUTES.myAssets} className={CARD_CLASS}>
            <span className="block font-medium">{ta('home.mineCard')}</span>
            <span className="mt-1 block text-sm text-slate-600">{ta('home.mineCardDescription')}</span>
          </Link>
        </li>
        {isItStaffOrAdmin(me.role) && (
          <li>
            <Link to={ROUTES.assets} className={CARD_CLASS}>
              <span className="block font-medium">{ta('home.inventoryCard')}</span>
              <span className="mt-1 block text-sm text-slate-600">{ta('home.inventoryCardDescription')}</span>
            </Link>
          </li>
        )}
        {me.role === 'admin' && (
          <li>
            <Link to={ROUTES.admin} className={CARD_CLASS}>
              <span className="block font-medium">{t('home.adminCard')}</span>
              <span className="mt-1 block text-sm text-slate-600">{t('home.adminCardDescription')}</span>
            </Link>
          </li>
        )}
      </ul>
    </section>
  );
}
