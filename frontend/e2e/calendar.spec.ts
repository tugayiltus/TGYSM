import { expect, test, type Page } from '@playwright/test';

import { expectNoHorizontalScroll, openCard, visibleEntry } from './helpers';
import {
  adminSession, closeChangeSuccessfully, seedChange, seedChangeTask, seedSecondAdmin, seedSecondTeam, seedWorld, submitChange, userSession, type Session,
} from './world';
import { readWorkbook } from './xlsx';

const CALENDAR_MONTH_URL = '/changes/calendar?date=2030-03-01';
const MOBILE_PROJECT = 'mobile-360';
const RECORDS_SHEET = 'Kayıtlar';
const HEADER_ROWS = 1;
const CALENDAR_TEST_TIMEOUT_MS = 180_000;
const MS_PER_HOUR = 3_600_000;
const THIS_WEEK_START_OFFSET_HOURS = 1;
const THIS_WEEK_LENGTH_HOURS = 1;
/** Company time is Europe/Istanbul, a fixed UTC+3 with no daylight saving. */
const COMPANY_UTC_OFFSET_HOURS = 3;
const DAYS_PER_WEEK = 7;
const MS_PER_DAY = 24 * 3_600_000;
const MONDAY_INDEX = 1;
const CONFLICT_TOKEN = 'Çakışma';
const CONFLICT_BANNER = 'Bakım penceresi 1 değişiklikle çakışıyor';

/**
 * A one-hour window that starts inside the current company week (Monday start), even in the last hour of Sunday: the
 * start is clamped so the window ends before the next Monday 00:00. The API accepts a start in the past.
 */
function thisWeekWindow(now: number): { windowStart: string; windowEnd: string } {
  const offsetMs = COMPANY_UTC_OFFSET_HOURS * MS_PER_HOUR;
  const companyNow = new Date(now + offsetMs);
  const daysSinceMonday = (companyNow.getUTCDay() + DAYS_PER_WEEK - MONDAY_INDEX) % DAYS_PER_WEEK;
  const mondayMs = Date.UTC(companyNow.getUTCFullYear(), companyNow.getUTCMonth(), companyNow.getUTCDate()) - daysSinceMonday * MS_PER_DAY - offsetMs;
  const lastStartMs = mondayMs + DAYS_PER_WEEK * MS_PER_DAY - THIS_WEEK_LENGTH_HOURS * MS_PER_HOUR - MS_PER_HOUR;
  const startMs = Math.min(now + THIS_WEEK_START_OFFSET_HOURS * MS_PER_HOUR, lastStartMs);
  return { windowStart: new Date(startMs).toISOString(), windowEnd: new Date(startMs + THIS_WEEK_LENGTH_HOURS * MS_PER_HOUR).toISOString() };
}

async function approve(approver: Session, number: string): Promise<void> {
  await approver.page.goto('/approvals');
  await approver.page.getByRole('button', { name: `Onayla ${number}` }).click();
  await approver.page.getByRole('dialog').getByRole('button', { name: 'Onayla' }).click();
  await expect(approver.page.getByRole('button', { name: `Onayla ${number}` })).toHaveCount(0);
}

async function expectCalendarEntries(page: Page, entries: readonly string[], project: string): Promise<void> {
  await page.goto(CALENDAR_MONTH_URL);
  const caption = project === MOBILE_PROJECT ? 'Gün gün planlı değişiklikler' : 'Bakım takvimi';
  await expect(page.getByRole('group', { name: caption })).toBeVisible();
  for (const entry of entries) {
    await expect(page.getByRole('link', { name: entry })).toBeVisible();
  }
  await expect(page.getByText(CONFLICT_TOKEN, { exact: true })).toHaveCount(entries.length);
  await expectNoHorizontalScroll(page);
}

test('calendar: two changes on one asset conflict, show on the calendar for their team only', async ({ browser }, info) => {
  test.setTimeout(CALENDAR_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const teamB = await seedSecondTeam(admin);
  const admin2Seed = await seedSecondAdmin(admin);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const outsider = await userSession(browser, teamB.email, teamB.password);
  const approver = await userSession(browser, admin2Seed.email, admin2Seed.password);

  const service = `Hizmet ${Date.now().toString()}`;
  const titles = ['Birinci bakım', 'İkinci bakım'];
  const numbers: string[] = [];
  for (const title of titles) {
    const number = await seedChange(owner, world, title, world.assetId, { affectedService: service });
    await seedChangeTask(owner, number, 'implementation', `${title} görevi`, world.teamId);
    await submitChange(owner, number);
    numbers.push(number);
  }

  await approver.page.goto('/approvals');
  await expect(approver.page.getByText('1 çakışma').first()).toBeVisible();
  for (const number of numbers) {
    await approve(approver, number);
  }

  for (const number of numbers) {
    await owner.page.goto(`/changes/${number}`);
    await expect(owner.page.getByText(CONFLICT_BANNER)).toBeVisible();
    await expectNoHorizontalScroll(owner.page);
  }

  const entries = numbers.map((number, index) => `${number} ${titles[index] ?? ''}`);
  await expectCalendarEntries(owner.page, entries, info.project.name);

  await outsider.page.goto(CALENDAR_MONTH_URL);
  await expect(outsider.page.getByRole('heading', { name: 'Bu ayda planlı değişiklik yok.' })).toBeVisible();
  for (const entry of entries) {
    await expect(outsider.page.getByRole('link', { name: entry })).toHaveCount(0);
  }

  await Promise.all([admin.context.close(), owner.context.close(), outsider.context.close(), approver.context.close()]);
});

test('calendar: İş listem "Bu hafta uygulanacak" opens the filtered list', async ({ browser }) => {
  test.setTimeout(CALENDAR_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const admin2Seed = await seedSecondAdmin(admin);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const approver = await userSession(browser, admin2Seed.email, admin2Seed.password);
  const now = Date.now();
  const number = await seedChange(owner, world, 'Bu haftaki bakım', null, {
    ...thisWeekWindow(now),
    affectedService: `Hizmet ${now.toString()}`,
  });
  await seedChangeTask(owner, number, 'implementation', 'Bakımı yap', world.teamId);
  await submitChange(owner, number);
  await approve(approver, number);

  await owner.page.goto('/workspace');
  await openCard(owner.page, 'Bu hafta uygulanacak');
  await expect(owner.page).toHaveURL(/\/changes\?queue=this_week$/);
  await expect(visibleEntry(owner.page, number)).toBeVisible();
  await expectNoHorizontalScroll(owner.page);

  await Promise.all([admin.context.close(), owner.context.close(), approver.context.close()]);
});

test('reports: a change closed as Başarılı is in the change report and its drill-down exports', async ({ browser }, testInfo) => {
  test.setTimeout(CALENDAR_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const admin2Seed = await seedSecondAdmin(admin);
  const owner = await userSession(browser, world.itEmail, world.itPassword);
  const approver = await userSession(browser, admin2Seed.email, admin2Seed.password);
  const number = await seedChange(owner, world, 'Raporlanacak bakım', null, { affectedService: `Hizmet ${Date.now().toString()}` });
  const task = await seedChangeTask(owner, number, 'implementation', 'Bakımı yap', world.teamId);
  await submitChange(owner, number);
  await approve(approver, number);
  await closeChangeSuccessfully(owner, number, task);

  const { page } = admin;
  // The team is unique to this world, so the figure counts only the change closed above.
  await page.goto(`/reports/changes?teamId=${world.teamId}`);
  const outcome = page.getByRole('article', { name: 'Sonuca göre kapanan değişiklikler', exact: true });
  await expect(outcome).toHaveAccessibleDescription('1');
  await expectNoHorizontalScroll(page);

  await outcome.getByRole('link', { name: 'Başarılı' }).click();
  await expect(page).toHaveURL(/\/reports\/changes\/list\?/);
  await expect(visibleEntry(page, number)).toBeVisible();
  await expectNoHorizontalScroll(page);

  const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: "Excel'e aktar" }).click()]);
  const path = testInfo.outputPath('changes.xlsx');
  await download.saveAs(path);
  const book = await readWorkbook(path);
  expect(book.rowCount(RECORDS_SHEET)).toBe(HEADER_ROWS + 1);

  await Promise.all([admin.context.close(), owner.context.close(), approver.context.close()]);
});
