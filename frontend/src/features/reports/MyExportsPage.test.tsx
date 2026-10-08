import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { exportJob, pagedJobs } from '../../test/reportData';

const READY_ID = '00000000-0000-7000-8000-0000000000e1';

const JOBS = [
  exportJob(READY_ID, 'ready', 1_234),
  exportJob('00000000-0000-7000-8000-0000000000e2', 'expired', 10),
  exportJob('00000000-0000-7000-8000-0000000000e3', 'failed'),
  exportJob('00000000-0000-7000-8000-0000000000e4', 'pending'),
  exportJob('00000000-0000-7000-8000-0000000000e5', 'running'),
];

const rowOf = (table: HTMLElement, status: string) => within(table).getByText(status).closest('tr') as HTMLElement;

describe('MyExportsPage', () => {
  it('shows each job state and a download link only for ready files', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/reports/exports': { status: 200, body: pagedJobs(JOBS) } });
    renderRoute('/reports/exports');

    expect(await screen.findByRole('heading', { name: 'Dışa aktarımlarım' })).toBeInTheDocument();
    const table = await screen.findByRole('table');
    for (const status of ['Hazır', 'Süresi doldu', 'Başarısız', 'Sırada', 'Hazırlanıyor']) {
      expect(rowOf(table, status)).toBeInTheDocument();
    }

    const downloads = within(table).getAllByRole('link', { name: 'İndir' });
    expect(downloads).toHaveLength(1);
    expect(downloads[0]).toHaveAttribute('href', `/api/v1/reports/exports/${READY_ID}/download`);
    expect(within(rowOf(table, 'Hazır')).getByText('1.234')).toBeInTheDocument();
    expect(within(rowOf(table, 'Hazır')).getByText('Olaylar')).toBeInTheDocument();
    expect(within(rowOf(table, 'Hazır')).getByText('Açılan kayıt')).toBeInTheDocument();
    expect(within(rowOf(table, 'Süresi doldu')).queryByRole('link', { name: 'İndir' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Raporlara dön' })).toHaveAttribute('href', '/reports');
  });

  it('says when there are no exports yet', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/reports/exports': { status: 200, body: pagedJobs([]) } });
    renderRoute('/reports/exports');

    expect(await screen.findByText('Henüz bir dışa aktarım yok.')).toBeInTheDocument();
  });

  it('pages through older exports', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/reports/exports': (request) => {
        const page = Number(new URL(request.url).searchParams.get('page'));
        return { status: 200, body: { items: [exportJob(`00000000-0000-7000-8000-00000000000${page}`, 'ready', page)], page, pageSize: 20, totalCount: 21 } };
      },
    });
    const { user } = renderRoute('/reports/exports');

    await user.click(await screen.findByRole('button', { name: 'Sonraki' }));

    expect(await screen.findByText('21–21 / 21 kayıt')).toBeInTheDocument();
    expect(new URL(api.calls.at(-1)?.url ?? '').searchParams.get('page')).toBe('2');
  });

  it('shows the 403 page to an employee', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/reports/exports');

    expect(await screen.findByRole('heading', { name: 'Bu sayfayı görme yetkiniz yok' })).toBeInTheDocument();
  });
});
