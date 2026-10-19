// Synthetic loopback product-flow verification. No browser dependency is added to the application.
const assert = require('node:assert/strict');
const { randomBytes, randomUUID } = require('node:crypto');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require(process.env.LINER_PLAYWRIGHT_MODULE || 'playwright');

async function main() {
  const base = new URL(process.env.LINER_GUEST_DEMO_URL || 'http://127.0.0.1:5092');
  assert.ok(['127.0.0.1', 'localhost', '[::1]'].includes(base.hostname), 'Only a loopback demo is allowed.');
  assert.ok(process.argv.includes('--approve-synthetic'), 'Pass --approve-synthetic for the disposable demo.');
  const output = path.resolve(process.env.LINER_GUEST_DEMO_OUTPUT || '/tmp/liner-guest-browser');
  await fs.mkdir(output, { recursive: true, mode: 0o700 });
  const browser = await chromium.launch({ headless: true,
    ...(process.env.LINER_CHROMIUM_PATH ? { executablePath: process.env.LINER_CHROMIUM_PATH } : {}),
    args: ['--disable-background-networking'] });
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, acceptDownloads: true });
  const page = await context.newPage();
  const errors = [], requests = [], checks = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => requests.push({ method: request.method(), path: new URL(request.url()).pathname }));
  await context.route('**/*', route => new URL(route.request().url()).origin === base.origin ? route.continue() : route.abort());
  const check = name => { checks.push(name); console.log(`PASS ${name}`); };
  const authenticatedJson = route => page.evaluate(async route => {
    const response = await apiFetch(route, { credentials: 'same-origin' });
    if (!response.ok) throw new Error(`Read failed: ${response.status}`);
    return response.json();
  }, route);
  const overview = async () => (await authenticatedJson('/api/discovery')).page;
  const chooseStyle = async style => {
    await page.locator('#nav-seeding').click();
    await page.locator('[data-discovery-action="step"][data-step="tags"]').first().click();
    await page.locator('#taste-tag-search').fill(style);
    await page.locator('#taste-tag-hits [data-style="' + style + '"]').click();
    await page.getByRole('button', { name: 'Continue', exact: true }).click();
    await page.locator('#discovery-kind').waitFor();
    await page.getByRole('button', { name: 'Continue', exact: true }).click();
    await page.getByRole('button', { name: 'Start discovery', exact: true }).click();
    await page.getByText('Picked for your taste', { exact: true }).waitFor({ timeout: 60_000 });
  };
  try {
    const metadata = await (await context.request.get(new URL('/api/discovery', base).href)).json();
    assert.equal(metadata.evidenceOrigin, 'Synthetic', 'This check must never generate against a live provider.');
    await page.goto(base.href);
    await page.getByRole('button', { name: 'Continue without an account', exact: true }).click();
    await page.getByRole('button', { name: 'Skip all questions', exact: true }).click();
    await page.getByRole('heading', { name: 'What sounds like you?', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Continue', exact: true }).click();
    await page.locator('#discovery-kind').waitFor();
    let releaseOld;
    const oldReleased = new Promise(resolve => { releaseOld = resolve; });
    let oldFinished;
    const oldDone = new Promise(resolve => { oldFinished = resolve; });
    const searchRoute = async route => {
      if (new URL(route.request().url()).searchParams.get('query') !== 'Echo') return route.continue();
      try { const response = await route.fetch(); await oldReleased; await route.fulfill({ response }); }
      catch { /* The client deliberately aborts this stale request. */ }
      finally { oldFinished(); }
    };
    await context.route('**/api/catalog/search?**', searchRoute);
    const echoStarted = page.waitForRequest(request => new URL(request.url()).searchParams.get('query') === 'Echo');
    await page.locator('#discovery-search').fill('Echo'); await echoStarted;
    await page.locator('#discovery-search').fill('Meridian');
    await page.getByRole('option').filter({ hasText: 'Synthetic Meridian' }).waitFor();
    releaseOld(); await oldDone;
    assert.equal(await page.getByRole('option').filter({ hasText: 'Synthetic Echo' }).count(), 0);
    await context.unroute('**/api/catalog/search?**', searchRoute);
    check('stale local responses cannot replace a newer search');
    await page.locator('#discovery-search').fill('Atlas');
    await page.getByRole('option').filter({ hasText: 'Synthetic Atlas' }).waitFor();
    await page.locator('#discovery-search').press('ArrowDown');
    await page.locator('#discovery-search').press('Enter');
    await page.locator('#discovery-kind').selectOption('Album');
    await page.locator('#discovery-search').fill('Horizons');
    await page.getByRole('option').filter({ hasText: 'Synthetic Horizons' }).click();
    assert.equal(await page.locator('.discovery-selection').count(), 2);
    assert.equal(requests.filter(r => r.path === '/api/catalog/lookup').length, 0);
    check('artist and album search is local and keyboard selection works');

    for (const [kind, name] of [['Artist', 'Unlisted Demo Artist'], ['Album', 'Unlisted Demo Album']]) {
      await page.locator('#discovery-kind').selectOption(kind);
      await page.locator('#discovery-search').fill(name);
      await page.getByRole('button', { name: 'Search more online', exact: true }).click();
      await page.getByRole('option').filter({ hasText: new RegExp(name, 'i') }).waitFor({ timeout: 45_000 });
      await page.getByRole('option').filter({ hasText: new RegExp(name, 'i') }).click();
    }
    assert.equal(await page.locator('.discovery-selection').count(), 4);
    assert.equal(requests.filter(r => r.method === 'POST' && r.path === '/api/catalog/lookup').length, 2);
    check('missing artists and albums use explicit lookup with independent short pacing');

    await page.getByRole('button', { name: 'Back', exact: true }).click();
    await page.getByRole('heading', { name: 'What sounds like you?', exact: true }).waitFor();
    await page.locator('#taste-tag-search').fill('jazz');
    await page.locator('#taste-tag-hits [data-style=\"jazz\"]').click();
    await page.getByRole('button', { name: 'Continue', exact: true }).click();
    await page.locator('#discovery-kind').waitFor();
    const started = Date.now();
    await page.getByRole('button', { name: 'Continue', exact: true }).click();
    await page.getByRole('button', { name: 'Start discovery', exact: true }).click();
    await page.getByText('Picked for your taste', { exact: true }).waitFor({ timeout: 60_000 });
    const firstBatchMilliseconds = Date.now() - started;
    assert.equal(await page.locator('.discovery-pick').count(), 5);
    assert.ok((await page.locator('.discovery-explanation').allTextContents()).every(text => text.trim().length > 0));
    await page.getByText('Demo recommendations', { exact: true }).waitFor();
    const guestProfile = (await authenticatedJson('/api/discovery/session')).profile.id;
    assert.ok((await context.cookies()).find(cookie => cookie.name === 'liner.guest')?.httpOnly);
    assert.ok(!(await page.evaluate(() => document.cookie)).includes('liner.guest='));
    check('guest receives onsite picks with saved explanations and a private cookie');
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'mobile-results.png'), fullPage: true });
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await page.setViewportSize({ width: 1280, height: 900 });

    const generated = requests.filter(r => r.method === 'POST' && r.path === '/api/discovery/batches').length;
    await page.reload();
    await page.getByText('Picked for your taste', { exact: true }).waitFor();
    assert.equal(requests.filter(r => r.method === 'POST' && r.path === '/api/discovery/batches').length, generated);
    assert.equal((await overview()).batches.length, 1);
    check('returning resumes saved results without automatic generation');

    const card = page.locator('.discovery-pick').first();
    await card.getByRole('button', { name: 'Like', exact: true }).click();
    await page.getByText('Feedback saved. Your next request will use it.', { exact: true }).waitFor();
    await card.locator('[data-discovery-rating]').selectOption('8');
    const note = '<img src=x onerror=window.fixtureXss=1>';
    await card.locator('[data-discovery-comment]').fill(note);
    const feedbackSaved = page.waitForResponse(response => response.url().includes('/feedback') && response.request().method() === 'POST');
    await card.getByRole('button', { name: 'Save rating and note', exact: true }).click();
    assert.equal((await feedbackSaved).status(), 200);
    await page.reload();
    await page.getByText('Picked for your taste', { exact: true }).waitFor();
    assert.equal(await page.locator('.discovery-pick').first().locator('[data-discovery-comment]').inputValue(), note);
    assert.equal(await page.locator('.discovery-pick').first().getByRole('button', { name: 'Like', exact: true }).getAttribute('aria-pressed'), 'true');
    assert.equal(await page.evaluate(() => window.fixtureXss), undefined);
    check('feedback rating and encoded note survive a return visit');

    await page.locator('#nav-your-data').click();
    const downloaded = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Download my data', exact: true }).click();
    const download = await downloaded;
    const exportPath = path.join(output, 'guest-export.json'); await download.saveAs(exportPath);
    const exported = JSON.parse(await fs.readFile(exportPath, 'utf8'));
    assert.equal(exported.discovery.profile.id, guestProfile);
    assert.equal(exported.discovery.batches.length, 1);
    check('guest data exports the owned profile and recommendation history');

    const username = `demo_ui_${randomUUID().slice(0, 8)}`;
    const password = randomBytes(32).toString('base64url');
    await page.locator('#nav-auth').click();
    assert.ok(await page.locator('#reg-preserve').isChecked());
    assert.ok(!await page.locator('#reg-weekly-email').isChecked());
    await page.locator('#reg-username').fill(username);
    await page.locator('#reg-email').fill(`${username}@example.test`);
    await page.locator('#reg-password').fill(password);
    await page.locator('#register-form').getByRole('button', { name: 'Create account', exact: true }).click();
    await page.getByRole('heading', { name: 'Your taste, at a glance', exact: true }).waitFor();
    assert.equal((await authenticatedJson('/api/discovery/session')).profile.id, guestProfile);
    assert.equal((await overview()).batches.length, 1);
    check('registration preserves the same profile and history with weekly email opt-in off');

    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'mobile-taste.png'), fullPage: true });
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    check('taste editor fits a narrow viewport');
    await page.setViewportSize({ width: 1280, height: 900 });

    await page.evaluate(() => logout());
    await page.getByRole('button', { name: 'Continue without an account', exact: true }).waitFor();
    await chooseStyle('ambient');
    const separateGuest = (await authenticatedJson('/api/discovery/session')).profile.id;
    assert.notEqual(separateGuest, guestProfile);
    await page.locator('#nav-auth').click();
    await page.locator('#login-email').fill(`${username}@example.test`);
    await page.locator('#login-password').fill(password);
    await page.locator('#login-form').getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.getByRole('button', { name: 'Import guest progress', exact: true }).waitFor();
    assert.equal((await overview()).batches.length, 1);
    page.once('dialog', dialog => dialog.accept());
    await page.getByRole('button', { name: 'Import guest progress', exact: true }).click();
    await page.waitForFunction(() => !document.querySelector('[data-discovery-action="import"]'));
    assert.equal((await overview()).batches.length, 2);
    assert.equal((await authenticatedJson('/api/discovery/session')).profile.id, guestProfile);
    check('sign-in keeps guest history separate until explicit import');

    const more = page.locator('#discovery-more');
    await more.waitFor();
    await page.waitForFunction(() => document.getElementById('discovery-more')?.disabled === false, undefined, { timeout: 35_000 });
    await more.click();
    await page.getByText('No new supported picks yet', { exact: true }).waitFor({ timeout: 60_000 });
    assert.equal((await overview()).batches.length, 3);
    check('explicit discovery obeys cooldown and reports exhausted fixture coverage honestly');
    await page.screenshot({ path: path.join(output, 'discovery-results.png'), fullPage: true });

    await page.locator('#nav-your-data').click();
    page.once('dialog', dialog => dialog.accept());
    await page.getByRole('button', { name: 'Delete account', exact: true }).click();
    await page.getByRole('button', { name: 'Continue without an account', exact: true }).waitFor();
    await chooseStyle('electronic');
    await page.locator('#nav-your-data').click();
    page.once('dialog', dialog => dialog.accept());
    await page.getByRole('button', { name: 'Clear guest data', exact: true }).click();
    await page.getByRole('button', { name: 'Continue without an account', exact: true }).waitFor();
    assert.equal((await overview()), null);
    assert.ok(!(await context.cookies()).some(cookie => cookie.name === 'liner.guest'));
    check('account deletion and guest clear remove their private data');
    assert.deepEqual(errors, []);
    await fs.writeFile(path.join(output, 'browser-results.json'), JSON.stringify({ checks, firstBatchMilliseconds,
      browser: await browser.version(), requests: requests.length, source: 'Synthetic; mechanics only' }, null, 2));
    console.log(JSON.stringify({ checks: checks.length, firstBatchMilliseconds, source: 'Synthetic; mechanics only' }));
  } finally { await context.close(); await browser.close(); }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
