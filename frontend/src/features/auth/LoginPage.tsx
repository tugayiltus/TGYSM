import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation } from 'react-router';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useLogin, useMe } from './api';

const loginSchema = z.object({
  email: z.string().trim().min(1, ERROR_CODES.required),
  password: z.string().min(1, ERROR_CODES.required),
});

type LoginValues = z.infer<typeof loginSchema>;

const EMPTY_LOGIN: LoginValues = { email: '', password: '' };

function redirectTarget(state: unknown): string {
  if (typeof state === 'object' && state !== null && 'from' in state) {
    const from = (state as { from: unknown }).from;
    if (typeof from === 'string' && from.startsWith('/') && from !== ROUTES.login) {
      return from;
    }
  }
  return ROUTES.home;
}

export function LoginPage() {
  const { t } = useTranslation('auth');
  const me = useMe();
  const login = useLogin();
  const location = useLocation();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema), defaultValues: EMPTY_LOGIN });

  // Single redirect point: after login the refreshed session lands here and goes where the user was heading.
  if (me.data) {
    return <Navigate to={redirectTarget(location.state)} replace />;
  }

  const onSubmit = handleSubmit((values) => login.mutate(values));

  return (
    <main className="flex min-h-dvh items-center justify-center p-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold">{t('login.title')}</h1>
        <p className="mt-1 text-sm text-slate-600">{t('login.subtitle')}</p>
        <form className="mt-6 flex flex-col gap-4" noValidate onSubmit={(event) => void onSubmit(event)}>
          <FormAlert message={login.error ? describeError(login.error) : null} />
          <TextField label={t('login.email')} type="email" autoComplete="username" error={fieldError(errors.email)} {...register('email')} />
          <TextField
            label={t('login.password')}
            type="password"
            autoComplete="current-password"
            hint={t('login.lockoutHint')}
            error={fieldError(errors.password)}
            {...register('password')}
          />
          <Button type="submit" isLoading={login.isPending}>
            {t('login.submit')}
          </Button>
        </form>
      </div>
    </main>
  );
}
