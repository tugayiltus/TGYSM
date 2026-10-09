import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';

const ROW = {
  row: 2, assetTag: 'DT-1', type: 'laptop', brand: 'Lenovo', model: 'T14', serialNumber: null, locationId: 'l-1', locationPath: 'Merkez Kampüs',
  departmentId: null, departmentName: null, status: 'in_stock', warrantyEndsOn: null, purchasedOn: null, notes: null,
};
const JOB = { jobId: 'j-1', fileName: 'cihazlar.csv', status: 'previewed', totalRows: 1, validRows: 1, errorRows: 0, rows: [ROW], errors: [], createdAt: '2026-09-26T10:00:00Z', committedAt: null };
const FAILED_JOB = { ...JOB, totalRows: 2, errorRows: 1, errors: [{ row: 3, field: 'brand', code: 'validation.required' }, { row: 3, field: 'location', code: 'imports.location_ambiguous' }] };

const csvFile = () => new File(['Demirbaş No,Tür\n'], 'cihazlar.csv', { type: 'text/csv' });

describe('AssetImportPage', () => {
  it('shows rows without a tag as getting one automatically', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'POST /api/v1/assets/import/preview': { status: 200, body: { ...JOB, rows: [{ ...ROW, assetTag: null }] } },
    });
    const { user } = renderRoute('/assets/import');
    await user.upload(await screen.findByLabelText('CSV dosyası'), csvFile());
    await user.click(screen.getByRole('button', { name: 'Önizle' }));
    expect(await screen.findByText('Otomatik')).toBeInTheDocument();
  });

  it('previews a file and lists row errors without allowing the commit', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'POST /api/v1/assets/import/preview': { status: 200, body: FAILED_JOB },
    });
    const { user } = renderRoute('/assets/import');

    expect(await screen.findByRole('link', { name: 'Şablonu indir' })).toHaveAttribute('href', '/api/v1/assets/import/template');
    await user.upload(screen.getByLabelText('CSV dosyası'), csvFile());
    await user.click(screen.getByRole('button', { name: 'Önizle' }));

    const errors = await screen.findByRole('table', { name: 'Hatalı satırlar' });
    expect(within(errors).getAllByText('3')).toHaveLength(2);
    expect(within(errors).getByText('Marka')).toBeInTheDocument();
    expect(within(errors).getByText('Bu alan zorunludur.')).toBeInTheDocument();
    expect(within(errors).getByText(/Bu adla birden fazla konum var/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'İçe aktar' })).toBeDisabled();
  });

  it('commits a clean preview and reports the result', async () => {
    const api = mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'POST /api/v1/assets/import/preview': { status: 200, body: JOB },
      'POST /api/v1/assets/import/j-1/commit': { status: 200, body: { ...JOB, status: 'committed', committedAt: '2026-09-26T10:01:00Z' } },
    });
    const { user } = renderRoute('/assets/import');

    await user.upload(await screen.findByLabelText('CSV dosyası'), csvFile());
    await user.click(screen.getByRole('button', { name: 'Önizle' }));
    await user.click(await screen.findByRole('button', { name: 'İçe aktar' }));

    expect(await screen.findByText('1 cihaz envantere eklendi.')).toBeInTheDocument();
    const preview = api.calls.find((c) => c.url.endsWith('/preview'));
    expect(preview?.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/);
  });

  it('shows a file-level error such as a wrong header', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'POST /api/v1/assets/import/preview': { status: 400, body: { code: 'validation.failed', errors: { file: ['imports.invalid_header'] } } },
    });
    const { user } = renderRoute('/assets/import');

    await user.upload(await screen.findByLabelText('CSV dosyası'), csvFile());
    await user.click(screen.getByRole('button', { name: 'Önizle' }));

    expect(await screen.findByText(/başlık satırı şablonla uyuşmuyor/)).toBeInTheDocument();
  });
});

describe('AssetImportPage after choosing another file', () => {
  it('drops the previous preview so the old job cannot be committed', async () => {
    mockApi({
      'GET /api/v1/auth/me': { status: 200, body: IT_ME },
      'POST /api/v1/assets/import/preview': { status: 200, body: JOB },
    });
    const { user } = renderRoute('/assets/import');

    await user.upload(await screen.findByLabelText('CSV dosyası'), csvFile());
    await user.click(screen.getByRole('button', { name: 'Önizle' }));
    expect(await screen.findByRole('button', { name: 'İçe aktar' })).toBeInTheDocument();
    await user.upload(screen.getByLabelText('CSV dosyası'), new File(['x'], 'yeni.csv', { type: 'text/csv' }));

    expect(screen.queryByRole('button', { name: 'İçe aktar' })).not.toBeInTheDocument();
  });
});
