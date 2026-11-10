/**
 * @input Nothing; reads the session.
 * @output The password change form: inside the app, or alone in a card when a change is required.
 * @position `/change-password`.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Heading } from '@astryxdesign/core/Heading';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { applyServerErrors, describeError, isHandledByFields } from '../../shared/api/errorMessage';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormTextInput } from '../../shared/form/formFields';
import { AuthCard } from '../../shared/layout/AuthCard';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { useChangePassword, useLogout } from './api';
import { changePasswordSchema, type ChangePasswordValues } from './passwordSchema';
import { useSession } from './session';

const EMPTY_VALUES: ChangePasswordValues = { currentPassword: '', newPassword: '', confirmPassword: '' };
const SERVER_FIELDS = ['currentPassword', 'newPassword'] as const;
/** Readable width for a short form inside the app. */
const FORM_MAX_WIDTH = 560;

export function ChangePasswordPage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const changePassword = useChangePassword();
  const logout = useLogout();
  const navigate = useNavigate();
  const { control, handleSubmit, setError } = useForm<ChangePasswordValues>({ resolver: zodResolver(changePasswordSchema), defaultValues: EMPTY_VALUES });

  const onSubmit = handleSubmit(({ currentPassword, newPassword }) =>
    changePassword.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => void navigate(ROUTES.home, { replace: true }),
        onError: (error) => applyServerErrors(error, setError, SERVER_FIELDS),
      },
    ),
  );

  const alert = changePassword.error && !isHandledByFields(changePassword.error, SERVER_FIELDS) ? describeError(changePassword.error) : null;

  const form = (
    <form noValidate onSubmit={(event) => void onSubmit(event)}>
      <VStack gap={4}>
        <FormAlert message={alert} />
        <FormTextInput control={control} name="currentPassword" label={t('changePassword.current')} type="password" startIcon={Icons.lock} autoComplete="current-password" />
        <FormTextInput control={control} name="newPassword" label={t('changePassword.new')} type="password" startIcon={Icons.lock} autoComplete="new-password" description={t('changePassword.hint')} />
        <FormTextInput control={control} name="confirmPassword" label={t('changePassword.confirm')} type="password" startIcon={Icons.lock} autoComplete="new-password" />
        <Button type="submit" label={t('changePassword.submit')} variant="primary" isLoading={changePassword.isPending} />
      </VStack>
    </form>
  );

  // Forced change: outside the app (no menu or breadcrumb to escape through); logout is the only way out.
  if (me.mustChangePassword) {
    return (
      <AuthCard>
        <Heading level={1}>{t('changePassword.title')}</Heading>
        <Banner status="info" title={t('changePassword.requiredNotice')} />
        {form}
        <Button
          label={tc('actions.logout')}
          variant="ghost"
          isLoading={logout.isPending}
          onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}
        />
      </AuthCard>
    );
  }

  return (
    <VStack gap={4}>
      <PageHeader icon="lock" />
      <Card padding={6} maxWidth={FORM_MAX_WIDTH}>
        {form}
      </Card>
    </VStack>
  );
}
