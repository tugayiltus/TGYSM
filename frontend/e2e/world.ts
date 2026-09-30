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
  if (await page.getByRole('alert').isVisible().catch(() => false)) {
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
  };
}
