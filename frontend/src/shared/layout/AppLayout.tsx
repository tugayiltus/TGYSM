import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { useTranslation } from 'react-i18next';
import { Outlet } from 'react-router';

import { useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';
import { Icons } from '../ui/icons';

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
    me.role === 'admin' ? <Button label={t('routes.admin')} variant="ghost" href={ROUTES.admin} icon={<Icon icon={Icons.settings} />} /> : undefined;
  return <ShellFrame area="app" title={t('appName')} navLabel={ta('shell.mainNav')} areaSwitch={areaSwitch} />;
}
