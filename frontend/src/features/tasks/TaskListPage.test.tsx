import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
import { TASK, TASK_ROW, taskRoutes } from '../../test/taskData';

const PAGE = { items: [TASK_ROW], page: 1, pageSize: 20, totalCount: 1 };

describe('TaskListPage', () => {
  it('lists tasks and keeps the queue and status filters in the URL', async () => {
    const api = mockApi(taskRoutes(TASK, { 'GET /api/v1/tasks': { status: 200, body: PAGE } }) as never);
    const { user, router } = renderRoute('/tasks?queue=team');

    expect(await screen.findAllByText(TASK.title)).not.toHaveLength(0);
    await chooseOption(user, 'Durum', 'İşlemde');

    await waitFor(() => expect(router.state.location.search).toContain('status=in_progress'));
    expect(router.state.location.search).toContain('queue=team');
    await waitFor(() =>
      expect(api.calls.some((c) => new URL(c.url).searchParams.get('status') === 'in_progress' && new URL(c.url).searchParams.get('queue') === 'team')).toBe(true),
    );
  });

  it('opens a new task for a team with an optional assignee', async () => {
    let posted: unknown;
    mockApi(
      taskRoutes(TASK, {
        'GET /api/v1/tasks': { status: 200, body: PAGE },
        'POST /api/v1/tasks': async (request: Request) => {
          posted = await request.json();
          return { status: 201, body: TASK };
        },
      }) as never,
    );
    const { user, router } = renderRoute('/tasks');
    await user.click(await screen.findByRole('button', { name: 'Yeni görev' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Görevi aç' }));
    expect((await within(dialog).findAllByText('Bu alan zorunludur.')).length).toBeGreaterThan(0);

    await user.type(within(dialog).getByLabelText('Başlık'), 'Gece yedeği al');
    await chooseOption(user, 'Ekip', 'Saha Destek', dialog);
    await chooseOption(user, 'Atanan', 'Deniz Yıldız', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Görevi aç' }));

    await waitFor(() =>
      expect(posted).toEqual({ title: 'Gece yedeği al', description: null, teamId: 'team-2', assigneeId: 'u-2', priority: 'p3', dueDate: null, ticketNumber: null }),
    );
    await waitFor(() => expect(router.state.location.pathname).toBe(`/tasks/${TASK.number}`));
  });
});
