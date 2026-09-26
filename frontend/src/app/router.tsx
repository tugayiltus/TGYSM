import { Navigate, Outlet, type RouteObject } from 'react-router';

import { AdminLayout } from '../features/admin/AdminLayout';
import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { RequireRole, RequireSession } from '../features/auth/guards';
import { LoginPage } from '../features/auth/LoginPage';
import { ADMIN_ONLY } from '../features/auth/session';
import { HomePage } from '../features/home/HomePage';
import { ProfilePage } from '../features/profile/ProfilePage';
import { ROUTES } from '../shared/routes';
import { AppShell } from '../shared/ui/AppShell';
import { NotFoundPage } from '../shared/ui/NotFoundPage';

export const routes: RouteObject[] = [
  { path: ROUTES.login, element: <LoginPage /> },
  {
    element: (
      <RequireSession>
        <Outlet />
      </RequireSession>
    ),
    children: [
      { path: ROUTES.changePassword, element: <ChangePasswordPage /> },
      {
        element: <AppShell />,
        children: [
          { index: true, element: <HomePage /> },
          { path: ROUTES.profile, element: <ProfilePage /> },
        ],
      },
      {
        path: ROUTES.admin,
        element: (
          <RequireRole roles={ADMIN_ONLY}>
            <AdminLayout />
          </RequireRole>
        ),
        children: [
          { index: true, element: <Navigate to={ROUTES.adminUsers} replace /> },
          { path: ROUTES.adminUsers, lazy: async () => ({ Component: (await import('../features/admin/users/UsersPage')).UsersPage }) },
          { path: ROUTES.adminDepartments, lazy: async () => ({ Component: (await import('../features/admin/departments/DepartmentsPage')).DepartmentsPage }) },
          { path: ROUTES.adminLocations, lazy: async () => ({ Component: (await import('../features/admin/locations/LocationsPage')).LocationsPage }) },
          { path: ROUTES.adminTeams, lazy: async () => ({ Component: (await import('../features/admin/teams/TeamsPage')).TeamsPage }) },
          { path: ROUTES.adminAudit, lazy: async () => ({ Component: (await import('../features/admin/audit/AuditPage')).AuditPage }) },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
];
