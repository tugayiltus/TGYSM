import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

export type TeamItem = components['schemas']['TeamItem'];
export type TeamDetail = components['schemas']['TeamDetail'];

export interface TeamListParams {
  search: string;
  page: number;
  pageSize: number;
}

export function useTeams(params: TeamListParams) {
  return useQuery({
    queryKey: [...adminKeys.teams, 'list', params],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/teams', { params: { query: { search: params.search === '' ? undefined : params.search, page: params.page, pageSize: params.pageSize } } })),
    placeholderData: keepPreviousData,
  });
}

export function useTeam(id: string) {
  return useQuery({
    queryKey: [...adminKeys.teams, 'detail', id],
    queryFn: () => unwrap(api.GET('/api/v1/admin/teams/{id}', { params: { path: { id } } })),
  });
}

function useInvalidateTeams() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: adminKeys.teams });
}

export function useCreateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({ mutationFn: (name: string) => unwrap(api.POST('/api/v1/admin/teams', { body: { name } })), onSuccess: invalidate });
}

export function useUpdateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ id, version, name }: { id: string; version: number; name: string }) =>
      unwrap(api.PUT('/api/v1/admin/teams/{id}', { params: { path: { id } }, body: { version, name } })),
    onSuccess: invalidate,
  });
}

export function useSetTeamActive() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ id, version, isActive }: { id: string; version: number; isActive: boolean }) => {
      const options = { params: { path: { id } }, body: { version } };
      return isActive
        ? unwrap(api.POST('/api/v1/admin/teams/{id}/reactivate', options))
        : unwrap(api.POST('/api/v1/admin/teams/{id}/deactivate', options));
    },
    onSuccess: invalidate,
  });
}

export function useAddTeamMember() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, userId }: { teamId: string; userId: string }) =>
      unwrap(api.POST('/api/v1/admin/teams/{id}/members', { params: { path: { id: teamId } }, body: { userId } })),
    onSuccess: invalidate,
  });
}

export function useRemoveTeamMember() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, userId }: { teamId: string; userId: string }) =>
      unwrap(api.DELETE('/api/v1/admin/teams/{id}/members/{userId}', { params: { path: { id: teamId, userId } } })),
    onSuccess: invalidate,
  });
}
