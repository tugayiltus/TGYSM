import { expect, test } from '@playwright/test';

import { chooseOption, expectNoHorizontalScroll, visibleEntry } from './helpers';
import { adminSession, requestBackgroundExport, seedIncidents, seedWorld, userSession } from './world';
import { readWorkbook } from './xlsx';

const RECORDS_SHEET = 'Kayıtlar';
const FILTERS_SHEET = 'Filtreler';
const HEADER_ROWS = 1;
const REPORT = 'incidents';
const REPORT_PARAM = 'report';
const DRILLDOWN_URL = /\/reports\/incidents\/list\?/;
/** Exports are polled every 5 s and the E2E Worker runs every 2 s (tools/e2e.sh). */
const EXPORT_READY_TIMEOUT_MS = 30_000;
/** Seeding, a synchronous export and waiting up to EXPORT_READY_TIMEOUT_MS for the background one exceed the 30 s default. */
const REPORTS_TEST_TIMEOUT_MS = 90_000;
const REPORT_PATHS = ['incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty'] as const;
/** Two rows of fields with "Diğer filtreler" closed; ten fields in one column measured 668 px before the fix. */
const FILTER_AREA_MAX_HEIGHT_PX = 200;
/** At 1280 px the filters measured about 200 px wide before the fix; the card is about 1000 px wide. */
const FILTER_AREA_MIN_WIDTH_PX = 700;

test('reports: the filters use the card width and stay within two rows on wide screens', async ({ browser }, info) => {
  test.skip(info.project.name !== 'desktop', 'The filters sit in a drawer below 768 px.');
  const admin = await adminSession(browser);
  for (const report of REPORT_PATHS) {
    await admin.page.goto(`/reports/${report}`);
    const form = admin.page.getByRole('form', { name: 'Rapor filtreleri' });
    await expect(form).toBeVisible();
    const box = await form.boundingBox();
    expect(box?.height, report).toBeLessThanOrEqual(FILTER_AREA_MAX_HEIGHT_PX);
    expect(box?.width, report).toBeGreaterThanOrEqual(FILTER_AREA_MIN_WIDTH_PX);
  }
});

test('reports: admin drills into an incident figure, exports it and downloads a background export', async ({ browser }, testInfo) => {
  test.setTimeout(REPORTS_TEST_TIMEOUT_MS);
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const token = Date.now().toString();
  const subjects = [`Ekran titriyor ${token}`, `Fare çalışmıyor ${token}`];
  await seedIncidents(employee, subjects);
  const page = admin.page;

  await page.goto('/reports');
  await expect(page).toHaveURL(/\/reports\/incidents$/);
  // The department is unique to this world, so the figure counts exactly the incidents seeded above.
  // On phones the filters sit behind the "Filtreler (n)" button in a drawer; filters apply as they change.
  const filtersButton = page.getByRole('button', { name: /^Filtreler \(\d+\)$/ });
  // Departman is a minor filter: it sits under the collapsed "Diğer filtreler" in both layouts.
  if (await filtersButton.isVisible()) {
    await filtersButton.click();
    const drawer = page.getByRole('dialog', { name: 'Filtreler' });
    await drawer.getByRole('button', { name: /^Diğer filtreler/ }).click();
    await chooseOption(drawer, 'Departman', world.departmentName);
    await page.keyboard.press('Escape');
  } else {
    await page.getByRole('button', { name: /^Diğer filtreler/ }).click();
    await chooseOption(page, 'Departman', world.departmentName);
  }
  // The card is described by its headline value only, so other numbers on it cannot satisfy the check.
  const volume = page.getByRole('article', { name: 'Açılan kayıt', exact: true });
  await expect(volume).toHaveAccessibleDescription(String(subjects.length));
  await expectNoHorizontalScroll(page);

  await volume.getByRole('link', { name: 'Açılan kayıt', exact: true }).click();
  await expect(page).toHaveURL(DRILLDOWN_URL);
  for (const subject of subjects) {
    await expect(visibleEntry(page, subject)).toBeVisible();
  }
  await expectNoHorizontalScroll(page);

  const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: "Excel'e aktar" }).click()]);
  const syncPath = testInfo.outputPath('sync.xlsx');
  await download.saveAs(syncPath);
  const syncBook = await readWorkbook(syncPath);
  expect(syncBook.sheetNames).toEqual([RECORDS_SHEET, FILTERS_SHEET]);
  expect(syncBook.rowCount(RECORDS_SHEET)).toBe(HEADER_ROWS + subjects.length);

  // The UI offers a background export only above the synchronous row limit; queue one with the drill-down's own query.
  const query = new URL(page.url()).searchParams;
  query.set(REPORT_PARAM, REPORT);
  await requestBackgroundExport(admin, query);
  await page.goto('/reports');
  // The sidebar has the same link; the page's own action is the one under test.
  await page.getByRole('main').getByRole('link', { name: 'Dışa aktarımlarım' }).click();
  await expect(page.getByRole('heading', { name: 'Dışa aktarımlarım' })).toBeVisible();
  const job = visibleEntry(page, 'Açılan kayıt');
  await expect(job.getByText('Hazır')).toBeVisible({ timeout: EXPORT_READY_TIMEOUT_MS });
  await expectNoHorizontalScroll(page);

  const [background] = await Promise.all([page.waitForEvent('download'), job.getByRole('link', { name: 'İndir' }).click()]);
  const backgroundPath = testInfo.outputPath('background.xlsx');
  await background.saveAs(backgroundPath);
  const backgroundBook = await readWorkbook(backgroundPath);
  expect(backgroundBook.sheetNames).toEqual([RECORDS_SHEET, FILTERS_SHEET]);
  expect(backgroundBook.rowCount(RECORDS_SHEET)).toBe(HEADER_ROWS + subjects.length);

  await Promise.all([admin.context.close(), employee.context.close()]);
});
