import createClient, { type Middleware } from 'openapi-fetch';

import { ApiError, ERROR_CODES, NetworkError, toApiError } from './errors';
import type { paths } from './schema';

export const CSRF_HEADER = 'X-CSRF-TOKEN';
const CSRF_PATH = '/api/v1/auth/csrf';
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

let csrfToken: string | null = null;

/** Fetches a new antiforgery token; call after login and logout because the token is bound to the user. */
export async function refreshCsrfToken(): Promise<void> {
  const response = await fetch(`${window.location.origin}${CSRF_PATH}`, { credentials: 'same-origin' });
  if (!response.ok) {
    throw new ApiError(response.status, ERROR_CODES.unexpected);
  }
  const body = (await response.json()) as { token: string };
  csrfToken = body.token;
}

export function resetCsrfToken(): void {
  csrfToken = null;
}

const csrfMiddleware: Middleware = {
  async onRequest({ request }) {
    if (SAFE_METHODS.has(request.method)) {
      return request;
    }
    if (csrfToken === null) {
      await refreshCsrfToken();
    }
    request.headers.set(CSRF_HEADER, csrfToken ?? '');
    return request;
  },
};

export const api = createClient<paths>({
  baseUrl: window.location.origin,
  credentials: 'same-origin',
  // Resolve fetch at call time so tests can stub the global.
  fetch: (request: Request) => globalThis.fetch(request),
});
api.use(csrfMiddleware);

interface ApiResult<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

/** Returns the response data or throws ApiError / NetworkError. */
export async function unwrap<T>(call: Promise<ApiResult<T>>): Promise<T> {
  let result: ApiResult<T>;
  try {
    result = await call;
  } catch (cause) {
    if (cause instanceof ApiError) {
      throw cause;
    }
    throw new NetworkError(cause);
  }

  if (!result.response.ok) {
    const error = toApiError(result.response.status, result.error);
    if (error.code === ERROR_CODES.csrfInvalid) {
      resetCsrfToken();
    }
    throw error;
  }
  return result.data as T;
}
