/**
 * @input Nothing; reads the signed-in user.
 * @output An avatar button whose menu holds profile, password change and logout.
 * @position Top bar of the app shell, at the end.
 */
import { Avatar } from '@astryxdesign/core/Avatar';
import { DropdownMenu, DropdownMenuDivider, DropdownMenuGroup, DropdownMenuItem } from '@astryxdesign/core/DropdownMenu';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { useLogout } from '../../features/auth/api';
import { useSession } from '../../features/auth/session';
import { ROUTES } from '../routes';

/** Only the avatar shows in the bar. The full name is the group title in the menu, so a long name cannot widen the bar. */
export function AccountMenu() {
  const { t } = useTranslation();
  const { t: ta } = useTranslation('auth');
  const me = useSession();
  const logout = useLogout();
  const navigate = useNavigate();
  const fullName = `${me.firstName} ${me.lastName}`;
  return (
    <DropdownMenu
      hasChevron={false}
      alignment="end"
      button={{ label: `${t('layout.accountMenu')}: ${fullName}`, variant: 'ghost', isIconOnly: true, icon: <Avatar name={fullName} size="sm" tooltip={false} /> }}
    >
      <DropdownMenuGroup title={fullName}>
        <DropdownMenuItem label={ta('shell.profile')} href={ROUTES.profile} />
        <DropdownMenuItem label={t('routes.changePassword')} href={ROUTES.changePassword} />
      </DropdownMenuGroup>
      <DropdownMenuDivider />
      <DropdownMenuItem
        label={t('actions.logout')}
        isDisabled={logout.isPending}
        onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}
      />
    </DropdownMenu>
  );
}
