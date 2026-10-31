import { test, expect } from '@playwright/test';
import { personalFixture } from './fixtures/personal';

test('receipt limits are reported as an unsaved request rather than a provider outage', async ({ page }) => {
  const fixture = await personalFixture(page);
  const job = { id: '60000000-0000-4000-8000-000000000007', state: 'Running', progressCode: 'preparing' };
  fixture.overview.page!.currentJob = job;
  await page.clock.install();
  await page.route('**/api/discovery/jobs/*', route => {
    fixture.overview.page!.currentJob = { ...job, state: 'Failed', errorCode: 'snapshot_limit' };
    return route.fulfill({ json: fixture.overview.page!.currentJob });
  });
  await page.goto('/discover');
  await expect(page.getByRole('button', { name: 'Finding your next favorites…' })).toBeDisabled();
  await page.clock.fastForward(2000);
  await expect(page.getByRole('alert')).toContainText('could not be saved within the discovery limits');
  await expect(page.getByRole('button', { name: 'Find my next favorites', exact: true })).toBeEnabled();
});

test('unavailable artwork does not restart HEAD polling after visibility changes', async ({ page }) => {
  await personalFixture(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  let reads = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    if (route.request().url().endsWith('10000000-0000-4000-8000-000000000000')) reads++;
    return route.fulfill({ status: 404, headers: { 'X-Artwork-State': 'unavailable' }, body: '' });
  });
  await page.goto('/discover');
  await expect(page.locator('.featured-pick .artwork-missing')).toHaveCount(1);
  expect(reads).toBe(1);
  await page.evaluate(() => scrollTo(0, document.body.scrollHeight));
  await expect.poll(() => page.locator('.featured-pick .artwork-frame').evaluate(el => el.getBoundingClientRect().bottom)).toBeLessThan(-200);
  await page.waitForTimeout(100);
  await page.evaluate(() => scrollTo(0, 0));
  await page.waitForTimeout(250);
  expect(reads).toBe(1);
  await expect(page.locator('.featured-pick img')).toHaveCount(0);
});

test('completed discoveries recover from a failed reload without submitting again', async ({ page }) => {
  const fixture = await personalFixture(page);
  const job = { id: '60000000-0000-4000-8000-000000000009', state: 'Running', progressCode: 'preparing' };
  fixture.overview.page!.currentJob = job;
  let reads = 0, polls = 0, submissions = 0;
  await page.clock.install();
  await page.route('**/api/discovery', route => {
    if (++reads === 2) return route.fulfill({ status: 503, json: {} });
    return route.fulfill({ json: fixture.overview });
  });
  await page.route('**/api/discovery/batches', route => { submissions++; return route.abort(); });
  await page.route('**/api/discovery/jobs/*', route => {
    polls++;
    fixture.overview.page!.currentJob = { ...job, state: 'Completed' };
    fixture.overview.page!.batches = [{ id: '20000000-0000-4000-8000-000000000009', kind: 'OnDemand', createdAtUtc: new Date().toISOString(), completedAtUtc: new Date().toISOString() }];
    fixture.batch.picks[0].title = 'Recovered discoveries';
    return route.fulfill({ json: fixture.overview.page!.currentJob });
  });
  await page.goto('/discover');
  await expect(page.getByRole('button', { name: 'Finding your next favorites…' })).toBeDisabled();
  await page.clock.fastForward(2000);
  await expect(page.getByRole('alert')).toBeVisible();
  await page.clock.fastForward(2000);
  await expect(page.getByRole('heading', { name: 'Recovered discoveries' })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  expect(polls).toBe(1); expect(submissions).toBe(0);
});

test('permanent status errors stop polling', async ({ page }) => {
  const fixture = await personalFixture(page);
  fixture.overview.page!.currentJob = { id: '60000000-0000-4000-8000-000000000008', state: 'Running', progressCode: 'preparing' };
  let polls = 0;
  await page.clock.install();
  await page.route('**/api/discovery/jobs/*', route => { polls++; return route.fulfill({ status: 404, json: {} }); });
  await page.goto('/discover');
  await expect(page.getByRole('button', { name: 'Finding your next favorites…' })).toBeDisabled();
  await page.clock.fastForward(2000);
  await expect(page.getByRole('alert')).toContainText('no longer available');
  await page.clock.fastForward(60_000);
  expect(polls).toBe(1);
});

test('pending covers recover after two minutes and accept real 80px art', async ({ page }) => {
  await personalFixture(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.clock.install();
  let reads = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    if (!route.request().url().includes('10000000-0000-4000-8000-000000000000'))
      return route.fulfill({ headers: { 'X-Artwork-State': 'unavailable' }, body: '' });
    if (route.request().method() === 'HEAD') reads++;
    if (reads < 12) return route.fulfill({ headers: { 'X-Artwork-State': 'pending', 'Retry-After': '2' }, body: '' });
    return route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80"><rect width="80" height="80" fill="red"/></svg>' });
  });
  await page.goto('/discover');
  await expect.poll(() => reads).toBe(1);
  for (let i = 0; i < 11; i++) {
    await page.clock.fastForward(15_000);
    await expect.poll(() => reads).toBe(i + 2);
  }
  await expect(page.locator('.featured-pick .artwork-ready')).toHaveCount(1);
});
