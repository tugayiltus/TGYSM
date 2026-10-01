import { expect, test } from '@playwright/test';

import { adminSession, seedWorld, userSession, waitForEmail } from './world';

const TICKET_URL = /\/tickets\/(INC|REQ)-\d{4}-\d{6}$/;
const RESOLUTION = 'Toner değiştirildi';
const INTERNAL_NOTE = 'Tedarikçiden toner istendi';

test('completing a ticket emails the requester the resolution and never the internal note', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  await employee.page.getByRole('link', { name: /Bir şey çalışmıyor/ }).click();
  await employee.page.getByLabel('Konu', { exact: true }).fill('Yazıcı çalışmıyor');
  await employee.page.getByLabel('Açıklama').fill('Kağıt sıkışıyor');
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  const number = employee.page.url().split('/').pop() ?? '';

  await it.page.goto(employee.page.url());
  await it.page.getByRole('button', { name: 'Üstlen' }).click();
  const box = it.page.getByRole('form', { name: 'Yazışmalar' });
  await box.getByRole('tab', { name: 'İç not' }).click();
  await box.getByLabel('Mesajınız').fill(INTERNAL_NOTE);
  await box.getByRole('button', { name: 'Gönder' }).click();
  await expect(it.page.getByText(INTERNAL_NOTE)).toBeVisible();
  // The note refreshes the ticket version; opening the dialog before that refetch finishes sends a stale version.
  await it.page.waitForLoadState('networkidle');
  await it.page.getByRole('button', { name: 'Tamamla' }).click();
  const dialog = it.page.getByRole('dialog');
  await dialog.getByLabel('Çözüm (kullanıcı görür)').fill(RESOLUTION);
  await dialog.getByLabel('Sonuç').selectOption({ label: 'Çözüldü' });
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(it.page.getByText('Tamamlandı').first()).toBeVisible();

  const email = await waitForEmail(world.employeeEmail, `Çözüm: ${RESOLUTION}`);
  expect(email.subject).toContain(number);
  expect(email.subject).toContain('tamamlandı');
  expect(email.body).toContain(`Çözüm: ${RESOLUTION}`);
  expect(email.body).not.toContain(INTERNAL_NOTE);

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close()]);
});
