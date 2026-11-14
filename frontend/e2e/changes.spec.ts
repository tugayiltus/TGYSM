import { expect, test, type Locator, type Page } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll } from './helpers';
import { adminSession, seedChange, seedChangeTask, seedSecondAdmin, seedSecondTeam, seedWorld, submitChange, userSession, type Session } from './world';

const CHANGE_URL = /\/changes\/CHG-\d{4}-\d{6}$/;
const TASK_URL = /\/tasks\/TSK-\d{4}-\d{6}$/;
const WINDOW_START_DATE = '2030-03-01';
const WINDOW_START_TIME = '22:00';
const WINDOW_END_DATE = '2030-03-02';
const WINDOW_END_TIME = '01:00';
const STAGE_TIMEOUT_MS = 120_000;

/** Touch viewports render native date and time inputs (ISO date); pointer viewports render typed Astryx fields (dd.MM.yyyy). */
async function fillDateTime(scope: Locator, label: string, dateIso: string, time: string): Promise<void> {
  const typed = scope.getByRole('combobox', { name: label, exact: true });
  if ((await typed.count()) > 0) {
    const [year, month, day] = dateIso.split('-');
    await typed.fill(`${day ?? ''}.${month ?? ''}.${year ?? ''}`);
  } else {
    await scope.getByRole('textbox', { name: label, exact: true }).fill(dateIso);
  }

  await scope.getByRole('textbox', { name: `${label} saati` }).fill(time);
}

/** Opens one task of a change phase from the change page and returns the task's path. */
async function addPhaseTask(page: Page, phase: string, title: string, team: string): Promise<string> {
  await page.getByRole('button', { name: `Görev ekle ${phase}` }).click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Başlık').fill(title);
  await chooseOption(form, 'Ekip', team);
  await form.getByRole('button', { name: 'Görevi aç' }).click();
  await expect(form).toBeHidden();
  const link = page.getByRole('link', { name: new RegExp(title) });
  await expect(link).toBeVisible();
  return (await link.getAttribute('href')) ?? '';
}

/** Claims, starts and completes a task the session's team owns. */
async function workTask(session: Session, taskPath: string, note: string): Promise<void> {
  const { page } = session;
  await page.goto(taskPath);
  await expect(page).toHaveURL(TASK_URL);
  await page.getByRole('button', { name: 'Üstlen' }).click();
  await page.getByRole('button', { name: 'Başla' }).click();
  await expect(page.getByText('İşlemde').first()).toBeVisible();
  await page.getByRole('button', { name: 'Tamamla' }).click();
  const complete = page.getByRole('dialog');
  await complete.getByLabel('Sonuç notu').fill(note);
  await complete.getByRole('button', { name: 'Onayla' }).click();
  await expect(page.getByText('Tamamlandı').first()).toBeVisible();
  await expectNoHorizontalScroll(page);
}

test('change: planned by one team, approved by a second admin, implemented across two teams and closed', async ({ browser }) => {
  test.setTimeout(STAGE_TIMEOUT_MS * 2);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const teamB = await seedSecondTeam(admin);
  const admin2Seed = await seedSecondAdmin(admin);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const memberB = await userSession(browser, teamB.email, teamB.password);
  const approver = await userSession(browser, admin2Seed.email, admin2Seed.password);
  const { page } = owner;

  await page.goto('/changes');
  await page.getByRole('button', { name: 'Yeni değişiklik' }).or(page.getByRole('link', { name: 'Yeni değişiklik' })).first().click();
  const form = page.getByRole('dialog');
  await form.getByLabel('Başlık').fill('Dosya sunucusu güncellemesi');
  await form.getByLabel('Gerekçe').fill('Güvenlik yaması');
  await chooseOption(form, 'Sahip ekip', world.teamName);
  await fillDateTime(form, 'Pencere başlangıcı', WINDOW_START_DATE, WINDOW_START_TIME);
  await fillDateTime(form, 'Pencere bitişi', WINDOW_END_DATE, WINDOW_END_TIME);
  await form.getByLabel('Uygulama planı').fill('Yamayı kur ve sunucuyu yeniden başlat');
  await form.getByLabel('Doğrulama planı').fill('Paylaşımlara eriş');
  await form.getByLabel('Geri dönüş planı').fill('Önceki sürüme dön');
  await form.getByLabel('Varlık ara').fill(world.assetTag);
  await form.getByRole('combobox', { name: 'Varlık ekle' }).click();
  await page.getByRole('option', { name: new RegExp(world.assetTag) }).click();
  await form.getByRole('button', { name: 'Değişikliği oluştur' }).click();
  await expect(page).toHaveURL(CHANGE_URL);
  const changePath = new URL(page.url()).pathname;

  const implementationTask = await addPhaseTask(page, 'Uygulama', 'Veritabanını durdur', teamB.teamName);
  const verificationTask = await addPhaseTask(page, 'Doğrulama', 'Paylaşımları denetle', world.teamName);
  await page.getByRole('button', { name: 'Onaya gönder' }).click();
  await expect(page.getByText('Onay bekliyor').first()).toBeVisible();

  await approver.page.goto('/approvals');
  await approver.page.getByRole('button', { name: /^Onayla CHG-/ }).first().click();
  await approver.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(approver.page.getByRole('button', { name: /^Onayla CHG-/ })).toHaveCount(0);

  await page.goto(changePath);
  await expect(page.getByText('Planlandı').first()).toBeVisible();
  await page.getByRole('button', { name: 'Uygulamaya başla' }).click();
  await expect(page.getByText('Uygulanıyor').first()).toBeVisible();
  await expectNoHorizontalScroll(page);

  await workTask(memberB, implementationTask, 'Veritabanı durduruldu');

  await page.goto(changePath);
  await page.getByRole('button', { name: 'Uygulama bitti' }).click();
  await expect(page.getByRole('button', { name: 'Kapat' })).toBeVisible();
  await workTask(owner, verificationTask, 'Paylaşımlar erişilebilir');

  await page.goto(changePath);
  await page.getByRole('button', { name: 'Kapat' }).click();
  const close = page.getByRole('dialog');
  await chooseOption(close, 'Sonuç', 'Başarılı');
  await close.getByLabel('Doğrulama notu').fill('Tüm paylaşımlar açıldı');
  await close.getByRole('button', { name: 'Onayla' }).click();
  await expect(page.getByText('Kapalı').first()).toBeVisible();
  await expect(page.getByText('Tüm paylaşımlar açıldı')).toBeVisible();
  await expectNoHorizontalScroll(page);

  await Promise.all([admin.context.close(), owner.context.close(), memberB.context.close(), approver.context.close()]);
});

test('change: editing the window of a planned change warns and sends it back to approval', async ({ browser }) => {
  test.setTimeout(STAGE_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const admin2Seed = await seedSecondAdmin(admin);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const approver = await userSession(browser, admin2Seed.email, admin2Seed.password);
  const number = await seedChange(owner, world, 'Yedekleme sunucusu taşıma', null);
  await seedChangeTask(owner, number, 'implementation', 'Sunucuyu taşı', world.teamId);
  await submitChange(owner, number);
  await approver.page.goto('/approvals');
  await approver.page.getByRole('button', { name: `Onayla ${number}` }).click();
  await approver.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(approver.page.getByRole('button', { name: `Onayla ${number}` })).toHaveCount(0);

  const { page } = owner;
  await page.goto(`/changes/${number}`);
  await expect(page.getByText('Planlandı').first()).toBeVisible();
  await page.getByRole('button', { name: 'Düzenle' }).click();
  const form = page.getByRole('dialog');
  await fillDateTime(form, 'Pencere bitişi', '2030-03-03', '02:00');
  await form.getByRole('button', { name: 'Kaydet' }).click();
  const warning = page.getByRole('dialog', { name: 'Onay geçersiz olacak' });
  await expect(warning.getByText('Bu değişiklik onayı geçersiz kılar.', { exact: false })).toBeVisible();
  await warning.getByRole('button', { name: 'Yine de kaydet' }).click();
  await expect(page.getByText('Onay bekliyor').first()).toBeVisible();
  await expectNoHorizontalScroll(page);

  // Approve again so no pending approval stays in the shared E2E database for other specs.
  await approver.page.goto('/approvals');
  await approver.page.getByRole('button', { name: `Onayla ${number}` }).click();
  await approver.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(approver.page.getByRole('button', { name: `Onayla ${number}` })).toHaveCount(0);

  await Promise.all([admin.context.close(), owner.context.close(), approver.context.close()]);
});
