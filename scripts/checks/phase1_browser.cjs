const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../src/Presentation/wwwroot/app.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '../../src/Presentation/wwwroot/index.html'), 'utf8');
assert(!/\bon(?:click|submit)\s*=/.test(source + html), 'Inline event handlers allow injected JavaScript');
assert(!/localStorage\.(setItem|getItem)|sessionStorage/.test(source), 'Credentials must stay in memory');

function client(fetch) {
  const elements = new Map();
  function getElement(id) {
    if (!elements.has(id)) elements.set(id, {
      innerHTML: '', replaceChildren() { this.innerHTML = ''; },
      classList: { add() {}, remove() {} }
    });
    return elements.get(id);
  }
  const sandbox = {
    fetch, console, Response,
    localStorage: { removeItem() {} },
    window: { addEventListener() {} },
    document: { addEventListener() {}, getElementById: getElement, querySelectorAll() { return []; } }
  };
  vm.createContext(sandbox);
  vm.runInContext(source + '\nglobalThis.api = {state, acceptSession, apiFetch, logout, renderTrackCard, loadYourData};', sandbox);
  sandbox.api.element = getElement('export-container');
  sandbox.api.acceptSession({ accessToken: 'old-access', refreshToken: 'old-refresh', user: {} });
  return sandbox.api;
}

(async () => {
  let refreshes = 0;
  const api = client(async (url, options) => {
    if (url.endsWith('/auth/refresh')) {
      refreshes++;
      await new Promise(resolve => setTimeout(resolve, 10));
      return Response.json({ accessToken: 'new-access', refreshToken: 'new-refresh' });
    }
    assert.equal(options.credentials, 'omit');
    return new Response('', { status: options.headers.Authorization === 'Bearer new-access' ? 200 : 401 });
  });
  const results = await Promise.all([api.apiFetch('/api/a'), api.apiFetch('/api/b'), api.apiFetch('/api/c')]);
  assert(results.every(r => r.status === 200));
  assert.equal(refreshes, 1, 'Concurrent requests must redeem a refresh token only once');
  assert.equal(api.state.refreshToken, 'new-refresh');

  let release;
  const stale = client(async (url) => {
    if (url.endsWith('/auth/refresh')) return new Promise(resolve => { release = resolve; });
    return new Response('', { status: 401 });
  });
  const pending = stale.apiFetch('/api/private');
  while (!release) await new Promise(resolve => setTimeout(resolve, 0));
  stale.logout();
  release(Response.json({ accessToken: 'must-not-return', refreshToken: 'must-not-return' }));
  await assert.rejects(pending, /Session changed/);
  assert.equal(stale.state.token, null);
  assert.equal(stale.state.refreshToken, null);

  let releaseExport;
  const exportRace = client(async () => ({ ok: true, status: 200,
    json: () => new Promise(resolve => { releaseExport = resolve; }) }));
  const pendingExport = exportRace.loadYourData();
  while (!releaseExport) await new Promise(resolve => setTimeout(resolve, 0));
  exportRace.logout();
  releaseExport({ exportVersion: 'private', exportedAtUtc: new Date().toISOString() });
  await pendingExport;
  assert.equal(exportRace.state.token, null);
  assert.equal(exportRace.element.innerHTML, '', 'Logout must clear private data and ignore late responses');

  let calls = 0;
  const denied = client(async () => { calls++; return new Response('', { status: 401 }); });
  await assert.rejects(denied.apiFetch('/api/private'), /sign in again/);
  assert.equal(calls, 2, 'Failed refresh must not cause a retry loop');
  assert.equal(denied.state.token, null);

  const markup = api.renderTrackCard({ id: "x' onclick='alert(1)", title: '<script>alert(1)</script>', artist: 'A', topTags: [] });
  assert(markup.includes('&lt;script&gt;'));
  assert(markup.includes('data-id="x&#39; onclick=&#39;alert(1)"'));
  assert(!markup.includes('<script>'));
  console.log('PASS browser sessions: one refresh, logout race, bounded retry, memory storage, escaped data attributes');
})().catch(error => { console.error(error); process.exitCode = 1; });
