import type { PropsWithChildren } from 'react';
import { Navigate, useLocation } from 'react-router';

import { ROUTES } from '../../shared/routes';
import { ForbiddenPage } from '../../shared/ui/ForbiddenPage';
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
    return <Navigate to={ROUTES.login} replace state={{ from: `${location.pathname}${location.search}` }} />;
  }
  if (me.data.mustChangePassword && location.pathname !== ROUTES.changePassword) {
    return <Navigate to={ROUTES.changePassword} replace />;
  }
  return <SessionContext value={me.data}>{children}</SessionContext>;
}

/** Other roles go home, or see the 403 page when `forbidden` is set (pages whose entry they never see, e.g. reports). */
export function RequireRole({ roles, forbidden = false, children }: PropsWithChildren<{ roles: readonly Role[]; forbidden?: boolean }>) {
  const me = useSession();
  if (!roles.includes(me.role)) {
    return forbidden ? <ForbiddenPage /> : <Navigate to={ROUTES.home} replace />;
  }
  return children;
}
