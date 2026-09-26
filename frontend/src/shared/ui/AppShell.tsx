import { useTranslation } from 'react-i18next';
import { Link, NavLink, Outlet, useNavigate } from 'react-router';

import { useLogout } from '../../features/auth/api';
import { useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';

import { Button } from './Button';

const NAV_LINK_CLASS = 'rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 aria-[current=page]:bg-brand-50 aria-[current=page]:text-brand-900';

export function AppShell() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const logout = useLogout();
  const navigate = useNavigate();

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white">
        <div className="mx-auto flex h-14 max-w-5xl items-center gap-2 px-4">
          <Link to={ROUTES.home} className="mr-auto text-base font-semibold text-brand-900">
            {tc('appName')}
          </Link>
          <nav aria-label={t('shell.mainNav')} className="flex items-center gap-1">
            <NavLink to={ROUTES.profile} className={NAV_LINK_CLASS}>
              {t('shell.profile')}
            </NavLink>
            {me.role === 'admin' && (
              <NavLink to={ROUTES.admin} className={NAV_LINK_CLASS}>
                {t('shell.admin')}
              </NavLink>
            )}
          </nav>
          <Button variant="ghost" isLoading={logout.isPending} onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}>
            {tc('actions.logout')}
          </Button>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl p-4 sm:p-6">
        <Outlet />
      </main>
    </div>
  );
}
