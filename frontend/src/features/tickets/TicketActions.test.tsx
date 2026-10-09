import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

const WORKABLE = { ...INCIDENT, canWork: true };
const BASE = `/api/v1/tickets/${INCIDENT.number}`;
const TEAMS = [
  { id: 'team-1', name: 'Servis Masası', members: [{ id: 'u-1', fullName: 'Can Demir' }] },
  { id: 'team-2', name: 'Saha Destek', members: [{ id: 'u-2', fullName: 'Deniz Yıldız' }] },
];

function routes(extra: Record<string, unknown> = {}) {
  return { 'GET /api/v1/auth/me': { status: 200, body: IT_ME }, ...detailRoutes(WORKABLE), 'GET /api/v1/reference/teams': { status: 200, body: TEAMS }, ...extra };
}

describe('TicketActions', () => {
  it('shows the work actions allowed for an open, unassigned ticket and claims it', async () => {
    let posted: unknown;
    mockApi(
      routes({
        [`POST ${BASE}/claim`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...WORKABLE, assigneeId: IT_ME.id, assigneeName: 'Can Demir' } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    for (const name of ['Üstlen', 'Başla', 'Beklet', 'Tamamla', 'Öncelik değiştir', 'Aktar', 'İptal et']) {
      expect(await screen.findByRole('button', { name })).toBeInTheDocument();
    }
    expect(screen.queryByRole('button', { name: 'Devam et' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Üstlen' }));

    await waitFor(() => expect(posted).toEqual({ version: 11 }));
  });

  it('shows the Turkish conflict message when the ticket changed meanwhile', async () => {
    mockApi(routes({ [`POST ${BASE}/start`]: { status: 409, body: { code: 'concurrency.conflict' } } }) as never);
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'Başla' }));

    expect(await screen.findByText(/başka biri tarafından değiştirildi/)).toBeInTheDocument();
  });

  it('requires a resolution and an outcome to complete an incident', async () => {
    let posted: unknown;
    mockApi(
      routes({
        [`POST ${BASE}/complete`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...WORKABLE, status: 'completed' } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'Tamamla' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Çözüm (kullanıcı görür)'), 'Toner değişti');
    await user.selectOptions(within(dialog).getByLabelText('Sonuç'), 'Çözüldü');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 11, resolution: 'Toner değişti', outcome: 'resolved', rootCause: null }));
  });

  it('holds the work actions while the ticket refreshes, so a dialog never starts from an outdated version', async () => {
    let release = () => undefined as void;
    const refreshed = new Promise<void>((resolve) => {
      release = resolve;
    });
    let loads = 0;
    let posted: unknown;
    mockApi(
      routes({
        [`GET ${BASE}`]: async () => {
          loads += 1;
          if (loads === 1) {
            return { status: 200, body: WORKABLE };
          }
          await refreshed;
          return { status: 200, body: { ...WORKABLE, version: 12 } };
        },
        [`POST ${BASE}/complete`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: { ...WORKABLE, status: 'completed' } };
        },
      }) as never,
    );
    const { user, queryClient } = renderRoute(`/tickets/${INCIDENT.number}`);
    const complete = await screen.findByRole('button', { name: 'Tamamla' });

    // A reply or another action invalidates the ticket; its new version is still on the way.
    void queryClient.invalidateQueries({ queryKey: ['tickets'] });
    await waitFor(() => expect(complete).toBeDisabled());
    release();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Tamamla' })).toBeEnabled());

    await user.click(screen.getByRole('button', { name: 'Tamamla' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Çözüm (kullanıcı görür)'), 'Toner değişti');
    await user.selectOptions(within(dialog).getByLabelText('Sonuç'), 'Çözüldü');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(posted).toMatchObject({ version: 12 }));
  });

  it('holds the work actions while a reply is being sent, since the reply changes the ticket version', async () => {
    let release = () => undefined as void;
    const sent = new Promise<void>((resolve) => {
      release = resolve;
    });
    mockApi(
      routes({
        [`POST ${BASE}/comments`]: async () => {
          await sent;
          return { status: 201, body: {} };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);
    const complete = await screen.findByRole('button', { name: 'Tamamla' });
    const box = screen.getByRole('form', { name: 'Yazışmalar' });

    await user.type(within(box).getByLabelText('Mesajınız'), 'Toneri değiştiriyoruz');
    await user.click(within(box).getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(complete).toBeDisabled());
    release();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Tamamla' })).toBeEnabled());
  });

  it('transfers to a member of another team with a reason, requiring a note for "other"', async () => {
    let posted: unknown;
    mockApi(
      routes({
        [`POST ${BASE}/transfer`]: async (request: Request) => {
          posted = await request.json();
          return { status: 200, body: WORKABLE };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'Aktar' }));
    const dialog = await screen.findByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Hedef ekip'), await within(dialog).findByRole('option', { name: 'Saha Destek' }));
    await user.selectOptions(within(dialog).getByLabelText('Atanacak kişi'), 'Deniz Yıldız');
    await user.selectOptions(within(dialog).getByLabelText('Aktarma nedeni'), 'Diğer');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));
    expect(await within(dialog).findByText('Bu alan zorunludur.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText('Not'), 'Yerinde bakılmalı');
    await user.click(within(dialog).getByRole('button', { name: 'Onayla' }));

    await waitFor(() => expect(posted).toEqual({ version: 11, teamId: 'team-2', assigneeId: 'u-2', reason: 'other', note: 'Yerinde bakılmalı' }));
  });

  it('lets people working on the ticket write internal notes', async () => {
    let visibility: FormDataEntryValue | null = null;
    mockApi(
      routes({
        [`POST ${BASE}/comments`]: async (request: Request) => {
          const text = await request.text();
          visibility = /name="visibility"\r\n\r\n(\w+)/.exec(text)?.[1] ?? null;
          return { status: 201, body: {} };
        },
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('tab', { name: 'İç not' }));
    await user.type(screen.getByLabelText('Mesajınız'), 'Tedarikçi arandı');
    await user.click(within(screen.getByRole('form', { name: 'Yazışmalar' })).getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(visibility).toBe('internal'));
  });

  it('locks the reply box while a message is sending so later typing is not lost', async () => {
    let release: () => void = () => undefined;
    mockApi(
      routes({
        [`POST ${BASE}/comments`]: () =>
          new Promise((resolve) => {
            release = () => resolve({ status: 201, body: {} });
          }),
      }) as never,
    );
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    const box = await screen.findByRole('form', { name: 'Yazışmalar' });
    await user.type(within(box).getByLabelText('Mesajınız'), 'Birinci');
    await user.click(within(box).getByRole('button', { name: 'Gönder' }));

    await waitFor(() => expect(within(box).getByLabelText('Mesajınız')).toBeDisabled());
    release();
    await waitFor(() => expect(within(box).getByLabelText('Mesajınız')).toBeEnabled());
    expect(within(box).getByLabelText('Mesajınız')).toHaveValue('');
  });
});
