import { Outlet, type RouteObject } from 'react-router';

import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { ForgotPasswordPage } from '../features/auth/ForgotPasswordPage';
import { RequireRole, RequireSession } from '../features/auth/guards';
import { LoginPage } from '../features/auth/LoginPage';
import { ADMIN_ONLY, IT_STAFF_OR_ADMIN } from '../features/auth/session';
import { SetPasswordPage } from '../features/auth/SetPasswordPage';
import { HomePage } from '../features/home/HomePage';
import { ProfilePage } from '../features/profile/ProfilePage';
import { AppLayout } from '../shared/layout/AppLayout';
import { ROUTES } from '../shared/routes';
import { NotFoundPage } from '../shared/ui/NotFoundPage';

export const routes: RouteObject[] = [
  { path: ROUTES.login, element: <LoginPage /> },
  { path: ROUTES.setPassword, element: <SetPasswordPage /> },
  { path: ROUTES.forgotPassword, element: <ForgotPasswordPage /> },
  {
    element: (
      <RequireSession>
        <Outlet />
      </RequireSession>
    ),
    children: [
      {
        element: <AppLayout />,
        children: [
          { id: 'home', index: true, element: <HomePage /> },
          { id: 'profile', path: ROUTES.profile, element: <ProfilePage /> },
          { id: 'changePassword', path: ROUTES.changePassword, element: <ChangePasswordPage /> },
          { id: 'myTickets', path: ROUTES.tickets, lazy: async () => ({ Component: (await import('../features/tickets/MyTicketsPage')).MyTicketsPage }) },
          { id: 'newIncident', path: ROUTES.newIncident, lazy: async () => ({ Component: (await import('../features/tickets/NewTicketPage')).NewIncidentPage }) },
          { id: 'newRequest', path: ROUTES.newRequest, lazy: async () => ({ Component: (await import('../features/tickets/NewTicketPage')).NewRequestPage }) },
          { id: 'ticketDetail', path: `${ROUTES.tickets}/:number`, lazy: async () => ({ Component: (await import('../features/tickets/TicketDetailPage')).TicketDetailPage }) },
          {
            element: (
              <RequireRole roles={ADMIN_ONLY}>
                <Outlet />
              </RequireRole>
            ),
            children: [{ id: 'approvals', path: ROUTES.approvals, lazy: async () => ({ Component: (await import('../features/approvals/ApprovalsPage')).ApprovalsPage }) }],
          },
          { id: 'kbList', path: ROUTES.kb, lazy: async () => ({ Component: (await import('../features/kb/KnowledgeListPage')).KnowledgeListPage }) },
          {
            element: (
              <RequireRole roles={IT_STAFF_OR_ADMIN}>
                <Outlet />
              </RequireRole>
            ),
            children: [
              { id: 'kbNew', path: ROUTES.kbNew, lazy: async () => ({ Component: (await import('../features/kb/ArticleEditorPage')).ArticleCreatePage }) },
              { id: 'kbEdit', path: `${ROUTES.kb}/:id/edit`, lazy: async () => ({ Component: (await import('../features/kb/ArticleEditorPage')).ArticleEditPage }) },
            ],
          },
          { id: 'kbArticle', path: `${ROUTES.kb}/:id`, lazy: async () => ({ Component: (await import('../features/kb/ArticlePage')).ArticlePage }) },
          { id: 'myAssets', path: ROUTES.myAssets, lazy: async () => ({ Component: (await import('../features/assets/MyAssetsPage')).MyAssetsPage }) },
          {
            element: (
              <RequireRole roles={IT_STAFF_OR_ADMIN}>
                <Outlet />
              </RequireRole>
            ),
            children: [
              { id: 'workspace', path: ROUTES.workspace, lazy: async () => ({ Component: (await import('../features/workspace/WorkspacePage')).WorkspacePage }) },
              { id: 'workspaceQueue', path: ROUTES.workspaceQueue, lazy: async () => ({ Component: (await import('../features/workspace/WorkspacePage')).QueuePage }) },
              { id: 'assets', path: ROUTES.assets, lazy: async () => ({ Component: (await import('../features/assets/AssetsPage')).AssetsPage }) },
              { id: 'assetImport', path: ROUTES.assetImport, lazy: async () => ({ Component: (await import('../features/assets/AssetImportPage')).AssetImportPage }) },
              { id: 'assetDetail', path: `${ROUTES.assets}/:id`, lazy: async () => ({ Component: (await import('../features/assets/AssetDetailPage')).AssetDetailPage }) },
            ],
          },
          {
            // Employees never see the entry; a direct URL shows the 403 page instead of silently going home.
            element: (
              <RequireRole roles={IT_STAFF_OR_ADMIN} forbidden>
                <Outlet />
              </RequireRole>
            ),
            children: [
              { id: 'reports', path: ROUTES.reports, lazy: async () => ({ Component: (await import('../features/reports/ReportsPage')).ReportsIndexRedirect }) },
              { id: 'reportExports', path: ROUTES.reportExports, lazy: async () => ({ Component: (await import('../features/reports/MyExportsPage')).MyExportsPage }) },
              { id: 'report', path: `${ROUTES.reports}/:report`, lazy: async () => ({ Component: (await import('../features/reports/ReportsPage')).ReportsPage }) },
              { id: 'reportList', path: `${ROUTES.reports}/:report/list`, lazy: async () => ({ Component: (await import('../features/reports/DrilldownPage')).DrilldownPage }) },
            ],
          },
        ],
      },
      {
        path: ROUTES.admin,
        element: (
          <RequireRole roles={ADMIN_ONLY}>
            <Outlet />
          </RequireRole>
        ),
        children: [
          {
            // Lazy like the admin pages: a static import would put the whole feature-admin chunk on every page's preload list.
            lazy: async () => ({ Component: (await import('../shared/layout/AdminLayout')).AdminLayout }),
            children: [
              { id: 'adminOverview', index: true, lazy: async () => ({ Component: (await import('../features/admin/overview/AdminOverviewPage')).AdminOverviewPage }) },
              { id: 'adminUsers', path: ROUTES.adminUsers, lazy: async () => ({ Component: (await import('../features/admin/users/UsersPage')).UsersPage }) },
              { id: 'adminDepartments', path: ROUTES.adminDepartments, lazy: async () => ({ Component: (await import('../features/admin/departments/DepartmentsPage')).DepartmentsPage }) },
              { id: 'adminLocations', path: ROUTES.adminLocations, lazy: async () => ({ Component: (await import('../features/admin/locations/LocationsPage')).LocationsPage }) },
              { id: 'adminTeams', path: ROUTES.adminTeams, lazy: async () => ({ Component: (await import('../features/admin/teams/TeamsPage')).TeamsPage }) },
              { id: 'adminCategories', path: ROUTES.adminCategories, lazy: async () => ({ Component: (await import('../features/admin/catalog/CategoriesPage')).CategoriesPage }) },
              { id: 'adminRequestTypes', path: ROUTES.adminRequestTypes, lazy: async () => ({ Component: (await import('../features/admin/catalog/RequestTypesPage')).RequestTypesPage }) },
              { id: 'adminEmailTemplates', path: ROUTES.adminEmailTemplates, lazy: async () => ({ Component: (await import('../features/admin/email/EmailTemplatesPage')).EmailTemplatesPage }) },
              { id: 'adminEmailDeliveries', path: ROUTES.adminEmailDeliveries, lazy: async () => ({ Component: (await import('../features/admin/email/EmailDeliveriesPage')).EmailDeliveriesPage }) },
              { id: 'adminSlaCalendar', path: ROUTES.adminSlaCalendar, lazy: async () => ({ Component: (await import('../features/admin/sla/SlaCalendarPage')).SlaCalendarPage }) },
              { id: 'adminSlaPolicy', path: ROUTES.adminSlaPolicy, lazy: async () => ({ Component: (await import('../features/admin/sla/SlaPolicyPage')).SlaPolicyPage }) },
              { id: 'adminSystem', path: ROUTES.adminSystem, lazy: async () => ({ Component: (await import('../features/admin/system/SystemPage')).SystemPage }) },
              { id: 'adminAudit', path: ROUTES.adminAudit, lazy: async () => ({ Component: (await import('../features/admin/audit/AuditPage')).AuditPage }) },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
];
