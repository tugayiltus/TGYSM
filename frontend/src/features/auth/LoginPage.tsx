/**
 * @input Nothing; reads where the visitor was heading from the router state.
 * @output The sign-in form, or a redirect once a session exists.
 * @position `/login`, outside the app shell.
 */
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation } from 'react-router';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormTextInput } from '../../shared/form/formFields';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

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
  const { control, handleSubmit } = useForm<LoginValues>({ resolver: zodResolver(loginSchema), defaultValues: EMPTY_LOGIN });

  // Single redirect point: after login the refreshed session lands here and goes where the user was heading.
  if (me.data) {
    return <Navigate to={redirectTarget(location.state)} replace />;
  }

  const onSubmit = handleSubmit((values) => login.mutate(values));

  return (
    <AuthCard>
      <VStack gap={1}>
        <Heading level={1}>{t('login.title')}</Heading>
        <Text type="supporting">{t('login.subtitle')}</Text>
      </VStack>
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <VStack gap={4}>
          <FormAlert message={login.error ? describeError(login.error) : null} />
          <FormTextInput control={control} name="email" label={t('login.email')} type="email" startIcon={Icons.envelopeOpen} autoComplete="username" />
          <FormTextInput
            control={control}
            name="password"
            label={t('login.password')}
            type="password"
            startIcon={Icons.lock}
            autoComplete="current-password"
            description={t('login.lockoutHint')}
          />
          <Button type="submit" label={t('login.submit')} variant="primary" width="100%" isLoading={login.isPending} />
          <HStack hAlign="center">
            <Link href={ROUTES.forgotPassword}>{t('login.forgotLink')}</Link>
          </HStack>
        </VStack>
      </form>
    </AuthCard>
  );
}
