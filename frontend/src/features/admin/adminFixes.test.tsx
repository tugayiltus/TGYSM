import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import admin from '../../shared/i18n/locales/tr/admin.json';
import { pageOf } from '../../test/assetData';
import { ADMIN_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const targets = ['incident', 'request'].flatMap((kind) =>
  ['p1', 'p2', 'p3', 'p4'].flatMap((priority) => [
    { kind, priority, requestTypeId: null, metric: 'first_response', targetMinutes: 60, warningPercent: 75 },
    { kind, priority, requestTypeId: null, metric: 'resolution', targetMinutes: 480, warningPercent: 75 },
  ]),
);
const POLICY = { versionId: 'v-1', number: 1, createdAt: '2026-09-27T09:00:00Z', createdBy: 'u-1', targets, pauseRules: [] };
const SLA_ROWS = 8;
const SLA_METRICS = 2;

describe('SLA policy (UX-03)', () => {
  it('shows a visible unit next to every target and warning value, and stacks rows into cards on phones', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/sla/policy': { status: 200, body: POLICY } });
    renderRoute('/admin/sla/policy');
    const table = await screen.findByRole('table');
    expect(within(table).getAllByText('dk')).toHaveLength(SLA_ROWS * SLA_METRICS);
    expect(within(table).getAllByText('%')).toHaveLength(SLA_ROWS * SLA_METRICS);
    expect(table).toHaveClass('tgysm-stack-table');
    expect(within(table).getAllByRole('cell')[0]).toHaveAttribute('data-label');
  });
});

describe('system page (UX-04)', () => {
  it('shows an instance that stopped reporting as such even when its job is on time', async () => {
    const recent = new Date().toISOString();
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
      'GET /api/v1/admin/system/workers': {
        status: 200,
        body: {
          status: 'ok',
          jobs: [
            { job: 'EmailSender', instance: 'new-host:1', lastRunAt: recent, lastError: null, intervalSeconds: 30, stale: false },
            { job: 'EmailSender', instance: 'old-host:1', lastRunAt: '2026-01-01T00:00:00Z', lastError: null, intervalSeconds: 30, stale: false },
          ],
        },
      },
    });
    renderRoute('/admin/system');
    const table = await screen.findByRole('table');
    const oldRow = within(table).getByText('old-host:1').closest('tr') as HTMLElement;
    const newRow = within(table).getByText('new-host:1').closest('tr') as HTMLElement;
    expect(within(oldRow).getByText('Rapor vermiyor')).toBeInTheDocument();
    expect(within(oldRow).queryByText('Güncel')).not.toBeInTheDocument();
    expect(within(newRow).getByText('Güncel')).toBeInTheDocument();
    expect(within(oldRow).getByText('Zamanında')).toBeInTheDocument();
  });
});

describe('audit filter (UX-07)', () => {
  it('offers every audited entity type', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/audit': { status: 200, body: pageOf([]) } });
    renderRoute('/admin/audit');
    const select = await screen.findByLabelText('Kayıt türü');
    const values = within(select).getAllByRole('option').map((option) => (option as HTMLOptionElement).value).filter((value) => value !== '');
    expect(values.sort()).toEqual(Object.keys(admin.entityTypes).sort());
  });
});

describe('teams intake badge (UX-08)', () => {
  const TEAM = { id: 't-1', name: 'Servis Masası', isActive: true, memberCount: 2, isDefaultIntake: true, version: 1 };
  it('shows the intake badge once in the table and once in the phone card', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/admin/teams': { status: 200, body: pageOf([TEAM]) } });
    renderRoute('/admin/teams');
    const table = await screen.findByRole('table');
    expect(within(table).getAllByText('Kayıt alan ekip')).toHaveLength(1);
    // ResponsiveList's phone cards: the list hidden from 768 px.
    const cards = document.querySelector('main ul.d-md-none') as HTMLElement;
    expect(within(cards).getAllByText('Kayıt alan ekip')).toHaveLength(1);
  });
});
