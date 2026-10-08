import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router';

import { applyServerErrors, describeError, fieldError, isHandledByFields, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../shared/api/errors';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useSetPassword } from './api';
import { setPasswordSchema, type SetPasswordValues } from './passwordSchema';

/** Query parameter of the emailed link; mirrors backend `AccountLinkUrl.SetPasswordPathFormat`. */
const TOKEN_PARAM = 'token';
const EMPTY_VALUES: SetPasswordValues = { newPassword: '', confirmPassword: '' };
const SERVER_FIELDS = ['newPassword'] as const;

/** Activation and reset links land here (anonymous); the token is kept in memory and removed from the address bar. */
export function SetPasswordPage() {
  const { t } = useTranslation('auth');
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const [token] = useState(() => searchParams.get(TOKEN_PARAM));
  const [done, setDone] = useState(false);
  const setPassword = useSetPassword();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<SetPasswordValues>({ resolver: zodResolver(setPasswordSchema), defaultValues: EMPTY_VALUES });

  // A token left in the URL would end up in history, referrers and shared screenshots.
  useEffect(() => {
    if (searchParams.has(TOKEN_PARAM)) {
      setSearchParams({}, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  const onSubmit = handleSubmit(({ newPassword }) => {
    if (token === null || token === '') {
      return;
    }
    setPassword.mutate(
      { token, newPassword },
      {
        onSuccess: () => setDone(true),
        onError: (error) => applyServerErrors(error, setError, SERVER_FIELDS),
      },
    );
  });

  const tokenMissing = token === null || token === '';
  const tokenRejected = setPassword.error instanceof ApiError && setPassword.error.code === ERROR_CODES.tokenInvalid;
  const alert = tokenMissing
    ? translateErrorCode(ERROR_CODES.tokenInvalid)
    : setPassword.error && !isHandledByFields(setPassword.error, SERVER_FIELDS)
      ? describeError(setPassword.error)
      : null;

  return (
    <AuthCard>
        <h1 className="h4 mb-0">{t('setPassword.title')}</h1>
        {done ? (
          <div className="d-flex flex-column gap-3">
            <p role="status" className="alert alert-success mb-0">
              {t('setPassword.success')}
            </p>
            <Button onClick={() => void navigate(ROUTES.login, { replace: true })}>{t('setPassword.toLogin')}</Button>
          </div>
        ) : (
          <>
            <p className="text-body-secondary mb-0">{t('setPassword.description')}</p>
            <form className="d-flex flex-column gap-3" noValidate onSubmit={(event) => void onSubmit(event)}>
              <FormAlert message={alert} />
              <TextField
                label={t('setPassword.new')}
                type="password"
                autoComplete="new-password"
                hint={t('setPassword.hint')}
                error={fieldError(errors.newPassword)}
                {...register('newPassword')}
              />
              <TextField label={t('setPassword.confirm')} type="password" autoComplete="new-password" error={fieldError(errors.confirmPassword)} {...register('confirmPassword')} />
              <Button type="submit" isLoading={setPassword.isPending} disabled={tokenMissing}>
                {t('setPassword.submit')}
              </Button>
              {(tokenMissing || tokenRejected) && (
                <Link to={ROUTES.forgotPassword} className="text-center">
                  {t('setPassword.requestNew')}
                </Link>
              )}
            </form>
          </>
        )}
        <Link to={ROUTES.login} className="text-center">
          {t('forgotPassword.backToLogin')}
        </Link>
    </AuthCard>
  );
}
