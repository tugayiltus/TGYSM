/**
 * @input Nothing; talks to `/api/v1/admin/auth-settings`.
 * @output Hooks to read and save the login method settings, test a provider and switch the method (ADR-0030).
 * @position The admin "Giriş yöntemi" page.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

export type AuthSettings = components['schemas']['AuthSettingsDto'];
export type LoginMethod = components['schemas']['LoginMethod'];
export type EntraSettingsInput = components['schemas']['UpdateEntraSettingsRequest'];
export type LdapSettingsInput = components['schemas']['UpdateLdapSettingsRequest'];

const SETTINGS_KEY = ['admin', 'auth-settings'] as const;
const PREVIEW_KEY = ['admin', 'auth-settings', 'preview'] as const;

export function useAuthSettings() {
  return useQuery({ queryKey: SETTINGS_KEY, queryFn: () => unwrap(api.GET('/api/v1/admin/auth-settings')) });
}

function useSettingsMutation<TInput>(send: (input: TInput) => Promise<AuthSettings>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: send, onSuccess: (settings) => queryClient.setQueryData(SETTINGS_KEY, settings) });
}

export const useSaveEntra = () =>
  useSettingsMutation((input: EntraSettingsInput) => unwrap(api.PUT('/api/v1/admin/auth-settings/entra', { body: input })));

export const useSaveLdap = () =>
  useSettingsMutation((input: LdapSettingsInput) => unwrap(api.PUT('/api/v1/admin/auth-settings/ldap', { body: input })));

export const useSwitchMethod = () =>
  useSettingsMutation((input: { method: LoginMethod; version: number }) => unwrap(api.POST('/api/v1/admin/auth-settings/method', { body: input })));

/** A passing test marks the provider tested on the server, so the settings are read again. */
export function useTestProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (method: LoginMethod) => unwrap(api.POST('/api/v1/admin/auth-settings/test', { body: { method } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SETTINGS_KEY }),
  });
}

export function useLocalSwitchPreview(enabled: boolean) {
  return useQuery({
    queryKey: PREVIEW_KEY,
    queryFn: () => unwrap(api.GET('/api/v1/admin/auth-settings/switch-preview', { params: { query: { method: 'local' } } })),
    enabled,
  });
}
