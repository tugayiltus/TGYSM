import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ADMIN_ME, mockApi, type MockRoutes } from '../../../test/mockApi';
import { renderRoute } from '../../../test/renderRoute';

const TEMPLATE = {
  id: 't-1',
  eventType: 'ticket_created',
  subject: 'Kayıt {{ticketNumber}} oluşturuldu',
  body: 'Merhaba {{recipientName}}',
  updatedBy: 'u-1',
  updatedAt: '2026-09-27T09:00:00Z',
  version: 3,
  allowedVariables: ['recipientName', 'status', 'subject', 'ticketNumber', 'ticketUrl'],
};

function setup(extra: MockRoutes = {}) {
  return mockApi({
    'GET /api/v1/auth/me': { status: 200, body: ADMIN_ME },
    'GET /api/v1/admin/email-templates': { status: 200, body: [TEMPLATE] },
    ...extra,
  });
}

describe('EmailTemplatesPage', () => {
  it('lists templates by their Turkish event name', async () => {
    setup();
    renderRoute('/admin/email-templates');

    expect((await screen.findAllByText('Kayıt oluşturuldu')).length).toBeGreaterThan(0);
  });

  it('lists the allowed variables of the event in the editor', async () => {
    setup();
    const { user } = renderRoute('/admin/email-templates');

    await user.click((await screen.findAllByRole('button', { name: /^Düzenle/ }))[0]!);
    const dialog = await screen.findByRole('dialog');

    const items = within(within(dialog).getByRole('region', { name: 'Kullanılabilir değişkenler' })).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual(['{{recipientName}}', '{{status}}', '{{subject}}', '{{ticketNumber}}', '{{ticketUrl}}']);
  });

  it('saves with the template version', async () => {
    let put: unknown;
    setup({
      'PUT /api/v1/admin/email-templates/ticket_created': async (request) => {
        put = await request.json();
        return { status: 200, body: { ...TEMPLATE, subject: 'Yeni konu', version: 4 } };
      },
    });
    const { user } = renderRoute('/admin/email-templates');

    await user.click((await screen.findAllByRole('button', { name: /^Düzenle/ }))[0]!);
    const dialog = await screen.findByRole('dialog');
    const subject = within(dialog).getByLabelText('Konu');
    await user.clear(subject);
    await user.type(subject, 'Yeni konu');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await screen.findAllByText(/Yeni konu|Kayıt oluşturuldu/)).not.toHaveLength(0);
    expect(put).toEqual({ version: 3, subject: 'Yeni konu', body: 'Merhaba {{recipientName}}' });
  });

  it('shows the server field error next to the field', async () => {
    setup({
      'PUT /api/v1/admin/email-templates/ticket_created': {
        status: 400,
        body: { code: 'validation.failed', errors: { body: ['email_templates.variable_not_allowed'] } },
      },
    });
    const { user } = renderRoute('/admin/email-templates');

    await user.click((await screen.findAllByRole('button', { name: /^Düzenle/ }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await within(dialog).findByText('Bu değişken bu bildirim türü için kullanılamaz.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('İçerik')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows an unknown-variable error on the subject', async () => {
    setup({
      'PUT /api/v1/admin/email-templates/ticket_created': {
        status: 400,
        body: { code: 'validation.failed', errors: { subject: ['email_templates.unknown_variable'] } },
      },
    });
    const { user } = renderRoute('/admin/email-templates');

    await user.click((await screen.findAllByRole('button', { name: /^Düzenle/ }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await within(dialog).findByText('Bu değişken tanımlı değil.')).toBeInTheDocument();
  });

  it('shows a concurrency conflict in the form alert', async () => {
    setup({ 'PUT /api/v1/admin/email-templates/ticket_created': { status: 409, body: { code: 'concurrency.conflict' } } });
    const { user } = renderRoute('/admin/email-templates');

    await user.click((await screen.findAllByRole('button', { name: /^Düzenle/ }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('başka biri tarafından değiştirildi');
  });
});
