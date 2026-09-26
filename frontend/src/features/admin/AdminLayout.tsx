import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, Outlet } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { Sheet } from '../../shared/ui/Sheet';

const NAV_ITEMS = [
  { to: ROUTES.adminUsers, labelKey: 'nav.users' },
  { to: ROUTES.adminDepartments, labelKey: 'nav.departments' },
  { to: ROUTES.adminLocations, labelKey: 'nav.locations' },
  { to: ROUTES.adminTeams, labelKey: 'nav.teams' },
  { to: ROUTES.adminCategories, labelKey: 'nav.categories' },
  { to: ROUTES.adminRequestTypes, labelKey: 'nav.requestTypes' },
  { to: ROUTES.adminAudit, labelKey: 'nav.audit' },
] as const;

const LINK_CLASS = 'block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-100 aria-[current=page]:bg-brand-50 aria-[current=page]:text-brand-900';

function AdminNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation('admin');
  return (
    <nav aria-label={t('nav.label')}>
      <ul className="flex flex-col gap-1">
        {NAV_ITEMS.map((item) => (
          <li key={item.to}>
            <NavLink to={item.to} className={LINK_CLASS} onClick={onNavigate}>
              {t(item.labelKey)}
            </NavLink>
          </li>
        ))}
        <li className="mt-4 border-t border-slate-100 pt-4">
          <NavLink to={ROUTES.home} end className={LINK_CLASS} onClick={onNavigate}>
            {t('nav.backToApp')}
          </NavLink>
        </li>
      </ul>
    </nav>
  );
}

/** Drawer menu under 1024 px, fixed sidebar above (ADR-0003). */
export function AdminLayout() {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[16rem_1fr]">
      <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-slate-200 bg-white px-2 lg:hidden">
        <Button variant="ghost" aria-label={tc('actions.openMenu')} onClick={() => setMenuOpen(true)}>
          <span aria-hidden="true">☰</span>
        </Button>
        <span className="font-semibold">{t('title')}</span>
      </header>
      <Sheet open={menuOpen} onOpenChange={setMenuOpen} title={t('nav.label')}>
        <AdminNav onNavigate={() => setMenuOpen(false)} />
      </Sheet>
      <aside className="hidden border-r border-slate-200 bg-white p-4 lg:block">
        <p className="mb-4 px-3 text-base font-semibold">{t('title')}</p>
        <AdminNav />
      </aside>
      <main className="mx-auto w-full max-w-6xl min-w-0 p-4 sm:p-6">
        <Outlet />
      </main>
    </div>
  );
}
