import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { EMPLOYEE_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

describe('MyAssetsPage', () => {
  it('lists the devices assigned to the signed-in employee', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/me/assets': {
        status: 200,
        body: [{ id: 'a-1', assetTag: 'DT-0001', type: 'laptop', brand: 'Lenovo', model: 'T14', serialNumber: 'SN-1', assignedAt: '2026-09-01T08:00:00Z' }],
      },
    });
    renderRoute('/assets/mine');

    expect(await screen.findByRole('heading', { name: 'Zimmetlerim' })).toBeInTheDocument();
    expect(await screen.findByText('DT-0001')).toBeInTheDocument();
    expect(screen.getByText('Lenovo T14')).toBeInTheDocument();
  });

  it('says so when nothing is assigned', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME },
      'GET /api/v1/me/assets': { status: 200, body: [] },
    });
    renderRoute('/assets/mine');

    expect(await screen.findByText('Üzerinize kayıtlı cihaz yok.')).toBeInTheDocument();
  });

  it('links employees to their devices from the home page', async () => {
    mockApi({ 'GET /api/v1/auth/me': { status: 200, body: EMPLOYEE_ME } });
    renderRoute('/');

    expect(await screen.findByRole('link', { name: /Üzerinize kayıtlı cihazları/ })).toHaveAttribute('href', '/assets/mine');
    expect(screen.queryByRole('link', { name: /Envanter/ })).not.toBeInTheDocument();
  });
});
