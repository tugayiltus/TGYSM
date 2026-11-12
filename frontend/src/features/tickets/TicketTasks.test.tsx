import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';
import { TASK, TEAMS } from '../../test/taskData';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

const WITH_TASKS = {
  ...INCIDENT,
  canWork: true,
  openTaskCount: 1,
  tasks: [{ number: 'TSK-2026-000001', title: 'Gece yedeği al', status: 'open' as const, teamName: 'Saha Destek', assigneeName: null }],
};

function routes(ticket = WITH_TASKS, extra: Record<string, unknown> = {}) {
  return { 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes(ticket), 'GET /api/v1/reference/teams': { status: 200, body: TEAMS }, ...extra };
}

describe('Ticket Görevler section', () => {
  it('lists the ticket tasks with a link to each', async () => {
    mockApi(routes() as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    const section = await screen.findByRole('region', { name: 'Görevler' });
    expect(within(section).getByRole('link', { name: /TSK-2026-000001/ })).toHaveAttribute('href', '/tasks/TSK-2026-000001');
    expect(within(section).getByText('Saha Destek')).toBeInTheDocument();
  });

  it('opens a task linked to the ticket from Görev ekle', async () => {
    let posted: { ticketNumber?: string } | undefined;
    mockApi(
      routes(WITH_TASKS, {
        'POST /api/v1/tasks': async (request: Request) => {
          posted = (await request.json()) as { ticketNumber?: string };
          return { status: 201, body: TASK };
        },
        [`GET /api/v1/tasks/${TASK.number}`]: { status: 200, body: TASK },
      }) as never,
    );
    const { user, router } = renderRoute(`/tickets/${INCIDENT.number}`);
    await user.click(await screen.findByRole('button', { name: 'Görev ekle' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Başlık'), 'Yedek al');
    await chooseOption(user, 'Ekip', 'Saha Destek', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Görevi aç' }));

    await waitFor(() => expect(posted?.ticketNumber).toBe(INCIDENT.number));
    await waitFor(() => expect(router.state.location.pathname).toBe(`/tasks/${TASK.number}`));
  });

  it('shows no section to the requester', async () => {
    mockApi({ ...routes({ ...WITH_TASKS, canWork: false, openTaskCount: 0, tasks: [] }) } as never);
    renderRoute(`/tickets/${INCIDENT.number}`);

    expect(await screen.findByRole('heading', { name: INCIDENT.subject })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Görev ekle' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Görevler' })).not.toBeInTheDocument();
  });

  it('warns about open tasks when completing and still completes', async () => {
    let posted = false;
    mockApi(
      routes(WITH_TASKS, {
        [`POST /api/v1/tickets/${INCIDENT.number}/complete`]: () => {
          posted = true;
          return { status: 200, body: { ...WITH_TASKS, status: 'completed' } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);
    await user.click(await screen.findByRole('button', { name: 'Tamamla' }));
    const dialog = await screen.findByRole('dialog');

    expect(within(dialog).getByText(/1 açık görev var/)).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Çözüm (kullanıcı görür)'), 'Toner değişti');
    await chooseOption(user, 'Sonuç', 'Çözüldü', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(posted).toBe(true));
  });
});
