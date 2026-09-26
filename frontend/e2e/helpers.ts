import { expect, type Locator, type Page } from '@playwright/test';

export function requiredEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set; run the E2E suite through tools/e2e.sh.`);
  }
  return value;
}

export async function login(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('E-posta').fill(email);
  await page.getByLabel('Şifre', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Giriş yap' }).click();
}

export async function changePassword(page: Page, current: string, next: string): Promise<void> {
  await page.getByLabel('Mevcut şifre').fill(current);
  await page.getByLabel('Yeni şifre', { exact: true }).fill(next);
  await page.getByLabel('Yeni şifre (tekrar)').fill(next);
  await page.getByRole('button', { name: 'Şifreyi değiştir' }).click();
}

/** Spec: basic flows need no horizontal scroll at 360 px. */
export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
}

/** The visible card (mobile) or table row (desktop) that contains the text. */
export function visibleEntry(page: Page, text: string): Locator {
  return page.locator('li:visible, tr:visible').filter({ hasText: text });
}
