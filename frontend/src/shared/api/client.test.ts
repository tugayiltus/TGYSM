import { describe, expect, it } from 'vitest';

import { mockApi } from '../../test/mockApi';

import { api, CSRF_HEADER, resetCsrfToken, unwrap } from './client';
import { ApiError } from './errors';

describe('api client', () => {
  it('sends the antiforgery header on unsafe requests only', async () => {
    resetCsrfToken();
    const mock = mockApi({
      'GET /api/v1/auth/me': { status: 401, body: { code: 'auth.unauthenticated' } },
      'POST /api/v1/auth/logout': { status: 204 },
    });

    await expect(unwrap(api.GET('/api/v1/auth/me'))).rejects.toBeInstanceOf(ApiError);
    await unwrap(api.POST('/api/v1/auth/logout'));

    const get = mock.calls.find((r) => r.method === 'GET' && r.url.endsWith('/auth/me'));
    const post = mock.calls.find((r) => r.method === 'POST');
    expect(get?.headers.get(CSRF_HEADER)).toBeNull();
    expect(post?.headers.get(CSRF_HEADER)).toBe('test-csrf');
  });

  it('forgets the token after a csrf rejection so the next call fetches a new one', async () => {
    resetCsrfToken();
    let csrfCalls = 0;
    const mock = mockApi({ 'POST /api/v1/auth/logout': { status: 403, body: { code: 'auth.csrf_invalid' } } });
    mock.set('GET /api/v1/auth/csrf', () => {
      csrfCalls += 1;
      return { status: 200, body: { token: `token-${csrfCalls}` } };
    });

    await expect(unwrap(api.POST('/api/v1/auth/logout'))).rejects.toMatchObject({ code: 'auth.csrf_invalid' });
    await expect(unwrap(api.POST('/api/v1/auth/logout'))).rejects.toBeInstanceOf(ApiError);

    expect(csrfCalls).toBe(2);
  });
});
