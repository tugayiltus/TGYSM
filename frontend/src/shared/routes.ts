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
  assetLabels: '/assets/labels',
  scanAsset: '/scan/asset/:id',
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
  adminBrands: '/admin/brands',
  adminModels: '/admin/models',
} as const;

export const assetPath = (id: string) => `${ROUTES.assets}/${id}`;
/** Print view of one asset's label; a bulk print passes its ids in navigation state (a long list does not fit a URL). */
export const assetLabelsPath = (id: string) => `${ROUTES.assetLabels}?${new URLSearchParams({ ids: id }).toString()}`;

export const ticketPath = (number: string) => `${ROUTES.tickets}/${number}`;

export const articlePath = (id: string) => `${ROUTES.kb}/${id}`;

export const articleEditPath = (id: string) => `${ROUTES.kb}/${id}/edit`;

export const reportPath = (report: string) => `${ROUTES.reports}/${report}`;

/** Drill-down rows behind a report figure; metric, bucket and filters go in the query string. */
export const reportListPath = (report: string) => `${ROUTES.reports}/${report}/list`;
