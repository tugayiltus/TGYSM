import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';

import { chromium } from '@playwright/test';

import { MANIFEST, ROLE_EMAILS, VIEW_VIEWPORTS, type GuideRole, type GuideShot, type GuideView } from './manifest.ts';

/**
 * Takes the user-guide screenshots listed in guide/manifest.ts, started by tools/guide-screenshots.sh against a seeded
 * stack. One browser context per role and view; each role signs in once. Any page that does not load aborts the run.
 */

const LOCALE = 'tr-TR';
const TIMEZONE = 'Europe/Istanbul';
const LOGIN_PATH = '/login';
const SETTLE_TIMEOUT_MS = 15_000;
const SIGNED_IN_TIMEOUT_MS = 20_000;
const ANIMATION_SETTLE_MS = 400;

function env(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set; run this through tools/guide-screenshots.sh.`);
  }
  return value;
}

async function main(): Promise<void> {
  const baseURL = env('GUIDE_BASE_URL');
  const password = env('GUIDE_PASSWORD');
  const outDir = env('GUIDE_IMAGES_DIR');
  const only = (process.env.GUIDE_ONLY ?? '').split(',').filter(Boolean);
  const shots: readonly GuideShot[] = only.length > 0 ? MANIFEST.filter((shot) => only.includes(shot.id)) : MANIFEST;
  if (shots.length === 0) {
    throw new Error('No screenshot matches GUIDE_ONLY.');
  }
  await mkdir(outDir, { recursive: true });

  const browser = await chromium.launch();
  try {
    const roles = [...new Set(shots.map((shot) => shot.role))];
    const views = [...new Set(shots.flatMap((shot) => shot.views))];
    let taken = 0;
    for (const view of views) {
      for (const role of roles) {
        const wanted = shots.filter((shot) => shot.role === role && shot.views.includes(view));
        if (wanted.length === 0) continue;
        taken += await shootRole(browser, baseURL, password, outDir, role, view, wanted);
      }
    }
    console.log(`Took ${taken} screenshots into ${outDir}`);
  } finally {
    await browser.close();
  }
}

async function shootRole(
  browser: Awaited<ReturnType<typeof chromium.launch>>,
  baseURL: string,
  password: string,
  outDir: string,
  role: GuideRole,
  view: GuideView,
  wanted: readonly GuideShot[],
): Promise<number> {
  const context = await browser.newContext({
    baseURL,
    locale: LOCALE,
    timezoneId: TIMEZONE,
    viewport: VIEW_VIEWPORTS[view],
    hasTouch: view === 'mobile',
  });
  try {
    const page = await context.newPage();
    await page.goto(LOGIN_PATH);
    await page.getByLabel('E-posta').fill(ROLE_EMAILS[role]);
    await page.getByLabel('Şifre', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Giriş yap' }).click();
    await page.waitForURL((url) => url.pathname !== LOGIN_PATH, { timeout: SIGNED_IN_TIMEOUT_MS });
    if (new URL(page.url()).pathname.startsWith('/change-password')) {
      throw new Error(`${ROLE_EMAILS[role]} must change its password first; the seed accounts should not.`);
    }
    for (const shot of wanted) {
      await page.goto(shot.path);
      await page.waitForLoadState('networkidle', { timeout: SETTLE_TIMEOUT_MS });
      if (shot.setup) {
        await shot.setup(page);
        await page.waitForLoadState('networkidle', { timeout: SETTLE_TIMEOUT_MS });
      }
      await page.waitForTimeout(ANIMATION_SETTLE_MS);
      await page.screenshot({ path: join(outDir, `${shot.id}-${view}.png`) });
    }
    return wanted.length;
  } finally {
    await context.close();
  }
}

await main();
