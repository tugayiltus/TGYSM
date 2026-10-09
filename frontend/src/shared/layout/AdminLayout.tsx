import CIcon from '@coreui/icons-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { ROUTES } from '../routes';
import { cilHome } from '../ui/icons';

import { ShellFrame } from './ShellFrame';

/** `/admin`: its own grouped menu and a way back to the application (spec §4.2). */
export function AdminLayout() {
  const { t } = useTranslation('admin');
  const backToApp = (
    <Link to={ROUTES.home} aria-label={t('nav.backToApp')} className="btn btn-ghost-secondary tgysm-touch d-inline-flex align-items-center gap-2">
      <CIcon icon={cilHome} aria-hidden="true" />
      <span className="d-none d-sm-inline">{t('nav.backToApp')}</span>
    </Link>
  );
  return <ShellFrame area="admin" title={t('title')} navLabel={t('nav.label')} areaSwitch={backToApp} />;
}
