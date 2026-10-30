import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { personalFixture } from './fixtures/personal';

for (const width of [390, 768, 1440]) {
  test(`Discover editorial layout at ${width}px — controlled contract data`, async ({ page }) => {
    await personalFixture(page);
    await page.setViewportSize({ width, height: 1000 });
    await page.goto('/discover');
    await expect(page.getByRole('heading', { name: 'Meet your next obsession.' })).toBeVisible();
    await expect(page.locator('.personal-pick')).toHaveCount(4);
    for (const image of await page.locator('.personal-pick img').all()) await image.scrollIntoViewIfNeeded();
    await expect(page.locator('.artwork-ready')).toHaveCount(4);
    await page.evaluate(() => document.fonts.ready);
    await page.evaluate(() => scrollTo(0, 0)); await page.mouse.move(0, 0); await page.waitForTimeout(450);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()).violations).toEqual([]);
    await page.screenshot({ path: `../../../design/superdesign/frontend-review/discover-controlled-${width}.png`, fullPage: true });
  });
}
test('Discover saves feedback and rating with the current profile revision', async ({ page }) => {
  await personalFixture(page); await page.goto('/discover');
  const first = page.locator('.featured-pick');
  await first.getByRole('button', { name: 'Like', exact: true }).click();
  await expect(first.getByRole('button', { name: 'Like', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await first.getByText('Rate or add a note').click();
  await first.getByLabel('Rating for Heaven or Las Vegas').selectOption('8');
  await first.getByLabel('Your note').fill('Saved through the existing endpoint.');
  const sent = page.waitForRequest(r => r.url().includes('/feedback') && r.method() === 'POST');
  await first.getByRole('button', { name: 'Save rating and note' }).click();
  const request = await sent;
  expect(request.postDataJSON()).toMatchObject({ revision: 2, feedback: 'Liked', rating: 8 });
  expect(request.headers()['x-csrf-token']).toBe('test-csrf');
});
test('Discover first visit does not manufacture picks or start a guest', async ({ page }) => {
  await personalFixture(page, { empty: true }); const writes: string[] = [];
  page.on('request', r => { if (r.method() !== 'GET') writes.push(r.url()); });
  await page.goto('/discover'); await expect(page.getByRole('heading', { name: 'Your next discovery starts here' })).toBeVisible();
  await expect(page.locator('.personal-pick')).toHaveCount(0); expect(writes).toEqual([]);
});
test('Discover retries an uncertain submission with the same idempotency key', async ({ page }) => {
  const fixture = await personalFixture(page); const keys: string[] = [];
  const pending = { id: '60000000-0000-4000-8000-000000000001', state: 'Pending', progressCode: 'queued', batchId: null };
  await page.route('**/api/discovery/batches', route => {
    keys.push(route.request().postDataJSON().requestKey);
    if (keys.length === 1) return route.abort();
    fixture.overview.page!.currentJob = pending;
    return route.fulfill({ status: 202, json: pending });
  });
  await page.route('**/api/discovery/jobs/*', route => route.fulfill({ headers: { 'Retry-After': '2' }, json: pending }));
  await page.goto('/discover');
  await page.getByRole('button', { name: 'Find my next favorites', exact: true }).click(); await expect(page.getByRole('alert')).toBeVisible();
  await page.getByRole('button', { name: 'Find my next favorites', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Finding your next favorites…', exact: true })).toBeDisabled();
  expect(keys).toHaveLength(2); expect(keys[0]).toBe(keys[1]);
});
test('Discover polling honors the server deadline and stops after completion', async ({ page }) => {
  const fixture = await personalFixture(page);
  const pending = { id: '60000000-0000-4000-8000-000000000002', state: 'Running', progressCode: 'preparing', batchId: null };
  fixture.overview.page!.currentJob = pending;
  let polls = 0;
  await page.clock.install({ time: new Date('2026-10-26T12:00:00Z') }); await page.clock.pauseAt(new Date('2026-10-26T12:00:01Z'));
  await page.route('**/api/discovery/jobs/*', route => {
    polls++;
    if (polls === 1) return route.fulfill({ headers: { 'Retry-After': '12' }, json: pending });
    fixture.overview.page!.currentJob = { ...pending, state: 'Completed' };
    return route.fulfill({ json: { ...pending, state: 'Completed' } });
  });
  await page.goto('/discover'); await expect(page.getByRole('button', { name: 'Finding your next favorites…' })).toBeDisabled();
  await page.clock.fastForward(1000); await expect.poll(() => polls).toBe(1);
  await page.clock.fastForward(11_000); expect(polls).toBe(1);
  await page.clock.fastForward(1000); await expect.poll(() => polls).toBe(2);
  await expect(page.getByRole('button', { name: 'Find my next favorites', exact: true })).toBeEnabled();
  await page.clock.fastForward(30_000); expect(polls).toBe(2);
});
