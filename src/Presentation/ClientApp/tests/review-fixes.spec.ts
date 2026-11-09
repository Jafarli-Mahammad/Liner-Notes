import { test, expect } from '@playwright/test';
import { personalFixture, overview } from './fixtures/personal';

test('switching rated feedback clears only contradictory ratings and keeps notes', async ({ page }) => {
  await personalFixture(page);
  await page.goto('/discover');
  const card = page.locator('.featured-pick');
  await card.getByText('Rate or add a note', { exact: true }).click();
  await card.getByRole('combobox').selectOption('8');
  await card.getByRole('textbox').fill('Keep café 日本語 🎵');
  await card.getByRole('button', { name: 'Save rating and note' }).click();
  await expect(card.getByRole('status')).toHaveText('Feedback saved.');
  await card.getByRole('button', { name: 'Already know', exact: true }).click();
  await expect(card.getByRole('button', { name: 'Already know', exact: true })).toHaveAttribute('aria-pressed', 'true');
  const request = page.waitForRequest(r => r.url().endsWith('/feedback') && r.method() === 'POST');
  await card.getByRole('button', { name: 'Not for me', exact: true }).click();
  expect((await request).postDataJSON()).toMatchObject({ feedback: 'Disliked', rating: null, comment: 'Keep café 日本語 🎵' });
  await expect(card.getByRole('combobox')).toHaveValue('');
  await expect(card.getByRole('status')).toContainText('previous rating was cleared');
  await page.reload();
  await expect(card.getByRole('button', { name: 'Not for me', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await card.getByText('Rate or add a note', { exact: true }).click();
  await expect(card.getByRole('textbox')).toHaveValue('Keep café 日本語 🎵');
  await card.getByRole('combobox').selectOption('2');
  await card.getByRole('button', { name: 'Save rating and note' }).click();
  await expect(card.getByRole('status')).toHaveText('Feedback saved.');
  await card.getByRole('button', { name: 'Like', exact: true }).click();
  await expect(card.getByRole('combobox')).toHaveValue('');
  await expect(card.getByRole('button', { name: 'Like', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await card.getByRole('combobox').selectOption('2');
  const rated = page.waitForRequest(r => r.url().endsWith('/feedback') && r.method() === 'POST');
  await card.getByRole('button', { name: 'Save rating and note' }).click();
  expect((await rated).postDataJSON()).toMatchObject({ feedback: 'None', rating: 2 });
  await expect(card.getByRole('combobox')).toHaveValue('2');
  await expect(card.getByRole('status')).toContainText('previous choice was cleared');
});

test('taste search has one announced message and deduplicates release labels', async ({ page }) => {
  await personalFixture(page);
  const saved = structuredClone(overview);
  saved.page!.favorites[0].releases = [
    { canonicalId: '10000000-0000-4000-8000-000000000001', title: 'Same album', date: null },
    { canonicalId: '10000000-0000-4000-8000-000000000002', title: 'Same album', date: null },
  ];
  await page.route('**/api/discovery', route => route.fulfill({ json: saved }));
  await page.route('**/api/catalog/search?*', route => route.fulfill({ json: { items: [], hasMore: false } }));
  await page.goto('/taste');
  await expect(page.locator('.favorite-row').first().locator('small')).toHaveText('Same album');
  await page.getByRole('combobox', { name: 'Search artists, albums, or tracks' }).fill('no match');
  await expect(page.getByText('No local matches. You can search the music source explicitly.', { exact: true })).toHaveCount(1);
  await expect(page.getByRole('status').filter({ hasText: 'No local matches.' })).toBeVisible();
  await page.getByRole('combobox', { name: 'Music type' }).selectOption('Track');
  await expect(page.getByText('Include the artist’s name to narrow down albums or tracks.')).toBeVisible();
});

test('saved picks distinguish their original taste from current preferences', async ({ page }) => {
  await personalFixture(page);
  await page.goto('/discover');
  await expect(page.getByText('Your taste for the next discovery', { exact: true })).toBeVisible();
  await expect(page.getByText('These saved picks reflect your taste when they were requested. Taste changes apply to your next discovery.')).toBeVisible();
});

test('unavailable artist portraits remain centered and retries back off', async ({ page }) => {
  await personalFixture(page);
  const saved = structuredClone(overview);
  saved.page!.favorites = [{ ...saved.page!.favorites[0], kind: 'Artist', canonicalId: '10000000-0000-4000-8000-000000000001', name: 'Radiohead' }];
  await page.route('**/api/discovery', route => route.fulfill({ json: saved }));
  let requests = 0;
  await page.route('**/api/artwork/artists/*', route => { requests++; return route.fulfill({ status: 503, headers: { 'Retry-After': '30' } }); });
  await page.clock.install();
  await page.goto('/taste');
  const initials = page.getByRole('img', { name: 'Photo unavailable for Radiohead' });
  await expect(initials).toHaveCSS('display', 'flex');
  await expect(initials).toHaveCSS('align-items', 'center');
  await expect(initials).toHaveCSS('justify-content', 'center');
  await expect.poll(() => requests).toBe(1);
  await page.clock.fastForward(29_000); expect(requests).toBe(1);
  await page.clock.fastForward(1000); await expect.poll(() => requests).toBe(2);
});

test('branded favicon is declared and served', async ({ page }) => {
  await personalFixture(page); await page.goto('/taste');
  const href = await page.locator('link[rel=icon]').getAttribute('href');
  expect(href).toBe('/home-assets/favicon.svg');
  const response = await page.request.get(href!);
  expect(response.status()).toBe(200);
  expect(response.headers()['content-type']).toContain('image/svg+xml');
});
