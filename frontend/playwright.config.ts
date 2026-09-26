import { defineConfig, devices } from '@playwright/test';

const BASE_URL = process.env.E2E_BASE_URL ?? 'http://localhost:4173';
const MOBILE_VIEWPORT = { width: 360, height: 800 };
const DESKTOP_VIEWPORT = { width: 1280, height: 800 };

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: { baseURL: BASE_URL, trace: 'retain-on-failure', locale: 'tr-TR', timezoneId: 'Europe/Istanbul' },
  projects: [
    { name: 'mobile-360', use: { ...devices['Desktop Chrome'], viewport: MOBILE_VIEWPORT, hasTouch: true } },
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: DESKTOP_VIEWPORT } },
  ],
});
