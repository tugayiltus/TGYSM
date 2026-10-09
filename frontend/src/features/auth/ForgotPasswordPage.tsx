import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { z } from 'zod';

import { describeError, fieldError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Button } from '../../shared/ui/Button';
import { FormAlert } from '../../shared/ui/FormAlert';
import { cilEnvelopeOpen } from '../../shared/ui/icons';
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
    <AuthCard>
        <h1 className="h4 mb-0">{t('forgotPassword.title')}</h1>
        {forgot.isSuccess ? (
          <p role="status" className="alert alert-info mb-0">
            {t('forgotPassword.confirmation')}
          </p>
        ) : (
          <>
            <p className="text-body-secondary mb-0">{t('forgotPassword.description')}</p>
            <form className="d-flex flex-column gap-3" noValidate onSubmit={(event) => void onSubmit(event)}>
              <FormAlert message={forgot.error ? describeError(forgot.error) : null} />
              <TextField label={t('forgotPassword.email')} type="email" startIcon={cilEnvelopeOpen} autoComplete="username" error={fieldError(errors.email)} {...register('email')} />
              <Button type="submit" isLoading={forgot.isPending}>
                {t('forgotPassword.submit')}
              </Button>
            </form>
          </>
        )}
        <Link to={ROUTES.login} className="text-center">
          {t('forgotPassword.backToLogin')}
        </Link>
    </AuthCard>
  );
}
