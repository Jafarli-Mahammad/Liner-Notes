import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { personalFixture, batchId } from './fixtures/personal';

for (const width of [390, 768, 1440]) {
  test(`History editorial layout at ${width}px — controlled contract data`, async ({ page }) => {
    const fixture = await personalFixture(page);
    const older = '20000000-0000-4000-8000-000000000002';
    fixture.overview.page!.batches.push({ id: older, kind: 'OnDemand', createdAtUtc: '2026-10-12T10:00:00Z', completedAtUtc: '2026-10-12T10:00:00Z' });
    fixture.batch.picks[0].feedback = 'Liked'; fixture.batch.picks[1].feedback = 'AlreadyKnown'; fixture.batch.picks[2].rating = 8;
    await page.route(`**/api/discovery/batches/${older}`, route => route.fulfill({ json: { ...fixture.batch, id: older, picks: [fixture.batch.picks[3], { ...fixture.batch.picks[1], feedback: 'Disliked' }] } }));
    await page.setViewportSize({ width, height: 1000 }); await page.goto('/history');
    await expect(page.getByRole('heading', { name: 'Discovery history' })).toBeVisible();
    await expect(page.locator('.history-card')).toHaveCount(2);
    await expect(page.getByText('04 recommendations')).toBeVisible();
    for (const image of await page.locator('.history-artwork img').all()) await image.scrollIntoViewIfNeeded();
    await expect(page.locator('.history-artwork .artwork-ready')).toHaveCount(5);
    await page.evaluate(() => document.fonts.ready); await page.evaluate(() => scrollTo(0, 0)); await page.mouse.move(0, 0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    await page.screenshot({ path: `../../../design/superdesign/frontend-review/history-controlled-${width}.png`, fullPage: true });
  });
}
test('History opens the selected batch, returns to history and preserves ownership errors', async ({ page }) => {
  await personalFixture(page); await page.goto('/history');
  await page.getByRole('link', { name: 'View recommendations' }).click(); await expect(page).toHaveURL(`/history/${batchId}`);
  await expect(page.getByRole('heading', { name: 'A discovery worth revisiting.' })).toBeVisible();
  await page.getByRole('link', { name: '← Discovery history' }).click(); await expect(page).toHaveURL('/history');
  await page.route(`**/api/discovery/batches/${batchId}`, route => route.fulfill({ status: 404, json: {} }));
  await page.goto(`/history/${batchId}`); await expect(page.getByRole('alert')).toBeVisible();
});
test('History bounds detail requests and reveals only available recent summaries', async ({ page }) => {
  const fixture = await personalFixture(page);
  fixture.overview.page!.batches = Array.from({ length: 10 }, (_, index) => ({ id: `20000000-0000-4000-8000-${String(index).padStart(12, '0')}`, kind: 'OnDemand', createdAtUtc: '2026-10-24T10:00:00Z', completedAtUtc: '2026-10-24T10:00:00Z' }));
  let inflight = 0, maximum = 0, reads = 0;
  await page.route('**/api/discovery/batches/*', async route => {
    inflight++; reads++; maximum = Math.max(maximum, inflight);
    await new Promise(resolve => setTimeout(resolve, 40));
    await route.fulfill({ json: { ...fixture.batch, id: route.request().url().split('/').at(-1) } }); inflight--;
  });
  await page.goto('/history'); await expect(page.locator('.history-card')).toHaveCount(4);
  await expect(page.getByText('04 recommendations', { exact: true })).toHaveCount(4);
  expect(reads).toBe(4); expect(maximum).toBeLessThanOrEqual(2);
  await page.getByRole('button', { name: 'Show more recent sessions' }).click();
  await expect(page.getByText('04 recommendations', { exact: true })).toHaveCount(8);
  expect(reads).toBe(8); expect(maximum).toBeLessThanOrEqual(2);
});
