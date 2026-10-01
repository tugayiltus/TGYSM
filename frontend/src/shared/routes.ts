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
  workspace: '/workspace',
  workspaceQueue: '/workspace/queue',
  approvals: '/approvals',
  admin: '/admin',
  adminUsers: '/admin/users',
  adminDepartments: '/admin/departments',
  adminLocations: '/admin/locations',
  adminTeams: '/admin/teams',
  adminAudit: '/admin/audit',
  adminEmailTemplates: '/admin/email-templates',
  adminEmailDeliveries: '/admin/email-deliveries',
  adminSystem: '/admin/system',
  adminCategories: '/admin/categories',
  adminRequestTypes: '/admin/request-types',
} as const;

export const assetPath = (id: string) => `${ROUTES.assets}/${id}`;

export const ticketPath = (number: string) => `${ROUTES.tickets}/${number}`;
