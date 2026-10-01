import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll } from './helpers';
import { adminSession, seedWorld, userSession } from './world';

const TICKET_URL = /\/tickets\/(INC|REQ)-\d{4}-\d{6}$/;
const QUEUE_COUNT = /^\d+ kayıt$/;

// A fresh ticket is never warned and the product has no test-only way to make it so, so the queue count is only
// checked for shape (the warned/breached membership is covered by the backend SLA monitor tests).
test('a new ticket shows both SLA timers and the IT workspace has an SLA Riski card that opens its queue', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  await employee.page.getByRole('link', { name: /Bir şey çalışmıyor/ }).click();
  await employee.page.getByLabel('Konu', { exact: true }).fill('Ağ bağlantısı yok');
  await employee.page.getByLabel('Açıklama').fill('Kablo takılı ama bağlanmıyor');
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  await expect(employee.page.getByText('İlk yanıt:').first()).toBeVisible();
  await expect(employee.page.getByText('Çözüm:').first()).toBeVisible();

  await it.page.goto(employee.page.url());
  await expect(it.page.getByText('İlk yanıt:').first()).toBeVisible();
  await expect(it.page.locator('[data-sla-state="running"]').first()).toBeVisible();
  await expectNoHorizontalScroll(it.page);

  await it.page.goto('/workspace');
  const card = it.page.getByRole('link', { name: /SLA Riski/ });
  await expect(card).toBeVisible();
  await expect(card.getByText(QUEUE_COUNT)).toBeVisible();
  await expectNoHorizontalScroll(it.page);
  await card.click();
  await expect(it.page).toHaveURL(/\/workspace\/queue\?queue=sla_risk$/);
  await expect(it.page.getByRole('heading', { name: 'SLA Riski' })).toBeVisible();

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close()]);
});
