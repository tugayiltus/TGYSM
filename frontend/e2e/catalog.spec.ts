import { expect, test } from '@playwright/test';

import { chooseFirstOption, chooseOption, expectNoHorizontalScroll, visibleEntry } from './helpers';
import { adminSession, seedBrandWithModel, seedWorld, userSession } from './world';

const GENERATED_TAG = /^Cihaz kaydedildi: ASSET-\d{4}-\d{4,}$/;

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
  await chooseOption(modelDialog, 'Marka', brand);
  await chooseOption(modelDialog, 'Tür', 'Dizüstü');
  await modelDialog.getByLabel('Ad').fill('Latitude 5440');
  await modelDialog.getByRole('button', { name: 'Oluştur' }).click();
  await expect(visibleEntry(admin.page, 'Latitude 5440')).toBeVisible();
  await expectNoHorizontalScroll(admin.page);

  await it.page.goto('/assets');
  await it.page.getByRole('button', { name: 'Yeni cihaz' }).click();
  const form = it.page.getByRole('dialog');
  await expect(form.getByText('Demirbaş numarası kaydedildiğinde otomatik atanır.')).toBeVisible();
  await chooseOption(form, 'Tür', 'Dizüstü');
  await chooseOption(form, 'Marka', brand);
  await chooseOption(form, 'Model', 'Latitude 5440');
  await chooseFirstOption(form, 'Lokasyon');
  await form.getByRole('button', { name: 'Oluştur' }).click();
  // Astryx also copies the toast text into its live region, so take the visible toast.
  await expect(it.page.getByText(GENERATED_TAG).first()).toBeVisible();
  // The inventory list with a device, its filters and paging stays inside a phone screen.
  await expect(visibleEntry(it.page, world.assetTag)).toBeVisible();
  await expectNoHorizontalScroll(it.page);
});

test('the asset form keeps type, brand and model in order and switches models with the brand', async ({ browser }) => {
  const admin = await adminSession(browser);
  const first = await seedBrandWithModel(admin, 'Acer', 'desktop', 'Veriton X');
  const second = await seedBrandWithModel(admin, 'HP', 'desktop', 'ProDesk 400');
  const page = admin.page;

  await page.goto('/assets');
  await page.getByRole('button', { name: 'Yeni cihaz' }).click();
  const form = page.getByRole('dialog');
  await chooseOption(form, 'Tür', 'Masaüstü');
  await chooseOption(form, 'Marka', first);
  await chooseOption(form, 'Model', 'Veriton X');
  await chooseOption(form, 'Marka', second);
  await expect(form.getByRole('combobox', { name: 'Model' })).toContainText('Model seçin');
  await form.getByRole('combobox', { name: 'Model' }).click();
  await expect(page.getByRole('option', { name: 'ProDesk 400' })).toBeVisible();
  await expect(page.getByRole('option', { name: 'Veriton X' })).toHaveCount(0);
});
