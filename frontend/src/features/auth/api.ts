import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, refreshCsrfToken, resetCsrfToken, unwrap } from '../../shared/api/client';
import { ApiError } from '../../shared/api/errors';
import { ME_QUERY_KEY } from '../../shared/api/queryKeys';
import type { components } from '../../shared/api/schema';

export type Me = components['schemas']['MeDto'];
export type Role = Me['role'];
export type LoginInput = components['schemas']['LoginRequest'];
export type ChangePasswordInput = components['schemas']['ChangePasswordCommand'];

const HTTP_UNAUTHORIZED = 401;
const ME_STALE_TIME_MS = 60_000;

/** null = no session (401); other errors propagate. */
async function fetchMe(): Promise<Me | null> {
  try {
    return await unwrap(api.GET('/api/v1/auth/me'));
  } catch (error) {
    if (error instanceof ApiError && error.status === HTTP_UNAUTHORIZED) {
      // A token bound to an ended session would make the next login fail with csrf_invalid.
      resetCsrfToken();
      return null;
    }
    throw error;
  }
}

export function useMe() {
  return useQuery({ queryKey: ME_QUERY_KEY, queryFn: fetchMe, staleTime: ME_STALE_TIME_MS });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: LoginInput) => {
      const result = await unwrap(api.POST('/api/v1/auth/login', { body: input }));
      await refreshCsrfToken();
      return result;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await unwrap(api.POST('/api/v1/auth/logout'));
      await refreshCsrfToken();
    },
    onSuccess: () => {
      queryClient.clear();
      queryClient.setQueryData(ME_QUERY_KEY, null);
    },
  });
}

export function useChangePassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ChangePasswordInput) => unwrap(api.POST('/api/v1/auth/change-password', { body: input })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }),
  });
}

export type SetPasswordInput = components['schemas']['SetPasswordRequest'];

/** Anonymous: the emailed link's token plus the new password; one error code (`auth.token_invalid`) for every bad token. */
export function useSetPassword() {
  return useMutation({ mutationFn: (input: SetPasswordInput) => unwrap(api.POST('/api/v1/auth/set-password', { body: input })) });
}

/** Anonymous: the server always answers 202, so success reveals nothing about the account. */
export function useForgotPassword() {
  return useMutation({ mutationFn: (email: string) => unwrap(api.POST('/api/v1/auth/forgot-password', { body: { email } })) });
}
