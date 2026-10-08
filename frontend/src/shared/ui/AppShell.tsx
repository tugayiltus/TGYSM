import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, NavLink, Outlet, useNavigate } from 'react-router';

import { useLogout } from '../../features/auth/api';
import { isItStaffOrAdmin, useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';

import { Button } from './Button';
import { Sheet } from './Sheet';

const NAV_LINK_CLASS = 'block rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 aria-[current=page]:bg-brand-50 aria-[current=page]:text-brand-900';

function MainLinks({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation('auth');
  const { t: ta } = useTranslation('assets');
  const { t: tt } = useTranslation('tickets');
  const { t: tk } = useTranslation('kb');
  const { t: tr } = useTranslation('reports');
  const me = useSession();
  const links = [
    { to: ROUTES.tickets, label: tt('nav.tickets'), visible: true },
    { to: ROUTES.workspace, label: tt('nav.workspace'), visible: isItStaffOrAdmin(me.role) },
    { to: ROUTES.approvals, label: tt('nav.approvals'), visible: me.role === 'admin' },
    { to: ROUTES.myAssets, label: ta('nav.mine'), visible: true },
    { to: ROUTES.assets, label: ta('nav.inventory'), visible: isItStaffOrAdmin(me.role) },
    { to: ROUTES.kb, label: tk('nav.kb'), visible: true },
    { to: ROUTES.reports, label: tr('nav.reports'), visible: isItStaffOrAdmin(me.role) },
    { to: ROUTES.profile, label: t('shell.profile'), visible: true },
    { to: ROUTES.admin, label: t('shell.admin'), visible: me.role === 'admin' },
  ];
  return links
    .filter((link) => link.visible)
    .map((link) => (
      <li key={link.to}>
        {/* "end" keeps "Envanter" from staying current on /assets/mine (and "Kayıtlarım" on its sub-pages). */}
        <NavLink to={link.to} end={link.to === ROUTES.assets || link.to === ROUTES.tickets} className={NAV_LINK_CLASS} onClick={onNavigate}>
          {link.label}
        </NavLink>
      </li>
    ));
}

/** Inline links from 1024 px, a drawer below (mobile-first, ADR-0003). */
export function AppShell() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const logout = useLogout();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-2 px-4">
          <Button variant="ghost" className="lg:hidden" aria-label={tc('actions.openMenu')} onClick={() => setMenuOpen(true)}>
            <span aria-hidden="true">☰</span>
          </Button>
          <Link to={ROUTES.home} className="mr-auto text-base font-semibold text-brand-900">
            {tc('appName')}
          </Link>
          <nav aria-label={t('shell.mainNav')} className="hidden lg:block">
            <ul className="flex items-center gap-1">
              <MainLinks />
            </ul>
          </nav>
          <Button variant="ghost" isLoading={logout.isPending} onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}>
            {tc('actions.logout')}
          </Button>
        </div>
      </header>
      <Sheet open={menuOpen} onOpenChange={setMenuOpen} title={t('shell.mainNav')}>
        <nav aria-label={t('shell.mainNav')}>
          <ul className="flex flex-col gap-1">
            <MainLinks onNavigate={() => setMenuOpen(false)} />
          </ul>
        </nav>
      </Sheet>
      <main className="mx-auto w-full max-w-6xl p-4 sm:p-6">
        <Outlet />
      </main>
    </div>
  );
}
