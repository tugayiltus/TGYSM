import { QueryClientProvider } from '@tanstack/react-query';
import { createBrowserRouter, RouterProvider } from 'react-router';

import { AstryxProvider } from '../shared/ui/AstryxProvider';

import { createQueryClient } from './queryClient';
import { routes } from './router';

const router = createBrowserRouter(routes);
const queryClient = createQueryClient();

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AstryxProvider>
        <RouterProvider router={router} />
      </AstryxProvider>
    </QueryClientProvider>
  );
}
