/** URL paths are English (ADR-0006). */
export const ROUTES = {
  home: '/',
  login: '/login',
  changePassword: '/change-password',
  profile: '/profile',
  assets: '/assets',
  assetImport: '/assets/import',
  myAssets: '/assets/mine',
  admin: '/admin',
  adminUsers: '/admin/users',
  adminDepartments: '/admin/departments',
  adminLocations: '/admin/locations',
  adminTeams: '/admin/teams',
  adminAudit: '/admin/audit',
} as const;

export const assetPath = (id: string) => `${ROUTES.assets}/${id}`;
