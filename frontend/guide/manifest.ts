import type { Page } from '@playwright/test';

/**
 * Screenshots of the user guide (docs/user-guides/images/<id>-<view>.png), driven by guide/screenshots.ts against the
 * seeded pilot database (tools/seed-pilot.sh). The ids follow the pages Task 16 writes.
 */

export type GuideRole = 'employee' | 'it' | 'approver' | 'admin';
export type GuideView = 'mobile' | 'desktop';

/** Seed accounts (backend Features/Pilot): the first account of each role. */
export const ROLE_EMAILS: Record<GuideRole, string> = {
  employee: 'personel01@pilot.example.com',
  it: 'it01@pilot.example.com',
  approver: 'onay01@pilot.example.com',
  admin: 'admin@pilot.example.com',
};

export const VIEW_VIEWPORTS: Record<GuideView, { width: number; height: number }> = {
  mobile: { width: 390, height: 844 },
  desktop: { width: 1280, height: 800 },
};

export interface GuideShot {
  /** File stem; the images are `<id>-mobile.png` and `<id>-desktop.png`. */
  id: string;
  role: GuideRole;
  path: string;
  /** Optional step run after the page loaded and before the picture is taken (open a dialog, switch a tab). */
  setup?: (page: Page) => Promise<void>;
  views: readonly GuideView[];
}

const ALL_VIEWS: readonly GuideView[] = ['mobile', 'desktop'];

export const MANIFEST: readonly GuideShot[] = [
  { id: 'home', role: 'employee', path: '/', views: ALL_VIEWS },
  { id: 'tickets-list', role: 'employee', path: '/tickets', views: ALL_VIEWS },
  { id: 'tickets-new-incident', role: 'employee', path: '/tickets/new/incident', views: ALL_VIEWS },
  { id: 'tickets-new-request', role: 'employee', path: '/tickets/new/request', views: ALL_VIEWS },
  { id: 'my-assets', role: 'employee', path: '/assets/mine', views: ALL_VIEWS },
  { id: 'knowledge-base', role: 'employee', path: '/kb', views: ALL_VIEWS },
  { id: 'workspace', role: 'it', path: '/workspace', views: ALL_VIEWS },
  { id: 'workspace-queue', role: 'it', path: '/workspace/queue', views: ALL_VIEWS },
  { id: 'assets-list', role: 'it', path: '/assets', views: ALL_VIEWS },
  { id: 'assets-import', role: 'it', path: '/assets/import', views: ALL_VIEWS },
  { id: 'approvals', role: 'approver', path: '/approvals', views: ALL_VIEWS },
  { id: 'reports-incidents', role: 'it', path: '/reports/incidents', views: ALL_VIEWS },
  { id: 'reports-exports', role: 'it', path: '/reports/exports', views: ALL_VIEWS },
  { id: 'admin-users', role: 'admin', path: '/admin/users', views: ALL_VIEWS },
  { id: 'admin-locations', role: 'admin', path: '/admin/locations', views: ALL_VIEWS },
  { id: 'admin-brands', role: 'admin', path: '/admin/brands', views: ALL_VIEWS },
  { id: 'admin-email-templates', role: 'admin', path: '/admin/email-templates', views: ALL_VIEWS },
  { id: 'admin-sla-policy', role: 'admin', path: '/admin/sla/policy', views: ALL_VIEWS },
  { id: 'admin-system', role: 'admin', path: '/admin/system', views: ALL_VIEWS },
  { id: 'admin-audit', role: 'admin', path: '/admin/audit', views: ALL_VIEWS },
];
