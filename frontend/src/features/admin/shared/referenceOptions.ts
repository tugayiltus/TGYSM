import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { api, unwrap } from '../../../shared/api/client';
import type { SelectOption } from '../../../shared/ui/SelectField';
import { useLocations } from '../locations/api';
import { buildLocationTree, flattenLocationTree } from '../locations/locationTree';
import type { UserRole } from '../users/api';

import { adminKeys } from './queryKeys';

/** Reference lists are small; one page of the API maximum (PageRequest.MaxPageSize) covers them. */
export const REFERENCE_PAGE_SIZE = 100;

export function useDepartmentOptions(): SelectOption[] {
  const departments = useQuery({
    queryKey: [...adminKeys.departments, 'options'],
    queryFn: () => unwrap(api.GET('/api/v1/admin/departments', { params: { query: { isActive: true, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
  return useMemo(() => (departments.data?.items ?? []).map((d) => ({ value: d.id, label: d.name })), [departments.data]);
}

export function useLocationOptions(): SelectOption[] {
  const locations = useLocations();
  return useMemo(
    () =>
      flattenLocationTree(buildLocationTree(locations.data ?? []))
        .filter((node) => node.item.isActive)
        .map((node) => ({ value: node.item.id, label: node.path })),
    [locations.data],
  );
}

/** Keeps the record's current (maybe now inactive) reference selectable so editing does not silently clear it. */
export function withCurrentOption(options: SelectOption[], id: string | null | undefined, label: string | null | undefined): SelectOption[] {
  if (!id || options.some((option) => option.value === id)) {
    return options;
  }
  return [...options, { value: id, label: label ?? id }];
}

function useActiveUsers(role: UserRole | undefined) {
  return useQuery({
    queryKey: [...adminKeys.users, 'options', role ?? 'all'],
    queryFn: () =>
      unwrap(api.GET('/api/v1/admin/users', { params: { query: { isActive: true, role, pageSize: REFERENCE_PAGE_SIZE } } })),
  });
}

/** Active admins: department approvers must be admins (ADR-0005). */
export function useAdminOptions(): SelectOption[] {
  const admins = useActiveUsers('admin');
  return useMemo(() => (admins.data?.items ?? []).map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName}` })), [admins.data]);
}

/** Active IT staff and admins: the only roles allowed in teams. */
export function useTeamCandidateOptions(): SelectOption[] {
  const users = useActiveUsers(undefined);
  return useMemo(
    () =>
      (users.data?.items ?? [])
        .filter((u) => u.role !== 'employee')
        .map((u) => ({ value: u.id, label: `${u.firstName} ${u.lastName} (${u.email})` })),
    [users.data],
  );
}
