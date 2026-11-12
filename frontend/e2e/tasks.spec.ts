import { expect, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll, openCard } from './helpers';
import { adminSession, seedIncidents, seedSecondTeam, seedWorld, userSession } from './world';

const TASK_URL = /\/tasks\/TSK-\d{4}-\d{6}$/;

test('task: IT hands work from an incident to another team, which claims, starts and completes it without seeing the incident', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const dbTeam = await seedSecondTeam(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const it = await userSession(browser, world.itEmail, world.itPassword);
  const db = await userSession(browser, dbTeam.email, dbTeam.password);
  const [incidentNumber] = await seedIncidents(employee, ['Ağ kesintisi']);
  const incidentPath = `/tickets/${incidentNumber}`;

  await it.page.goto(incidentPath);
  await it.page.getByRole('button', { name: 'Görev ekle' }).click();
  const form = it.page.getByRole('dialog');
  await form.getByLabel('Başlık').fill('Veritabanı yedeği al');
  await chooseOption(form, 'Ekip', dbTeam.teamName);
  await form.getByRole('button', { name: 'Görevi aç' }).click();
  await expect(it.page).toHaveURL(TASK_URL);
  const taskPath = new URL(it.page.url()).pathname;
  await expect(it.page.getByRole('link', { name: new RegExp(incidentNumber) })).toBeVisible();

  await db.page.goto('/workspace');
  await openCard(db.page, 'Ekibimin görevleri');
  await expect(db.page).toHaveURL(/\/tasks\?queue=team/);
  await db.page.goto(taskPath);
  await expect(db.page.getByText('Bağlı kayıt: erişiminiz yok')).toBeVisible();
  await db.page.getByRole('button', { name: 'Üstlen' }).click();
  await db.page.getByRole('button', { name: 'Başla' }).click();
  await expect(db.page.getByText('İşlemde').first()).toBeVisible();
  await db.page.getByRole('button', { name: 'Tamamla' }).click();
  const complete = db.page.getByRole('dialog');
  await complete.getByLabel('Sonuç notu').fill('Yedek 02:00 itibarıyla alındı');
  await complete.getByRole('button', { name: 'Onayla' }).click();
  await expect(db.page.getByText('Tamamlandı').first()).toBeVisible();
  await expect(db.page.getByText('Yedek 02:00 itibarıyla alındı')).toBeVisible();
  await expectNoHorizontalScroll(db.page);

  await db.page.goto(incidentPath);
  await expect(db.page.getByText('Kayıt bulunamadı.')).toBeVisible();
  await expect(db.page.getByRole('heading', { name: 'Ağ kesintisi' })).toHaveCount(0);

  await it.page.goto(incidentPath);
  const tasks = it.page.getByRole('region', { name: 'Görevler' });
  await expect(tasks.getByText('Tamamlandı')).toBeVisible();
  // The incident is untouched: still open and unassigned, so it can still be claimed.
  await expect(it.page.getByRole('button', { name: 'Üstlen' })).toBeVisible();
  await expectNoHorizontalScroll(it.page);

  await Promise.all([admin.context.close(), employee.context.close(), it.context.close(), db.context.close()]);
});
