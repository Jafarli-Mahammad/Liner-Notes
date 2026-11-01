import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { personalFixture } from './fixtures/personal';

const item = { sequence: 10, recordingId: '10000000-0000-4000-8000-000000000001', title: 'A track & a reprise', artistName: 'An artist / duo', origin: 'Public', attribution: 'MusicBrainz · CC0-1.0' };
const cover = '<svg xmlns="http://www.w3.org/2000/svg" width="500" height="500"><rect width="500" height="500" fill="#ccc6ba"/></svg>';

test('Home keeps its sleeve while pending and stops treatment for unavailable artwork', async ({ page }) => {
  await page.clock.install();
  await page.route('**/api/discovery/recent?limit=4', route => route.fulfill({ json: { items: [item], nextCursor: null } }));
  let heads = 0;
  await page.route('**/api/artwork/recordings/*', route => route.fulfill({ status: 404, headers: { 'X-Artwork-State': ++heads === 1 ? 'pending' : 'unavailable', 'Retry-After': '2' } }));
  await page.goto('/');
  await expect(page.locator('.artwork-loading .artwork-placeholder')).toBeVisible();
  await expect(page.locator('.artwork-loading-mark')).toHaveCount(1);
  const before = await page.locator('.artwork-frame').boundingBox();
  await page.clock.fastForward(2500);
  await expect(page.locator('.artwork-missing')).toHaveCount(1);
  await expect(page.locator('.artwork-loading-mark')).toHaveCount(0);
  await page.clock.fastForward(60_000);
  expect(heads).toBe(2);
  expect(await page.locator('.artwork-frame').boundingBox()).toEqual(before);
});

test('Home offers all four encoded service searches and retains feed through reload', async ({ page }) => {
  await page.route('**/api/discovery/recent?limit=4', route => route.fulfill({ json: { items: [item], nextCursor: null } }));
  await page.route('**/api/artwork/recordings/*', route => route.fulfill({ contentType: 'image/svg+xml', body: cover }));
  await page.goto('/');
  for (const service of ['Spotify', 'Apple Music', 'YouTube', 'Bandcamp']) {
    const link = page.getByRole('link', { name: `Search ${service}:`, exact: false });
    await expect(link).toBeVisible();
    expect(decodeURIComponent((await link.getAttribute('href'))!)).toContain(`${item.artistName} ${item.title}`);
  }
  await page.reload();
  await expect(page.getByRole('heading', { name: item.title })).toBeVisible();
});

test('every discovery card offers the same services', async ({ page }) => {
  await personalFixture(page); await page.goto('/discover');
  await expect(page.locator('.music-links')).toHaveCount(4);
  for (const group of await page.locator('.music-links').all()) {
    await expect(group.locator('a')).toHaveCount(4);
    for (const service of ['spotify', 'apple', 'youtube', 'bandcamp']) await expect(group.locator(`.music-link-${service}`)).toBeVisible();
  }
});

test('sign-in password visibility, delayed submission and reduced motion at mobile size', async ({ page }) => {
  await personalFixture(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  let finish!: () => void;
  const pending = new Promise<void>(resolve => { finish = resolve; });
  await page.route('**/api/auth/login', async route => { await pending; await route.fulfill({ status: 401, json: { title: 'Invalid credentials' } }); });
  await page.goto('/account');
  const form = page.getByRole('form', { name: 'Sign in', exact: true });
  await form.getByLabel('Email', { exact: true }).fill('listener@example.test');
  await form.getByLabel('Password', { exact: true }).fill('Test-only-password1!');
  await form.getByRole('button', { name: 'Show password' }).click();
  await expect(form.getByLabel('Password', { exact: true })).toHaveAttribute('type', 'text');
  await form.getByRole('button', { name: 'Hide password' }).click();
  await form.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(form).toHaveAttribute('aria-busy', 'true');
  await expect(form.getByRole('button', { name: 'Signing in…' })).toBeDisabled();
  finish();
  await expect(form.getByRole('alert')).toBeVisible();
  await expect(form.getByLabel('Password', { exact: true })).toHaveValue('Test-only-password1!');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()).violations).toEqual([]);
});

test('History orders saved sessions newest first and keeps the correct batch links after reload', async ({ page }) => {
  const fixture = await personalFixture(page);
  const original = fixture.overview.page!.batches[0];
  const older = { ...original, id: '20000000-0000-4000-8000-000000000002', createdAtUtc: '2026-09-01T10:00:00Z' };
  fixture.overview.page!.batches.unshift(older);
  await page.route(`**/api/discovery/batches/${older.id}`, route => route.fulfill({ json: { ...fixture.batch, id: older.id } }));
  await page.goto('/history');
  await expect(page.locator('.history-card').first().getByRole('link', { name: 'View recommendations' })).toHaveAttribute('href', `/history/${original.id}`);
  await page.reload();
  await expect(page.locator('.history-card')).toHaveCount(2);
  await expect(page.locator('.history-card').first().getByRole('link', { name: 'View recommendations' })).toHaveAttribute('href', `/history/${original.id}`);
});

test('broken image bytes exhaust their recovery budget without an indefinite loading mark', async ({ page }) => {
  await page.clock.install();
  await page.route('**/api/discovery/recent?limit=4', route => route.fulfill({ json: { items: [item], nextCursor: null } }));
  await page.route('**/api/artwork/recordings/*', route => route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/jpeg', body: 'invalid image bytes' }));
  await page.goto('/');
  await expect(page.locator('.artwork-loading')).toHaveCount(1);
  await page.clock.fastForward(11 * 60_000);
  await expect(page.locator('.artwork-missing')).toHaveCount(1);
  await expect(page.locator('.artwork-loading-mark')).toHaveCount(0);
});

test('a stalled public feed becomes a recoverable error instead of a permanent skeleton', async ({ page }) => {
  await page.clock.install();
  let release!: () => void;
  const paused = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/discovery/recent?limit=4', async route => { await paused; await route.fulfill({ json: { items: [], nextCursor: null } }).catch(() => {}); });
  await page.goto('/');
  await expect(page.locator('.skeleton-card')).toHaveCount(4);
  await page.clock.fastForward(20_001);
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.locator('.skeleton-card')).toHaveCount(0);
  release();
});

test('an image GET that stalls after HEAD readiness is retried and decoded', async ({ page }) => {
  await page.clock.install();
  await page.route('**/api/discovery/recent?limit=4', route => route.fulfill({ json: { items: [item], nextCursor: null } }));
  let gets = 0, release!: () => void;
  const paused = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/artwork/recordings/*', async route => {
    if (route.request().method() === 'GET' && ++gets === 1) await paused;
    await route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml', body: cover }).catch(() => {});
  });
  await page.goto('/'); await expect.poll(() => gets).toBe(1);
  await page.clock.fastForward(20_001);
  await expect(page.locator('.artwork-frame img')).toHaveCount(0);
  release(); await page.clock.fastForward(2500);
  await expect(page.locator('.artwork-ready')).toHaveCount(1);
  expect(gets).toBe(2);
});
