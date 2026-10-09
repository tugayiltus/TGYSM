import { useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';
import { Button } from '../ui/Button';

import { AccountMenu } from './AccountMenu';

export interface AppHeaderProps {
  title: string;
  onOpenMenu: (trigger: HTMLButtonElement | null) => void;
  /** Link to the other area: "Yönetim" for admins in the app, "Uygulamaya dön" in /admin. */
  areaSwitch?: ReactNode;
}

export function AppHeader({ title, onOpenMenu, areaSwitch }: AppHeaderProps) {
  const { t } = useTranslation();
  const menuButton = useRef<HTMLButtonElement>(null);
  return (
    <header className="header sticky-top bg-body border-bottom px-2 px-sm-3">
      <div className="d-flex align-items-center gap-1 gap-sm-2 w-100 min-w-0">
        <Button ref={menuButton} variant="ghost" className="d-lg-none" aria-label={t('actions.openMenu')} onClick={() => onOpenMenu(menuButton.current)}>
          <span aria-hidden="true">☰</span>
        </Button>
        <Link to={ROUTES.home} className="fw-semibold text-decoration-none me-auto d-lg-none">
          {title}
        </Link>
        <span className="me-auto d-none d-lg-block" />
        {areaSwitch}
        <AccountMenu />
      </div>
    </header>
  );
}
