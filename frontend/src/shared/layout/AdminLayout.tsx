import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';

import { ShellFrame } from './ShellFrame';

/** `/admin`: its own grouped menu and a way back to the application (spec §4.2). */
export function AdminLayout() {
  const { t } = useTranslation('admin');
  const backToApp = (
    <Link to={ROUTES.home} className="btn btn-ghost-secondary tgysm-touch d-inline-flex align-items-center">
      {t('nav.backToApp')}
    </Link>
  );
  return <ShellFrame area="admin" title={t('title')} navLabel={t('nav.label')} areaSwitch={backToApp} />;
}
