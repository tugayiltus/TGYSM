import type { TaskDetail, TaskListItem } from '../features/tasks/api';

import { IT_ME } from './mockApi';

export const TASK: TaskDetail = {
  id: 'task-1',
  number: 'TSK-2026-000001',
  title: 'Gece yedeği al',
  description: 'Veritabanı yedeği',
  status: 'open',
  holdReason: null,
  holdNote: null,
  priority: 'p2',
  dueDate: '2026-12-01',
  isOverdue: false,
  teamId: 'team-2',
  teamName: 'Saha Destek',
  assigneeId: null,
  assigneeName: null,
  createdById: 'u-9',
  createdByName: 'Can Demir',
  link: null,
  resultNote: null,
  cancelReason: null,
  createdAt: '2026-11-13T08:00:00Z',
  updatedAt: '2026-11-13T08:00:00Z',
  closedAt: null,
  canWork: true,
  version: 7,
};

export const TASK_ROW: TaskListItem = {
  id: TASK.id,
  number: TASK.number,
  title: TASK.title,
  status: 'open',
  priority: 'p2',
  dueDate: TASK.dueDate,
  isOverdue: false,
  teamId: TASK.teamId,
  teamName: TASK.teamName,
  assigneeName: null,
  updatedAt: TASK.updatedAt,
};

export const TEAMS = [
  { id: 'team-1', name: 'Servis Masası', members: [{ id: IT_ME.id, fullName: 'Can Demir' }] },
  { id: 'team-2', name: 'Saha Destek', members: [{ id: 'u-2', fullName: 'Deniz Yıldız' }] },
];

export function taskRoutes(task: TaskDetail = TASK, extra: Record<string, unknown> = {}) {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    [`GET /api/v1/tasks/${task.number}`]: { status: 200, body: task },
    'GET /api/v1/reference/teams': { status: 200, body: TEAMS },
    ...extra,
  };
}
