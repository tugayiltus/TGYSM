import { expect, test } from '@playwright/test';

import { changePassword, expectNoHorizontalScroll, login, requiredEnv, visibleEntry } from './helpers';

const ADMIN_NEW_PASSWORD = 'Admin-new-pass-2026';
const USER_NEW_PASSWORD = 'User-new-pass-2026';

test('login form works with the keyboard only', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('E-posta').focus();
  await page.keyboard.type('nobody@example.com');
  await page.keyboard.press('Tab');
  await page.keyboard.type('Wrong-pass-2026');
  await page.keyboard.press('Enter');

  await expect(page.getByRole('alert')).toHaveText('E-posta veya şifre hatalı.');
});

test('admin onboards a user; deactivation ends that user session', async ({ page, browser }) => {
  const adminEmail = requiredEnv('E2E_ADMIN_EMAIL');
  const adminTemporaryPassword = requiredEnv('E2E_ADMIN_TEMP_PASSWORD');
  const userEmail = `e2e-${Date.now()}@example.com`;

  await page.goto('/');
  await expect(page).toHaveURL(/\/login$/);
  await login(page, adminEmail, adminTemporaryPassword);
  await expect(page).toHaveURL(/\/change-password$/);
  await changePassword(page, adminTemporaryPassword, ADMIN_NEW_PASSWORD);
  await expect(page.getByRole('heading', { name: /^Merhaba/ })).toBeVisible();
  await expectNoHorizontalScroll(page);

  await page.goto('/admin/users');
  await page.getByRole('button', { name: 'Yeni kullanıcı' }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Ad', { exact: true }).fill('Deniz');
  await form.getByLabel('Soyad').fill('Aydın');
  await form.getByLabel('E-posta').fill(userEmail);
  await form.getByRole('button', { name: 'Oluştur' }).click();
  const userTemporaryPassword = ((await page.getByTestId('temporary-password').textContent()) ?? '').trim();
  expect(userTemporaryPassword).not.toBe('');
  await page.getByRole('dialog').getByRole('button', { name: 'Kapat' }).last().click();
  await expectNoHorizontalScroll(page);

  const userContext = await browser.newContext();
  const userPage = await userContext.newPage();
  await login(userPage, userEmail, userTemporaryPassword);
  await changePassword(userPage, userTemporaryPassword, USER_NEW_PASSWORD);
  await expect(userPage.getByRole('heading', { name: 'Merhaba, Deniz' })).toBeVisible();
  await userPage.goto('/admin/users');
  await expect(userPage).toHaveURL(/\/$/);

  await page.getByLabel('Kullanıcı ara').fill(userEmail);
  const entry = visibleEntry(page, userEmail);
  await entry.getByRole('button', { name: 'Pasifleştir' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(entry.getByText('Pasif', { exact: true })).toBeVisible();

  await userPage.reload();
  await expect(userPage).toHaveURL(/\/login$/);
  await userContext.close();
});
