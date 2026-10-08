import { CCard, CCardBody } from '@coreui/react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';

import { applyServerErrors, describeError, fieldError, isHandledByFields } from '../../shared/api/errorMessage';
import { AuthCard } from '../../shared/layout/AuthCard';
import { PageHeader } from '../../shared/navigation/PageHeader';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useChangePassword, useLogout } from './api';
import { changePasswordSchema, type ChangePasswordValues } from './passwordSchema';
import { useSession } from './session';

const EMPTY_VALUES: ChangePasswordValues = { currentPassword: '', newPassword: '', confirmPassword: '' };
const SERVER_FIELDS = ['currentPassword', 'newPassword'] as const;

export function ChangePasswordPage() {
  const { t } = useTranslation('auth');
  const { t: tc } = useTranslation();
  const me = useSession();
  const changePassword = useChangePassword();
  const logout = useLogout();
  const navigate = useNavigate();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ChangePasswordValues>({ resolver: zodResolver(changePasswordSchema), defaultValues: EMPTY_VALUES });

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
    <form className="d-flex flex-column gap-3" noValidate onSubmit={(event) => void onSubmit(event)}>
      <FormAlert message={alert} />
      <TextField label={t('changePassword.current')} type="password" autoComplete="current-password" error={fieldError(errors.currentPassword)} {...register('currentPassword')} />
      <TextField
        label={t('changePassword.new')}
        type="password"
        autoComplete="new-password"
        hint={t('changePassword.hint')}
        error={fieldError(errors.newPassword)}
        {...register('newPassword')}
      />
      <TextField label={t('changePassword.confirm')} type="password" autoComplete="new-password" error={fieldError(errors.confirmPassword)} {...register('confirmPassword')} />
      <Button type="submit" isLoading={changePassword.isPending}>
        {t('changePassword.submit')}
      </Button>
    </form>
  );

  // Forced change: outside the app (no menu or breadcrumb to escape through); logout is the only way out.
  if (me.mustChangePassword) {
    return (
      <AuthCard>
        <h1 className="h4 mb-0">{t('changePassword.title')}</h1>
        <p className="alert alert-info mb-0">{t('changePassword.requiredNotice')}</p>
        {form}
        <Button variant="ghost" isLoading={logout.isPending} onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(ROUTES.login, { replace: true }) })}>
          {tc('actions.logout')}
        </Button>
      </AuthCard>
    );
  }

  return (
    <>
      <PageHeader />
      <CCard className="tgysm-form-card">
        <CCardBody>{form}</CCardBody>
      </CCard>
    </>
  );
}
