import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll } from './helpers';
import { adminSession, seedWorld, userSession } from './world';

const TICKET_URL = /\/tickets\/(INC|REQ)-\d{4}-\d{6}$/;

test('incident: IT claims, notes, replies and completes; employee sees the resolution but not the note', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  await employee.page.getByRole('link', { name: /Bir şey çalışmıyor/ }).click();
  await employee.page.getByLabel('Konu', { exact: true }).fill('Yazıcı çalışmıyor');
  await employee.page.getByLabel('Açıklama').fill('Kağıt sıkışıyor');
  await expectNoHorizontalScroll(employee.page);
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  const ticketUrl = employee.page.url();

  await it.page.goto(ticketUrl);
  await it.page.getByRole('button', { name: 'Üstlen' }).click();
  await expect(it.page.getByText('Can Demir').first()).toBeVisible();
  const box = it.page.getByRole('form', { name: 'Yazışmalar' });
  await box.getByRole('tab', { name: 'İç not' }).click();
  await box.getByLabel('Mesajınız').fill('Tedarikçiden toner istendi');
  await box.getByRole('button', { name: 'Gönder' }).click();
  await expect(it.page.getByText('Tedarikçiden toner istendi')).toBeVisible();
  await box.getByRole('tab', { name: 'Yanıt' }).click();
  await box.getByLabel('Mesajınız').fill('Toneri değiştiriyoruz');
  await box.getByRole('button', { name: 'Gönder' }).click();
  await expect(it.page.getByText('Toneri değiştiriyoruz')).toBeVisible();
  await it.page.getByRole('button', { name: 'Tamamla' }).click();
  const dialog = it.page.getByRole('dialog');
  await dialog.getByLabel('Çözüm (kullanıcı görür)').fill('Toner değiştirildi');
  await dialog.getByLabel('Sonuç').selectOption({ label: 'Çözüldü' });
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(it.page.getByText('Tamamlandı').first()).toBeVisible();
  await expectNoHorizontalScroll(it.page);

  await employee.page.reload();
  await expect(employee.page.getByText('Tamamlandı').first()).toBeVisible();
  await expect(employee.page.getByText('Toner değiştirildi')).toBeVisible();
  await expect(employee.page.getByText('Toneri değiştiriyoruz')).toBeVisible();
  await expect(employee.page.getByText('Tedarikçiden toner istendi')).toHaveCount(0);
  await expectNoHorizontalScroll(employee.page);

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close()]);
});

test('request: admin approves, IT hands over a device, employee sees it under Zimmetlerim', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  await employee.page.getByRole('link', { name: /Bir şey istiyorum/ }).click();
  await employee.page.getByLabel('Talep tipi').selectOption({ label: world.requestTypeName });
  await expect(employee.page.getByText('Bu talep yöneticinizin onayından sonra işleme alınır.')).toBeVisible();
  await employee.page.getByLabel('Konu', { exact: true }).fill('Yeni dizüstü');
  await employee.page.getByLabel('Açıklama').fill('Göreve başlıyorum');
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  await expect(employee.page.getByText('Onay: Onay bekliyor')).toBeVisible();
  const number = employee.page.url().split('/').pop() ?? '';

  await admin.page.goto('/approvals');
  await admin.page.getByRole('button', { name: `Onayla ${number}` }).click();
  await admin.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(admin.page.getByText('Onay bekleyen talep yok.')).toBeVisible();

  await it.page.goto('/assets');
  await it.page.getByLabel('Demirbaş no, seri no veya model ara').fill(world.assetTag);
  await it.page.getByRole('link', { name: `Aç ${world.assetTag}` }).click();
  await it.page.getByRole('button', { name: 'Teslim et' }).click();
  const dialog = it.page.getByRole('dialog');
  await dialog.getByLabel('Kullanıcı ara').fill('Ayşe');
  await dialog.getByLabel('Kullanıcı', { exact: true }).selectOption({ label: `Ayşe Kaya (${world.employeeEmail})` });
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(dialog).toHaveCount(0);
  await expect(it.page.getByText('Zimmetli', { exact: true }).first()).toBeVisible();
  await expectNoHorizontalScroll(it.page);

  await employee.page.goto('/assets/mine');
  await expect(employee.page.getByText(world.assetTag)).toBeVisible();
  await expect(employee.page.getByText('Lenovo T14')).toBeVisible();
  await expectNoHorizontalScroll(employee.page);

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close()]);
});
