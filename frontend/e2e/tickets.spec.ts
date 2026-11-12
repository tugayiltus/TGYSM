import { expect, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll, openCard } from './helpers';
import { adminSession, seedWorld, userSession } from './world';

const TICKET_URL = /\/tickets\/(INC|REQ)-\d{4}-\d{6}$/;

test('incident: IT claims, notes, replies and completes; employee sees the resolution but not the note', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  await openCard(employee.page, 'Bir şey çalışmıyor');
  await employee.page.getByLabel(/^Konu/).fill('Yazıcı çalışmıyor');
  await employee.page.getByLabel('Açıklama').fill('Kağıt sıkışıyor');
  await expectNoHorizontalScroll(employee.page);
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  const ticketUrl = employee.page.url();

  await it.page.goto(ticketUrl);
  await it.page.getByRole('button', { name: 'Üstlen' }).click();
  // Scoped to the page content: the header also holds the (phone-hidden) account name.
  await expect(it.page.getByRole('main').getByText('Can Demir').first()).toBeVisible();
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
  await chooseOption(dialog, 'Sonuç', 'Çözüldü');
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

  await openCard(employee.page, 'Bir şey istiyorum');
  await chooseOption(employee.page, /^Talep tipi/, world.requestTypeName);
  await expect(employee.page.getByText('Bu talep yöneticinizin onayından sonra işleme alınır.')).toBeVisible();
  await employee.page.getByLabel(/^Konu/).fill('Yeni dizüstü');
  await employee.page.getByLabel('Açıklama').fill('Göreve başlıyorum');
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  await expect(employee.page.getByText('Onay: Onay bekliyor')).toBeVisible();
  const number = employee.page.url().split('/').pop() ?? '';

  await admin.page.goto('/approvals');
  await admin.page.getByRole('button', { name: `Onayla ${number}` }).click();
  await admin.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(admin.page.getByRole('heading', { name: 'Onay bekleyen talep yok.' })).toBeVisible();

  await it.page.goto('/assets');
  await it.page.getByLabel('Demirbaş no, seri no veya model ara').fill(world.assetTag);
  await it.page.getByRole('link', { name: `Aç ${world.assetTag}` }).click();
  await it.page.getByRole('button', { name: 'Teslim et' }).click();
  const dialog = it.page.getByRole('dialog');
  await dialog.getByLabel('Kullanıcı ara').fill('Ayşe');
  await chooseOption(dialog, 'Kullanıcı', `Ayşe Kaya (${world.employeeEmail})`);
  await dialog.getByRole('button', { name: 'Onayla' }).click();
  await expect(dialog).toHaveCount(0);
  await expect(it.page.getByText('Zimmetli', { exact: true }).first()).toBeVisible();
  await expectNoHorizontalScroll(it.page);

  await employee.page.goto('/assets/mine');
  await expect(employee.page.getByText(world.assetTag)).toBeVisible();
  await expect(employee.page.getByText(world.assetName)).toBeVisible();
  await expectNoHorizontalScroll(employee.page);

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close()]);
});

test('a scanned device label opens a prefilled incident for employees and the device for IT', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);

  // The device is in stock, not assigned to the employee: a scan still names it (ADR-0028).
  await employee.page.goto(`/scan/asset/${world.assetId}`);
  await expect(employee.page).toHaveURL(/\/tickets\/new\/incident\?asset=/);
  await expect(employee.page.getByRole('combobox', { name: 'İlgili cihaz' })).toContainText(world.assetTag);
  await employee.page.getByLabel(/^Konu/).fill('Ekran titriyor');
  await employee.page.getByLabel(/^Açıklama/).fill('Okutulan cihazda ekran titriyor.');
  await employee.page.getByRole('button', { name: 'Gönder' }).click();
  await expect(employee.page).toHaveURL(TICKET_URL);
  await expect(employee.page.getByText(world.assetTag).first()).toBeVisible();

  await it.page.goto(`/scan/asset/${world.assetId}`);
  await expect(it.page).toHaveURL(new RegExp(`/assets/${world.assetId}$`));
  await expect(it.page.getByRole('region', { name: 'QR kod' }).getByRole('img', { name: `${world.assetTag} etiketi` })).toBeVisible();
});

/** 96 CSS pixels per inch, 25.4 mm per inch. */
const PX_PER_MM = 96 / 25.4;
const LABEL_MM = { width: 50, height: 30 } as const;
const SIZE_TOLERANCE_PX = 2;

test('the label prints at 50×30 mm with the whole drawing, and the preview keeps its shape', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const page = admin.page;

  await page.goto(`/assets/${world.assetId}`);
  const preview = page.getByRole('region', { name: 'QR kod' }).getByRole('img', { name: `${world.assetTag} etiketi` });
  await expect(preview).toBeVisible();
  const previewBox = await preview.boundingBox();
  expect((previewBox?.width ?? 0) / (previewBox?.height ?? 1)).toBeCloseTo(LABEL_MM.width / LABEL_MM.height, 1);

  await page.addInitScript(() => {
    window.print = () => undefined;
  });
  await page.goto(`/assets/labels?ids=${world.assetId}`);
  const label = page.getByRole('img', { name: `${world.assetTag} etiketi` });
  await expect(label).toBeVisible();
  // The drawing is the whole label, not a crop: same 5:3 shape as the paper.
  const natural = await label.evaluate((img: HTMLImageElement) => ({ width: img.naturalWidth, height: img.naturalHeight }));
  expect(natural.width / natural.height).toBeCloseTo(LABEL_MM.width / LABEL_MM.height, 1);
  await page.emulateMedia({ media: 'print' });
  const box = await label.boundingBox();
  expect(Math.abs((box?.width ?? 0) - LABEL_MM.width * PX_PER_MM)).toBeLessThanOrEqual(SIZE_TOLERANCE_PX);
  expect(Math.abs((box?.height ?? 0) - LABEL_MM.height * PX_PER_MM)).toBeLessThanOrEqual(SIZE_TOLERANCE_PX);
});
