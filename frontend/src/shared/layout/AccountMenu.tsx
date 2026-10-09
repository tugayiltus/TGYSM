import { useId, useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';

import { useLogout } from '../../features/auth/api';
import { useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';

/** Header account menu: profile, password change and logout (spec §4.1). */
export function AccountMenu() {
  const { t } = useTranslation();
  const { t: ta } = useTranslation('auth');
  const me = useSession();
  const logout = useLogout();
  const navigate = useNavigate();
  const menuId = useId();
  const [open, setOpen] = useState(false);
  const close = () => setOpen(false);
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      close();
    }
  };

  return (
    <div className="position-relative">
      <button type="button" className="btn btn-ghost-secondary d-flex align-items-center gap-2 tgysm-touch" aria-haspopup="menu" aria-expanded={open} aria-controls={menuId} onKeyDown={onKeyDown} onClick={() => setOpen((v) => !v)}>
        {/* Phones show initials only: the full name next to the menu button and area link would not fit 360 px. */}
        <span className="d-sm-none fw-semibold" aria-hidden="true">{`${me.firstName.charAt(0)}${me.lastName.charAt(0)}`}</span>
        <span className="d-none d-sm-inline text-truncate tgysm-account-name">{`${me.firstName} ${me.lastName}`}</span>
        <span className="visually-hidden">{t('layout.accountMenu')}</span>
      </button>
      {open && (
        <ul id={menuId} role="menu" className="dropdown-menu dropdown-menu-end show position-absolute end-0" onKeyDown={onKeyDown}>
          <li role="none">
            <Link role="menuitem" className="dropdown-item" to={ROUTES.profile} onClick={close}>
              {ta('shell.profile')}
            </Link>
          </li>
          <li role="none">
            <Link role="menuitem" className="dropdown-item" to={ROUTES.changePassword} onClick={close}>
              {t('routes.changePassword')}
            </Link>
          </li>
          <li role="none">
            <hr className="dropdown-divider" />
          </li>
          <li role="none">
            <button
              type="button"
              role="menuitem"
              className="dropdown-item"
              disabled={logout.isPending}
              onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}
            >
              {t('actions.logout')}
            </button>
          </li>
        </ul>
      )}
    </div>
  );
}
