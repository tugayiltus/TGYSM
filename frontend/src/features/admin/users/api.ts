import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type UserListItem = components['schemas']['UserListItem'];
export type UserRole = UserListItem['role'];
export type CredentialDelivery = components['schemas']['CredentialDelivery'];
export type CreateUserInput = Omit<components['schemas']['CreateUserCommand'], 'delivery'> & { delivery: CredentialDelivery };

/** Spec §4 default: the user gets a set-password link by email, no password is shown to the admin. */
export const DEFAULT_DELIVERY: CredentialDelivery = 'email_link';
export type UpdateUserInput = components['schemas']['UpdateUserRequest'] & { id: string };

export interface UserListParams {
  search: string;
  isActive: boolean | undefined;
  role: UserRole | undefined;
  page: number;
  pageSize: number;
}

export function useUsers(params: UserListParams) {
  return useQuery({
    queryKey: [...adminKeys.users, 'list', params],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/admin/users', {
          params: {
            query: {
              search: params.search === '' ? undefined : params.search,
              isActive: params.isActive,
              role: params.role,
              page: params.page,
              pageSize: params.pageSize,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

function useInvalidateUsers() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: adminKeys.users });
}

export function useCreateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: (body: CreateUserInput) => unwrap(api.POST('/api/v1/admin/users', { body })),
    onSuccess: invalidate,
  });
}

export function useUpdateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateUserInput) => unwrap(api.PUT('/api/v1/admin/users/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetUserActive() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/users/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/users/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useResetUserPassword() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: ({ id, delivery }: { id: string; delivery: CredentialDelivery }) =>
      unwrap(api.POST('/api/v1/admin/users/{id}/reset-password', { params: { path: { id } }, body: { delivery } })),
    onSuccess: invalidate,
  });
}
