import type { PropsWithChildren } from 'react';
import { Navigate, useLocation } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { ErrorState, LoadingState } from '../../shared/ui/States';

import { useMe, type Role } from './api';
import { SessionContext, useSession } from './session';

export function RequireSession({ children }: PropsWithChildren) {
  const me = useMe();
  const location = useLocation();

  if (me.isPending) {
    return <LoadingState />;
  }
  if (me.isError) {
    return <ErrorState error={me.error} onRetry={() => void me.refetch()} />;
  }
  if (me.data === null) {
    return <Navigate to={ROUTES.login} replace state={{ from: location.pathname }} />;
  }
  if (me.data.mustChangePassword && location.pathname !== ROUTES.changePassword) {
    return <Navigate to={ROUTES.changePassword} replace />;
  }
  return <SessionContext value={me.data}>{children}</SessionContext>;
}

export function RequireRole({ roles, children }: PropsWithChildren<{ roles: readonly Role[] }>) {
  const me = useSession();
  if (!roles.includes(me.role)) {
    return <Navigate to={ROUTES.home} replace />;
  }
  return children;
}
