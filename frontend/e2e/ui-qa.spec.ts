/**
 * @input A fresh E2E database (tools/e2e.sh) and UI_QA_OUT, the folder for the results.
 * @output A full-page screenshot of every screen in light and dark mode per viewport, and problems.txt with sideways
 * overflow, console errors and page errors.
 * @position Browser QA sweep; run it with tools/ui-qa.sh, the regular E2E run ignores it.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { test } from '@playwright/test';

import { requiredEnv } from './helpers';
import { adminSession, seedIncidents, seedPublishedArticle, seedWorld } from './world';

const OUT = requiredEnv('UI_QA_OUT');
const MODES = ['light', 'dark'] as const;
const COLOR_MODE_STORAGE_KEY = 'tgysm-theme';
const SWEEP_TIMEOUT_MS = 600_000;
const SETTLE_MS = 800;
const OVERFLOW_TOLERANCE_PX = 1;
const MESSAGE_LENGTH = 200;
const FILE_NAME_LENGTH = 80;

test('every screen in light and dark mode', async ({ browser }, info) => {
  test.setTimeout(SWEEP_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const [ticket] = await seedIncidents(admin, ['Ekran titriyor, monitör kablosu gevşek olabilir']);
  const article = await seedPublishedArticle(admin, 'VPN bağlantısı nasıl kurulur', 'employees');
  const page = admin.page;
  const found = (await (await admin.context.request.get(`/api/v1/assets?search=${encodeURIComponent(world.assetTag)}`)).json()) as { items: { id: string }[] };
  const assetUrl = `/assets/${found.items[0]?.id ?? ''}`;

  const paths = [
    '/', '/workspace', '/workspace/queue', '/tickets', `/tickets/${ticket ?? ''}`, '/tickets/new/incident', '/tickets/new/request', '/approvals',
    '/assets', assetUrl, '/assets/mine', '/assets/import', '/kb', `/kb/${article.id}`, `/kb/${article.id}/edit`, '/kb/new',
    '/reports', '/reports/incidents', '/reports/exports', '/profile', '/change-password',
    '/admin', '/admin/users', '/admin/departments', '/admin/teams', '/admin/locations', '/admin/categories', '/admin/request-types',
    '/admin/brands', '/admin/models', '/admin/sla/policy', '/admin/sla/calendar', '/admin/email-templates', '/admin/email-deliveries',
    '/admin/audit', '/admin/system', '/nope-404',
  ];
  const dir = path.join(OUT, info.project.name);
  mkdirSync(dir, { recursive: true });
  const problems: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      problems.push(`console ${page.url()}: ${message.text().slice(0, MESSAGE_LENGTH)}`);
    }
  });
  page.on('pageerror', (error) => problems.push(`pageerror ${page.url()}: ${error.message.slice(0, MESSAGE_LENGTH)}`));

  for (const mode of MODES) {
    await page.evaluate(({ key, value }) => localStorage.setItem(key, value), { key: COLOR_MODE_STORAGE_KEY, value: mode });
    for (const target of paths) {
      await page.goto(target);
      await page.waitForLoadState('load');
      await page.waitForTimeout(SETTLE_MS);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      if (overflow > OVERFLOW_TOLERANCE_PX) {
        problems.push(`overflow ${overflow}px ${mode} ${target}`);
      }
      const name = `${mode}${target.replaceAll('/', '_') || '_home'}`.slice(0, FILE_NAME_LENGTH);
      await page.screenshot({ path: path.join(dir, `${name}.png`), fullPage: true });
    }
  }
  writeFileSync(path.join(dir, 'problems.txt'), problems.join('\n'));
});
