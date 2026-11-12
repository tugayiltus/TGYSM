/**
 * @input The asset id from a scanned QR label link.
 * @output A redirect: IT staff and admins to the device, employees to a new incident with the device chosen.
 * @position `/scan/asset/:id`, behind sign-in; a signed-out scan returns here after login (ADR-0028).
 */
import { Navigate, useParams } from 'react-router';

import { assetPath, newIncidentPath, ROUTES } from '../../../shared/routes';
import { isItStaffOrAdmin, useSession } from '../../auth/session';

export function ScanAssetPage() {
  const { id } = useParams();
  const me = useSession();
  if (id === undefined) {
    return <Navigate to={ROUTES.home} replace />;
  }
  return <Navigate to={isItStaffOrAdmin(me.role) ? assetPath(id) : newIncidentPath(id)} replace />;
}
