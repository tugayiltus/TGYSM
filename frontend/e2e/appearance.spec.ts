import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll, expectStable, recordStability } from './helpers';
import { adminSession, seedIncidents, seedWorld } from './world';

const DARK = 'dark';
const THEME_ATTRIBUTE = 'data-coreui-theme';
/** Scrolls the ticket page so the side column sticks, but not so far that it reaches the end of its column. */
const SCROLL_PX = 250;

test('dark mode is kept after a reload and pages stay inside the screen', async ({ browser }) => {
  const admin = await adminSession(browser);
  await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Ekran titriyor']);
  const page = admin.page;
  await page.goto('/assets');
  await page.getByRole('button', { name: /Görünüm/ }).click();
  await page.getByRole('button', { name: 'Koyu' }).click();
  await expect(page.locator('html')).toHaveAttribute(THEME_ATTRIBUTE, DARK);
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute(THEME_ATTRIBUTE, DARK);
  for (const path of ['/assets', `/tickets/${ticket ?? ''}`, '/reports']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute(THEME_ATTRIBUTE, DARK);
    await expectNoHorizontalScroll(page);
  }
});

test('lists, details and dashboards do not jump or freeze while loading', async ({ browser }) => {
  const admin = await adminSession(browser);
  await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Klavye çalışmıyor']);
  const page = admin.page;
  await recordStability(page);
  for (const path of ['/', '/assets', `/tickets/${ticket ?? ''}`, '/reports']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await expectStable(page);
  }
});

test('the sticky side column of a detail page stays below the sticky header', async ({ browser }, info) => {
  test.skip(info.project.name !== 'desktop', 'The side column is sticky from 992 px only.');
  const admin = await adminSession(browser);
  await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Monitör görüntü vermiyor']);
  const page = admin.page;
  await page.goto(`/tickets/${ticket ?? ''}`);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await page.mouse.wheel(0, SCROLL_PX);
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(0);
  const header = await page.getByRole('banner').boundingBox();
  const actions = await page.getByRole('region', { name: 'İşlemler' }).boundingBox();
  expect(actions?.y ?? 0).toBeGreaterThanOrEqual((header?.y ?? 0) + (header?.height ?? 0));
});
