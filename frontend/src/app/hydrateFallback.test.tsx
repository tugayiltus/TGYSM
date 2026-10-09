import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ADMIN_ME, mockApi } from '../test/mockApi';
import { renderRoute } from '../test/renderRoute';
import { INCIDENTS, REFERENCE_ROUTES } from '../test/reportData';

describe('first load of a lazy route (UX-16)', () => {
  afterEach(() => vi.restoreAllMocks());

  it('logs no HydrateFallback warning', async () => {
    const warn = vi.spyOn(console, 'warn');
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME }, 'GET /api/v1/reports/incidents': { status: 200, body: INCIDENTS }, ...REFERENCE_ROUTES });
    renderRoute('/reports/incidents');
    await screen.findByRole('heading', { level: 1 });
    expect(warn.mock.calls.flat().join(' ')).not.toMatch(/HydrateFallback/);
  });
});
