/**
 * @input Nothing; reads the login settings.
 * @output "Giriş yöntemi": the active method, one card per provider with its settings, a connection test and the
 *   switch, and the way back to Yerel (ADR-0030).
 * @position `/admin/login-method`, admin only.
 */
import { Banner } from '@astryxdesign/core/Banner';
import { Button } from '@astryxdesign/core/Button';
import { HStack } from '@astryxdesign/core/HStack';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { VStack } from '@astryxdesign/core/VStack';
import { zodResolver } from '@hookform/resolvers/zod';
import { useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { describeError, translateErrorCode } from '../../../shared/api/errorMessage';
import { ERROR_CODES } from '../../../shared/api/errors';
import { ConfirmDialog } from '../../../shared/feedback/ConfirmDialog';
import { ErrorState, LoadingState } from '../../../shared/feedback/States';
import { UnsavedChangesGuard } from '../../../shared/feedback/UnsavedChangesGuard';
import { FormNumberInput, FormSelector, FormTextInput } from '../../../shared/form/formFields';
import { PageHeader } from '../../../shared/navigation/PageHeader';
import { ROUTE_META } from '../../../shared/navigation/routeMeta';
import { SectionCard } from '../../../shared/page/SectionCard';

import { useAuthSettings, useLocalSwitchPreview, useSaveEntra, useSaveLdap, useSwitchMethod, useTestProvider, type AuthSettings, type LoginMethod } from './api';

const DOMAIN_SEPARATOR = /[,\s]+/;
const MIN_PORT = 1;
const MAX_PORT = 65535;
const LDAP_SECURITIES = ['ldaps', 'start_tls'] as const;

/** An empty secret field keeps the stored secret; the API never returns it. */
const secretOrNull = (value: string) => (value.trim() === '' ? null : value);

const entraSchema = z.object({
  tenantId: z.string().trim().min(1, ERROR_CODES.required),
  clientId: z.string().trim().min(1, ERROR_CODES.required),
  clientSecret: z.string(),
  allowedDomains: z.string(),
});

const ldapSchema = z.object({
  host: z.string().trim().min(1, ERROR_CODES.required),
  port: z.number().int().min(MIN_PORT).max(MAX_PORT),
  security: z.enum(LDAP_SECURITIES),
  baseDn: z.string().trim().min(1, ERROR_CODES.required),
  bindDn: z.string().trim().min(1, ERROR_CODES.required),
  bindPassword: z.string(),
  userFilter: z.string(),
});

/** The provider card frame: test, switch-on and their results, shared by both providers. */
function ProviderActions({ method, settings, isDirty }: { method: Exclude<LoginMethod, 'local'>; settings: AuthSettings; isDirty: boolean }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const test = useTestProvider();
  const switchMethod = useSwitchMethod();
  const [confirming, setConfirming] = useState(false);
  const isTested = method === 'entra' ? settings.entra.isTested : settings.ldap.isTested;
  const isActive = settings.method === method;
  return (
    <VStack gap={2}>
      {test.data && (test.data.ok ? <Banner status="success" title={t('loginMethod.testOk')} /> : <Banner status="error" title={translateErrorCode(test.data.errorCode ?? '')} />)}
      {switchMethod.error && <Banner status="error" title={describeError(switchMethod.error)} />}
      <HStack gap={2} wrap="wrap">
        <Button label={t('loginMethod.test')} variant="secondary" isDisabled={isDirty} isLoading={test.isPending} onClick={() => test.mutate(method)} />
        {!isActive && (
          <Button
            label={t('loginMethod.switchOn')}
            variant="primary"
            isDisabled={!isTested || isDirty}
            onClick={() => setConfirming(true)}
          />
        )}
      </HStack>
      {!isActive && !isTested && <Text type="supporting">{t('loginMethod.switchDisabled')}</Text>}
      {confirming && (
        <ConfirmDialog
          title={t('loginMethod.switchOn')}
          message={t('loginMethod.switchConfirm', { method: t(`loginMethod.methods.${method}`) })}
          confirmLabel={tc('actions.confirm')}
          variant="primary"
          isPending={switchMethod.isPending}
          error={switchMethod.error}
          onConfirm={() => switchMethod.mutate({ method, version: settings.version }, { onSuccess: () => setConfirming(false) })}
          onCancel={() => setConfirming(false)}
        />
      )}
    </VStack>
  );
}

function SecretHint({ stored }: { stored: boolean }) {
  const { t } = useTranslation('admin');
  return <Text type="supporting">{stored ? t('loginMethod.secretStored') : t('loginMethod.secretMissing')}</Text>;
}

function EntraCard({ settings }: { settings: AuthSettings }) {
  const { t } = useTranslation('admin');
  const save = useSaveEntra();
  const { control, handleSubmit, reset, formState } = useForm({
    resolver: zodResolver(entraSchema),
    defaultValues: { tenantId: settings.entra.tenantId, clientId: settings.entra.clientId, clientSecret: '', allowedDomains: settings.entra.allowedDomains.join(', ') },
  });
  const onSubmit = handleSubmit((values) =>
    save.mutate(
      {
        tenantId: values.tenantId,
        clientId: values.clientId,
        clientSecret: secretOrNull(values.clientSecret),
        allowedDomains: values.allowedDomains.split(DOMAIN_SEPARATOR).filter((d) => d !== ''),
        version: settings.version,
      },
      { onSuccess: (saved) => reset({ ...values, clientSecret: '', allowedDomains: saved.entra.allowedDomains.join(', ') }) },
    ),
  );
  return (
    <ProviderCard title={t('loginMethod.methods.entra')} isDirty={formState.isDirty}>
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <VStack gap={3}>
          {save.error && <Banner status="error" title={describeError(save.error)} />}
          <FormTextInput control={control} name="tenantId" label={t('loginMethod.fields.tenantId')} autoComplete="off" isRequired />
          <FormTextInput control={control} name="clientId" label={t('loginMethod.fields.clientId')} autoComplete="off" isRequired />
          <FormTextInput control={control} name="clientSecret" label={t('loginMethod.fields.clientSecret')} type="password" autoComplete="new-password" />
          <SecretHint stored={settings.entra.hasClientSecret} />
          <FormTextInput control={control} name="allowedDomains" label={t('loginMethod.fields.allowedDomains')} description={t('loginMethod.fields.allowedDomainsHint')} autoComplete="off" />
          <HStack gap={2} vAlign="end">
            <TextInput label={t('loginMethod.fields.redirectUri')} value={settings.redirectUri} isReadOnly />
            <Button label={t('loginMethod.copy')} variant="ghost" onClick={() => void navigator.clipboard.writeText(settings.redirectUri)} />
          </HStack>
          <HStack>
            <Button type="submit" label={t('loginMethod.save')} variant="secondary" isLoading={save.isPending} />
          </HStack>
        </VStack>
      </form>
      <ProviderActions method="entra" settings={settings} isDirty={formState.isDirty} />
    </ProviderCard>
  );
}

function LdapCard({ settings }: { settings: AuthSettings }) {
  const { t } = useTranslation('admin');
  const save = useSaveLdap();
  const { control, handleSubmit, reset, formState } = useForm({
    resolver: zodResolver(ldapSchema),
    defaultValues: {
      host: settings.ldap.host,
      port: settings.ldap.port,
      security: settings.ldap.security,
      baseDn: settings.ldap.baseDn,
      bindDn: settings.ldap.bindDn,
      bindPassword: '',
      userFilter: settings.ldap.userFilter ?? '',
    },
  });
  const onSubmit = handleSubmit((values) =>
    save.mutate(
      { ...values, bindPassword: secretOrNull(values.bindPassword), userFilter: values.userFilter.trim() === '' ? null : values.userFilter, version: settings.version },
      { onSuccess: () => reset({ ...values, bindPassword: '' }) },
    ),
  );
  return (
    <ProviderCard title={t('loginMethod.methods.ldap')} isDirty={formState.isDirty}>
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <VStack gap={3}>
          {save.error && <Banner status="error" title={describeError(save.error)} />}
          <FormTextInput control={control} name="host" label={t('loginMethod.fields.host')} autoComplete="off" isRequired />
          <FormNumberInput control={control} name="port" label={t('loginMethod.fields.port')} min={MIN_PORT} max={MAX_PORT} />
          <FormSelector
            control={control}
            name="security"
            label={t('loginMethod.fields.security')}
            options={LDAP_SECURITIES.map((value) => ({ value, label: t(`loginMethod.security.${value}`) }))}
          />
          <FormTextInput control={control} name="baseDn" label={t('loginMethod.fields.baseDn')} autoComplete="off" isRequired />
          <FormTextInput control={control} name="bindDn" label={t('loginMethod.fields.bindDn')} autoComplete="off" isRequired />
          <FormTextInput control={control} name="bindPassword" label={t('loginMethod.fields.bindPassword')} type="password" autoComplete="new-password" />
          <SecretHint stored={settings.ldap.hasBindPassword} />
          <FormTextInput control={control} name="userFilter" label={t('loginMethod.fields.userFilter')} description={t('loginMethod.fields.userFilterHint')} autoComplete="off" />
          <HStack>
            <Button type="submit" label={t('loginMethod.save')} variant="secondary" isLoading={save.isPending} />
          </HStack>
        </VStack>
      </form>
      <ProviderActions method="ldap" settings={settings} isDirty={formState.isDirty} />
    </ProviderCard>
  );
}

function ProviderCard({ title, isDirty, children }: { title: string; isDirty: boolean; children: ReactNode }) {
  return (
    <SectionCard title={title}>
      <UnsavedChangesGuard when={isDirty} />
      {children}
    </SectionCard>
  );
}

function BackToLocal({ settings }: { settings: AuthSettings }) {
  const { t } = useTranslation('admin');
  const { t: tc } = useTranslation();
  const [confirming, setConfirming] = useState(false);
  const preview = useLocalSwitchPreview(confirming);
  const switchMethod = useSwitchMethod();
  return (
    <>
      <HStack>
        <Button label={t('loginMethod.backToLocal')} variant="secondary" onClick={() => setConfirming(true)} />
      </HStack>
      {confirming && preview.data && (
        <ConfirmDialog
          title={t('loginMethod.backToLocal')}
          message={t('loginMethod.backToLocalConfirm', { count: preview.data.usersWithoutPassword })}
          confirmLabel={tc('actions.confirm')}
          variant="danger"
          isPending={switchMethod.isPending}
          error={switchMethod.error}
          onConfirm={() => switchMethod.mutate({ method: 'local', version: settings.version }, { onSuccess: () => setConfirming(false) })}
          onCancel={() => setConfirming(false)}
        />
      )}
    </>
  );
}

export function LoginMethodPage() {
  const { t } = useTranslation('admin');
  const settings = useAuthSettings();
  return (
    <VStack gap={4}>
      <PageHeader icon={ROUTE_META.adminLoginMethod.icon} description={t('loginMethod.intro')} />
      {settings.isPending && <LoadingState />}
      {settings.isError && <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />}
      {settings.data && (
        <>
          <SectionCard title={t('loginMethod.active')}>
            <Text type="label">{t(`loginMethod.methods.${settings.data.method}`)}</Text>
            {settings.data.method !== 'local' && <BackToLocal settings={settings.data} />}
          </SectionCard>
          {/* Each card remounts after a save or test so its form starts from the stored values and version. */}
          <EntraCard key={`entra-${settings.data.version}`} settings={settings.data} />
          <LdapCard key={`ldap-${settings.data.version}`} settings={settings.data} />
        </>
      )}
    </VStack>
  );
}
