import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  outputDir: process.env.LINER_LIVE_FRONTEND ? 'test-results-live' : 'test-results',
  testMatch: '**/*.spec.ts',
  testIgnore: process.env.LINER_LIVE_FRONTEND ? [] : ['**/live.spec.ts'],
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env.LINER_HOME_URL ?? 'http://localhost:5080',
    headless: true,
    launchOptions: process.env.LINER_BROWSER_EXECUTABLE ? { executablePath: process.env.LINER_BROWSER_EXECUTABLE } : {},
    trace: 'retain-on-failure',
  },
});
