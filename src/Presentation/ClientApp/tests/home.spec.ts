import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';

// These inputs exist only in browser interception tests, never in the application bundle.
const items = Array.from({ length: 4 }, (_, index) => ({
  sequence: index + 1, recordingId: `10000000-0000-4000-8000-00000000000${index}`,
  title: ['Heaven or Las Vegas', 'Violator', 'Bloom', 'Ambient 1: Music for Airports'][index],
  artistName: ['Cocteau Twins', 'Depeche Mode', 'Beach House', 'Brian Eno'][index],
  origin: 'Public', attribution: 'MusicBrainz · CC0-1.0',
}));
const cover = '<svg xmlns="http://www.w3.org/2000/svg" width="500" height="500"><rect width="500" height="500" fill="#DCCFFD"/></svg>';

async function feed(page: Page, value: unknown = { items, nextCursor: null }) {
  await page.route('**/api/discovery/recent?limit=4', route => route.fulfill({ json: value }));
  await page.route('**/api/artwork/recordings/*', route => route.fulfill({ contentType: 'image/svg+xml', body: cover }));
}

for (const width of [390, 768, 1440]) {
  test(`real API and Home integration at ${width}px`, async ({ page, request }) => {
    const response = await request.get('/api/discovery/recent?limit=4');
    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toContain('application/json');
    expect(response.headers()['cache-control']).toContain('no-store');
    const data = await response.json();
    expect(Array.isArray(data.items)).toBe(true);
    const requests: { url: string; method: string }[] = [];
    const errors: string[] = [];
    page.on('request', req => requests.push({ url: req.url(), method: req.method() }));
    page.on('pageerror', error => errors.push(error.message));
    await page.setViewportSize({ width, height: 1000 });
    const host = await page.goto('/');
    expect(host?.headers()['content-security-policy']).toContain("connect-src 'self'");
    await expect(page.getByRole('heading', { name: 'Recent recommendations' })).toBeVisible();
    if (data.items.length) await expect(page.locator('.music-card')).toHaveCount(data.items.length);
    else await expect(page.getByRole('heading', { name: 'No public recommendations yet' })).toBeVisible();
    await page.evaluate(() => document.fonts.ready);
    await expect(page.locator('.brand img')).toBeVisible();
    expect(await page.locator('.brand img').evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBe(true);
    expect(await page.locator('.brand img').evaluate((img: HTMLImageElement) => Number(img.getAttribute('width')) / Number(img.getAttribute('height')) === img.naturalWidth / img.naturalHeight)).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(requests.filter(req => req.method !== 'GET')).toEqual([]);
    expect(requests.filter(req => !req.url.startsWith(new URL(page.url()).origin))).toEqual([]);
    expect(requests.filter(req => /discovery\/session|api\/catalog/.test(req.url))).toEqual([]);
    expect(errors).toEqual([]);
    await page.screenshot({ path: `../../../design/superdesign/home-checkpoint/live-${width}.png`, fullPage: true });
    const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(axe.violations).toEqual([]);
  });

  test(`populated cards and accessibility at ${width}px — intercepted data`, async ({ page }) => {
    await feed(page);
    await page.setViewportSize({ width, height: 1000 });
    await page.goto('/');
    await expect(page.locator('.music-card')).toHaveCount(4);
    for (const image of await page.locator('.music-card img').all()) await image.scrollIntoViewIfNeeded();
    await expect(page.locator('.artwork-ready')).toHaveCount(4);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(await page.locator('.music-grid').evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length)).toBe(width === 390 ? 1 : width === 768 ? 2 : 4);
    const axes = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(axes.violations).toEqual([]);
    expect(await page.locator('.artwork-frame').locator('button, a').count()).toBe(0);
  });
}

test('keyboard skip link and mobile disclosure close/restore focus', async ({ page }) => {
  await feed(page);
  await page.setViewportSize({ width: 390, height: 1000 });
  await page.goto('/');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Skip to content' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('main')).toBeFocused();
  await page.getByRole('button', { name: 'Open navigation' }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Close navigation' })).toHaveAttribute('aria-expanded', 'true');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('navigation', { name: 'Mobile navigation' }).getByRole('link', { name: 'Home', exact: true })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('button', { name: 'Open navigation' })).toBeFocused();
  await expect(page.getByRole('navigation', { name: 'Mobile navigation' })).toBeHidden();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.screenshot({ path: '../../../design/superdesign/home-checkpoint/mobile-menu-test.png', fullPage: true });
  await page.setViewportSize({ width: 768, height: 1000 });
  await page.setViewportSize({ width: 390, height: 1000 });
  await expect(page.getByRole('button', { name: 'Open navigation' })).toHaveAttribute('aria-expanded', 'false');
});

test('loading geometry and empty response remain honest', async ({ page }) => {
  let resolve!: () => void;
  const pending = new Promise<void>(done => { resolve = done; });
  await page.route('**/api/discovery/recent?limit=4', async route => { await pending; await route.fulfill({ json: { items: [], nextCursor: null } }); });
  await page.goto('/');
  await expect(page.getByRole('status')).toHaveText('Loading recent recommendations…');
  await expect(page.locator('.skeleton-card')).toHaveCount(4);
  await page.screenshot({ path: '../../../design/superdesign/home-checkpoint/loading-test.png', fullPage: true });
  resolve();
  await expect(page.getByRole('heading', { name: 'No public recommendations yet' })).toBeVisible();
  await expect(page.locator('.music-card')).toHaveCount(0);
});

for (const kind of ['network', '503', '404', 'html', 'invalid']) {
  test(`recoverable feed failure: ${kind}`, async ({ page }) => {
    let retry = false;
    await page.route('**/api/discovery/recent?limit=4', route => {
      if (retry) return route.fulfill({ json: { items: [], nextCursor: null } });
      if (kind === 'network') return route.abort();
      if (kind === 'html') return route.fulfill({ contentType: 'text/html', body: '<html>Not JSON</html>' });
      if (kind === 'invalid') return route.fulfill({ json: { items: [{ title: 'Incomplete' }], nextCursor: null } });
      return route.fulfill({ status: Number(kind), json: { title: 'Unavailable' } });
    });
    await page.goto('/');
    await expect(page.getByRole('alert')).toBeVisible();
    if (kind === '503') await page.screenshot({ path: '../../../design/superdesign/home-checkpoint/error-test.png', fullPage: true });
    await expect(page.locator('.music-card')).toHaveCount(0);
    retry = true;
    await page.getByRole('button', { name: 'Try again', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'No public recommendations yet' })).toBeVisible();
  });
}

for (const header of ['seconds', 'date']) {
  test(`Retry-After ${header} prevents early retry`, async ({ page }) => {
    await page.clock.install({ time: new Date('2026-10-26T12:00:00Z') });
    await page.clock.pauseAt(new Date('2026-10-26T12:00:01Z'));
    let count = 0;
    await page.route('**/api/discovery/recent?limit=4', route => {
      count++;
      return route.fulfill({ status: 429, headers: { 'Retry-After': header === 'seconds' ? '10' : 'Mon, 26 Oct 2026 12:00:11 GMT' }, json: {} });
    });
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Try again in 10s' })).toBeDisabled();
    await page.clock.fastForward(9000);
    expect(count).toBe(1);
    await expect(page.getByRole('button', { name: 'Try again in 1s' })).toBeDisabled();
    await page.clock.fastForward(1000);
    await expect(page.getByRole('button', { name: 'Try again', exact: true })).toBeEnabled();
  });
}

test('unavailable artwork stops retrying while transport failures have bounded recovery', async ({ page }) => {
  await page.clock.install();
  await feed(page, { items: items.map((item, index) => ({ ...item, recordingId: index === 3 ? 'not-a-recording' : item.recordingId })), nextCursor: null });
  let requests = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    requests++;
    if (route.request().url().includes(items[0].recordingId)) return route.abort();
    return route.fulfill({ headers: { 'X-Artwork-State': 'unavailable' }, contentType: 'image/svg+xml', body: cover.replace('width="500" height="500"', 'width="80" height="80"') });
  });
  await page.goto('/');
  await expect(page.locator('.artwork-missing')).toHaveCount(3);
  const initial = requests;
  for (let i = 0; i < 5; i++) { await page.clock.fastForward(30_000); await page.waitForTimeout(100); }
  expect(requests).toBeLessThanOrEqual(initial + 12);
  await expect(page.locator('.artwork-ready')).toHaveCount(0);
  await expect(page.getByText('Cover unavailable')).toHaveCount(3);
});

test('reduced motion and long text reflow at 320px', async ({ page }) => {
  await feed(page, { items: items.map(item => ({ ...item, title: 'A very long recording title with a title that must wrap without covering the artwork or leaving the page' })), nextCursor: null });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.setViewportSize({ width: 320, height: 1000 });
  await page.goto('/');
  await expect(page.locator('.music-card')).toHaveCount(4);
  await page.locator('.music-card').first().hover();
  expect(await page.locator('.artwork-frame').first().evaluate(el => ({ transition: getComputedStyle(el).transitionDuration, transform: getComputedStyle(el).transform }))).toEqual({ transition: '0s', transform: 'none' });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('personal navigation opens the React taste editor without a legacy redirect', async ({ page }) => {
  await page.goto('/taste');
  await expect(page).toHaveURL(/\/taste$/);
  await expect(page.getByRole('heading', { name: 'Your taste', exact: true })).toBeVisible();
  await expect(page.locator('#tab-seeding')).toHaveCount(0);
});


test('artwork recovers when GET fails after a successful readiness check', async ({ page }) => {
  await page.clock.install();
  await feed(page, { items: [items[0]], nextCursor: null });
  let gets = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    if (route.request().method() === 'GET' && ++gets === 1) return route.abort();
    return route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml', body: cover });
  });
  await page.goto('/');
  await expect.poll(() => gets).toBe(1);
  await page.clock.fastForward(2500);
  await expect(page.locator('.artwork-ready')).toHaveCount(1);
  expect(gets).toBe(2);
});


test('artwork honors a negative-cache retry window without repeated HEAD requests', async ({ page }) => {
  await page.clock.install();
  await feed(page, { items: [items[0]], nextCursor: null });
  let heads = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    if (route.request().method() === 'HEAD' && ++heads === 1)
      return route.fulfill({ headers: { 'X-Artwork-State': 'pending', 'Retry-After': '60' }, body: '' });
    return route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml', body: cover });
  });
  await page.goto('/'); await expect.poll(() => heads).toBe(1);
  await page.clock.fastForward(45_000); expect(heads).toBe(1);
  await page.clock.fastForward(16_000);
  await expect(page.locator('.artwork-ready')).toHaveCount(1); expect(heads).toBe(2);
});

test('artwork visibility changes do not reset the total retry deadline', async ({ page }) => {
  await page.clock.install();
  await feed(page, { items: [items[0]], nextCursor: null });
  let heads = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    heads++; return route.fulfill({ headers: { 'X-Artwork-State': 'pending' }, body: '' });
  });
  await page.goto('/'); await expect.poll(() => heads).toBe(1);
  await page.evaluate(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await page.clock.fastForward(11 * 60_000);
  await page.evaluate(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await expect(page.locator('.artwork-missing')).toHaveCount(1); expect(heads).toBe(1);
});


test('an image GET failure while hidden recovers when the tab becomes visible', async ({ page }) => {
  await feed(page, { items: [items[0]], nextCursor: null });
  let gets = 0, release!: () => void;
  const failed = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/artwork/recordings/*', async route => {
    if (route.request().method() === 'GET' && ++gets === 1) { await failed; return route.abort(); }
    return route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml', body: cover });
  });
  await page.goto('/'); await expect.poll(() => gets).toBe(1);
  await page.evaluate(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await page.waitForTimeout(100); release();
  await expect(page.locator('.artwork-frame img')).not.toHaveAttribute('src');
  await page.evaluate(() => {
    Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await expect(page.locator('.artwork-ready')).toHaveCount(1); expect(gets).toBe(2);
});


test('artwork recovers when the ready cover expires between HEAD and GET', async ({ page }) => {
  await page.clock.install(); await feed(page, { items: [items[0]], nextCursor: null });
  let gets = 0;
  await page.route('**/api/artwork/recordings/*', route => {
    if (route.request().method() === 'GET' && ++gets === 1)
      return route.fulfill({ status: 503, headers: { 'X-Artwork-State': 'pending', 'Retry-After': '2' }, body: '' });
    return route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml', body: cover });
  });
  await page.goto('/'); await expect.poll(() => gets).toBe(1);
  await page.clock.fastForward(2500);
  await expect(page.locator('.artwork-ready')).toHaveCount(1); expect(gets).toBe(2);
});


test('offscreen artwork starts its retry budget on first visible demand', async ({ page }) => {
  await page.clock.install(); await feed(page, { items: [items[0]], nextCursor: null });
  let release!: () => void, heads = 0;
  const pendingFeed = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/discovery/recent?limit=4', async route => {
    await pendingFeed; return route.fulfill({ json: { items: [items[0]], nextCursor: null } });
  });
  await page.route('**/api/artwork/recordings/*', route => {
    if (route.request().method() === 'HEAD') heads++;
    return route.fulfill({ headers: { 'X-Artwork-State': 'ready' }, contentType: 'image/svg+xml', body: cover });
  });
  await page.goto('/'); await page.addStyleTag({ content: '.artwork-frame { transform: translateY(3000px); }' }); release();
  await expect(page.locator('.artwork-frame')).toHaveCount(1);
  await page.clock.fastForward(11 * 60_000); expect(heads).toBe(0);
  await page.addStyleTag({ content: '.artwork-frame { transform: none !important; }' });
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
  await expect(page.locator('.artwork-ready')).toHaveCount(1); expect(heads).toBe(1);
});
