import { expect, test, type Page } from '@playwright/test';

import { expectNoHorizontalScroll } from './helpers';
import { adminSession, seedIncidents, seedPublishedArticle, seedWorld, userSession } from './world';

const ARTICLE_URL = /\/kb\/[0-9a-f-]{36}$/;
const NOT_FOUND_TEXT = 'Kayıt bulunamadı.';
const KB_ARTICLES_PATH = '/api/v1/kb/articles';
/** Query names: `search` on the API, `q` in the page URL. */
const SEARCH_PARAM = 'search';
const KB_QUERY_PARAM = 'q';

/** IT writes an article through the editor, saves it and publishes it; returns the article URL. */
async function writeAndPublish(page: Page, title: string, body: string, visibility: string): Promise<string> {
  await page.goto('/kb');
  await page.getByRole('link', { name: 'Yeni makale' }).click();
  await page.getByLabel('Başlık', { exact: true }).fill(title);
  await page.getByLabel('Özet').fill(`${title} özeti`);
  await page.getByLabel('Görünürlük').selectOption({ label: visibility });
  await page.getByLabel('İçerik (Markdown)').fill(body);
  await expectNoHorizontalScroll(page);
  await page.getByRole('button', { name: 'Kaydet' }).click();
  await expect(page).toHaveURL(ARTICLE_URL);
  await expect(page.getByText('Taslak')).toBeVisible();
  await page.getByRole('button', { name: 'Yayınla' }).click();
  await expect(page.getByText('Yayında')).toBeVisible();
  await expectNoHorizontalScroll(page);
  return page.url();
}

test('knowledge: IT publishes an employee and an internal article; the employee finds only the public one', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const it = await userSession(browser, world.itEmail, world.itPassword);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const token = Date.now().toString();
  const publicTitle = `Yazıcı kurulumu ${token}`;
  const internalTitle = `Sunucu yeniden başlatma ${token}`;

  await writeAndPublish(it.page, publicTitle, '## Adımlar\n\n1. Ayarlar\n2. **Yazıcı ekle**', 'Tüm çalışanlar');
  const internalUrl = await writeAndPublish(it.page, internalTitle, 'Yalnızca BT için iç not.', 'Yalnızca BT personeli');

  await employee.page.getByRole('link', { name: /Nasıl Yapılır\?/ }).click();
  // The list is debounced: wait for the searched result, or the "internal title absent" check below would pass on the unfiltered list.
  const searched = employee.page.waitForResponse(
    (response) => new URL(response.url()).pathname === KB_ARTICLES_PATH && new URL(response.url()).searchParams.get(SEARCH_PARAM) === token,
  );
  await employee.page.getByLabel('Makalelerde ara').fill(token);
  await expect(employee.page).toHaveURL(new RegExp(`[?&]${KB_QUERY_PARAM}=${token}`));
  await searched;
  const results = employee.page.getByRole('list', { name: 'Makaleler' });
  await expect(results.getByRole('link', { name: publicTitle })).toBeVisible();
  await expect(results.getByText(internalTitle)).toHaveCount(0);
  await expectNoHorizontalScroll(employee.page);
  await results.getByRole('link', { name: publicTitle }).click();
  await expect(employee.page.getByRole('heading', { name: publicTitle })).toBeVisible();
  await expect(employee.page.getByRole('heading', { name: 'Adımlar' })).toBeVisible();
  await expect(employee.page.getByText('Yazıcı ekle')).toBeVisible();
  await expectNoHorizontalScroll(employee.page);

  await employee.page.goto(new URL(internalUrl).pathname);
  await expect(employee.page.getByText(NOT_FOUND_TEXT)).toBeVisible();
  await expect(employee.page.getByText(internalTitle)).toHaveCount(0);
  await expect(employee.page.getByText('Yalnızca BT için iç not.')).toHaveCount(0);

  await Promise.all([admin.context.close(), it.context.close(), employee.context.close()]);
});

test('knowledge: IT links two articles to a ticket; the requester sees only the public link', async ({ browser }) => {
  const admin = await adminSession(browser);
  const world = await seedWorld(admin);
  const it = await userSession(browser, world.itEmail, world.itPassword);
  const employee = await userSession(browser, world.employeeEmail, world.employeePassword);
  const token = Date.now().toString();
  const publicArticle = await seedPublishedArticle(it, `VPN bağlantısı ${token}`, 'employees');
  const internalArticle = await seedPublishedArticle(it, `VPN sunucu ayarı ${token}`, 'internal');
  const [number = ''] = await seedIncidents(employee, ['VPN bağlanmıyor']);

  await it.page.goto(`/tickets/${number}`);
  const section = it.page.getByRole('region', { name: 'İlgili makaleler' });
  await expect(section.getByText('Bu kayda bağlı makale yok.')).toBeVisible();
  for (const article of [publicArticle, internalArticle]) {
    await section.getByRole('button', { name: 'Makale bağla' }).click();
    const dialog = it.page.getByRole('dialog');
    await dialog.getByLabel('Makale ara').fill(token);
    await dialog.getByLabel('Makale', { exact: true }).selectOption({ label: article.title });
    await dialog.getByRole('button', { name: 'Onayla' }).click();
    await expect(dialog).toHaveCount(0);
    await expect(section.getByRole('link', { name: article.title })).toBeVisible();
  }
  await expect(section.getByText('Çalışana gösterilmez')).toHaveCount(1);
  await expectNoHorizontalScroll(it.page);

  await employee.page.goto(`/tickets/${number}`);
  const requesterSection = employee.page.getByRole('region', { name: 'İlgili makaleler' });
  await expect(requesterSection.getByRole('link', { name: publicArticle.title })).toBeVisible();
  await expect(employee.page.getByText(internalArticle.title)).toHaveCount(0);
  await expect(requesterSection.getByRole('button', { name: 'Makale bağla' })).toHaveCount(0);
  await expectNoHorizontalScroll(employee.page);

  await Promise.all([admin.context.close(), it.context.close(), employee.context.close()]);
});
