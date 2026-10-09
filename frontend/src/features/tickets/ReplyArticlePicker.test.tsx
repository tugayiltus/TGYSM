import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ARTICLE_ID, kbRoutes, LIST_ARTICLE } from '../../test/kbData';
import { IT_ME, mockApi } from '../../test/mockApi';
import { renderRoute } from '../../test/renderRoute';
import { detailRoutes, INCIDENT } from '../../test/ticketData';

const base = (extra = {}) =>
  mockApi({
    'GET /api/v1/auth/me': { status: 200, body: IT_ME },
    ...detailRoutes({ ...INCIDENT, canWork: true }),
    ...kbRoutes([LIST_ARTICLE]),
    [`GET /api/v1/tickets/${INCIDENT.number}/articles`]: { status: 200, body: [] },
    ...extra,
  } as never);

describe('reply article picker', () => {
  it('asks only for published, employee-visible articles', async () => {
    const api = base();
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.click(await screen.findByRole('button', { name: 'Makale ekle' }));
    await screen.findByRole('option', { name: LIST_ARTICLE.title });

    const calls = api.calls.filter((c) => c.method === 'GET' && new URL(c.url).pathname === '/api/v1/kb/articles');
    expect(calls.length).toBeGreaterThan(0);
    for (const call of calls) {
      const query = new URL(call.url).searchParams;
      expect(query.get('status')).toBe('published');
      expect(query.get('visibility')).toBe('employees');
    }
  });

  it('inserts the article link into the reply', async () => {
    base();
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.type(await screen.findByLabelText('Mesajınız'), 'Şunu deneyin:');
    await user.click(screen.getByRole('button', { name: 'Makale ekle' }));
    await user.selectOptions(await screen.findByLabelText('Makale'), LIST_ARTICLE.title);
    await user.click(screen.getByRole('button', { name: 'Ekle' }));

    await waitFor(() => expect(screen.getByLabelText('Mesajınız')).toHaveValue(`Şunu deneyin:\n${LIST_ARTICLE.title}: /kb/${ARTICLE_ID}`));
  });

  it('never sends the reply when Enter is pressed in the article search', async () => {
    const api = base({
      [`POST /api/v1/tickets/${INCIDENT.number}/comments`]: { status: 201, body: {} },
    });
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.type(await screen.findByLabelText('Mesajınız'), 'Taslak yanıt');
    await user.click(screen.getByRole('button', { name: 'Makale ekle' }));
    await user.type(await screen.findByRole('searchbox', { name: 'Makale ara' }), 'yazıcı{Enter}');

    expect(api.calls.filter((c) => c.method === 'POST')).toHaveLength(0);
    expect(screen.getByLabelText('Mesajınız')).toHaveValue('Taslak yanıt');
    expect(screen.getByRole('searchbox', { name: 'Makale ara' })).toHaveValue('yazıcı');
  });

  it('shows the refusal when the server rejects an internal article in a public reply', async () => {
    base({
      [`POST /api/v1/tickets/${INCIDENT.number}/comments`]: { status: 409, body: { code: 'knowledge.internal_article_in_public_reply' } },
    });
    const { user } = renderRoute(`/tickets/${INCIDENT.number}`);

    await user.type(await screen.findByLabelText('Mesajınız'), `Bakın /kb/${ARTICLE_ID}`);
    await user.click(screen.getByRole('button', { name: 'Gönder' }));

    expect(await screen.findByText(/Yalnızca IT personeline açık bir makale/)).toBeInTheDocument();
  });
});
