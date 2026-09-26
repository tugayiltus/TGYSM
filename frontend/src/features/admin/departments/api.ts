import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type DepartmentItem = components['schemas']['DepartmentItem'];
export type CreateDepartmentInput = components['schemas']['CreateDepartmentCommand'];
export type UpdateDepartmentInput = components['schemas']['UpdateDepartmentRequest'] & { id: string };

export interface DepartmentListParams {
  search: string;
  isActive: boolean | undefined;
  page: number;
  pageSize: number;
}

export function useDepartments(params: DepartmentListParams) {
  return useQuery({
    queryKey: [...adminKeys.departments, 'list', params],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/admin/departments', {
          params: { query: { search: params.search === '' ? undefined : params.search, isActive: params.isActive, page: params.page, pageSize: params.pageSize } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

/** Department names also appear in user lists. */
function useInvalidateDepartments() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: adminKeys.departments });
    await queryClient.invalidateQueries({ queryKey: adminKeys.users });
  };
}

export function useCreateDepartment() {
  const invalidate = useInvalidateDepartments();
  return useMutation({ mutationFn: (body: CreateDepartmentInput) => unwrap(api.POST('/api/v1/admin/departments', { body })), onSuccess: invalidate });
}

export function useUpdateDepartment() {
  const invalidate = useInvalidateDepartments();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateDepartmentInput) => unwrap(api.PUT('/api/v1/admin/departments/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetDepartmentActive() {
  const invalidate = useInvalidateDepartments();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/departments/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/departments/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}
