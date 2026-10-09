import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, Navigate, useLocation } from 'react-router';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { cilEnvelopeOpen, cilLockLocked } from '../../shared/ui/icons';
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
    // "//host" and "/\\host" are protocol-relative: never follow them.
    const isLocalPath = typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') && !from.startsWith('/\\');
    if (isLocalPath && from !== ROUTES.login) {
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
    <AuthCard>
      <div>
        <h1 className="h4 mb-1">{t('login.title')}</h1>
        <p className="text-body-secondary mb-0">{t('login.subtitle')}</p>
      </div>
      <form className="d-flex flex-column gap-3" noValidate onSubmit={(event) => void onSubmit(event)}>
          <FormAlert message={login.error ? describeError(login.error) : null} />
          <TextField label={t('login.email')} type="email" startIcon={cilEnvelopeOpen} autoComplete="username" error={fieldError(errors.email)} {...register('email')} />
          <TextField
            label={t('login.password')}
            type="password"
            startIcon={cilLockLocked}
            autoComplete="current-password"
            hint={t('login.lockoutHint')}
            error={fieldError(errors.password)}
            {...register('password')}
          />
          <Button type="submit" className="w-100" isLoading={login.isPending}>
            {t('login.submit')}
          </Button>
          <Link to={ROUTES.forgotPassword} className="text-center">
            {t('login.forgotLink')}
          </Link>
        </form>
    </AuthCard>
  );
}
