import CIcon from '@coreui/icons-react';
import { useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { Breadcrumbs } from '../navigation/Breadcrumbs';
import { ROUTES } from '../routes';
import { cilMenu } from '../ui/icons';

import { AccountMenu } from './AccountMenu';
import { ThemeMenu } from './ThemeMenu';

export interface AppHeaderProps {
  title: string;
  onOpenMenu: (trigger: HTMLButtonElement | null) => void;
  /** Link to the other area: "Yönetim" for admins in the app, "Uygulamaya dön" in /admin. */
  areaSwitch?: ReactNode;
}

/** Sticky template header (spec §2.4): menu button, area switch, theme and account menus; breadcrumb strip below. */
export function AppHeader({ title, onOpenMenu, areaSwitch }: AppHeaderProps) {
  const { t } = useTranslation();
  const menuButton = useRef<HTMLButtonElement>(null);
  return (
    <header className="header header-sticky p-0 mb-0 border-bottom">
      <div className="container-fluid d-flex align-items-center gap-1 gap-sm-2 px-2 px-sm-3 tgysm-header-row min-w-0">
        <button
          ref={menuButton}
          type="button"
          className="header-toggler d-lg-none tgysm-touch"
          aria-label={t('actions.openMenu')}
          onClick={() => onOpenMenu(menuButton.current)}
        >
          <CIcon icon={cilMenu} size="lg" aria-hidden="true" />
        </button>
        <Link to={ROUTES.home} className="fw-semibold text-decoration-none text-body me-auto d-lg-none text-truncate">
          {title}
        </Link>
        <span className="me-auto d-none d-lg-block" />
        {areaSwitch}
        <ThemeMenu />
        <AccountMenu />
      </div>
      <div className="container-fluid border-top px-2 px-sm-3 tgysm-header-crumbs min-w-0">
        <Breadcrumbs />
      </div>
    </header>
  );
}
