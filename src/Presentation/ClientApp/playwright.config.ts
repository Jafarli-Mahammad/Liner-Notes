import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  outputDir: process.env.LINER_LIVE_FRONTEND ? 'test-results-live' : 'test-results',
  testMatch: process.env.LINER_PRODUCTION_ACCEPTANCE ? '**/production.spec.ts' : '**/*.spec.ts',
  testIgnore: process.env.LINER_PRODUCTION_ACCEPTANCE ? [] : process.env.LINER_LIVE_FRONTEND ? ['**/production.spec.ts'] : ['**/live.spec.ts', '**/production.spec.ts'],
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env.LINER_HOME_URL ?? 'http://localhost:5080',
    headless: true,
    launchOptions: {
      ...(process.env.LINER_BROWSER_EXECUTABLE ? { executablePath: process.env.LINER_BROWSER_EXECUTABLE } : {}),
      ...((process.env.LINER_PRODUCTION_ACCEPTANCE || process.env.LINER_LIVE_FRONTEND) && process.env.LINER_TEST_CA_SPKI ? { args: [
        '--host-resolver-rules=MAP liner.test 127.0.0.1',
        `--ignore-certificate-errors-spki-list=${process.env.LINER_TEST_CA_SPKI}`,
      ] } : {}),
    },
    trace: 'retain-on-failure',
  },
});
