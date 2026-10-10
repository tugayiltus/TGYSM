import { expect, test, type Locator, type Page } from '@playwright/test';

import { adminSession, seedWorld, userSession } from './world';

/**
 * Acceptance criterion 16: the main flows work with the keyboard only. Every control is reached with Tab and used with
 * Enter, Space, arrows or typing; no step clicks or fills.
 */

const TICKET_URL = /\/tickets\/INC-\d{4}-\d{6}$/;
/** Enough presses to cross the header and side menu of any page. */
const MAX_TABS = 80;
const REQUIRED_ERROR = 'Bu alan zorunludur.';
const SUBJECT = 'Klavye ile açılan kayıt';
const DESCRIPTION = 'Fare kullanılmadı';
const REPLY = 'Klavye ile yanıt verildi';

/** Presses Tab until `target` has the focus; fails when it never comes. */
async function tabTo(page: Page, target: Locator): Promise<void> {
  await expect(target).toBeVisible();
  for (let presses = 0; presses < MAX_TABS; presses++) {
    if (await target.evaluate((element) => element === document.activeElement)) {
      return;
    }
    await page.keyboard.press('Tab');
  }
  throw new Error(`Tab never reached ${target.toString()}`);
}

test('keyboard only: employee opens an incident after fixing the form errors; IT claims and replies; admin creates a user', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  const page = employee.page;
  await tabTo(page, page.getByRole('link', { name: /Bir şey çalışmıyor/ }));
  await page.keyboard.press('Enter');
  const subject = page.getByLabel('Konu', { exact: true });
  await tabTo(page, page.getByRole('button', { name: 'Gönder' }));
  await page.keyboard.press('Enter');
  await expect(page.getByText(REQUIRED_ERROR)).toHaveCount(2);
  await expect(subject).toBeFocused();
  await page.keyboard.type(SUBJECT);
  await tabTo(page, page.getByLabel('Açıklama'));
  await page.keyboard.type(DESCRIPTION);
  await tabTo(page, page.getByRole('button', { name: 'Gönder' }));
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(TICKET_URL);

  await it.page.goto(page.url());
  await tabTo(it.page, it.page.getByRole('button', { name: 'Üstlen' }));
  await it.page.keyboard.press('Enter');
  await expect(it.page.getByRole('main').getByText('Can Demir').first()).toBeVisible();
  const box = it.page.getByRole('form', { name: 'Yazışmalar' });
  await expect(box.getByRole('tab', { name: 'Yanıt' })).toHaveAttribute('aria-selected', 'true');
  await tabTo(it.page, box.getByLabel('Mesajınız'));
  await it.page.keyboard.type(REPLY);
  await tabTo(it.page, box.getByRole('button', { name: 'Gönder' }));
  await it.page.keyboard.press('Enter');
  await expect(it.page.getByText(REPLY)).toBeVisible();

  const adminPage = admin.page;
  const userEmail = `klavye-${Date.now()}@example.com`;
  await adminPage.goto('/admin/users');
  await tabTo(adminPage, adminPage.getByRole('button', { name: 'Yeni kullanıcı' }));
  await adminPage.keyboard.press('Enter');
  const form = adminPage.getByRole('dialog');
  await tabTo(adminPage, form.getByLabel('Ad', { exact: true }));
  await adminPage.keyboard.type('Deniz');
  await tabTo(adminPage, form.getByLabel('Soyad'));
  await adminPage.keyboard.type('Klavye');
  await tabTo(adminPage, form.getByLabel('E-posta', { exact: true }));
  await adminPage.keyboard.type(userEmail);
  await tabTo(adminPage, form.getByRole('radio', { checked: true }));
  await adminPage.keyboard.press('ArrowDown');
  await expect(form.getByRole('radio', { name: 'Geçici şifre göster' })).toBeChecked();
  await tabTo(adminPage, form.getByRole('button', { name: 'Oluştur' }));
  await adminPage.keyboard.press('Enter');
  await expect(adminPage.getByTestId('temporary-password')).not.toBeEmpty();

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close()]);
});
