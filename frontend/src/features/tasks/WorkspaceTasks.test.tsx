import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { TASK_ROW } from '../../test/taskData';
import { LIST_ITEM } from '../../test/ticketData';

const TASK_COUNTS: Record<string, number> = { mine: 2, team: 6, overdue: 1 };
const WORK_ROWS = [
  { kind: 'task', number: 'TSK-2026-000001', title: 'Gece yedeği al', status: 'open', priority: 'p2', dueDate: '2026-12-01', teamName: 'Saha Destek', assigneeName: 'Can Demir', updatedAt: '2026-11-13T09:00:00Z' },
  { kind: 'incident', number: 'INC-2026-000001', title: 'Yazıcı çalışmıyor', status: 'in_progress', priority: 'p3', dueDate: null, teamName: 'Servis Masası', assigneeName: 'Can Demir', updatedAt: '2026-11-13T08:00:00Z' },
];

function routes() {
  return {
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    'GET /api/v1/tickets': { status: 200, body: { items: [LIST_ITEM], page: 1, pageSize: 1, totalCount: 0 } },
    'GET /api/v1/tasks': (request: Request) => {
      const queue = new URL(request.url).searchParams.get('queue') ?? '';
      return { status: 200, body: { items: [TASK_ROW], page: 1, pageSize: 1, totalCount: TASK_COUNTS[queue] ?? 0 } };
    },
    'GET /api/v1/work-items': { status: 200, body: { items: WORK_ROWS, page: 1, pageSize: 20, totalCount: 2 } },
  };
}

describe('İş listem tasks', () => {
  it('shows task cards with counts and opens the overdue task list', async () => {
    mockApi(routes() as never);
    const { user, router } = renderRoute('/workspace');

    expect(await screen.findByRole('link', { name: /Görevlerim 2 görev/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Ekibimin görevleri 6 görev/ })).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: /Geciken görevler 1 görev/ }));

    await waitFor(() => expect(router.state.location.pathname + router.state.location.search).toBe('/tasks?queue=overdue'));
  });

  it('lists tickets and tasks assigned to me in one list with their kind', async () => {
    mockApi(routes() as never);
    renderRoute('/workspace');

    const list = await screen.findByRole('region', { name: 'Bana atanan işler' });
    expect(await within(list).findAllByText('Gece yedeği al')).not.toHaveLength(0);
    expect(within(list).getAllByText('Görev').length).toBeGreaterThan(0);
    expect(within(list).getAllByText('Arıza').length).toBeGreaterThan(0);
    expect(within(list).getAllByRole('link', { name: /TSK-2026-000001/ })[0]).toHaveAttribute('href', '/tasks/TSK-2026-000001');
    expect(within(list).getAllByRole('link', { name: /INC-2026-000001/ })[0]).toHaveAttribute('href', '/tickets/INC-2026-000001');
  });
});
