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
