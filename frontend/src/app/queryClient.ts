import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';

import { resetCsrfToken } from '../shared/api/client';
import { ApiError, ERROR_CODES } from '../shared/api/errors';
import { ADMIN_QUERY_ROOT, ME_QUERY_KEY } from '../shared/api/queryKeys';

const MAX_RETRIES = 2;
const HTTP_UNAUTHORIZED = 401;
const HTTP_CLIENT_ERROR_MIN = 400;
const HTTP_SERVER_ERROR_MIN = 500;

function shouldRetry(failureCount: number, error: unknown): boolean {
  const isClientError = error instanceof ApiError && error.status >= HTTP_CLIENT_ERROR_MIN && error.status < HTTP_SERVER_ERROR_MIN;
  return !isClientError && failureCount < MAX_RETRIES;
}

/**
 * A 401 anywhere means the session ended (deactivated, role changed): drop "me" so the guard sends the user to login,
 * and drop the antiforgery token, which was bound to the old session. A concurrency conflict reloads admin lists so
 * the next attempt uses the current version.
 */
function handleSessionError(queryClient: QueryClient, error: unknown): void {
  if (!(error instanceof ApiError)) {
    return;
  }
  if (error.status === HTTP_UNAUTHORIZED) {
    resetCsrfToken();
    queryClient.removeQueries({ queryKey: ADMIN_QUERY_ROOT });
    queryClient.setQueryData(ME_QUERY_KEY, null);
  } else if (error.code === ERROR_CODES.passwordChangeRequired) {
    void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
  } else if (error.code === ERROR_CODES.concurrencyConflict) {
    void queryClient.invalidateQueries({ queryKey: ADMIN_QUERY_ROOT });
  }
}

export function createQueryClient(options: { retry?: boolean } = {}): QueryClient {
  const queryClient: QueryClient = new QueryClient({
    queryCache: new QueryCache({ onError: (error) => handleSessionError(queryClient, error) }),
    mutationCache: new MutationCache({ onError: (error) => handleSessionError(queryClient, error) }),
    defaultOptions: {
      queries: { retry: options.retry === false ? false : shouldRetry, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
  return queryClient;
}
