import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const job = (name: string, stale: boolean, lastError: string | null = null) => ({
  job: name,
  instance: 'host:1',
  lastRunAt: '2026-09-27T09:00:00Z',
  lastError,
  intervalSeconds: 30,
  stale,
});

function setup(status: string, jobs: unknown[]) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/admin/system/workers': { status: 200, body: { status, jobs } },
  });
}

describe('SystemPage', () => {
  it('marks stale jobs and the degraded overall status', async () => {
    setup('degraded', [job('OutboxDispatcher', false), job('EmailSender', true, 'SMTP kapalı')]);
    renderRoute('/admin/system');

    expect(await screen.findByText('Bazı işler zamanında çalışmadı.')).toBeInTheDocument();
    expect(screen.getAllByText('Gecikmiş')).not.toHaveLength(0);
    expect(screen.getAllByText('Zamanında')).not.toHaveLength(0);
    expect(screen.getAllByText('SMTP kapalı')).not.toHaveLength(0);
  });

  it('shows ok when nothing is stale', async () => {
    setup('ok', [job('OutboxDispatcher', false)]);
    renderRoute('/admin/system');

    expect(await screen.findByText('Tüm işler zamanında çalışıyor.')).toBeInTheDocument();
    expect(screen.queryByText('Gecikmiş')).not.toBeInTheDocument();
  });

  it('says so when no job has reported yet', async () => {
    setup('ok', []);
    renderRoute('/admin/system');

    expect(await screen.findByText('Henüz çalışan iş kaydı yok.')).toBeInTheDocument();
  });
});
