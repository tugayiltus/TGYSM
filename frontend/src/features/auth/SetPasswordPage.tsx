import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router';

import { applyServerErrors, describeError, fieldError, isHandledByFields, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../shared/api/errors';
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
    <main className="flex min-h-dvh items-center justify-center p-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold">{t('setPassword.title')}</h1>
        {done ? (
          <div className="mt-6 flex flex-col gap-4">
            <p role="status" className="rounded-lg bg-brand-50 p-3 text-sm text-brand-900">
              {t('setPassword.success')}
            </p>
            <Button onClick={() => void navigate(ROUTES.login, { replace: true })}>{t('setPassword.toLogin')}</Button>
          </div>
        ) : (
          <>
            <p className="mt-1 text-sm text-slate-600">{t('setPassword.description')}</p>
            <form className="mt-6 flex flex-col gap-4" noValidate onSubmit={(event) => void onSubmit(event)}>
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
                <Link to={ROUTES.forgotPassword} className="text-center text-sm font-medium text-brand-900 underline">
                  {t('setPassword.requestNew')}
                </Link>
              )}
            </form>
          </>
        )}
      </div>
    </main>
  );
}
