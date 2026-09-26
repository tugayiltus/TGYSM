import { vi } from 'vitest';

export interface MockResponse {
  status: number;
  body?: unknown;
}

export type MockHandler = MockResponse | ((request: Request) => MockResponse | Promise<MockResponse>);

/** Keys are "METHOD /path" without the query string, e.g. "GET /api/v1/auth/me". */
export type MockRoutes = Record<string, MockHandler>;

export interface MockApi {
  calls: Request[];
  set: (key: string, handler: MockHandler) => void;
}

const HTTP_OK = 200;
const HTTP_NO_CONTENT = 204;
const HTTP_BAD_REQUEST = 400;
const HTTP_NOT_FOUND = 404;
const CSRF_ROUTE = 'GET /api/v1/auth/csrf';

export const ADMIN_ME = {
  id: '00000000-0000-7000-8000-000000000001',
  firstName: 'Tugay',
  lastName: 'İltuş',
  email: 'admin@example.com',
  role: 'admin',
  mustChangePassword: false,
  departmentName: null,
  locationName: null,
} as const;

export const EMPLOYEE_ME = {
  ...ADMIN_ME,
  id: '00000000-0000-7000-8000-000000000002',
  firstName: 'Ayşe',
  lastName: 'Kaya',
  email: 'ayse@example.com',
  role: 'employee',
} as const;

function toResponse({ status, body }: MockResponse): Response {
  if (status === HTTP_NO_CONTENT) {
    return new Response(null, { status });
  }
  const contentType = status >= HTTP_BAD_REQUEST ? 'application/problem+json' : 'application/json';
  return new Response(JSON.stringify(body ?? {}), { status, headers: { 'Content-Type': contentType } });
}

export function mockApi(routes: MockRoutes): MockApi {
  const table = new Map<string, MockHandler>([[CSRF_ROUTE, { status: HTTP_OK, body: { token: 'test-csrf' } }], ...Object.entries(routes)]);
  const calls: Request[] = [];

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = input instanceof Request ? input : new Request(new URL(String(input), window.location.origin), init);
      calls.push(request.clone());
      const handler = table.get(`${request.method} ${new URL(request.url).pathname}`);
      if (handler === undefined) {
        return toResponse({ status: HTTP_NOT_FOUND, body: { code: 'common.not_found' } });
      }
      return toResponse(typeof handler === 'function' ? await handler(request) : handler);
    }),
  );

  return { calls, set: (key, handler) => table.set(key, handler) };
}
