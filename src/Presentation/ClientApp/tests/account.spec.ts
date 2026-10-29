import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { personalFixture, session } from './fixtures/personal';
import type { Page } from '@playwright/test';

const user = { id: '90000000-0000-4000-8000-000000000001', email: 'listener@example.test', timeZone: 'UTC', deliveryDay: 'Sunday', deliveryHourUtc: 8, weeklyEmailOptIn: false };
const auth = { accessToken: 'test-access', refreshToken: 'test-refresh', expiresIn: 3600, user };
async function accountFixture(page: Page, imported = false) {
  await personalFixture(page);
  await page.route('**/api/auth/login', route => route.fulfill({ json: auth }));
  await page.route('**/api/auth/register', route => route.fulfill({ status: 201, json: auth }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
  await page.route('**/api/discovery/session', route => route.fulfill({ json: route.request().headers().authorization ? { ...session, profile: { ...session.profile, userId: user.id, isGuest: false }, guestImportAvailable: imported } : session }));
  await page.route('**/api/export/my-data', route => route.fulfill({ json: { exportVersion: 'test', user, discovery: {} } }));
  await page.route('**/api/discovery/session/data', route => route.fulfill({ json: { exportVersion: 'guest-discovery-1', discovery: {} } }));
}
async function login(page: Page) {
  await page.goto('/account?returnTo=/account');
  const form = page.getByRole('form', { name: 'Sign in', exact: true });
  await form.getByLabel('Email', { exact: true }).fill(user.email);
  await form.getByLabel('Password', { exact: true }).fill('Test-only-password1!');
  await form.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Good to have you here.' })).toBeVisible();
}

for (const width of [390, 768, 1440]) {
  test(`Account forms and signed-in layout at ${width}px`, async ({ page }) => {
    await accountFixture(page); await page.setViewportSize({ width, height: 1000 });
    await page.goto('/account'); await expect(page.getByRole('heading', { name: 'Create an account', exact: true })).toBeVisible();
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    await page.screenshot({ path: `../../../design/superdesign/frontend-review/account-anonymous-${width}.png`, fullPage: true });
    await login(page);
    await expect(page.getByText(user.email, { exact: true })).toBeVisible();
    expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    await page.screenshot({ path: `../../../design/superdesign/frontend-review/account-signed-in-${width}.png`, fullPage: true });
    await page.getByRole('button', { name: 'Sign out', exact: true }).click();
    await expect(page.getByRole('form', { name: 'Sign in', exact: true })).toBeVisible();
    expect(await page.evaluate(() => Object.keys(localStorage).filter(key => /token|password/i.test(key)))).toEqual([]);
  });
}
test('Account registration preserves guest progress with email consent off by default', async ({ page }) => {
  await accountFixture(page); await page.goto('/account?returnTo=/account');
  const form = page.getByRole('form', { name: 'Create an account', exact: true });
  await form.getByLabel('Username', { exact: true }).fill('listener'); await form.getByLabel('Email', { exact: true }).fill(user.email); await form.getByLabel('Password', { exact: true }).fill('Test-only-password1!');
  const registration = page.waitForRequest(r => r.url().endsWith('/api/auth/register'));
  await form.getByRole('button', { name: 'Create account', exact: true }).click();
  const request = await registration;
  expect(request.postDataJSON()).toMatchObject({ preserveGuestProgress: true, weeklyEmailOptIn: false });
  expect(request.headers()['x-csrf-token']).toBe('test-csrf');
  await expect(page.getByRole('heading', { name: 'Good to have you here.' })).toBeVisible();
});
test('Account registration displays server field errors and retains entries', async ({ page }) => {
  await accountFixture(page); await page.route('**/api/auth/register', route => route.fulfill({ status: 400, json: { errors: { Password: ['A symbol is required.'] } } }));
  await page.goto('/account'); const form = page.getByRole('form', { name: 'Create an account', exact: true });
  await form.getByLabel('Username', { exact: true }).fill('listener'); await form.getByLabel('Email', { exact: true }).fill(user.email); await form.getByLabel('Password', { exact: true }).fill('LongPassword123');
  await form.getByRole('button', { name: 'Create account', exact: true }).click(); await expect(form.getByRole('alert')).toContainText('A symbol is required.');
  await expect(form.getByLabel('Email', { exact: true })).toHaveValue(user.email); await expect(form.getByLabel('Password', { exact: true })).toHaveAttribute('aria-invalid', 'true');
});
test('Account import is explicit and export uses its existing API', async ({ page }) => {
  await accountFixture(page, true); let imports = 0;
  await page.route('**/api/discovery/session/import-preview', route => route.fulfill({ json: { account: { goal: 'new-artists' }, guest: { goal: 'favorites' }, conflicts: true, policy: 'keep-account-answers-fill-unanswered' } }));
  await page.route('**/api/discovery/session/import', route => { imports++; return route.fulfill({ status: 204 }); });
  await login(page); expect(imports).toBe(0);
  await page.getByRole('button', { name: 'Review guest progress' }).click(); await expect(page.getByText('Some answers differ.', { exact: false })).toBeVisible(); expect(imports).toBe(0);
  await page.getByRole('button', { name: 'Import guest progress', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Guest progress imported.' })).toBeVisible(); expect(imports).toBe(1);
  const download = page.waitForEvent('download'); await page.getByRole('button', { name: 'Download your data' }).click(); expect((await download).suggestedFilename()).toBe('liner-notes-data.json');
});
test('Account deletion distinguishes incomplete local-copy cleanup', async ({ page }) => {
  await accountFixture(page); await page.route('**/api/subscribers/me', route => route.fulfill({ json: { accountDeleted: true, localCopiesDeleted: false, issues: ['copy_cleanup_unavailable'] } }));
  await login(page); page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: 'Delete my account' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'operator cleanup is still required' })).toBeVisible();
  await expect(page.getByRole('form', { name: 'Sign in', exact: true })).toBeVisible();
});
test('Account refreshes the existing JWT contract once and retains navigation', async ({ page }) => {
  await accountFixture(page); let refreshed = 0;
  await page.route('**/api/auth/me', route => route.fulfill(route.request().headers().authorization === 'Bearer test-access' ? { status: 401, json: {} } : { json: user }));
  await page.route('**/api/auth/refresh', route => { refreshed++; return route.fulfill({ json: { ...auth, accessToken: 'test-refreshed' } }); });
  await login(page); await expect(page.getByText(user.email, { exact: true })).toBeVisible();
  await expect.poll(() => refreshed).toBe(1);
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'History', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Discovery history' })).toBeVisible();
  await page.getByRole('link', { name: 'Account', exact: true }).click(); await expect(page.getByText(user.email, { exact: true })).toBeVisible(); expect(refreshed).toBe(1);
});
test('Account ignores an in-flight private response after sign-out', async ({ page }) => {
  await accountFixture(page); let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/auth/me', async route => { await pending; await route.fulfill({ json: { ...user, email: 'stale-private@example.test' } }); });
  await login(page); await page.getByRole('button', { name: 'Sign out', exact: true }).click(); release();
  await expect(page.getByRole('form', { name: 'Sign in', exact: true })).toBeVisible();
  await expect(page.getByText('stale-private@example.test', { exact: true })).toHaveCount(0);
});


test('temporary refresh failure preserves sign-in and recovers on the next private read', async ({ page }) => {
  await accountFixture(page);
  let refreshed = 0;
  await page.route('**/api/auth/me', route => route.fulfill(route.request().headers().authorization === 'Bearer test-access'
    ? { status: 401, json: {} } : { json: user }));
  await page.route('**/api/auth/refresh', route => {
    refreshed++;
    return route.fulfill(refreshed === 1 ? { status: 503, headers: { 'Retry-After': '5' }, json: {} }
      : { json: { ...auth, accessToken: 'test-refreshed' } });
  });
  await login(page);
  await expect.poll(() => refreshed).toBe(1);
  await expect(page.getByRole('button', { name: 'Sign out', exact: true })).toBeVisible();
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'History', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Discovery history' })).toBeVisible();
  await page.getByRole('link', { name: 'Account', exact: true }).click();
  await expect.poll(() => refreshed).toBe(2);
  await expect(page.getByText(user.email, { exact: true })).toBeVisible();
});
