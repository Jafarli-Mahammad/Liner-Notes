import { test, expect } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { writeFile } from 'node:fs/promises';

test.use({ trace: 'off' });
test('actual provider-backed guest and account journey', async ({ page }) => {
  test.setTimeout(300_000);
  const errors: string[] = [], checks: string[] = [], results: Record<string, unknown> = {};
  async function capture(name: string) {
    await page.evaluate(() => { (document.activeElement as HTMLElement | null)?.blur(); scrollTo(0, 0); });
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(150);
    await page.screenshot({ path: `../../../design/superdesign/frontend-review/${name}.png`, fullPage: true });
  }
  page.on('pageerror', e => errors.push(e.message));
  const email = `frontend-qa-${Date.now()}@example.test`, username = `frontendqa${Date.now()}`;
  const password = randomBytes(24).toString('base64url') + 'Aa1!';
  let accountCreated = false;
  try {
    await page.goto('/taste'); await expect(page.getByRole('heading', { name: 'Your taste', exact: true })).toBeVisible();
    const search = page.getByRole('combobox', { name: 'Search artists, albums, or tracks' });
    await search.fill('Radiohead');
    await page.getByRole('button', { name: 'Search music source', exact: true }).click();
    await expect(page.getByRole('listbox').getByRole('option').first()).toBeVisible({ timeout: 100_000 });
    await search.press('ArrowDown'); await search.press('Enter');
    await expect(page.locator('.favorite-row')).toHaveCount(1); checks.push('Real catalog artist selected');
    await page.getByRole('button', { name: '＋ Add tag', exact: true }).click();
    await page.getByLabel('Find styles', { exact: true }).fill('ambient');
    await page.getByRole('button', { name: 'ambient', exact: true }).click();
    await page.getByRole('button', { name: 'Save taste without generating' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Your taste is saved.' })).toBeVisible({ timeout: 60_000 });
    checks.push('Guest creation and taste persistence through real API');
    for (const width of [390, 768, 1440]) { await page.setViewportSize({ width, height: 1000 }); await capture(`taste-live-${width}`); }
    await page.getByRole('button', { name: 'Find recommendations', exact: true }).click();
    await expect(page).toHaveURL(/\/discover$/, { timeout: 60_000 });
    await expect(page.locator('.personal-pick').first()).toBeVisible({ timeout: 180_000 });
    const count = await page.locator('.personal-pick').count(); results.pickCount = count;
    checks.push('Real provider-backed recommendations and stored explanations');
    await page.locator('.featured-pick').getByRole('button', { name: 'Like', exact: true }).click();
    await expect(page.locator('.featured-pick').getByRole('button', { name: 'Like', exact: true })).toHaveAttribute('aria-pressed', 'true', { timeout: 30_000 });
    checks.push('Real feedback save');
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 1000 }); await page.evaluate(() => scrollTo(0, 0));
      await capture(`discover-live-${width}`);
    }
    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'History', exact: true }).click();
    await expect(page.locator('.history-card')).toHaveCount(1); await expect(page.getByText('♡ Liked', { exact: true })).toBeVisible({ timeout: 30_000 });
    checks.push('Persisted batch history and feedback');
    for (const width of [390, 768, 1440]) { await page.setViewportSize({ width, height: 1000 }); await capture(`history-live-${width}`); }
    await page.getByRole('link', { name: 'Sign in', exact: true }).click();
    const downloaded = page.waitForEvent('download'); await page.getByRole('button', { name: 'Download your data' }).click(); await downloaded;
    checks.push('Real guest export');
    const form = page.getByRole('form', { name: 'Create an account', exact: true });
    await form.getByLabel('Username', { exact: true }).fill(username); await form.getByLabel('Email', { exact: true }).fill(email); await form.getByLabel('Password', { exact: true }).fill(password);
    await form.getByRole('button', { name: 'Create account', exact: true }).click();
    await expect(page).toHaveURL(/\/discover$/, { timeout: 60_000 }); accountCreated = true;
    await expect(page.locator('.personal-pick')).toHaveCount(count); checks.push('Real registration preserving guest batch and taste');
    await page.getByRole('link', { name: 'Account', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Good to have you here.' })).toBeVisible();
    const accountDownload = page.waitForEvent('download'); await page.getByRole('button', { name: 'Download your data' }).click(); await accountDownload; checks.push('Real account export');
    for (const width of [390, 768, 1440]) { await page.setViewportSize({ width, height: 1000 }); await capture(`account-live-${width}`); }
    await page.getByRole('button', { name: 'Sign out', exact: true }).click(); await expect(page.getByRole('form', { name: 'Sign in', exact: true })).toBeVisible();
    const login = page.getByRole('form', { name: 'Sign in', exact: true });
    await login.getByLabel('Email', { exact: true }).fill(email); await login.getByLabel('Password', { exact: true }).fill(password); await login.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page).toHaveURL(/\/discover$/, { timeout: 60_000 }); await expect(page.locator('.personal-pick')).toHaveCount(count); checks.push('Real login restores owned discoveries');
    await page.getByRole('link', { name: 'Account', exact: true }).click(); page.once('dialog', d => d.accept()); await page.getByRole('button', { name: 'Delete my account' }).click();
    const deleted = page.getByRole('status').filter({ hasText: /Your account and its stored data were deleted\.|The account was deleted\./ });
    await expect(deleted).toBeVisible({ timeout: 60_000 }); accountCreated = false;
    results.localCleanupWarning = (await deleted.textContent())?.includes('operator cleanup') ?? false;
    checks.push('Disposable account deleted through existing endpoint');
    expect(errors).toEqual([]);
  } finally {
    results.checks = checks; results.pageExceptions = errors; results.accountCleanupNeeded = accountCreated;
    results.lastRoute = new URL(page.url()).pathname;
    await writeFile('../../../design/superdesign/frontend-review/live-journey.json', JSON.stringify(results, null, 2) + '\n');
  }
});
