import { QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider, type InitialEntry } from 'react-router';

import { createQueryClient } from '../app/queryClient';
import { routes } from '../app/router';
import { resetCsrfToken } from '../shared/api/client';
import { ToastProvider } from '../shared/ui/Toast';

export function renderRoute(path: InitialEntry) {
  resetCsrfToken();
  const queryClient = createQueryClient({ retry: false });
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const user = userEvent.setup();
  render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <RouterProvider router={router} />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return { router, user, queryClient };
}
