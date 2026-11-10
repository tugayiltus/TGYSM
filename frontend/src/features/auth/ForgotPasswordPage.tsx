/**
 * @input Nothing.
 * @output The reset-link request form, then a neutral confirmation.
 * @position `/forgot-password`, outside the app shell.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormTextInput } from '../../shared/form/formFields';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { useForgotPassword } from './api';

const forgotSchema = z.object({ email: z.string().trim().min(1, ERROR_CODES.required) });

type ForgotValues = z.infer<typeof forgotSchema>;

const EMPTY_VALUES: ForgotValues = { email: '' };

/** Anonymous. Every accepted request shows the same text, so the page never reveals whether the account exists. */
export function ForgotPasswordPage() {
  const { t } = useTranslation('auth');
  const forgot = useForgotPassword();
  const { control, handleSubmit } = useForm<ForgotValues>({ resolver: zodResolver(forgotSchema), defaultValues: EMPTY_VALUES });

  const onSubmit = handleSubmit(({ email }) => forgot.mutate(email));

  return (
    <AuthCard>
      <Heading level={1}>{t('forgotPassword.title')}</Heading>
      {forgot.isSuccess ? (
        <VStack role="status">
          <Banner status="info" title={t('forgotPassword.confirmation')} />
        </VStack>
      ) : (
        <>
          <Text type="supporting">{t('forgotPassword.description')}</Text>
          <form noValidate onSubmit={(event) => void onSubmit(event)}>
            <VStack gap={4}>
              <FormAlert message={forgot.error ? describeError(forgot.error) : null} />
              <FormTextInput control={control} name="email" label={t('forgotPassword.email')} type="email" startIcon={Icons.envelopeOpen} autoComplete="username" />
              <Button type="submit" label={t('forgotPassword.submit')} variant="primary" isLoading={forgot.isPending} />
            </VStack>
          </form>
        </>
      )}
      <HStack hAlign="center">
        <Link href={ROUTES.login}>{t('forgotPassword.backToLogin')}</Link>
      </HStack>
    </AuthCard>
  );
}
