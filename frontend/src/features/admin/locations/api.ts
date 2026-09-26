import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type LocationItem = components['schemas']['LocationItem'];

export function useLocations() {
  return useQuery({ queryKey: adminKeys.locations, queryFn: () => unwrap(api.GET('/api/v1/admin/locations')) });
}

export type CreateLocationInput = components['schemas']['CreateLocationCommand'];
export type UpdateLocationInput = components['schemas']['UpdateLocationRequest'] & { id: string };

/** Location names also appear in user lists and option labels. */
function useInvalidateLocations() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: adminKeys.locations });
    await queryClient.invalidateQueries({ queryKey: adminKeys.users });
  };
}

export function useCreateLocation() {
  const invalidate = useInvalidateLocations();
  return useMutation({ mutationFn: (body: CreateLocationInput) => unwrap(api.POST('/api/v1/admin/locations', { body })), onSuccess: invalidate });
}

export function useUpdateLocation() {
  const invalidate = useInvalidateLocations();
  return useMutation({
    mutationFn: ({ id, ...body }: UpdateLocationInput) => unwrap(api.PUT('/api/v1/admin/locations/{id}', { params: { path: { id } }, body })),
    onSuccess: invalidate,
  });
}

export function useSetLocationActive() {
  const invalidate = useInvalidateLocations();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/locations/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/locations/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}
