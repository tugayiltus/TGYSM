import { describe, expect, it } from 'vitest';

import { api, resetCsrfToken, unwrap } from '../shared/api/client';
import { mockApi } from '../test/mockApi';

import { createQueryClient } from './queryClient';

describe('createQueryClient', () => {
  it('forgets the antiforgery token when the session ends, so the next login is not rejected', async () => {
    resetCsrfToken();
    let csrfCalls = 0;
    const mock = mockApi({
      'POST /api/v1/auth/logout': { status: 204 },
      'GET /api/v1/admin/users': { status: 401, body: { code: 'auth.unauthenticated' } },
    });
    mock.set('GET /api/v1/auth/csrf', () => {
      csrfCalls += 1;
      return { status: 200, body: { token: `token-${csrfCalls}` } };
    });
    const queryClient = createQueryClient({ retry: false });

    await unwrap(api.POST('/api/v1/auth/logout'));
    await queryClient
      .fetchQuery({ queryKey: ['admin', 'users'], queryFn: () => unwrap(api.GET('/api/v1/admin/users')) })
      .catch(() => undefined);
    await unwrap(api.POST('/api/v1/auth/logout'));

    expect(csrfCalls).toBe(2);
  });

  it('drops cached admin data when the session ends', async () => {
    mockApi({ 'GET /api/v1/admin/users': { status: 401, body: { code: 'auth.unauthenticated' } } });
    const queryClient = createQueryClient({ retry: false });
    queryClient.setQueryData(['admin', 'departments', 'list'], { items: ['cached'] });

    await queryClient
      .fetchQuery({ queryKey: ['admin', 'users'], queryFn: () => unwrap(api.GET('/api/v1/admin/users')) })
      .catch(() => undefined);

    expect(queryClient.getQueryData(['admin', 'departments', 'list'])).toBeUndefined();
  });
});
