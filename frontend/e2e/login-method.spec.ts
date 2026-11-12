import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll } from './helpers';
import { adminSession } from './world';

test('admin saves Active Directory settings; the method cannot be switched on untested and login stays local', async ({ browser }) => {
  const admin = await adminSession(browser);
  const page = admin.page;

  await page.goto('/admin/login-method');
  const ldap = page.getByRole('region', { name: 'Active Directory (LDAP)' });
  await ldap.getByLabel(/^Sunucu/).fill('dc1.sirket.local');
  await ldap.getByLabel(/^Arama kökü/).fill('DC=sirket,DC=local');
  await ldap.getByLabel(/^Servis hesabı DN/).fill('CN=svc,DC=sirket,DC=local');
  await ldap.getByLabel(/^Servis hesabı şifresi/).fill('svc-şifre');
  await ldap.getByRole('button', { name: 'Kaydet' }).click();

  await expect(ldap.getByText('Kayıtlı. Değiştirmek için yeni değeri yazın.')).toBeVisible();
  await expect(ldap.getByLabel(/^Servis hesabı şifresi/)).toHaveValue('');
  await expect(ldap.getByRole('button', { name: 'Bu yöntemi aç' })).toHaveAttribute('aria-disabled', 'true');
  await expectNoHorizontalScroll(page);

  const visitor = await browser.newContext();
  const login = await visitor.newPage();
  await login.goto('/login');
  await expect(login.getByLabel('E-posta')).toBeVisible();
  await expect(login.getByRole('button', { name: 'Microsoft ile giriş yap' })).toHaveCount(0);
  await visitor.close();
});
