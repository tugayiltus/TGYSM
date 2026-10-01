import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { TextField } from '../../shared/ui/TextField';

import { useForgotPassword } from './api';

const forgotSchema = z.object({ email: z.string().trim().min(1, ERROR_CODES.required) });

type ForgotValues = z.infer<typeof forgotSchema>;

const EMPTY_VALUES: ForgotValues = { email: '' };

/** Anonymous. Every accepted request shows the same text, so the page never reveals whether the account exists. */
export function ForgotPasswordPage() {
  const { t } = useTranslation('auth');
  const forgot = useForgotPassword();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotValues>({ resolver: zodResolver(forgotSchema), defaultValues: EMPTY_VALUES });

  const onSubmit = handleSubmit(({ email }) => forgot.mutate(email));

  return (
    <main className="flex min-h-dvh items-center justify-center p-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold">{t('forgotPassword.title')}</h1>
        {forgot.isSuccess ? (
          <p role="status" className="mt-6 rounded-lg bg-brand-50 p-3 text-sm text-brand-900">
            {t('forgotPassword.confirmation')}
          </p>
        ) : (
          <>
            <p className="mt-1 text-sm text-slate-600">{t('forgotPassword.description')}</p>
            <form className="mt-6 flex flex-col gap-4" noValidate onSubmit={(event) => void onSubmit(event)}>
              <FormAlert message={forgot.error ? describeError(forgot.error) : null} />
              <TextField label={t('forgotPassword.email')} type="email" autoComplete="username" error={fieldError(errors.email)} {...register('email')} />
              <Button type="submit" isLoading={forgot.isPending}>
                {t('forgotPassword.submit')}
              </Button>
            </form>
          </>
        )}
        <Link to={ROUTES.login} className="mt-4 block text-center text-sm font-medium text-brand-900 underline">
          {t('forgotPassword.backToLogin')}
        </Link>
      </div>
    </main>
  );
}
