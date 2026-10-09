import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';
import { Link, Outlet } from 'react-router';

import { useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';
import { cilSettings } from '../ui/icons';

import { ShellFrame } from './ShellFrame';

/** Employee and IT pages: grouped left menu by role; admins get a "Yönetim" link. */
export function AppLayout() {
  const { t } = useTranslation();
  const { t: ta } = useTranslation('auth');
  const me = useSession();
  // A forced password change shows only its own card: the menu would offer a way around it.
  if (me.mustChangePassword) {
    return <Outlet />;
  }
  const areaSwitch =
    me.role === 'admin' ? (
      <Link to={ROUTES.admin} aria-label={t('routes.admin')} className="btn btn-ghost-secondary tgysm-touch d-inline-flex align-items-center gap-2">
        <CIcon icon={cilSettings} aria-hidden="true" />
        <span className="d-none d-sm-inline">{t('routes.admin')}</span>
      </Link>
    ) : undefined;
  return <ShellFrame area="app" title={t('appName')} navLabel={ta('shell.mainNav')} areaSwitch={areaSwitch} />;
}
