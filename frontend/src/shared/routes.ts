/** URL paths are English (ADR-0006). */
export const ROUTES = {
  home: '/',
  login: '/login',
  changePassword: '/change-password',
  setPassword: '/set-password',
  forgotPassword: '/forgot-password',
  profile: '/profile',
  assets: '/assets',
  assetImport: '/assets/import',
  myAssets: '/assets/mine',
  tickets: '/tickets',
  newIncident: '/tickets/new/incident',
  newRequest: '/tickets/new/request',
  kb: '/kb',
  kbNew: '/kb/new',
  workspace: '/workspace',
  workspaceQueue: '/workspace/queue',
  approvals: '/approvals',
  reports: '/reports',
  reportExports: '/reports/exports',
  admin: '/admin',
  adminUsers: '/admin/users',
  adminDepartments: '/admin/departments',
  adminLocations: '/admin/locations',
  adminTeams: '/admin/teams',
  adminAudit: '/admin/audit',
  adminEmailTemplates: '/admin/email-templates',
  adminEmailDeliveries: '/admin/email-deliveries',
  adminSystem: '/admin/system',
  adminSlaCalendar: '/admin/sla/calendar',
  adminSlaPolicy: '/admin/sla/policy',
  adminCategories: '/admin/categories',
  adminRequestTypes: '/admin/request-types',
} as const;

export const assetPath = (id: string) => `${ROUTES.assets}/${id}`;

export const ticketPath = (number: string) => `${ROUTES.tickets}/${number}`;

export const articlePath = (id: string) => `${ROUTES.kb}/${id}`;

export const articleEditPath = (id: string) => `${ROUTES.kb}/${id}/edit`;

export const reportPath = (report: string) => `${ROUTES.reports}/${report}`;

/** Drill-down rows behind a report figure; metric, bucket and filters go in the query string. */
export const reportListPath = (report: string) => `${ROUTES.reports}/${report}/list`;
