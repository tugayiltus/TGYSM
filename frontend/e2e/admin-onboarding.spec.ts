import { expect, test } from '@playwright/test';

import { changePassword, expectNoHorizontalScroll, login, visibleEntry } from './helpers';
import { adminSession } from './world';

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

test('admin onboards a user; deactivation ends that user session', async ({ browser }) => {
  // adminSession performs the first-login password change when no earlier spec has done it (specs run alphabetically).
  const { context: adminContext, page } = await adminSession(browser);
  const userEmail = `e2e-${Date.now()}@example.com`;

  await expectNoHorizontalScroll(page);

  await page.goto('/admin/users');
  await page.getByRole('button', { name: 'Yeni kullanıcı' }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Ad', { exact: true }).fill('Deniz');
  await form.getByLabel('Soyad').fill('Aydın');
  await form.getByLabel('E-posta', { exact: true }).fill(userEmail);
  await form.getByRole('radio', { name: 'Geçici şifre göster' }).check();
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
  await Promise.all([userContext.close(), adminContext.close()]);
});
