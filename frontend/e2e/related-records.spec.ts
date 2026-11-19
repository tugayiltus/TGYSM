import { expect, test, type Page } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll, visibleEntry } from './helpers';
import {
  adminSession, closeChangeSuccessfully, seedChange, seedChangeTask, seedIncidents, seedSecondAdmin, seedSecondTeam, seedWorld, submitChange,
  transferTicket, userSession,
} from './world';

const RELATED_TEST_TIMEOUT_MS = 240_000;
const SEARCH_BUTTON = 'Ara';
const LINK_KIND_CAUSED = 'Değişiklik sonucu oluştu';
const NOT_OPENABLE_TEXT = 'ekibinin kaydı';

async function openSearch(page: Page): Promise<void> {
  await page.getByRole('button', { name: SEARCH_BUTTON, exact: true }).first().click();
}

test('related records: link, desk view, report drill-down and central search', async ({ browser }) => {
  test.setTimeout(RELATED_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const desk = await seedSecondTeam(admin);
  const admin2 = await seedSecondAdmin(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const deskStaff = await userSession(browser, desk.email, desk.password);
  const approver = await userSession(browser, admin2.email, admin2.password);

  const suffix = Date.now().toString();
  const changeTitle = `Yedekleme${suffix}`;
  const [ticket = ''] = await seedIncidents(employee, [`Paylaşım erişilemiyor ${suffix}`]);
  await transferTicket(owner, ticket, desk.teamId);
  const change = await seedChange(owner, world, changeTitle, null, { affectedService: `Hizmet ${suffix}` });
  const task = await seedChangeTask(owner, change, 'implementation', 'Bakımı yap', world.teamId);

  // The owner team links the desk team's incident as caused by the change.
  await owner.page.goto(`/changes/${change}`);
  await owner.page.getByRole('button', { name: 'Kayıt bağla' }).click();
  const dialog = owner.page.getByRole('dialog');
  await dialog.getByLabel('Kayıt numarası').fill(ticket);
  await chooseOption(dialog, 'Bağlantı türü', LINK_KIND_CAUSED);
  await dialog.getByRole('button', { name: 'Bağla' }).click();
  await expect(owner.page.getByText(`${ticket} · `)).toBeVisible();
  await expectNoHorizontalScroll(owner.page);

  // Desk staff see the change on the incident, but cannot open it.
  await deskStaff.page.goto(`/tickets/${ticket}`);
  await expect(deskStaff.page.getByRole('heading', { name: 'İlgili değişiklikler' })).toBeVisible();
  await expect(deskStaff.page.getByText(`${change} · ${changeTitle}`)).toBeVisible();
  await expect(deskStaff.page.getByText(new RegExp(NOT_OPENABLE_TEXT))).toBeVisible();
  await expect(deskStaff.page.getByRole('link', { name: new RegExp(change) })).toHaveCount(0);
  await expectNoHorizontalScroll(deskStaff.page);

  // Closing the change counts the incident in the report.
  await submitChange(owner, change);
  await approver.page.goto('/approvals');
  await approver.page.getByRole('button', { name: `Onayla ${change}` }).click();
  await approver.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(approver.page.getByRole('button', { name: `Onayla ${change}` })).toHaveCount(0);
  await closeChangeSuccessfully(owner, change, task);

  await admin.page.goto(`/reports/changes?teamId=${world.teamId}`);
  const caused = admin.page.getByRole('article', { name: 'Sorun çıkaran değişiklikler', exact: true });
  await expect(caused).toHaveAccessibleDescription('1');
  await caused.getByRole('link', { name: 'Sorun çıkaran değişiklikler' }).click();
  await expect(admin.page).toHaveURL(/\/reports\/changes\/list\?/);
  await expect(visibleEntry(admin.page, change)).toBeVisible();
  await expectNoHorizontalScroll(admin.page);

  // Central search: a title word finds the change group; a full ticket number opens the ticket.
  await owner.page.goto('/workspace');
  await openSearch(owner.page);
  await owner.page.keyboard.type(changeTitle);
  await expect(owner.page.getByText(`${change} ${changeTitle}`)).toBeVisible();
  await expect(owner.page.getByRole('dialog', { name: SEARCH_BUTTON }).getByText('Değişiklikler', { exact: true })).toBeVisible();
  await expectNoHorizontalScroll(owner.page);
  await owner.page.keyboard.press('Escape');

  await deskStaff.page.goto('/workspace');
  await openSearch(deskStaff.page);
  await deskStaff.page.keyboard.type(ticket);
  await expect(deskStaff.page).toHaveURL(new RegExp(`/tickets/${ticket}`));

  await Promise.all([admin.context.close(), employee.context.close(), owner.context.close(), deskStaff.context.close(), approver.context.close()]);
});
