import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { personalFixture } from './fixtures/personal';

for (const width of [390, 768, 1440]) {
  test(`Taste editorial layout at ${width}px — controlled contract data`, async ({ page }) => {
    await personalFixture(page); await page.setViewportSize({ width, height: 1000 }); await page.goto('/taste');
    await expect(page.getByRole('heading', { name: 'Your taste', exact: true })).toBeVisible();
    await expect(page.locator('.favorite-row')).toHaveCount(3);
    await expect(page.getByRole('button', { name: 'Shoegaze', exact: true })).toBeVisible();
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    await page.screenshot({ path: `../../../design/superdesign/frontend-review/taste-controlled-${width}.png`, fullPage: true });
  });
}
test('Taste keyboard catalog selection, tags, discrete preferences and save payload', async ({ page }) => {
  await personalFixture(page);
  const writes: string[] = []; page.on('request', r => { if (r.method() !== 'GET') writes.push(r.url()); });
  await page.route('**/api/catalog/search?*', route => route.fulfill({ json: { items: [{ id: '70000000-0000-4000-8000-000000000001', canonicalId: '80000000-0000-4000-8000-000000000001', name: 'Radiohead', artistName: null, kind: 'Artist', disambiguation: 'UK group', attribution: 'MusicBrainz' }], hasMore: false, normalizationVersion: 'test' } }));
  await page.goto('/taste');
  const search = page.getByRole('combobox', { name: 'Search artists, albums, or tracks' });
  await search.fill('Radiohead'); await expect(page.getByRole('listbox', { name: 'Music search results' }).getByRole('option')).toHaveCount(1);
  await search.press('ArrowDown'); await search.press('Enter');
  await expect(page.locator('.favorite-row')).toHaveCount(4); expect(writes).toEqual([]);
  await page.getByRole('button', { name: 'Shoegaze', exact: true }).click();
  await page.getByLabel('Exploration', { exact: true }).focus(); await page.getByLabel('Exploration', { exact: true }).press('End');
  await page.getByRole('radio', { name: 'Instrumental', exact: true }).check();
  const saved = page.waitForRequest(r => r.url().endsWith('/api/discovery/onboarding') && r.method() === 'PUT');
  await page.getByRole('button', { name: 'Save taste without generating' }).click();
  const body = (await saved).postDataJSON();
  expect(body.answers).toMatchObject({ exploration: 'surprise', vocals: 'instrumental', step: 'complete' });
  expect(body.selection.tags).toContain('shoegaze'); expect(body.selection.catalogRecordIds).toContain('70000000-0000-4000-8000-000000000001');
  await expect(page.getByRole('status').filter({ hasText: 'Your taste is saved.' })).toBeVisible();
});
test('Taste preserves dirty edits on conflict until explicit discard', async ({ page }) => {
  await personalFixture(page);
  await page.route('**/api/discovery/onboarding', route => route.fulfill({ status: 409, json: {} }));
  await page.goto('/taste'); await page.getByRole('button', { name: 'Remove Beach House' }).click();
  await page.getByRole('button', { name: 'Save taste without generating' }).click();
  await expect(page.getByRole('alert')).toBeVisible(); await expect(page.locator('.favorite-row')).toHaveCount(2);
  await expect(page.getByRole('button', { name: 'Find recommendations', exact: true })).toBeDisabled();
  page.once('dialog', dialog => dialog.dismiss()); await page.getByRole('button', { name: 'Refresh saved information' }).click();
  await expect(page.locator('.favorite-row')).toHaveCount(2);
  page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: 'Refresh saved information' }).click();
  await expect(page.locator('.favorite-row')).toHaveCount(3);
});
test('Taste unsaved changes protect in-app navigation', async ({ page }) => {
  await personalFixture(page); await page.goto('/taste'); await page.getByRole('button', { name: 'Remove Beach House' }).click();
  page.once('dialog', dialog => dialog.dismiss()); await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Home', exact: true }).click();
  await expect(page).toHaveURL(/\/taste$/); await expect(page.locator('.favorite-row')).toHaveCount(2);
});
