import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll, login } from './helpers';
import { adminSession, waitForEmail } from './world';

const NEW_PASSWORD = 'Link-set-pass-2026';
const LINK_PATTERN = /Bağlantı: (\S+)/;

test('admin creates a user with an emailed link; the activation email lets them set a password and sign in', async ({ browser }) => {
  const admin = await adminSession(browser);
  const email = `link-${Date.now()}@example.com`;

  await admin.page.goto('/admin/users');
  await admin.page.getByRole('button', { name: 'Yeni kullanıcı' }).click();
  const form = admin.page.getByRole('dialog');
  await form.getByLabel('Ad', { exact: true }).fill('Selin');
  await form.getByLabel('Soyad').fill('Arslan');
  await form.getByLabel('E-posta', { exact: true }).fill(email);
  await form.getByLabel('Rol').selectOption({ label: 'IT personeli' });
  await expect(form.getByRole('radio', { name: 'E-posta ile bağlantı gönder' })).toBeChecked();
  await form.getByRole('button', { name: 'Oluştur' }).click();
  await expect(admin.page.getByRole('heading', { name: 'Bağlantı gönderildi' })).toBeVisible();
  await expect(admin.page.getByTestId('temporary-password')).toHaveCount(0);
  await expectNoHorizontalScroll(admin.page);

  const activation = await waitForEmail(email, 'hesabınızı etkinleştirmek');
  expect(activation.subject).toBe('Hesabınızı etkinleştirin');
  const link = new URL(LINK_PATTERN.exec(activation.body)?.[1] ?? '');
  expect(link.pathname).toBe('/set-password');

  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(`${link.pathname}${link.search}`);
  await expect(page).toHaveURL(/\/set-password$/);
  await page.getByLabel('Yeni şifre', { exact: true }).fill(NEW_PASSWORD);
  await page.getByLabel('Yeni şifre (tekrar)').fill(NEW_PASSWORD);
  await expectNoHorizontalScroll(page);
  await page.getByRole('button', { name: 'Şifreyi kaydet' }).click();
  await expect(page.getByRole('status')).toHaveText('Şifreniz belirlendi. Şimdi giriş yapabilirsiniz.');

  await login(page, email, NEW_PASSWORD);
  await expect(page.getByRole('heading', { name: 'Merhaba, Selin' })).toBeVisible();

  await Promise.all([admin.context.close(), context.close()]);
});
