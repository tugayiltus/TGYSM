/**
 * @input The `token` query parameter of an emailed link.
 * @output The new-password form, then a success message with a link to sign in.
 * @position `/set-password`, outside the app shell; activation and reset links land here.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { Heading } from '@astryxdesign/core/Heading';
import { HStack } from '@astryxdesign/core/HStack';
import { Link } from '@astryxdesign/core/Link';
import { Text } from '@astryxdesign/core/Text';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';

import { applyServerErrors, describeError, isHandledByFields, translateErrorCode } from '../../shared/api/errorMessage';
import { ApiError, ERROR_CODES } from '../../shared/api/errors';
import { FormAlert } from '../../shared/feedback/FormAlert';
import { FormTextInput } from '../../shared/form/formFields';
import { AuthCard } from '../../shared/layout/AuthCard';
import { ROUTES } from '../../shared/routes';
import { Icons } from '../../shared/ui/icons';

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
  const { control, handleSubmit, setError } = useForm<SetPasswordValues>({ resolver: zodResolver(setPasswordSchema), defaultValues: EMPTY_VALUES });

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
      <Heading level={1}>{t('setPassword.title')}</Heading>
      {done ? (
        <VStack gap={4}>
          <VStack role="status">
            <Banner status="success" title={t('setPassword.success')} />
          </VStack>
          <Button label={t('setPassword.toLogin')} variant="primary" onClick={() => void navigate(ROUTES.login, { replace: true })} />
        </VStack>
      ) : (
        <>
          <Text type="supporting">{t('setPassword.description')}</Text>
          <form noValidate onSubmit={(event) => void onSubmit(event)}>
            <VStack gap={4}>
              <FormAlert message={alert} />
              <FormTextInput control={control} name="newPassword" label={t('setPassword.new')} type="password" startIcon={Icons.lock} autoComplete="new-password" description={t('setPassword.hint')} />
              <FormTextInput control={control} name="confirmPassword" label={t('setPassword.confirm')} type="password" startIcon={Icons.lock} autoComplete="new-password" />
              <Button type="submit" label={t('setPassword.submit')} variant="primary" isLoading={setPassword.isPending} isDisabled={tokenMissing} />
              {(tokenMissing || tokenRejected) && (
                <HStack hAlign="center">
                  <Link href={ROUTES.forgotPassword}>{t('setPassword.requestNew')}</Link>
                </HStack>
              )}
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
