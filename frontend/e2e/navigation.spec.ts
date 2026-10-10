import { expect, test, type Page } from '@playwright/test';

import { expectNoHorizontalScroll } from './helpers';
import { adminSession, seedIncidents, seedPublishedArticle, seedWorld, userSession } from './world';

const MAIN_MENU = 'Ana menü';

/** The left menu: always visible from 992 px, behind the menu button on phones. */
async function openMenu(page: Page) {
  const button = page.getByRole('button', { name: 'Menüyü aç' });
  if (await button.isVisible()) {
    await button.click();
    return page.getByRole('dialog', { name: MAIN_MENU }).getByRole('navigation', { name: MAIN_MENU });
  }
  return page.getByRole('navigation', { name: MAIN_MENU });
}

const trail = (page: Page) => page.getByRole('navigation', { name: 'Sayfa yolu' });

test('menus follow the role, and admins switch between the app and Yönetim', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);

  const employeeMenu = await openMenu(employee.page);
  await expect(employeeMenu.getByRole('link', { name: 'Kayıtlarım' })).toBeVisible();
  await expect(employeeMenu.getByRole('link', { name: 'Envanter' })).toHaveCount(0);
  await expect(employeeMenu.getByRole('link', { name: 'Raporlar' })).toHaveCount(0);
  await expectNoHorizontalScroll(employee.page);

  await admin.page.goto('/');
  await admin.page.getByRole('link', { name: 'Yönetim', exact: true }).click();
  await expect(admin.page).toHaveURL(/\/admin$/);
  await expect(admin.page.getByRole('heading', { level: 1, name: 'Genel bakış' })).toBeVisible();
  await admin.page.getByRole('link', { name: 'Uygulamaya dön' }).click();
  await expect(admin.page).toHaveURL(/\/$/);
  await expectNoHorizontalScroll(admin.page);
});

test('breadcrumbs lead back from an article edit and from a ticket to its filtered queue', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const it = await userSession(browser, world.itEmail, world.itPassword);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const token = Date.now().toString();
  const article = await seedPublishedArticle(it, `VPN kurulumu ${token}`, 'employees');
  const [number] = await seedIncidents(employee, [`Ekran donuyor ${token}`]);

  await it.page.goto(`/kb/${article.id}/edit`);
  await trail(it.page).getByRole('link', { name: article.title }).click();
  await expect(it.page).toHaveURL(new RegExp(`/kb/${article.id}$`));
  await trail(it.page).getByRole('link', { name: 'Bilgi bankası' }).click();
  await expect(it.page).toHaveURL(/\/kb$/);
  await expectNoHorizontalScroll(it.page);

  await it.page.goto('/workspace/queue?queue=team&status=open');
  await it.page.getByRole('link', { name: new RegExp(`Aç.*${number ?? ''}`) }).first().click();
  await expect(it.page).toHaveURL(new RegExp(`/tickets/${number ?? ''}$`));
  await trail(it.page).getByRole('link', { name: 'Kuyruk' }).click();
  await expect(it.page).toHaveURL(/\/workspace\/queue\?queue=team&status=open$/);
});

test('header controls and breadcrumb fit the screen', async ({ browser }) => {
  const admin = await adminSession(browser);
  await admin.page.goto('/assets/import');
  await expect(admin.page.getByRole('banner').getByRole('navigation')).toBeVisible();
  await expectNoHorizontalScroll(admin.page);
});

const NARROW_WIDTHS = [360, 320] as const;
const PHONE_HEIGHT = 720;
/** A report segment of 120 characters becomes a 120-character crumb in the collapsed middle. */
const LONG_CRUMB_LENGTH = 120;

test('the breadcrumb overflow menu stays inside narrow screens and wraps long crumbs', async ({ browser }, info) => {
  test.skip(info.project.name !== 'mobile-360', 'The middle crumbs collapse below 768 px only.');
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const [number] = await seedIncidents(employee, [`Ekran donuyor ${Date.now().toString()}`]);
  const longSegment = 'x'.repeat(LONG_CRUMB_LENGTH);
  const page = employee.page;

  await page.goto(`/tickets/${number ?? ''}`);
  // As if the ticket had been opened from a drill-down list whose report segment is very long (router state `from`).
  await page.evaluate((from) => {
    const current = window.history.state as { key?: string; idx?: number } | null;
    window.history.replaceState({ usr: { from }, key: current?.key, idx: current?.idx }, '');
    window.dispatchEvent(new PopStateEvent('popstate', { state: window.history.state }));
  }, `/reports/${longSegment}/list`);

  for (const width of NARROW_WIDTHS) {
    await page.setViewportSize({ width, height: PHONE_HEIGHT });
    const more = trail(page).getByRole('button', { name: 'Diğer üst sayfalar' });
    await more.click();
    const menu = trail(page).getByRole('menu');
    const longItem = menu.getByText(longSegment, { exact: false });
    await expect(longItem).toBeVisible();
    const box = await menu.boundingBox();
    expect(box).not.toBeNull();
    expect(box?.x ?? -1).toBeGreaterThanOrEqual(0);
    expect((box?.x ?? 0) + (box?.width ?? 0)).toBeLessThanOrEqual(width);
    const clipped = await longItem.evaluate((element) => element.scrollWidth - element.clientWidth);
    expect(clipped).toBeLessThanOrEqual(0);
    await expectNoHorizontalScroll(page);
    await more.click();
  }
});
