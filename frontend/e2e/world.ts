import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

import { expect, type APIRequestContext, type Browser, type BrowserContext, type Page } from '@playwright/test';

import { changePassword, login, requiredEnv } from './helpers';

export const ADMIN_NEW_PASSWORD = 'Admin-new-pass-2026';
const USER_PASSWORD = 'User-new-pass-2026';
const CSRF_PATH = '/api/v1/auth/csrf';
const CSRF_HEADER = 'X-CSRF-TOKEN';
/** Seeded users get a temporary password so the tests can sign in without reading email. */
const TEMPORARY_PASSWORD_DELIVERY = 'temporary_password';
const HOME_HEADING = /^Merhaba/;

export interface Session {
  context: BrowserContext;
  page: Page;
}

/** Signs the first admin in; changes the temporary password when this is its first login in the database. */
export async function adminSession(browser: Browser): Promise<Session> {
  const context = await browser.newContext();
  const page = await context.newPage();
  const email = requiredEnv('E2E_ADMIN_EMAIL');
  await login(page, email, ADMIN_NEW_PASSWORD);
  const alert = page.getByRole('alert');
  // Wait for the login outcome: the alert (first run, new password not set yet) or the home heading.
  await expect(alert.or(page.getByRole('heading', { name: HOME_HEADING }))).toBeVisible();
  if (await alert.isVisible()) {
    const temporary = requiredEnv('E2E_ADMIN_TEMP_PASSWORD');
    await login(page, email, temporary);
    await expect(page).toHaveURL(/\/change-password$/);
    await changePassword(page, temporary, ADMIN_NEW_PASSWORD);
  }
  await expect(page.getByRole('heading', { name: HOME_HEADING })).toBeVisible();
  return { context, page };
}

/** Signs a user in with the temporary password and sets their own. */
export async function userSession(browser: Browser, email: string, temporaryPassword: string): Promise<Session> {
  const context = await browser.newContext();
  const page = await context.newPage();
  await login(page, email, temporaryPassword);
  await changePassword(page, temporaryPassword, USER_PASSWORD);
  await expect(page.getByRole('heading', { name: HOME_HEADING })).toBeVisible();
  return { context, page };
}

async function send(request: APIRequestContext, method: 'POST' | 'PUT', path: string, data: unknown): Promise<Record<string, unknown>> {
  const csrf = (await (await request.get(CSRF_PATH)).json()) as { token: string };
  const response = await request.fetch(path, { method, data, headers: { [CSRF_HEADER]: csrf.token } });
  expect(response.ok(), `${method} ${path} → ${response.status()}`).toBe(true);
  return (await response.json()) as Record<string, unknown>;
}

export interface World {
  employeeEmail: string;
  employeePassword: string;
  itEmail: string;
  itPassword: string;
  requestTypeName: string;
  assetTag: string;
  /** Unique per seed, so a report filtered by it counts only this world's tickets. */
  departmentName: string;
}

/**
 * Seeds one organisation through the admin API: location, department approved by the admin, intake team with an
 * IT member, a request type that needs approval, an employee and a device in stock.
 */
export async function seedWorld(admin: Session): Promise<World> {
  const request = admin.context.request;
  const suffix = Date.now().toString();
  const me = (await (await request.get('/api/v1/auth/me')).json()) as { id: string };
  const location = await send(request, 'POST', '/api/v1/admin/locations', { name: `Merkez ${suffix}`, parentId: null });
  const department = await send(request, 'POST', '/api/v1/admin/departments', { name: `Muhasebe ${suffix}`, approverUserId: me.id });
  const employeeEmail = `calisan-${suffix}@example.com`;
  const itEmail = `it-${suffix}@example.com`;
  const employee = await send(request, 'POST', '/api/v1/admin/users', {
    firstName: 'Ayşe', lastName: 'Kaya', email: employeeEmail, role: 'employee', departmentId: department.id, locationId: location.id,
    delivery: TEMPORARY_PASSWORD_DELIVERY,
  });
  const it = await send(request, 'POST', '/api/v1/admin/users', {
    firstName: 'Can', lastName: 'Demir', email: itEmail, role: 'it_staff', departmentId: null, locationId: null, delivery: TEMPORARY_PASSWORD_DELIVERY,
  });
  const team = await send(request, 'POST', '/api/v1/admin/teams', { name: `Servis Masası ${suffix}` });
  await send(request, 'POST', `/api/v1/admin/teams/${String(team.id)}/members`, { userId: it.userId });
  await send(request, 'PUT', `/api/v1/admin/teams/${String(team.id)}`, { version: team.version, name: team.name, isDefaultIntake: true });
  const requestTypeName = `Yeni dizüstü ${suffix}`;
  await send(request, 'POST', '/api/v1/admin/request-types', { name: requestTypeName, requiresApproval: true });
  const assetTag = `DT-${suffix}`;
  await send(request, 'POST', '/api/v1/assets', { assetTag, type: 'laptop', brand: 'Lenovo', model: 'T14', locationId: location.id });
  return {
    employeeEmail,
    employeePassword: String(employee.temporaryPassword),
    itEmail,
    itPassword: String(it.temporaryPassword),
    requestTypeName,
    assetTag,
    departmentName: String(department.name),
  };
}

export type ArticleVisibility = 'employees' | 'internal';

export interface SeededArticle {
  id: string;
  title: string;
}

/** Creates and publishes an article as the session's user (IT staff or admin). */
export async function seedPublishedArticle(author: Session, title: string, visibility: ArticleVisibility): Promise<SeededArticle> {
  const request = author.context.request;
  const created = await send(request, 'POST', '/api/v1/kb/articles', { title, summary: `${title} özeti`, body: `${title} adımları`, categoryId: null, visibility });
  await send(request, 'POST', `/api/v1/kb/articles/${String(created.id)}/publish`, { version: created.version });
  return { id: String(created.id), title };
}

/** Opens incidents as the session's user (the requester) and returns their numbers, oldest first. */
export async function seedIncidents(requester: Session, subjects: readonly string[]): Promise<string[]> {
  const numbers: string[] = [];
  for (const subject of subjects) {
    const ticket = await send(requester.context.request, 'POST', '/api/v1/tickets/incidents', { subject, description: subject, categoryId: null, locationId: null });
    numbers.push(String(ticket.number));
  }
  return numbers;
}

/** Queues a background export of a report figure's rows as the session's user; the query matches the drill-down URL. */
export async function requestBackgroundExport(session: Session, query: URLSearchParams): Promise<void> {
  await send(session.context.request, 'POST', `/api/v1/reports/exports?${query.toString()}`, undefined);
}

const PICKUP_ENV = 'E2E_EMAIL_PICKUP_FOLDER';
const EMAIL_TIMEOUT_MS = 30_000;
const EMAIL_POLL_MS = 500;
const EML_EXTENSION = '.eml';
const HEADER_BODY_SEPARATOR = /\r?\n\r?\n/;
const HEADER_FOLD = /\r?\n[ \t]+/g;

export interface PickedUpEmail {
  to: string;
  subject: string;
  body: string;
}

function decodeQuotedPrintable(text: string): string {
  const unfolded = text.replace(/=\r?\n/g, '');
  const bytes: number[] = [];
  for (let index = 0; index < unfolded.length; index++) {
    const hex = unfolded.slice(index + 1, index + 3);
    if (unfolded[index] === '=' && /^[0-9A-Fa-f]{2}$/.test(hex)) {
      bytes.push(Number.parseInt(hex, 16));
      index += 2;
    } else {
      bytes.push(...Buffer.from(unfolded[index] ?? ''));
    }
  }
  return Buffer.from(bytes).toString('utf8');
}

function decodeEncodedWords(value: string): string {
  return value.replace(/=\?utf-8\?([bq])\?([^?]*)\?=/gi, (_match, encoding: string, payload: string) =>
    encoding.toLowerCase() === 'b' ? Buffer.from(payload, 'base64').toString('utf8') : decodeQuotedPrintable(payload.replace(/_/g, ' ')),
  );
}

/** Parses the plain-text single-part message the Pickup sender writes (UTF-8, quoted-printable or base64 body). */
export function parseEml(raw: string): PickedUpEmail {
  const [head = '', ...rest] = raw.split(HEADER_BODY_SEPARATOR);
  const headers = head.replace(HEADER_FOLD, ' ');
  const header = (name: string) => new RegExp(`^${name}:\\s*(.*)$`, 'im').exec(headers)?.[1] ?? '';
  const encoding = header('Content-Transfer-Encoding').trim().toLowerCase();
  const rawBody = rest.join('\n\n');
  const body =
    encoding === 'base64' ? Buffer.from(rawBody.replace(/\s+/g, ''), 'base64').toString('utf8')
    : encoding === 'quoted-printable' ? decodeQuotedPrintable(rawBody)
    : rawBody;
  const address = /<([^>]+)>/.exec(header('To'))?.[1] ?? header('To').trim();
  return { to: address.toLowerCase(), subject: decodeEncodedWords(header('Subject')), body };
}

/** Polls the Pickup folder (set by tools/e2e.sh) until a message to `to` whose body contains `bodyText` arrives. */
export async function waitForEmail(to: string, bodyText: string): Promise<PickedUpEmail> {
  const folder = requiredEnv(PICKUP_ENV);
  const deadline = Date.now() + EMAIL_TIMEOUT_MS;
  for (;;) {
    const names = (await readdir(folder).catch(() => [] as string[])).filter((name) => name.endsWith(EML_EXTENSION));
    for (const name of names) {
      const email = parseEml(await readFile(join(folder, name), 'utf8'));
      if (email.to === to.toLowerCase() && email.body.includes(bodyText)) {
        return email;
      }
    }
    if (Date.now() > deadline) {
      throw new Error(`No email to ${to} containing "${bodyText}" in ${folder} within ${EMAIL_TIMEOUT_MS} ms.`);
    }
    await new Promise((resolve) => setTimeout(resolve, EMAIL_POLL_MS));
  }
}
