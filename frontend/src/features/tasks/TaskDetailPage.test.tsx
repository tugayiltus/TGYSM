import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { TASK, taskRoutes } from '../../test/taskData';

const BASE = `/api/v1/tasks/${TASK.number}`;

describe('TaskDetailPage', () => {
  it('shows an unassigned open task with Üstlen as the primary action and claims it', async () => {
    let posted: unknown;
    mockApi(
      taskRoutes(TASK, {
        [`POST ${BASE}/claim`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...TASK, assigneeId: 'u-2', assigneeName: 'Deniz Yıldız' } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tasks/${TASK.number}`);

    expect(await screen.findByRole('heading', { name: TASK.title })).toBeInTheDocument();
    expect(screen.getByText('Saha Destek')).toBeInTheDocument();
    expect(screen.getByText('Bağlı kayıt yok')).toBeInTheDocument();
    for (const name of ['Üstlen', 'Başla', 'Beklet', 'Tamamla', 'Ata', 'Aktar', 'İptal et']) {
      expect(screen.getByRole('button', { name })).toBeInTheDocument();
    }
    await user.click(screen.getByRole('button', { name: 'Üstlen' }));
    await waitFor(() => expect(posted).toEqual({ version: 7 }));
  });

  it('requires a result note to complete', async () => {
    let posted: unknown;
    mockApi(
      taskRoutes(TASK, {
        [`POST ${BASE}/complete`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...TASK, status: 'completed', resultNote: 'Yedek alındı' } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tasks/${TASK.number}`);
    await user.click(await screen.findByRole('button', { name: 'Tamamla' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText('Sonuç notu'), 'Yedek alındı');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 7, resultNote: 'Yedek alındı' }));
  });

  it('hides the linked ticket the viewer cannot see', async () => {
    mockApi(taskRoutes({ ...TASK, link: { accessible: false, number: null, kind: null, title: null, status: null } }) as never);
    renderRoute(`/tasks/${TASK.number}`);

    expect(await screen.findByText('Bağlı kayıt: erişiminiz yok')).toBeInTheDocument();
  });

  it('links the visible ticket', async () => {
    mockApi(taskRoutes({ ...TASK, link: { accessible: true, number: 'INC-2026-000001', kind: 'incident', title: 'Yazıcı', status: 'open' } }) as never);
    renderRoute(`/tasks/${TASK.number}`);

    expect(await screen.findByRole('link', { name: /INC-2026-000001/ })).toHaveAttribute('href', '/tickets/INC-2026-000001');
  });

  it('shows the conflict message when the task changed meanwhile', async () => {
    mockApi(taskRoutes(TASK, { [`POST ${BASE}/start`]: { status: 409, body: { code: 'concurrency.conflict' } } }) as never);
    const { user } = renderRoute(`/tasks/${TASK.number}`);
    await user.click(await screen.findByRole('button', { name: 'Başla' }));

    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
  });

  it('offers only Yeniden aç on a completed task and no actions without work access', async () => {
    mockApi(taskRoutes({ ...TASK, status: 'completed', resultNote: 'Bitti', assigneeId: 'u-2' }) as never);
    renderRoute(`/tasks/${TASK.number}`);
    expect(await screen.findByRole('button', { name: 'Yeniden aç' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tamamla' })).not.toBeInTheDocument();
    expect(screen.getByText('Bitti')).toBeInTheDocument();
  });

  it('shows no actions to a viewer without work access', async () => {
    mockApi(taskRoutes({ ...TASK, canWork: false }) as never);
    renderRoute(`/tasks/${TASK.number}`);
    expect(await screen.findByRole('heading', { name: TASK.title })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Üstlen' })).not.toBeInTheDocument();
  });
});
