import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { components } from '../../shared/api/schema';
import { CHANGE, CHANGE_SUMMARY, changeRoutes } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';

type LinkedTicketItem = components['schemas']['LinkedTicketItem'];

const PATH = `/changes/${CHANGE.number}`;
const BASE = `/api/v1/changes/${CHANGE.number}`;
const LINKS_KEY = `GET ${BASE}/tickets`;

const PROMPTED: LinkedTicketItem = {
  number: 'INC-2026-000007',
  title: 'Ağ kesintisi',
  kind: 'incident',
  status: 'in_progress',
  teamName: 'Ağ Ekibi',
  linkKind: 'prompted',
  linkedAt: '2026-11-13T08:00:00Z',
  linkedByName: 'Can Demir',
  canOpen: true,
};
const CAUSED: LinkedTicketItem = { ...PROMPTED, number: 'INC-2026-000008', title: 'Yama sonrası yavaşlık', linkKind: 'caused' };
const HIDDEN: LinkedTicketItem = { ...PROMPTED, number: 'INC-2026-000009', title: 'Gizli kayıt', teamName: 'Güvenlik', canOpen: false };

const panel = () => screen.findByRole('region', { name: 'İlgili kayıtlar' });

describe('Change ilgili kayıtlar panel', () => {
  it('lists the two groups with kind labels and links', async () => {
    mockApi(changeRoutes(CHANGE, { [LINKS_KEY]: { status: 200, body: [PROMPTED, CAUSED] } }) as never);
    renderRoute(PATH);

    const region = await panel();
    expect(await within(region).findByText('Bu değişikliğe yol açan kayıtlar')).toBeInTheDocument();
    expect(within(region).getByText('Bu değişikliğin yol açtığı olaylar')).toBeInTheDocument();
    expect(within(region).getByRole('link', { name: /INC-2026-000007/ })).toHaveAttribute('href', '/tickets/INC-2026-000007');
    expect(within(region).getByText(/Değişikliğe yol açtı/)).toBeInTheDocument();
    expect(within(region).getByText(/Değişiklik sonucu oluştu/)).toBeInTheDocument();
  });

  it('shows a row the viewer cannot open as text with the team name', async () => {
    mockApi(changeRoutes(CHANGE, { [LINKS_KEY]: { status: 200, body: [HIDDEN] } }) as never);
    renderRoute(PATH);

    const region = await panel();
    expect(await within(region).findByText(/INC-2026-000009/)).toBeInTheDocument();
    expect(within(region).queryByRole('link', { name: /INC-2026-000009/ })).not.toBeInTheDocument();
    expect(within(region).getByText(/Güvenlik ekibinin kaydı/)).toBeInTheDocument();
    expect(within(region).getByText(/Değişikliğe yol açtı/)).toBeInTheDocument();
    expect(within(region).getByText('İşlemde')).toBeInTheDocument();
  });

  it('binds a record with its number and kind', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(CHANGE, {
        [LINKS_KEY]: { status: 200, body: [] },
        [`POST ${BASE}/tickets`]: async (request: Request) => {
          posted = await request.json();
          return { status: 201, body: [PROMPTED] };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);
    await user.click(await within(await panel()).findByRole('button', { name: 'Kayıt bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Kayıt numarası'), 'INC-2026-000007');
    await chooseOption(user, 'Bağlantı türü', 'Değişikliğe yol açtı', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Bağla' }));

    await waitFor(() => expect(posted).toEqual({ ticketNumber: 'INC-2026-000007', kind: 'prompted' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows the Turkish error when the pair is already linked', async () => {
    mockApi(
      changeRoutes(CHANGE, {
        [LINKS_KEY]: { status: 200, body: [] },
        [`POST ${BASE}/tickets`]: { status: 409, body: { title: 'x', status: 409, code: 'changes.ticket_link_exists' } },
      }) as never,
    );
    const { user } = renderRoute(PATH);
    await user.click(await within(await panel()).findByRole('button', { name: 'Kayıt bağla' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText('Kayıt numarası'), 'INC-2026-000007');
    await chooseOption(user, 'Bağlantı türü', 'Değişikliğe yol açtı', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Bağla' }));

    expect(await within(dialog).findByText('Bu kayıt zaten bu değişikliğe bağlı.')).toBeInTheDocument();
  });

  it('asks for confirmation before it removes a link, then refreshes', async () => {
    let deleted = false;
    let listed = [PROMPTED];
    mockApi(
      changeRoutes(CHANGE, {
        [LINKS_KEY]: () => ({ status: 200, body: listed }),
        [`DELETE ${BASE}/tickets/${PROMPTED.number}`]: () => {
          deleted = true;
          listed = [];
          return { status: 200, body: [] };
        },
      }) as never,
    );
    const { user } = renderRoute(PATH);
    const region = await panel();
    await user.click(await within(region).findByRole('button', { name: /Kaldır/ }));
    expect(deleted).toBe(false);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Kaldır' }));

    await waitFor(() => expect(deleted).toBe(true));
    await waitFor(() => expect(within(region).queryByRole('link', { name: /INC-2026-000007/ })).not.toBeInTheDocument());
  });

  it('hides the bind and remove actions in the summary view', async () => {
    mockApi(
      changeRoutes(CHANGE, {
        [`GET ${BASE}`]: { status: 200, body: { access: 'summary', detail: null, summary: CHANGE_SUMMARY } },
        [LINKS_KEY]: { status: 200, body: [PROMPTED] },
      }) as never,
    );
    renderRoute(PATH);

    const region = await panel();
    expect(await within(region).findByRole('link', { name: /INC-2026-000007/ })).toBeInTheDocument();
    expect(within(region).queryByRole('button', { name: 'Kayıt bağla' })).not.toBeInTheDocument();
    expect(within(region).queryByRole('button', { name: /Kaldır/ })).not.toBeInTheDocument();
  });

  it('hides the bind action when the viewer cannot work on the change', async () => {
    mockApi(changeRoutes({ ...CHANGE, canWork: false }, { [LINKS_KEY]: { status: 200, body: [] } }) as never);
    renderRoute(PATH);

    const region = await panel();
    expect(within(region).queryByRole('button', { name: 'Kayıt bağla' })).not.toBeInTheDocument();
  });
});
