/**
 * @input Nothing; reads where the visitor was heading from the router state.
 * @output The sign-in form of the active login method (email, Windows user name or Microsoft), the local admin
 *   form behind a link under a directory method, or a redirect once a session exists.
 * @position `/login`, outside the app shell.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation, useSearchParams } from 'react-router';
import { z } from 'zod';

import { describeError } from '../../shared/api/errorMessage';
import { ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormTextInput } from '../../shared/form/formFields';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

import { useLdapLogin, useLogin, useLoginMethod, useMe } from './api';

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

/** Microsoft sign-in leaves the SPA: the API redirects to Microsoft, so this is a full page load, not a router link. */
const ENTRA_START_PATH = '/api/v1/auth/oidc/start';
const ERROR_PARAM = 'error';
const SSO_ERROR = 'sso';

function PasswordForm({ mode }: { mode: 'local' | 'ldap' }) {
  const { t } = useTranslation('auth');
  const login = useLogin();
  const ldapLogin = useLdapLogin();
  const active = mode === 'ldap' ? ldapLogin : login;
  const { control, handleSubmit } = useForm<LoginValues>({ resolver: zodResolver(loginSchema), defaultValues: EMPTY_LOGIN });
  const onSubmit = handleSubmit((values) =>
    mode === 'ldap' ? ldapLogin.mutate({ identifier: values.email, password: values.password }) : login.mutate(values),
  );
  return (
    <form noValidate onSubmit={(event) => void onSubmit(event)}>
      <VStack gap={4}>
        <FormAlert message={active.error ? describeError(active.error) : null} />
        {mode === 'ldap' ? (
          <FormTextInput control={control} name="email" label={t('login.identifier')} startIcon={Icons.user} autoComplete="username" description={t('login.identifierHint')} />
        ) : (
          <FormTextInput control={control} name="email" label={t('login.email')} type="email" startIcon={Icons.envelopeOpen} autoComplete="username" />
        )}
        <FormTextInput
          control={control}
          name="password"
          label={t('login.password')}
          type="password"
          startIcon={Icons.lock}
          autoComplete="current-password"
          description={t('login.lockoutHint')}
        />
        <Button type="submit" label={t('login.submit')} variant="primary" width="100%" isLoading={active.isPending} />
        {mode === 'local' && (
          <HStack hAlign="center">
            <Link href={ROUTES.forgotPassword}>{t('login.forgotLink')}</Link>
          </HStack>
        )}
      </VStack>
    </form>
  );
}

export function LoginPage() {
  const { t } = useTranslation('auth');
  const me = useMe();
  const { method } = useLoginMethod();
  const location = useLocation();
  const [params] = useSearchParams();
  const [showLocalAdmin, setShowLocalAdmin] = useState(false);

  // Single redirect point: after login the refreshed session lands here and goes where the user was heading.
  if (me.data) {
    return <Navigate to={redirectTarget(location.state)} replace />;
  }

  // Under a directory method, local passwords are for admins only (break-glass, ADR-0030).
  const breakGlass = method !== 'local' && (
    <HStack hAlign="center">
      <Button label={t('login.localAdmin')} variant="ghost" size="sm" onClick={() => setShowLocalAdmin((open) => !open)} />
    </HStack>
  );

  return (
    <AuthCard>
      <VStack gap={1}>
        <Heading level={1}>{t('login.title')}</Heading>
        <Text type="supporting">{method === 'entra' ? t('login.microsoftHint') : t('login.subtitle')}</Text>
      </VStack>
      {params.get(ERROR_PARAM) === SSO_ERROR && <Banner status="error" title={t('login.ssoError')} />}
      {method === 'entra' && (
        <Button label={t('login.microsoft')} variant="primary" width="100%" onClick={() => window.location.assign(ENTRA_START_PATH)} />
      )}
      {method === 'ldap' && !showLocalAdmin && <PasswordForm mode="ldap" />}
      {(method === 'local' || showLocalAdmin) && <PasswordForm mode="local" />}
      {breakGlass}
    </AuthCard>
  );
}
