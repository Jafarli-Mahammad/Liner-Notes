const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../src/Presentation/wwwroot/app.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '../../src/Presentation/wwwroot/index.html'), 'utf8');
assert(!/\bon(?:click|submit)\s*=/.test(source + html), 'Inline event handlers allow injected JavaScript');
assert(!/localStorage\.(setItem|getItem)|sessionStorage/.test(source), 'Credentials must stay in memory');

function classSet(initial = []) {
  const set = new Set(initial);
  return {
    add(value) { set.add(value); },
    remove(value) { set.delete(value); },
    toggle(value, enabled) { if (enabled) set.add(value); else set.delete(value); },
    contains(value) { return set.has(value); }
  };
}

function client(fetch, seedSession = true) {
  const elements = new Map();
  const alerts = [];
  const values = new Map();
  function getElement(id) {
    if (!elements.has(id)) elements.set(id, {
      innerHTML: '', replaceChildren() { this.innerHTML = ''; },
      value: values.get(id) || '', classList: classSet()
    });
    return elements.get(id);
  }
  const sandbox = {
    fetch, console, Response,
    alert: message => alerts.push(message),
    localStorage: { removeItem() {} },
    window: { addEventListener() {} },
    document: { addEventListener() {}, getElementById: getElement, querySelectorAll() { return []; } }
  };
  vm.createContext(sandbox);
  vm.runInContext(source + '\nglobalThis.api = {state, acceptSession, apiFetch, logout, renderTrackCard, loadYourData, handleLogin, handleRegister, submitFeedback, setRatingSelection, handleFeedbackAction};', sandbox);
  sandbox.api.element = getElement('export-container');
  sandbox.api.alerts = alerts;
  sandbox.api.setValue = (id, value) => { values.set(id, value); getElement(id).value = value; };
  sandbox.api.setElement = (id, element) => elements.set(id, element);
  if (seedSession) sandbox.api.acceptSession({ accessToken: 'old-access', refreshToken: 'old-refresh', user: {} });
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

  const authReleases = {};
  const auth = client(async (url, options) => {
    if (url.endsWith('/auth/login')) {
      const email = JSON.parse(options.body).email;
      return new Promise(resolve => { authReleases[email] = resolve; });
    }
    if (url.endsWith('/digests/latest')) return Response.json({ week: '2026-W40', recommendations: [] });
    throw new Error(`Unexpected request ${url}`);
  }, false);
  auth.setValue('login-email', 'account-a@example.test');
  const loginA = auth.handleLogin({ preventDefault() {} });
  auth.setValue('login-email', 'account-b@example.test');
  const loginB = auth.handleLogin({ preventDefault() {} });
  authReleases['account-b@example.test'](Response.json({ accessToken: 'b-access', refreshToken: 'b-refresh', user: { email: 'account-b@example.test' } }));
  await loginB;
  authReleases['account-a@example.test'](Response.json({ accessToken: 'a-access', refreshToken: 'a-refresh', user: { email: 'account-a@example.test' } }));
  await loginA;
  assert.equal(auth.state.user.email, 'account-b@example.test', 'Late earlier login must not replace the newest session');

  let releaseLogin;
  const logoutRace = client(url => url.endsWith('/auth/login')
    ? new Promise(resolve => { releaseLogin = resolve; })
    : Response.json({ week: '2026-W40', recommendations: [] }), false);
  logoutRace.setValue('login-email', 'pending@example.test');
  const pendingLogin = logoutRace.handleLogin({ preventDefault() {} });
  while (!releaseLogin) await new Promise(resolve => setTimeout(resolve, 0));
  logoutRace.logout();
  releaseLogin(Response.json({ accessToken: 'late-access', refreshToken: 'late-refresh', user: { email: 'pending@example.test' } }));
  await pendingLogin;
  assert.equal(logoutRace.state.token, null, 'A pending auth response must not restore a logged-out session');

  let releaseRegistration;
  const registerRace = client(url => url.endsWith('/auth/register')
    ? new Promise(resolve => { releaseRegistration = resolve; })
    : Response.json({ week: '2026-W40', recommendations: [] }), false);
  registerRace.setValue('reg-username', 'pending');
  registerRace.setValue('reg-email', 'pending@example.test');
  registerRace.setValue('reg-password', 'a test password');
  const pendingRegistration = registerRace.handleRegister({ preventDefault() {} });
  while (!releaseRegistration) await new Promise(resolve => setTimeout(resolve, 0));
  registerRace.logout();
  releaseRegistration(Response.json({ accessToken: 'late-access', refreshToken: 'late-refresh', user: { email: 'pending@example.test' } }));
  await pendingRegistration;
  assert.equal(registerRace.state.token, null, 'A pending registration must not restore a logged-out session');

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

  const button = (text, active = false) => ({
    textContent: String(text), style: {}, classList: classSet(active ? ['active'] : [])
  });
  const knownButton = button('Known', true);
  const ratingButtons = [button(8, true), button(9)];
  const feedbackButtons = [knownButton, button('Like'), button('Dislike')];
  const card = {
    querySelector(selector) {
      if (selector === '.btn-feedback.known.active') return knownButton.classList.contains('active') ? knownButton : null;
      return null;
    },
    querySelectorAll(selector) { return selector === '.btn-rating-num' ? ratingButtons : feedbackButtons; }
  };
  const sentPayloads = [];
  const feedbackClient = client(async (_url, options) => {
    sentPayloads.push(JSON.parse(options.body));
    return Response.json({ ok: true });
  });
  feedbackClient.setElement('card-rec-1', card);
  await feedbackClient.handleFeedbackAction({ id: 'rec-1', feedback: 'Liked', rating: '9', guest: 'false' });
  assert.equal(sentPayloads[0].feedback, 'AlreadyKnown', 'Rating a familiar track must preserve familiarity');
  assert.equal(sentPayloads[0].rating, 9);
  await feedbackClient.submitFeedback('rec-1', 'Liked', null, false);
  assert(ratingButtons.every(b => !b.classList.contains('active')), 'A sentiment-only update must clear rating highlights');

  const failures = client(async () => Response.json({ title: 'Not found' }, { status: 404 }));
  failures.setElement('card-rec-1', card);
  await failures.submitFeedback('rec-1', 'Liked', null, false);
  assert(failures.alerts.some(message => message.includes('Feedback was not saved') && message.includes('Not found')),
    'HTTP feedback failures must be visible and retryable');

  let calls = 0;
  const denied = client(async () => { calls++; return new Response('', { status: 401 }); });
  await assert.rejects(denied.apiFetch('/api/private'), /sign in again/);
  assert.equal(calls, 2, 'Failed refresh must not cause a retry loop');
  assert.equal(denied.state.token, null);

  const markup = api.renderTrackCard({ id: "x' onclick='alert(1)", title: '<script>alert(1)</script>', artist: 'A', topTags: [] });
  assert(markup.includes('&lt;script&gt;'));
  assert(markup.includes('data-id="x&#39; onclick=&#39;alert(1)"'));
  assert(!markup.includes('<script>'));
  console.log('PASS browser: refresh and auth races, logout, rating/familiarity state, visible feedback failures, memory storage, escaped data attributes');
})().catch(error => { console.error(error); process.exitCode = 1; });
