import { expect, type Locator, type Page } from '@playwright/test';

export function requiredEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set; run the E2E suite through tools/e2e.sh.`);
  }
  return value;
}

/** Astryx shows status messages in alerts and keeps its own empty alert live region: match only alerts with text. */
export function messageAlert(scope: Page | Locator): Locator {
  return scope.getByRole('alert').filter({ hasText: /\S/ });
}

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * Opens an Astryx select: a combobox, or a button named "label value" when the select has its own search box.
 * Its popup list renders at the page root.
 */
async function openSelect(scope: Page | Locator, label: string | RegExp): Promise<Page> {
  const page = 'page' in scope ? scope.page() : scope;
  const buttonName = typeof label === 'string' ? new RegExp(`^${escapeRegExp(label)}(?:\\s|$)`) : label;
  await scope.getByRole('combobox', { name: label }).or(scope.getByRole('button', { name: buttonName })).first().click();
  return page;
}

export async function chooseOption(scope: Page | Locator, label: string | RegExp, option: string): Promise<void> {
  const page = await openSelect(scope, label);
  await page.getByRole('option', { name: option, exact: true }).click();
}

export async function chooseFirstOption(scope: Page | Locator, label: string | RegExp): Promise<void> {
  const page = await openSelect(scope, label);
  await page.getByRole('option').first().click();
}

/** A whole-card link (Astryx ClickableCard): its anchor is an invisible layer, so the person clicks the card's title. */
export async function openCard(page: Page, title: string): Promise<void> {
  await page.getByText(title, { exact: true }).click();
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

/** Layout stability budget (spec criterion 8): cumulative layout shift and the longest main-thread task while a page loads. */
export const MAX_CLS = 0.1;
export const MAX_LONG_TASK_MS = 200;

/** Records layout shifts and long tasks from the first byte of every document this page loads. */
export async function recordStability(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const w = window as unknown as { tgysmCls: number; tgysmLongTasks: number[] };
    w.tgysmCls = 0;
    w.tgysmLongTasks = [];
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries() as (PerformanceEntry & { value: number; hadRecentInput: boolean })[]) {
        if (!entry.hadRecentInput) {
          w.tgysmCls += entry.value;
        }
      }
    }).observe({ type: 'layout-shift', buffered: true });
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        w.tgysmLongTasks.push(entry.duration);
      }
    }).observe({ type: 'longtask', buffered: true });
  });
}

/** The current document stayed within the layout-stability budget. */
export async function expectStable(page: Page): Promise<void> {
  const { cls, longTasks } = await page.evaluate(() => {
    const w = window as unknown as { tgysmCls: number; tgysmLongTasks: number[] };
    return { cls: w.tgysmCls, longTasks: w.tgysmLongTasks };
  });
  expect(cls).toBeLessThan(MAX_CLS);
  expect(longTasks.filter((duration) => duration > MAX_LONG_TASK_MS)).toEqual([]);
}
