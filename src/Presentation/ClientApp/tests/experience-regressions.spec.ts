import { test, expect } from '@playwright/test';
import { personalFixture, batch, overview } from './fixtures/personal';

const user = { id: '80000000-0000-4000-8000-000000000001', email: 'saved@example.test', timeZone: 'UTC', deliveryDay: 'Sunday', deliveryHourUtc: 8, weeklyEmailOptIn: false };
const auth = { accessToken: 'restored-access', refreshToken: 'restored-refresh', expiresIn: 900, user };
async function signedIn(page: import('@playwright/test').Page) {
  await personalFixture(page);
  // Override the fixture's explicit anonymous bootstrap.
  await page.route('**/api/auth/session', route => route.fulfill({ json: auth }));
  await page.route('**/api/auth/me', route => route.fulfill({ json: user }));
}

test('restored session loads newest private history on Home and never calls public feed', async ({ page, baseURL }) => {
  await signedIn(page);
  await page.context().addCookies([{ name: 'liner.signed-in', value: '1', url: baseURL! }]);
  let publicReads = 0;
  await page.route('**/api/discovery/recent?*', route => { publicReads++; return route.fulfill({ json: { items: [], nextCursor: null } }); });
  const newer = { ...batch, id: '20000000-0000-4000-8000-000000000002', picks: [{ ...batch.picks[0], id: 'new-pick', title: 'Newest saved recommendation' }] };
  await page.route('**/api/discovery', route => route.fulfill({ json: { ...overview, page: { ...overview.page, batches: [
    { id: batch.id, createdAtUtc: '2026-01-01T00:00:00Z' }, { id: newer.id, createdAtUtc: '2026-10-31T00:00:00Z' },
  ] } } }));
  await page.route(`**/api/discovery/batches/${newer.id}`, route => {
    expect(route.request().headers().authorization).toBe('Bearer restored-access');
    return route.fulfill({ json: newer });
  });
  await page.goto('/');
  await expect(page.locator('.music-title').first()).toHaveText('Newest saved recommendation');
  await expect(page.locator('.music-card')).toHaveCount(4);
  await expect(page.getByRole('link', { name: 'View all history' })).toBeVisible();
  await page.reload(); await expect(page.locator('.music-title').first()).toHaveText('Newest saved recommendation');
  expect(publicReads).toBe(0);
});

test('personal empty history stays personal and authorization errors do not reveal public data', async ({ page, baseURL }) => {
  await signedIn(page); await page.context().addCookies([{ name: 'liner.signed-in', value: '1', url: baseURL! }]);
  await page.route('**/api/discovery', route => route.fulfill({ json: { ...overview, page: null } }));
  await page.goto('/'); await expect(page.getByRole('heading', { name: 'Your discoveries start here' })).toBeVisible();
  await page.route('**/api/discovery', route => route.fulfill({ status: 403, json: {} }));
  await page.reload(); await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByText('No public recommendations yet', { exact: true })).toHaveCount(0);
});

test('temporary session bootstrap failure offers recovery without displaying guest state', async ({ page, baseURL }) => {
  await signedIn(page); await page.context().addCookies([{ name: 'liner.signed-in', value: '1', url: baseURL! }]);
  await page.route('**/api/auth/session', route => route.fulfill({ status: 503, json: {} }));
  await page.goto('/taste'); await expect(page.getByRole('heading', { name: 'Your session could not be checked' })).toBeVisible();
  await page.route('**/api/auth/session', route => route.fulfill({ json: auth }));
  await page.getByRole('button', { name: 'Try again' }).click(); await expect(page.getByRole('heading', { name: 'Your taste' })).toBeVisible();
});

test('artist photos load with credit, fall back on missing/error and survive refresh', async ({ page }) => {
  await personalFixture(page);
  const artists = [ ['Lin Pesto', '10000000-0000-4000-8000-000000000001'], ['Jakuzi', '10000000-0000-4000-8000-000000000002'], ['Available Artist', '10000000-0000-4000-8000-000000000003'] ];
  await page.route('**/api/discovery', route => route.fulfill({ json: { ...overview, page: { ...overview.page, favorites: artists.map(([name, canonicalId]) => ({ name, canonicalId, catalogRecordId: canonicalId, kind: 'Artist' })) } } }));
  await page.route('**/api/artwork/artists/*', route => route.request().url().includes(artists[2][1]) ? route.fulfill({ json: {
    imageUrl: `/api/artwork/artists/${artists[2][1]}/image`, sourceUrl: 'https://commons.wikimedia.org/wiki/File:Example.jpg', credit: 'Test photographer', license: 'CC BY 4.0', licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
  } }) : route.fulfill({ status: 404 }));
  await page.route('**/api/artwork/artists/*/image', route => route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48"><rect width="48" height="48" fill="green"/></svg>' }));
  await page.goto('/taste'); await expect(page.getByRole('img', { name: 'Photo unavailable for Lin Pesto' })).toBeVisible();
  await expect(page.getByRole('img', { name: 'Photo unavailable for Jakuzi' })).toBeVisible();
  await expect(page.locator('.artist-photo img')).toHaveJSProperty('naturalWidth', 48);
  await expect(page.locator('.artist-photo')).toHaveAttribute('title', /Test photographer.*CC BY 4.0/);
  await page.reload(); await expect(page.locator('.artist-photo img')).toHaveJSProperty('naturalWidth', 48);
  await page.route('**/api/artwork/artists/*/image', route => route.fulfill({ status: 404 }));
  await page.reload(); await expect(page.getByRole('img', { name: 'Photo unavailable for Available Artist' })).toBeVisible();
});

test('double-slash root and direct client routes render on the actual host', async ({ page }) => {
  await personalFixture(page);
  for (const path of ['//', '///?from=launch', '/taste', '/history', '/account']) {
    await page.goto(path.startsWith('//') ? `${test.info().project.use.baseURL}${path}` : path);
    await expect(page.locator('main h1')).toBeVisible();
    await expect(page.getByText('This page could not open', { exact: true })).toHaveCount(0);
  }
});

test('missing browser session during access renewal clears signed-in state', async ({ page, baseURL }) => {
  await signedIn(page); await page.context().addCookies([{ name: 'liner.signed-in', value: '1', url: baseURL! }]);
  await page.goto('/account'); await expect(page.getByRole('heading', { name: 'Good to have you here.' })).toBeVisible();
  await page.route('**/api/auth/session', route => route.fulfill({ status: 204 }));
  await page.route('**/api/discovery', route => route.fulfill(route.request().headers().authorization ? { status: 401, json: {} } : { json: overview }));
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Home', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Sign in', exact: true })).toBeVisible();
});

test('a shared cookie changing account clears the previous identity and private Home state', async ({ page, baseURL }) => {
  await signedIn(page); await page.context().addCookies([{ name: 'liner.signed-in', value: '1', url: baseURL! }]);
  await page.goto('/'); await expect(page.locator('.music-card')).toHaveCount(4);
  const nextUser = { ...user, id: '80000000-0000-4000-8000-000000000002', email: 'second@example.test' };
  await page.route('**/api/auth/session', route => route.fulfill({ json: { ...auth, accessToken: 'second-access', user: nextUser } }));
  await page.route('**/api/discovery', route => route.fulfill(route.request().headers().authorization === 'Bearer second-access'
    ? { json: { ...overview, page: null } } : { status: 401, json: {} }));
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'History', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Discovery history' })).toBeVisible();
  await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: 'Home', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your discoveries start here' })).toBeVisible();
  await expect(page.locator('.music-card')).toHaveCount(0);
});
