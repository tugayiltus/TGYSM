import { createContext, useContext } from 'react';

import type { Me, Role } from './api';

export const SessionContext = createContext<Me | null>(null);

export const ADMIN_ONLY: readonly Role[] = ['admin'];

/** The signed-in user; only valid inside RequireSession. */
export function useSession(): Me {
  const me = useContext(SessionContext);
  if (me === null) {
    throw new Error('useSession must be used inside RequireSession.');
  }
  return me;
}
