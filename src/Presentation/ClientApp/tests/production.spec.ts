import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test.use({ trace: 'off' });

test('Production HTTPS release loads assets, signs in and preserves taste after reload', async ({ page }) => {
  if (!process.env.LINER_PRODUCTION_FIXTURE || !process.env.LINER_TEST_CA_SPKI)
    throw new Error('Production acceptance requires a disposable account and scoped test CA pin');
  const fixture = JSON.parse(await readFile(process.env.LINER_PRODUCTION_FIXTURE, 'utf8'));
  const failures: string[] = [];
  page.on('pageerror', () => failures.push('page_exception'));
  page.on('requestfailed', request => console.log('request_failed', new URL(request.url()).pathname, request.failure()?.errorText));
  page.on('response', response => {
    if (new URL(response.url()).pathname.startsWith('/home-assets/') && ![200, 304].includes(response.status()))
      failures.push('release_asset_failed');
  });
  await page.goto('/account');
  const login = page.getByRole('form', { name: 'Sign in', exact: true });
  await login.getByLabel('Email', { exact: true }).fill(fixture.email);
  await login.getByLabel('Password', { exact: true }).fill(fixture.password);
  await login.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/discover$/);
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Your taste', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your taste', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Your taste', exact: true })).toBeVisible();
  // Bearer credentials intentionally stay in memory. Reauthenticate after a full reload.
  await page.goto('/account');
  await login.getByLabel('Email', { exact: true }).fill(fixture.email);
  await login.getByLabel('Password', { exact: true }).fill(fixture.password);
  await login.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/discover$/);
  await page.getByRole('link', { name: 'Account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Good to have you here.' })).toBeVisible();
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download your data' }).click();
  expect((await download).suggestedFilename()).toMatch(/json$/);
  expect(failures).toEqual([]);
});
