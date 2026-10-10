import type { Locator, Page } from '@playwright/test';

/**
 * Screenshots of the user guide (docs/user-guides/images/<id>-<view>.png), driven by guide/screenshots.ts against the
 * seeded pilot database (tools/seed-pilot.sh). Ids are grouped by the guide page that shows them.
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
const MOBILE: readonly GuideView[] = ['mobile'];
const DESKTOP: readonly GuideView[] = ['desktop'];

/** Visible "Aç" (open) links of a list; tables and phone cards both render one, only one of them is visible. */
const OPEN_LINK = /^Aç /;

/** Article links of the knowledge base list (not the "Yeni makale" link). */
const ARTICLE_LINK = 'main a[href^="/kb/"]:not([href="/kb/new"])';

/** Clicks the first visible match and waits until the page it leads to is open. */
const openFirst =
  (find: (page: Page) => Locator) =>
  async (page: Page): Promise<void> => {
    const before = page.url();
    await find(page).filter({ visible: true }).first().click();
    await page.waitForURL((url) => url.href !== before);
  };

/** Opens the first record of the list on the page. */
const openFirstRecord = openFirst((page) => page.getByRole('link', { name: OPEN_LINK }));
const openFirstArticle = openFirst((page) => page.locator(ARTICLE_LINK));

const clickButton =
  (name: string) =>
  async (page: Page): Promise<void> => {
    await page.getByRole('button', { name, exact: true }).filter({ visible: true }).first().click();
    await page.getByRole('dialog').waitFor();
  };

/** Tickets assigned to the signed-in IT person that are being worked on. */
const MY_WORK_QUEUE = '/workspace/queue?queue=assigned_to_me&status=in_progress';

/** Opens the first ticket of the list and scrolls its "İşlemler" card into view. */
const openWorkActions = async (page: Page): Promise<void> => {
  await openFirstRecord(page);
  await page.getByRole('heading', { name: 'İşlemler' }).scrollIntoViewIfNeeded();
};

/** Opens the phone menu and waits until its slide-in animation is over. */
const openMobileMenu = async (page: Page): Promise<void> => {
  await page.getByRole('button', { name: 'Menüyü aç' }).click();
  await page.getByRole('dialog').getByRole('link', { name: 'Kayıtlarım' }).waitFor();
  await page.waitForFunction(() => document.getAnimations().every((animation) => animation.playState !== 'running'));
};

/** Seed names the guide shows in its examples (backend Features/Pilot). */
const PILOT_CATEGORY = 'TGYSM Pilot';
const APPROVAL_REQUEST_TYPE = 'Yeni bilgisayar';

export const MANIFEST: readonly GuideShot[] = [
  // getting-started
  { id: 'home', role: 'employee', path: '/', views: ALL_VIEWS },
  { id: 'home-menu', role: 'employee', path: '/', setup: openMobileMenu, views: MOBILE },
  // incidents-and-requests, feedback
  { id: 'tickets-new-incident', role: 'employee', path: '/tickets/new/incident', views: DESKTOP },
  {
    id: 'tickets-new-request',
    role: 'employee',
    path: '/tickets/new/request',
    setup: async (page) => {
      await page.getByLabel('Talep tipi').selectOption({ label: APPROVAL_REQUEST_TYPE });
    },
    views: MOBILE,
  },
  {
    id: 'feedback-incident',
    role: 'employee',
    path: '/tickets/new/incident',
    setup: async (page) => {
      await page.getByLabel('Konu').fill('Rapor sayfası açılmıyor');
      await page.getByLabel('Kategori').selectOption({ label: PILOT_CATEGORY });
    },
    views: MOBILE,
  },
  // my-tickets
  { id: 'tickets-list', role: 'employee', path: '/tickets', views: DESKTOP },
  { id: 'ticket-detail', role: 'employee', path: '/tickets', setup: openFirstRecord, views: DESKTOP },
  // my-assets
  { id: 'my-assets', role: 'employee', path: '/assets/mine', views: MOBILE },
  // knowledge-base
  { id: 'knowledge-base', role: 'employee', path: '/kb', views: DESKTOP },
  { id: 'kb-article', role: 'employee', path: '/kb', setup: openFirstArticle, views: MOBILE },
  { id: 'kb-new', role: 'it', path: '/kb/new', views: DESKTOP },
  // it-workspace
  { id: 'workspace', role: 'it', path: '/workspace', views: DESKTOP },
  { id: 'workspace-queue', role: 'it', path: '/workspace/queue?queue=team', views: DESKTOP },
  { id: 'ticket-work', role: 'it', path: MY_WORK_QUEUE, setup: openWorkActions, views: DESKTOP },
  {
    id: 'ticket-complete',
    role: 'it',
    path: MY_WORK_QUEUE,
    setup: async (page) => {
      await openFirstRecord(page);
      await clickButton('Tamamla')(page);
    },
    views: DESKTOP,
  },
  // inventory
  { id: 'assets-list', role: 'it', path: '/assets', views: DESKTOP },
  { id: 'asset-detail', role: 'it', path: '/assets?status=assigned', setup: openFirstRecord, views: DESKTOP },
  { id: 'asset-new', role: 'it', path: '/assets', setup: clickButton('Yeni cihaz'), views: DESKTOP },
  { id: 'assets-import', role: 'it', path: '/assets/import', views: DESKTOP },
  // reports
  { id: 'reports-incidents', role: 'it', path: '/reports/incidents', views: ALL_VIEWS },
  { id: 'reports-exports', role: 'it', path: '/reports/exports', views: DESKTOP },
  // approvals
  { id: 'approvals', role: 'approver', path: '/approvals', views: DESKTOP },
  // admin-users-and-organization, first-setup
  { id: 'admin-overview', role: 'admin', path: '/admin', views: DESKTOP },
  { id: 'admin-users', role: 'admin', path: '/admin/users', views: DESKTOP },
  { id: 'admin-user-new', role: 'admin', path: '/admin/users', setup: clickButton('Yeni kullanıcı'), views: DESKTOP },
  { id: 'admin-departments', role: 'admin', path: '/admin/departments', views: DESKTOP },
  { id: 'admin-locations', role: 'admin', path: '/admin/locations', views: DESKTOP },
  { id: 'admin-teams', role: 'admin', path: '/admin/teams', views: DESKTOP },
  { id: 'admin-audit', role: 'admin', path: '/admin/audit', views: DESKTOP },
  // admin-catalogs
  { id: 'admin-categories', role: 'admin', path: '/admin/categories', views: DESKTOP },
  { id: 'admin-request-types', role: 'admin', path: '/admin/request-types', views: DESKTOP },
  { id: 'admin-brands', role: 'admin', path: '/admin/brands', views: DESKTOP },
  { id: 'admin-models', role: 'admin', path: '/admin/models', views: DESKTOP },
  // admin-sla
  { id: 'admin-sla-calendar', role: 'admin', path: '/admin/sla/calendar', views: DESKTOP },
  { id: 'admin-sla-policy', role: 'admin', path: '/admin/sla/policy', views: DESKTOP },
  // admin-email-and-system
  { id: 'admin-email-templates', role: 'admin', path: '/admin/email-templates', views: DESKTOP },
  { id: 'admin-email-deliveries', role: 'admin', path: '/admin/email-deliveries', views: DESKTOP },
  { id: 'admin-system', role: 'admin', path: '/admin/system', views: DESKTOP },
];
