import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CHANGE, CHANGE_ROW, changeRoutes, full } from '../../test/changeData';
import { mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { chooseOption } from '../../test/select';

const BASE = `/api/v1/changes/${CHANGE.number}`;
const ASSET = {
  id: 'a-2',
  assetTag: 'DMR-000002',
  type: 'laptop',
  modelId: 'm-1',
  brand: 'HP',
  model: 'EliteBook',
  serialNumber: null,
  status: 'assigned',
  locationId: 'l-1',
  locationName: 'Merkez',
};

describe('ChangeFormDialog', () => {
  it('creates a change with the nested body, owner team, owner and assets', async () => {
    let posted: unknown;
    mockApi(
      changeRoutes(CHANGE, {
        'GET /api/v1/changes': { status: 200, body: { items: [CHANGE_ROW], page: 1, pageSize: 20, totalCount: 1 } },
        'GET /api/v1/assets': { status: 200, body: { items: [ASSET], page: 1, pageSize: 20, totalCount: 1 } },
        'POST /api/v1/changes': async (request: Request) => {
          posted = await request.json();
          return { status: 201, body: CHANGE };
        },
      }) as never,
    );
    const { user } = renderRoute('/changes');

    await user.click(await screen.findByRole('button', { name: 'Yeni değişiklik' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Değişikliği oluştur' }));
    expect((await within(dialog).findAllByText('Bu alan zorunludur.')).length).toBeGreaterThan(0);

    await user.type(within(dialog).getByLabelText('Başlık'), 'Anahtar güncelle');
    await chooseOption(user, 'Sahip ekip', 'Saha Destek', dialog);
    await chooseOption(user, 'Sahip', 'Deniz Yıldız', dialog);
    await chooseOption(user, 'Varlık ekle', /DMR-000002/, dialog);
    expect(within(dialog).getByText('DMR-000002 · HP EliteBook')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Değişikliği oluştur' }));

    await waitFor(() =>
      expect(posted).toEqual({
        draft: {
          title: 'Anahtar güncelle',
          justification: null,
          affectedService: null,
          risk: 'medium',
          impact: 'medium',
          windowStart: null,
          windowEnd: null,
          implementationPlan: null,
          verificationPlan: null,
          backoutPlan: null,
        },
        ownerTeamId: 'team-2',
        ownerId: 'u-2',
        assetIds: ['a-2'],
      }),
    );
  });

  it('saves a title change of an approved plan without asking', async () => {
    let put: unknown;
    mockApi(
      changeRoutes({ ...CHANGE, status: 'scheduled' }, {
        [`PUT ${BASE}`]: async (request: Request) => {
          put = await request.json();
          return { status: 200, body: { ...CHANGE, status: 'scheduled', title: 'Yeni başlık', version: 4 } };
        },
      }) as never,
    );
    const { user } = renderRoute(`/changes/${CHANGE.number}`);

    await user.click(await screen.findByRole('button', { name: 'Düzenle' }));
    const dialog = await screen.findByRole('dialog');
    const title = within(dialog).getByLabelText('Başlık');
    await user.clear(title);
    await user.type(title, 'Yeni başlık');
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    await waitFor(() => expect(put).toMatchObject({ version: CHANGE.version, draft: { title: 'Yeni başlık' }, assetIds: ['a-1'] }));
    expect(screen.queryByText(/onayı geçersiz kılar/)).not.toBeInTheDocument();
  });

  it('asks first when an approved plan changes in a field the approval covers', async () => {
    let put: unknown;
    let current = { ...CHANGE, status: 'scheduled' as const };
    mockApi(
      changeRoutes(current, {
        [`GET ${BASE}`]: () => ({ status: 200, body: full(current) }),
        [`PUT ${BASE}`]: async (request: Request) => {
          put = await request.json();
          current = { ...current, risk: 'low', version: 4 };
          return { status: 200, body: current };
        },
      }) as never,
    );
    const { user } = renderRoute(`/changes/${CHANGE.number}`);

    await user.click(await screen.findByRole('button', { name: 'Düzenle' }));
    const dialog = await screen.findByRole('dialog');
    await chooseOption(user, 'Risk', 'Düşük', dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Kaydet' }));

    expect(await screen.findByText(/Bu değişiklik onayı geçersiz kılar/)).toBeInTheDocument();
    expect(put).toBeUndefined();
    await user.click(screen.getByRole('button', { name: 'Yine de kaydet' }));

    await waitFor(() => expect(put).toMatchObject({ version: CHANGE.version, draft: { risk: 'low' } }));
  });

  it('does not offer to edit a change that is being implemented', async () => {
    mockApi(changeRoutes({ ...CHANGE, status: 'implementing' }) as never);
    renderRoute(`/changes/${CHANGE.number}`);

    expect(await screen.findByRole('button', { name: 'Uygulama bitti' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Düzenle' })).not.toBeInTheDocument();
  });
});
