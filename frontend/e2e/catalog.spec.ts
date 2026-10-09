import { expect, test } from '@playwright/test';

import { expectNoHorizontalScroll, visibleEntry } from './helpers';
import { adminSession, seedWorld, userSession } from './world';

const GENERATED_TAG = /^Cihaz kaydedildi: DMR-\d{6,}$/;

test('admin adds a brand and model; IT registers a device with them and gets a generated tag', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const it = await userSession(browser, world.itEmail, world.itPassword);
  const brand = `Dell ${Date.now().toString()}`;

  await admin.page.goto('/admin/brands');
  await admin.page.getByRole('button', { name: 'Yeni marka' }).click();
  await admin.page.getByRole('dialog').getByLabel('Ad').fill(brand);
  await admin.page.getByRole('dialog').getByRole('button', { name: 'Oluştur' }).click();
  await expect(visibleEntry(admin.page, brand)).toBeVisible();

  await admin.page.goto('/admin/models');
  await admin.page.getByRole('button', { name: 'Yeni model' }).click();
  const modelDialog = admin.page.getByRole('dialog');
  await modelDialog.getByLabel('Marka').selectOption({ label: brand });
  await modelDialog.getByLabel('Tür').selectOption({ label: 'Dizüstü' });
  await modelDialog.getByLabel('Ad').fill('Latitude 5440');
  await modelDialog.getByRole('button', { name: 'Oluştur' }).click();
  await expect(visibleEntry(admin.page, 'Latitude 5440')).toBeVisible();
  await expectNoHorizontalScroll(admin.page);

  await it.page.goto('/assets');
  await it.page.getByRole('button', { name: 'Yeni cihaz' }).click();
  const form = it.page.getByRole('dialog');
  await expect(form.getByText('Demirbaş numarası kaydedildiğinde otomatik atanır.')).toBeVisible();
  await form.getByLabel('Tür').selectOption({ label: 'Dizüstü' });
  await form.getByRole('combobox', { name: 'Marka' }).fill(brand.slice(0, 4));
  await form.getByRole('option', { name: brand }).click();
  await form.getByLabel('Model').selectOption({ label: 'Latitude 5440' });
  await form.getByLabel('Lokasyon').selectOption({ index: 1 });
  await form.getByRole('button', { name: 'Oluştur' }).click();
  await expect(it.page.getByRole('status', { name: 'Bildirimler' })).toHaveText(GENERATED_TAG);
});
