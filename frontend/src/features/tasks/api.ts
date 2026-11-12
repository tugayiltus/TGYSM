import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { ROUTES } from '../../shared/routes';
import { ticketKeys, type HoldReason, type Priority, type TransferReason } from '../tickets/api';

type Schemas = components['schemas'];
export type TaskDetail = Schemas['TaskDetail'];
export type TaskListItem = Schemas['TaskListItem'];
export type TaskStatus = Schemas['WorkTaskStatus'];
export type WorkItemRow = Schemas['WorkItemRow'];
export type TaskQueue = 'mine' | 'team' | 'unassigned' | 'overdue';
export type WorkQueue = 'assigned_to_me' | 'my_teams';

export const TASK_STATUSES: readonly TaskStatus[] = ['open', 'in_progress', 'on_hold', 'completed', 'cancelled'];
export const TASK_QUEUES: readonly TaskQueue[] = ['mine', 'team', 'unassigned', 'overdue'];
const QUEUE_PARAM = 'queue';

/** The task list opened on one queue, as the İş listem cards link to it. */
export const taskQueuePath = (queue: TaskQueue) => `${ROUTES.tasks}?${new URLSearchParams({ [QUEUE_PARAM]: queue }).toString()}`;

export const taskKeys = {
  all: ['tasks'],
  list: (params: TaskListParams) => ['tasks', 'list', params],
  detail: (number: string) => ['tasks', 'detail', number],
  work: (params: WorkListParams) => ['tasks', 'work', params],
} as const;

export interface TaskListParams {
  queue: TaskQueue | undefined;
  status: TaskStatus | undefined;
  priority: Priority | undefined;
  search: string;
  page: number;
  pageSize: number;
}

export function useTasks(params: TaskListParams) {
  return useQuery({
    queryKey: taskKeys.list(params),
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/tasks', {
          params: {
            query: {
              queue: params.queue,
              status: params.status,
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

export interface WorkListParams {
  queue: WorkQueue;
  page: number;
  pageSize: number;
}

/** İş listem: open tickets and tasks in one list (ADR-0031). */
export function useWorkItems(params: WorkListParams) {
  return useQuery({
    queryKey: taskKeys.work(params),
    queryFn: () => unwrap(api.GET('/api/v1/work-items', { params: { query: { queue: params.queue, page: params.page, pageSize: params.pageSize } } })),
    placeholderData: keepPreviousData,
  });
}

export function useTask(number: string) {
  return useQuery({ queryKey: taskKeys.detail(number), queryFn: () => unwrap(api.GET('/api/v1/tasks/{number}', { params: { path: { number } } })) });
}

/** A task changes ticket data shown elsewhere (open-task count), so both caches refresh. */
function useInvalidateWork() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: taskKeys.all });
    await queryClient.invalidateQueries({ queryKey: ticketKeys.all });
  };
}

export interface NewTaskInput {
  title: string;
  description: string | null;
  teamId: string;
  assigneeId: string | null;
  priority: Priority;
  dueDate: string | null;
  ticketNumber: string | null;
}

export function useCreateTask() {
  const invalidate = useInvalidateWork();
  return useMutation({ mutationFn: (body: NewTaskInput) => unwrap(api.POST('/api/v1/tasks', { body })), onSuccess: invalidate });
}

/** One task action. Every body carries the task's version (optimistic concurrency). */
export type TaskAction =
  | { kind: 'claim' }
  | { kind: 'start' }
  | { kind: 'resume' }
  | { kind: 'reopen' }
  | { kind: 'hold'; reason: HoldReason; note: string | null }
  | { kind: 'complete'; resultNote: string }
  | { kind: 'cancel'; reason: string }
  | { kind: 'assign'; assigneeId: string | null }
  | { kind: 'transfer'; teamId: string; assigneeId: string | null; reason: TransferReason; note: string | null };
export type TaskActionKind = TaskAction['kind'];

function runAction(task: TaskDetail, action: TaskAction) {
  const path = { params: { path: { number: task.number } } };
  const version = task.version;
  switch (action.kind) {
    case 'claim':
      return api.POST('/api/v1/tasks/{number}/claim', { ...path, body: { version } });
    case 'start':
      return api.POST('/api/v1/tasks/{number}/start', { ...path, body: { version } });
    case 'resume':
      return api.POST('/api/v1/tasks/{number}/resume', { ...path, body: { version } });
    case 'reopen':
      return api.POST('/api/v1/tasks/{number}/reopen', { ...path, body: { version } });
    case 'hold':
      return api.POST('/api/v1/tasks/{number}/hold', { ...path, body: { version, reason: action.reason, note: action.note } });
    case 'complete':
      return api.POST('/api/v1/tasks/{number}/complete', { ...path, body: { version, resultNote: action.resultNote } });
    case 'cancel':
      return api.POST('/api/v1/tasks/{number}/cancel', { ...path, body: { version, reason: action.reason } });
    case 'assign':
      return api.POST('/api/v1/tasks/{number}/assign', { ...path, body: { version, assigneeId: action.assigneeId } });
    case 'transfer':
      return api.POST('/api/v1/tasks/{number}/transfer', {
        ...path,
        body: { version, teamId: action.teamId, assigneeId: action.assigneeId, reason: action.reason, note: action.note },
      });
  }
}

export function useTaskAction(task: TaskDetail) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateWork();
  return useMutation({
    mutationFn: (action: TaskAction) => unwrap(runAction(task, action)),
    // The answer carries the new version: store it at once, so the next action does not send the old one and get 409.
    onSuccess: async (updated) => {
      queryClient.setQueryData(taskKeys.detail(updated.number), updated);
      await invalidate();
    },
  });
}
