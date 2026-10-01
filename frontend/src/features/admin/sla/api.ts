import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { adminKeys } from '../shared/queryKeys';

type Schemas = components['schemas'];
export type CalendarDetail = Schemas['CalendarDetail'];
export type WorkingHoursItem = Schemas['WorkingHoursItem'];
export type HolidayItem = Schemas['HolidayItem'];
export type PolicyDetail = Schemas['PolicyDetail'];
export type PolicyTargetItem = Schemas['PolicyTargetItem'];
export type PauseRuleItem = Schemas['PauseRuleItem'];
export type DayOfWeek = Schemas['DayOfWeek'];
export type SlaMetric = Schemas['SlaMetric'];
export type HoldReason = Schemas['HoldReason'];
export type TicketKind = Schemas['TicketKind'];
export type Priority = Schemas['Priority'];

/** Monday first, as the working week is shown. */
export const WEEKDAYS: readonly DayOfWeek[] = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
export const SLA_METRICS: readonly SlaMetric[] = ['first_response', 'resolution'];
export const SLA_KINDS: readonly TicketKind[] = ['incident', 'request'];
export const SLA_PRIORITIES: readonly Priority[] = ['p1', 'p2', 'p3', 'p4'];
export const SLA_HOLD_REASONS: readonly HoldReason[] = ['user', 'manager_approval', 'vendor', 'technical_team', 'third_party', 'scheduled_date', 'other'];

export function useSlaCalendar() {
  return useQuery({ queryKey: adminKeys.slaCalendar, queryFn: () => unwrap(api.GET('/api/v1/admin/sla/calendar')) });
}

export interface UpdateCalendarInput {
  version: number;
  timeZone: string;
  hours: WorkingHoursItem[];
}

export function useUpdateSlaCalendar() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateCalendarInput) => unwrap(api.PUT('/api/v1/admin/sla/calendar', { body })),
    onSuccess: (calendar) => queryClient.setQueryData(adminKeys.slaCalendar, calendar),
  });
}

export function useSlaHolidays() {
  return useQuery({ queryKey: adminKeys.slaHolidays, queryFn: () => unwrap(api.GET('/api/v1/admin/sla/holidays')) });
}

export function useAddHoliday() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { date: string; name: string }) => unwrap(api.POST('/api/v1/admin/sla/holidays', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.slaHolidays }),
  });
}

export function useRemoveHoliday() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE('/api/v1/admin/sla/holidays/{id}', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.slaHolidays }),
  });
}

export function useSlaPolicy() {
  return useQuery({ queryKey: adminKeys.slaPolicy, queryFn: () => unwrap(api.GET('/api/v1/admin/sla/policy')) });
}

export interface CreatePolicyInput {
  targets: PolicyTargetItem[];
  pauseRules: PauseRuleItem[];
}

export function useCreateSlaPolicyVersion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreatePolicyInput) => unwrap(api.POST('/api/v1/admin/sla/policy', { body })),
    onSuccess: (policy) => queryClient.setQueryData(adminKeys.slaPolicy, policy),
  });
}
