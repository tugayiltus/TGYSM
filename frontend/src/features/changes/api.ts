import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { taskKeys } from '../tasks/api';

type Schemas = components['schemas'];
export type ChangeView = Schemas['ChangeView'];
export type ChangeDetail = Schemas['ChangeDetail'];
export type ChangeSummary = Schemas['ChangeSummary'];
export type ChangeListItem = Schemas['ChangeListItem'];
export type ChangeDraft = Schemas['ChangeDraft'];
export type ChangeTimelineItem = Schemas['ChangeTimelineItem'];
export type ChangePhase = Schemas['ChangePhase'];
export type ChangeOutcome = NonNullable<Schemas['ChangeOutcome']>;
export type ChangeStatus = NonNullable<Schemas['ChangeStatus']>;
export type ChangeEventKind = Schemas['ChangeEventKind'];
export type ChangeApprovalItem = Schemas['ChangeApprovalItem'];
export type ChangeAssetItem = Schemas['ChangeAssetItem'];
export type Level = Schemas['Level'];
export type ChangeQueue = 'mine' | 'team' | 'pending_approval' | 'open';

export const CHANGE_QUEUES: readonly ChangeQueue[] = ['mine', 'team', 'pending_approval', 'open'];
export const CHANGE_STATUSES: readonly ChangeStatus[] = ['draft', 'pending_approval', 'scheduled', 'implementing', 'verifying', 'closed', 'cancelled'];
export const CHANGE_PHASES: readonly ChangePhase[] = ['preparation', 'implementation', 'verification', 'backout'];
export const CHANGE_OUTCOMES: readonly ChangeOutcome[] = ['successful', 'rolled_back', 'failed'];
export const LEVELS: readonly Level[] = ['low', 'medium', 'high'];

export const changeKeys = {
  all: ['changes'],
  list: (params: ChangeListParams) => ['changes', 'list', params],
  detail: (number: string) => ['changes', 'detail', number],
  timeline: (number: string) => ['changes', 'timeline', number],
  approvals: ['changes', 'approvals'],
} as const;

export interface ChangeListParams {
  queue: ChangeQueue | undefined;
  status: ChangeStatus | undefined;
  risk: Level | undefined;
  search: string;
  page: number;
  pageSize: number;
}

export function useChanges(params: ChangeListParams) {
  return useQuery({
    queryKey: changeKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/changes', {
          params: {
            query: {
              queue: params.queue,
              status: params.status,
              risk: params.risk,
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

export function useChange(number: string) {
  return useQuery({ queryKey: changeKeys.detail(number), queryFn: () => unwrap(api.GET('/api/v1/changes/{number}', { params: { path: { number } } })) });
}

export function useChangeTimeline(number: string, enabled: boolean) {
  return useQuery({
    enabled,
    queryKey: changeKeys.timeline(number),
    queryFn: () => unwrap(api.GET('/api/v1/changes/{number}/timeline', { params: { path: { number } } })),
  });
}

/** The change as the full view the page renders. */
const fullView = (detail: ChangeDetail): ChangeView => ({ access: 'full', detail, summary: null });

/** A change touches task rows (phase groups, task counts), so both caches refresh after a write. */
function useInvalidateChanges() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: changeKeys.all });
    await queryClient.invalidateQueries({ queryKey: taskKeys.all });
  };
}

/** A failed write most often means a stale version: reload so the next try sends the current one. */
function useReloadOnError() {
  const queryClient = useQueryClient();
  return () => void queryClient.invalidateQueries({ queryKey: changeKeys.all });
}

export interface NewChangeInput {
  draft: ChangeDraft;
  ownerTeamId: string;
  ownerId: string;
  assetIds: string[];
}

export function useCreateChange() {
  const invalidate = useInvalidateChanges();
  return useMutation({ mutationFn: (body: NewChangeInput) => unwrap(api.POST('/api/v1/changes', { body })), onSuccess: invalidate });
}

export interface EditChangeInput {
  draft: ChangeDraft;
  assetIds: string[];
}

export function useEditChange(number: string, version: number) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateChanges();
  const reload = useReloadOnError();
  return useMutation({
    mutationFn: (input: EditChangeInput) =>
      unwrap(api.PUT('/api/v1/changes/{number}', { params: { path: { number } }, body: { version, ...input } })),
    onSuccess: async (updated) => {
      queryClient.setQueryData(changeKeys.detail(updated.number), fullView(updated));
      await invalidate();
    },
    onError: reload,
  });
}

/** One change action. Every body carries the change's version (optimistic concurrency). */
export type ChangeAction =
  | { kind: 'submit' }
  | { kind: 'withdraw' }
  | { kind: 'start-implementation' }
  | { kind: 'finish-implementation' }
  | { kind: 'start-backout' }
  | { kind: 'close'; outcome: ChangeOutcome; verificationNote: string | null; backoutNote: string | null; closingNote: string | null; actualEnd: string | null }
  | { kind: 'cancel'; reason: string };
export type ChangeActionKind = ChangeAction['kind'];

function runAction(change: ChangeDetail, action: ChangeAction) {
  const path = { params: { path: { number: change.number } } };
  const version = change.version;
  switch (action.kind) {
    case 'submit':
      return api.POST('/api/v1/changes/{number}/submit', { ...path, body: { version } });
    case 'withdraw':
      return api.POST('/api/v1/changes/{number}/withdraw', { ...path, body: { version } });
    case 'start-implementation':
      return api.POST('/api/v1/changes/{number}/start-implementation', { ...path, body: { version } });
    case 'finish-implementation':
      return api.POST('/api/v1/changes/{number}/finish-implementation', { ...path, body: { version } });
    case 'start-backout':
      return api.POST('/api/v1/changes/{number}/start-backout', { ...path, body: { version } });
    case 'close':
      return api.POST('/api/v1/changes/{number}/close', {
        ...path,
        body: {
          version,
          outcome: action.outcome,
          verificationNote: action.verificationNote,
          backoutNote: action.backoutNote,
          closingNote: action.closingNote,
          actualEnd: action.actualEnd,
        },
      });
    case 'cancel':
      return api.POST('/api/v1/changes/{number}/cancel', { ...path, body: { version, reason: action.reason } });
  }
}

export function useChangeAction(change: ChangeDetail) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateChanges();
  const reload = useReloadOnError();
  return useMutation({
    mutationFn: (action: ChangeAction) => unwrap(runAction(change, action)),
    // The answer carries the new version: store it at once, so the next action does not send the old one and get 409.
    onSuccess: async (updated) => {
      queryClient.setQueryData(changeKeys.detail(updated.number), fullView(updated));
      await invalidate();
    },
    onError: reload,
  });
}

export type ApprovalDecision = 'approve' | 'reject' | 'return';

function postDecision(number: string, decision: ApprovalDecision, approvalVersion: number, reason: string | null) {
  const options = { params: { path: { number } }, body: { approvalVersion, reason } };
  switch (decision) {
    case 'approve':
      return unwrap(api.POST('/api/v1/changes/{number}/approval/approve', options));
    case 'reject':
      return unwrap(api.POST('/api/v1/changes/{number}/approval/reject', options));
    case 'return':
      return unwrap(api.POST('/api/v1/changes/{number}/approval/return', options));
  }
}

export function useDecideChangeApproval(change: ChangeDetail) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateChanges();
  const reload = useReloadOnError();
  return useMutation({
    mutationFn: ({ decision, reason }: { decision: ApprovalDecision; reason: string | null }) =>
      postDecision(change.number, decision, change.approval?.version ?? 0, reason),
    onSuccess: async (updated) => {
      queryClient.setQueryData(changeKeys.detail(updated.number), fullView(updated));
      await invalidate();
    },
    onError: reload,
  });
}

/** Change approvals the caller may decide (the Onaylar page). */
export function useChangeApprovals() {
  return useQuery({ queryKey: changeKeys.approvals, queryFn: () => unwrap(api.GET('/api/v1/change-approvals')) });
}

/** Decides a listed approval: the list row carries the approval version the page showed. */
export function useDecideListedChangeApproval() {
  const invalidate = useInvalidateChanges();
  const reload = useReloadOnError();
  return useMutation({
    mutationFn: ({ number, decision, approvalVersion, reason }: { number: string; decision: ApprovalDecision; approvalVersion: number; reason: string | null }) =>
      postDecision(number, decision, approvalVersion, reason),
    onSuccess: invalidate,
    onError: reload,
  });
}
