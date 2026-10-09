import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';

type Schemas = components['schemas'];
export type TicketDetail = Schemas['TicketDetail'];
export type TicketListItem = Schemas['TicketListItem'];
export type TicketKind = Schemas['TicketKind'];
export type TicketStatus = Schemas['TicketStatus'];
export type Priority = Schemas['Priority'];
export type HoldReason = NonNullable<Schemas['HoldReason']>;
export type Level = Schemas['Level'];
export type TicketOutcome = NonNullable<Schemas['TicketOutcome']>;
export type TransferReason = Schemas['TransferReason'];
export type Visibility = Schemas['Visibility'];
export type CommentItem = Schemas['CommentItem'];
export type TimelineItem = Schemas['TimelineItem'];
export type TicketAssetItem = Schemas['TicketAssetItem'];
export type MyApprovalItem = Schemas['MyApprovalItem'];
export type TicketSla = Schemas['TicketSla'];
export type TicketQueue = 'mine' | 'team' | 'assigned_to_me' | 'unassigned' | 'on_hold' | 'sla_risk';

export const TICKET_STATUSES: readonly TicketStatus[] = ['open', 'in_progress', 'on_hold', 'completed', 'cancelled'];
export const PRIORITIES: readonly Priority[] = ['p1', 'p2', 'p3', 'p4'];
export const LEVELS: readonly Level[] = ['high', 'medium', 'low'];
/** manager_approval is set only by the approval flow. */
export const HOLD_REASONS: readonly HoldReason[] = ['user', 'vendor', 'technical_team', 'third_party', 'scheduled_date', 'other'];
export const INCIDENT_OUTCOMES: readonly TicketOutcome[] = ['resolved', 'service_request_required'];
export const TRANSFER_REASONS: readonly TransferReason[] = [
  'expertise_needed',
  'wrong_assignment',
  'other_team_responsibility',
  'authorization_needed',
  'workload',
  'escalation',
  'location',
  'other',
];
export const WORKSPACE_QUEUES: readonly TicketQueue[] = ['assigned_to_me', 'team', 'unassigned', 'on_hold', 'sla_risk'];

export const ticketKeys = {
  all: ['tickets'],
  list: (params: TicketListParams) => ['tickets', 'list', params],
  detail: (number: string) => ['tickets', 'detail', number],
  comments: (number: string) => ['tickets', 'comments', number],
  timeline: (number: string) => ['tickets', 'timeline', number],
  assets: (number: string) => ['tickets', 'assets', number],
  approvals: ['tickets', 'approvals'],
  reference: ['reference'],
} as const;

export interface TicketListParams {
  queue: TicketQueue | undefined;
  status: TicketStatus | undefined;
  kind: TicketKind | undefined;
  priority: Priority | undefined;
  search: string;
  page: number;
  pageSize: number;
}

export function useTickets(params: TicketListParams) {
  return useQuery({
    queryKey: ticketKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/tickets', {
          params: {
            query: {
              queue: params.queue,
              status: params.status,
              kind: params.kind,
              priority: params.priority,
              search: params.search === '' ? undefined : params.search,
              page: params.page,
              pageSize: params.pageSize,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useTicket(number: string) {
  return useQuery({ queryKey: ticketKeys.detail(number), queryFn: () => unwrap(api.GET('/api/v1/tickets/{number}', { params: { path: { number } } })) });
}

export function useComments(number: string) {
  return useQuery({ queryKey: ticketKeys.comments(number), queryFn: () => unwrap(api.GET('/api/v1/tickets/{number}/comments', { params: { path: { number } } })) });
}

export function useTimeline(number: string) {
  return useQuery({ queryKey: ticketKeys.timeline(number), queryFn: () => unwrap(api.GET('/api/v1/tickets/{number}/timeline', { params: { path: { number } } })) });
}

export function useTicketAssets(number: string) {
  return useQuery({ queryKey: ticketKeys.assets(number), queryFn: () => unwrap(api.GET('/api/v1/tickets/{number}/assets', { params: { path: { number } } })) });
}

export function useReferenceCategories(kind: TicketKind) {
  return useQuery({
    queryKey: [...ticketKeys.reference, 'categories', kind],
    queryFn: () => unwrap(api.GET('/api/v1/reference/categories', { params: { query: { kind } } })),
  });
}

export function useReferenceRequestTypes() {
  return useQuery({ queryKey: [...ticketKeys.reference, 'request-types'], queryFn: () => unwrap(api.GET('/api/v1/reference/request-types')) });
}

export function useReferenceTeams(enabled: boolean) {
  return useQuery({ queryKey: [...ticketKeys.reference, 'teams'], queryFn: () => unwrap(api.GET('/api/v1/reference/teams')), enabled });
}

function useInvalidateTickets() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ticketKeys.all });
}

export interface NewTicketInput {
  kind: TicketKind;
  subject: string;
  description: string;
  categoryId: string | null;
  locationId: string | null;
  requestTypeId: string | null;
  /** Incidents only: a device currently assigned to the requester. */
  assetId: string | null;
}

export function useCreateTicket() {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: ({ kind, requestTypeId, assetId, ...body }: NewTicketInput) =>
      kind === 'incident'
        ? unwrap(api.POST('/api/v1/tickets/incidents', { body: { ...body, assetId } }))
        : unwrap(api.POST('/api/v1/tickets/requests', { body: { ...body, requestTypeId: requestTypeId ?? '' } })),
    onSuccess: invalidate,
  });
}

export interface CommentInput {
  number: string;
  body: string;
  visibility: Visibility;
  files: readonly File[];
}

export function useAddComment() {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: ({ number, body, visibility, files }: CommentInput) => {
      const form = new FormData();
      form.append('body', body);
      form.append('visibility', visibility);
      files.forEach((file) => form.append('files', file));
      return unwrap(
        api.POST('/api/v1/tickets/{number}/comments', {
          params: { path: { number } },
          // The schema describes a generic form; the browser sends the FormData with its boundary.
          body: [],
          bodySerializer: () => form,
        }),
      );
    },
    onSuccess: invalidate,
  });
}

/** One ticket action. Every body carries the ticket's version (optimistic concurrency). */
export type TicketAction =
  | { kind: 'claim' }
  | { kind: 'start' }
  | { kind: 'resume' }
  | { kind: 'hold'; reason: HoldReason; note: string | null }
  | { kind: 'complete'; resolution: string; outcome: TicketOutcome | null; rootCause: string | null }
  | { kind: 'cancel'; reason: string }
  | { kind: 'priority'; impact: Level; urgency: Level; reason: string }
  | { kind: 'transfer'; teamId: string; assigneeId: string | null; reason: TransferReason; note: string | null }
  | { kind: 'invalidate'; reason: string };

export type TicketActionKind = TicketAction['kind'];

function runAction(ticket: TicketDetail, action: TicketAction) {
  const path = { params: { path: { number: ticket.number } } };
  const version = ticket.version;
  switch (action.kind) {
    case 'claim':
      return api.POST('/api/v1/tickets/{number}/claim', { ...path, body: { version } });
    case 'start':
      return api.POST('/api/v1/tickets/{number}/start', { ...path, body: { version } });
    case 'resume':
      return api.POST('/api/v1/tickets/{number}/resume', { ...path, body: { version } });
    case 'hold':
      return api.POST('/api/v1/tickets/{number}/hold', { ...path, body: { version, reason: action.reason, note: action.note } });
    case 'complete':
      return api.POST('/api/v1/tickets/{number}/complete', { ...path, body: { version, resolution: action.resolution, outcome: action.outcome, rootCause: action.rootCause } });
    case 'cancel':
      return api.POST('/api/v1/tickets/{number}/cancel', { ...path, body: { version, reason: action.reason } });
    case 'priority':
      return api.POST('/api/v1/tickets/{number}/priority', { ...path, body: { version, impact: action.impact, urgency: action.urgency, reason: action.reason } });
    case 'transfer':
      return api.POST('/api/v1/tickets/{number}/transfer', {
        ...path,
        body: { version, teamId: action.teamId, assigneeId: action.assigneeId, reason: action.reason, note: action.note },
      });
    case 'invalidate':
      return api.POST('/api/v1/tickets/{number}/approval/invalidate', { ...path, body: { version, reason: action.reason } });
  }
}

export function useTicketAction(ticket: TicketDetail) {
  const invalidate = useInvalidateTickets();
  return useMutation({ mutationFn: (action: TicketAction) => unwrap(runAction(ticket, action)), onSuccess: invalidate });
}

export function useCreateRelatedRequest(number: string) {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: (requestTypeId: string) => unwrap(api.POST('/api/v1/tickets/{number}/related-request', { params: { path: { number } }, body: { requestTypeId } })),
    onSuccess: invalidate,
  });
}

export function useLinkAsset(number: string) {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: (assetId: string) => unwrap(api.POST('/api/v1/tickets/{number}/assets', { params: { path: { number } }, body: { assetId } })),
    onSuccess: invalidate,
  });
}

export function useUnlinkAsset(number: string) {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: (assetId: string) => unwrap(api.DELETE('/api/v1/tickets/{number}/assets/{assetId}', { params: { path: { number, assetId } } })),
    onSuccess: invalidate,
  });
}

export function useMyApprovals() {
  return useQuery({ queryKey: ticketKeys.approvals, queryFn: () => unwrap(api.GET('/api/v1/approvals/mine')) });
}

export type ApprovalDecision = { id: string; version: number; approve: true } | { id: string; version: number; approve: false; reason: string };

export function useDecideApproval() {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: (decision: ApprovalDecision) => {
      const path = { params: { path: { id: decision.id } } };
      return decision.approve
        ? unwrap(api.POST('/api/v1/approvals/{id}/approve', { ...path, body: { version: decision.version, reason: null } }))
        : unwrap(api.POST('/api/v1/approvals/{id}/reject', { ...path, body: { version: decision.version, reason: decision.reason } }));
    },
    onSuccess: invalidate,
  });
}

export function useReassignApprover() {
  const invalidate = useInvalidateTickets();
  return useMutation({
    mutationFn: ({ number, approverId, reason }: { number: string; approverId: string; reason: string }) =>
      unwrap(api.POST('/api/v1/tickets/{number}/approval/reassign', { params: { path: { number } }, body: { approverId, reason } })),
    onSuccess: invalidate,
  });
}
