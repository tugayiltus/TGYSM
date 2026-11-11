import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll, expectStable, recordStability } from './helpers';
import { adminSession, seedIncidents, seedWorld } from './world';

const DARK = 'dark';
const COLOR_SCHEME = 'color-scheme';
/** Scrolls the ticket page so the side column sticks, but not so far that it reaches the end of its column. */

test('dark mode is kept after a reload and pages stay inside the screen', async ({ browser }) => {
  const admin = await adminSession(browser);
  await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Ekran titriyor']);
  const page = admin.page;
  await page.goto('/assets');
  await page.getByRole('button', { name: /Görünüm/ }).click();
  await page.getByRole('menuitemradio', { name: 'Koyu' }).click();
  await expect(page.locator('html')).toHaveCSS(COLOR_SCHEME, DARK);
  await page.reload();
  await expect(page.locator('html')).toHaveCSS(COLOR_SCHEME, DARK);
  for (const path of ['/assets', `/tickets/${ticket ?? ''}`, '/reports']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.locator('html')).toHaveCSS(COLOR_SCHEME, DARK);
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

test('the side column of a detail page sits beside the content, below the header', async ({ browser }, info) => {
  test.skip(info.project.name !== 'desktop', 'The side column sits beside the content from 768 px only.');
  const admin = await adminSession(browser);
  await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Monitör görüntü vermiyor']);
  const page = admin.page;
  await page.goto(`/tickets/${ticket ?? ''}`);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  const header = await page.getByRole('banner').boundingBox();
  const actions = await page.getByRole('region', { name: 'İşlemler' }).boundingBox();
  const comments = await page.getByRole('region', { name: 'Yazışmalar' }).boundingBox();
  expect(actions?.y ?? 0).toBeGreaterThanOrEqual((header?.y ?? 0) + (header?.height ?? 0));
  expect(actions?.x ?? 0).toBeGreaterThan((comments?.x ?? 0) + (comments?.width ?? 0));
});
