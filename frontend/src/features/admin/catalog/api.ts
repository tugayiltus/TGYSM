import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

export type CategoryItem = components['schemas']['CategoryItem'];
export type CategoryScope = components['schemas']['CategoryScope'];
export type RequestTypeItem = components['schemas']['RequestTypeItem'];

export const CATEGORY_SCOPES: readonly CategoryScope[] = ['both', 'incident', 'request'];

const catalogKeys = {
  categories: ['admin', 'categories'],
  requestTypes: ['admin', 'request-types'],
  reference: ['reference'],
} as const;

function useInvalidate(key: readonly string[]) {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: key });
    await queryClient.invalidateQueries({ queryKey: catalogKeys.reference });
  };
}

export function useCategories() {
  return useQuery({ queryKey: catalogKeys.categories, queryFn: () => unwrap(api.GET('/api/v1/admin/categories')) });
}

export interface CategoryInput {
  id: string | null;
  version: number;
  name: string;
  parentId: string | null;
  scope: CategoryScope;
}

export function useSaveCategory() {
  const invalidate = useInvalidate(catalogKeys.categories);
  return useMutation({
    mutationFn: ({ id, version, ...body }: CategoryInput) =>
      id === null
        ? unwrap(api.POST('/api/v1/admin/categories', { body }))
        : unwrap(api.PUT('/api/v1/admin/categories/{id}', { params: { path: { id } }, body: { version, ...body } })),
    onSuccess: invalidate,
  });
}

export function useSetCategoryActive() {
  const invalidate = useInvalidate(catalogKeys.categories);
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/categories/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/categories/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useRequestTypes() {
  return useQuery({ queryKey: catalogKeys.requestTypes, queryFn: () => unwrap(api.GET('/api/v1/admin/request-types')) });
}

export interface RequestTypeInput {
  id: string | null;
  version: number;
  name: string;
  requiresApproval: boolean;
}

export function useSaveRequestType() {
  const invalidate = useInvalidate(catalogKeys.requestTypes);
  return useMutation({
    mutationFn: ({ id, version, ...body }: RequestTypeInput) =>
      id === null
        ? unwrap(api.POST('/api/v1/admin/request-types', { body }))
        : unwrap(api.PUT('/api/v1/admin/request-types/{id}', { params: { path: { id } }, body: { version, ...body } })),
    onSuccess: invalidate,
  });
}

export function useSetRequestTypeActive() {
  const invalidate = useInvalidate(catalogKeys.requestTypes);
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/request-types/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/request-types/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}
