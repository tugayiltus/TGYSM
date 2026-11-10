import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { useTranslation } from 'react-i18next';

import { ROUTES } from '../routes';
import { Icons } from '../ui/icons';

import { ShellFrame } from './ShellFrame';

/** `/admin`: its own grouped menu and a way back to the application (spec §4.2). */
export function AdminLayout() {
  const { t } = useTranslation('admin');
  const backToApp = <Button label={t('nav.backToApp')} variant="ghost" href={ROUTES.home} icon={<Icon icon={Icons.home} />} />;
  return <ShellFrame area="admin" title={t('title')} navLabel={t('nav.label')} areaSwitch={backToApp} />;
}
