const state = { accessToken: null, refreshToken: null, user: null, activity: [], displayedDigestWeek: null };
let refreshInFlight = null;
const byId = (id) => document.getElementById(id);
const sessionControls = [...document.querySelectorAll('.requires-session')];

function setMessage(id, message, kind = '') {
  const element = byId(id);
  if (!element) return;
  element.textContent = message ?? '';
  if (kind) element.dataset.kind = kind;
  else delete element.dataset.kind;
}

function announce(message) {
  byId('page-message').textContent = message;
}

function logActivity(method, path, status) {
  const entry = { time: new Date().toLocaleTimeString(), method, path: new URL(path, location.origin).pathname, status };
  state.activity.unshift(entry);
  state.activity.length = Math.min(state.activity.length, 50);
  renderActivity();
}

function renderActivity() {
  const log = byId('activity-log');
  log.replaceChildren();
  for (const item of state.activity) {
    const row = document.createElement('li');
    row.textContent = `${item.time}  ${item.method} ${item.path}  ${item.status}`;
    log.append(row);
  }
}

function updateSessionUi() {
  const signedIn = Boolean(state.accessToken);
  byId('session-indicator').textContent = signedIn ? 'Signed in' : 'Signed out';
  byId('signout-button').disabled = !signedIn;
  sessionControls.forEach((control) => { control.disabled = !signedIn; });
  byId('delete-confirmation').disabled = !signedIn;
  byId('delete-account-button').disabled = !signedIn || byId('delete-confirmation').value !== 'DELETE ACCOUNT';
  if (!signedIn) byId('delete-confirmation').value = '';
}

function saveSession(auth) {
  state.accessToken = auth.accessToken;
  state.refreshToken = auth.refreshToken;
  state.user = auth.user;
  updateSessionUi();
  announce('Signed in. Session tokens remain in this tab only.');
}

function clearSession() {
  state.accessToken = null;
  state.refreshToken = null;
  state.user = null;
  updateSessionUi();
}

async function request(path, options = {}) {
  const url = new URL(path, location.origin);
  if (url.origin !== location.origin) throw new Error('Manual QA requests must stay on this host.');
  const method = (options.method || 'GET').toUpperCase();
  const headers = new Headers(options.headers || {});
  headers.set('Accept', 'application/json');
  if (options.body !== undefined) headers.set('Content-Type', 'application/json');
  if (options.auth !== false && state.accessToken) headers.set('Authorization', `Bearer ${state.accessToken}`);

  let response;
  try {
    response = await fetch(url.pathname + url.search, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      credentials: 'omit',
      cache: 'no-store'
    });
  } catch {
    logActivity(method, url.pathname, 'network error');
    throw new Error('Could not reach this local app host. Check that it is running.');
  }

  logActivity(method, url.pathname, response.status);
  if (response.status === 401 && options.auth !== false && state.accessToken && state.refreshToken && !options.retriedAfterRefresh) {
    try {
      await rotateTokenPair();
    } catch {
      clearSession();
    }
    if (state.accessToken) return request(path, { ...options, retriedAfterRefresh: true });
  }
  if (response.status === 401 && options.auth !== false && state.accessToken) clearSession();
  const contentType = response.headers.get('content-type') || '';
  if (!response.ok) {
    let problem = null;
    if (contentType.includes('json')) {
      try { problem = await response.json(); } catch { problem = null; }
    }
    const message = problem?.detail || problem?.title || `The server returned ${response.status}.`;
    throw new Error(`${message} (HTTP ${response.status})`);
  }
  if (options.rawResponse) return { response, data: null };
  let data = null;
  if (contentType.includes('json')) {
    try { data = await response.json(); } catch { data = null; }
  }
  return { response, data };
}

function renderStatusItem(label, value, kind = '') {
  const item = document.createElement('div');
  item.className = 'status-item';
  const name = document.createElement('span');
  name.className = 'status-label';
  name.textContent = label;
  const result = document.createElement('strong');
  result.className = 'status-value';
  result.textContent = value;
  if (kind) result.dataset.kind = kind;
  item.append(name, result);
  return item;
}

async function refreshStatus() {
  const container = byId('environment-status');
  container.replaceChildren();
  try {
    const [{ data: status }, { response: healthResponse, data: health }] = await Promise.all([
      request('/api/dev/manual-test/status', { auth: false }),
      request('/health', { auth: false })
    ]);
    const rows = [
      ['Host environment', status.environment, status.environment === 'Development' ? 'good' : 'warn'],
      ['Generation input', status.origin, status.origin === 'Synthetic' ? 'good' : 'warn'],
      ['Local health', healthResponse.ok ? 'Healthy' : (health?.status || healthResponse.status), healthResponse.ok ? 'good' : 'warn'],
      ['Storage settings', status.storageConfigured ? 'Configured' : 'Missing', status.storageConfigured ? 'good' : 'warn'],
      ['Inventory file', status.inventoryPresent ? 'Present; reconcile before use' : 'Needs reconciliation', status.inventoryPresent ? 'warn' : 'warn'],
      ['Recorded input', status.recordingConfigured ? `Ready until ${status.recordingExpiresAtUtc}` : 'Not configured', status.recordingConfigured ? 'good' : 'warn']
    ];
    rows.forEach(([label, value, kind]) => container.append(renderStatusItem(label, value, kind)));
    if (status.environment !== 'Development') throw new Error('This QA page requires the Development environment.');
    announce('Environment status refreshed.');
  } catch (error) {
    container.append(renderStatusItem('Status unavailable', error.message, 'warn'));
    setMessage('generation-result', error.message, 'error');
  }
}

function formValue(form, name) {
  return new FormData(form).get(name)?.toString().trim() || '';
}

async function handleRegister(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  button.disabled = true;
  setMessage('register-result', 'Creating disposable account…');
  try {
    const { data } = await request('/api/auth/register', {
      method: 'POST', auth: false, body: {
        userName: formValue(form, 'userName'),
        email: formValue(form, 'email'),
        password: formValue(form, 'password'),
        timeZone: formValue(form, 'timeZone'),
        deliveryDay: formValue(form, 'deliveryDay'),
        deliveryHourUtc: Number(formValue(form, 'deliveryHourUtc'))
      }
    });
    saveSession(data);
    form.reset();
    byId('register-timezone').value = 'UTC';
    byId('register-delivery-hour').value = '8';
    setMessage('register-result', 'Account created and signed in.', 'success');
    await loadProfile();
  } catch (error) {
    setMessage('register-result', error.message, 'error');
  } finally {
    button.disabled = false;
  }
}

async function handleLogin(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  button.disabled = true;
  setMessage('login-result', 'Signing in…');
  try {
    const { data } = await request('/api/auth/login', {
      method: 'POST', auth: false, body: {
        email: formValue(form, 'email'),
        password: formValue(form, 'password')
      }
    });
    saveSession(data);
    form.reset();
    setMessage('login-result', 'Signed in.', 'success');
    await loadProfile();
  } catch (error) {
    setMessage('login-result', error.message, 'error');
  } finally {
    button.disabled = false;
  }
}

async function loadProfile() {
  try {
    const [auth, subscriber] = await Promise.all([
      request('/api/auth/me'),
      request('/api/subscribers/me')
    ]);
    byId('profile-result').textContent = JSON.stringify({ identity: auth.data, subscriber: subscriber.data }, null, 2);
  } catch (error) {
    byId('profile-result').textContent = error.message;
  }
}

async function rotateSession() {
  if (!state.accessToken || !state.refreshToken) {
    setMessage('profile-result', 'Sign in before refreshing the session.', 'error');
    return;
  }
  try {
    await rotateTokenPair();
    setMessage('profile-result', 'Session refreshed; the prior refresh token was rotated.', 'success');
  } catch (error) {
    setMessage('profile-result', error.message, 'error');
  }
}

async function rotateTokenPair() {
  if (refreshInFlight) return refreshInFlight;
  const accessToken = state.accessToken;
  const refreshToken = state.refreshToken;
  const operation = (async () => {
    const { data } = await request('/api/auth/refresh', {
      method: 'POST', auth: false,
      body: { accessToken, refreshToken }
    });
    if (state.accessToken !== accessToken || state.refreshToken !== refreshToken) {
      throw new Error('Session changed while refreshing.');
    }
    saveSession(data);
    return data;
  })();
  refreshInFlight = operation;
  try { return await operation; }
  finally { if (refreshInFlight === operation) refreshInFlight = null; }
}

function splitItems(value) {
  return [...new Set(value.split(',').map((item) => item.trim()).filter(Boolean))];
}

async function saveSeeds(invalidProbe = false) {
  const artists = splitItems(byId('seed-artists').value);
  const tags = splitItems(byId('seed-tags').value);
  const allArtists = invalidProbe ? artists.slice(0, 1) : artists;
  const allTags = invalidProbe ? tags.slice(0, 1) : tags;
  const count = allArtists.length + allTags.length;
  if (!invalidProbe && (count < 3 || count > 5)) {
    setMessage('seeds-result', `Choose 3–5 items total. Current count: ${count}.`, 'error');
    return;
  }
  try {
    const { data } = await request('/api/taste/seed', {
      method: 'POST',
      body: {
        artists: allArtists.map((artistName) => ({ artistName, weight: 1, context: 'Manual QA seed' })),
        tags: allTags.map((tag) => ({ tag, weight: 1, context: 'Manual QA seed' }))
      }
    });
    setMessage('seeds-result', `${data.signalsAdded} seed signals saved. Reconcile storage before the next new digest.`, 'success');
    await loadSignals();
    await refreshStatus();
  } catch (error) {
    setMessage('seeds-result', error.message, 'error');
  }
}

async function loadSignals() {
  try {
    const { data } = await request('/api/taste/signals');
    byId('signals-result').textContent = JSON.stringify(data, null, 2);
  } catch (error) {
    byId('signals-result').textContent = error.message;
  }
}

async function reconcileStorage() {
  setMessage('generation-result', 'Reconciling configured local roots…');
  try {
    const { data } = await request('/api/dev/manual-test/storage/reconcile', { method: 'POST', body: {} });
    if (data.status === 'reconciled') {
      setMessage('generation-result', `Inventory reconciled at ${data.measuredAtUtc}; accounted bytes: ${data.totalBytes}.`, 'success');
    } else {
      setMessage('generation-result', `Storage stopped: ${data.reason}`, 'error');
    }
    await refreshStatus();
  } catch (error) {
    setMessage('generation-result', error.message, 'error');
  }
}

function renderLink(label, href) {
  const link = document.createElement('a');
  link.className = 'button button-secondary';
  link.href = href;
  link.target = '_blank';
  link.rel = 'noopener noreferrer';
  link.textContent = label;
  return link;
}

function renderPick(recommendation) {
  const card = document.createElement('article');
  card.className = 'digest-card';
  const title = document.createElement('h3');
  title.textContent = recommendation.track?.title || 'Unknown track';
  const artist = document.createElement('p');
  artist.className = 'digest-artist';
  artist.textContent = recommendation.track?.artistName || 'Unknown artist';
  const meta = document.createElement('p');
  meta.className = 'digest-meta';
  meta.textContent = `Pick ${recommendation.rank} · score ${(recommendation.scoreBreakdown?.finalScore ?? 0).toFixed(3)} · feedback ${recommendation.feedback}`;

  const links = document.createElement('div');
  links.className = 'action-row';
  const query = encodeURIComponent(`${recommendation.track?.artistName || ''} ${recommendation.track?.title || ''}`);
  links.append(
    renderLink('YouTube', `https://www.youtube.com/results?search_query=${query}`),
    renderLink('Spotify', `https://open.spotify.com/search/${query}`),
    renderLink('Bandcamp', `https://bandcamp.com/search?q=${query}`),
    renderLink('Apple Music', `https://music.apple.com/search?term=${query}`)
  );

  const explanation = document.createElement('p');
  explanation.className = 'panel-copy';
  explanation.textContent = recommendation.whyThisPick || 'No explanation text was returned.';

  const snapshotDetails = document.createElement('details');
  snapshotDetails.className = 'snapshot-toggle';
  const summary = document.createElement('summary');
  summary.textContent = 'Inspect stored score snapshot';
  const snapshot = document.createElement('pre');
  snapshot.textContent = JSON.stringify(recommendation.scoreBreakdown?.snapshot || recommendation.scoreBreakdown, null, 2);
  snapshotDetails.append(summary, snapshot);

  const feedbackLabel = document.createElement('label');
  feedbackLabel.textContent = 'Categorical feedback';
  const feedbackSelect = document.createElement('select');
  feedbackSelect.setAttribute('aria-label', 'Categorical feedback');
  for (const value of ['None', 'Liked', 'Disliked', 'AlreadyKnown']) {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = value;
    option.selected = recommendation.feedback === value;
    feedbackSelect.append(option);
  }

  const ratingLabel = document.createElement('label');
  ratingLabel.textContent = 'Numeric rating';
  const ratingSelect = document.createElement('select');
  ratingSelect.setAttribute('aria-label', 'Numeric rating');
  const unset = document.createElement('option');
  unset.value = '';
  unset.textContent = 'No rating';
  unset.selected = recommendation.rating == null;
  ratingSelect.append(unset);
  for (let rating = 1; rating <= 10; rating++) {
    const option = document.createElement('option');
    option.value = String(rating);
    option.textContent = String(rating);
    option.selected = recommendation.rating === rating;
    ratingSelect.append(option);
  }

  const feedbackResult = document.createElement('p');
  feedbackResult.className = 'inline-result';
  feedbackResult.setAttribute('aria-live', 'polite');
  const saveButton = document.createElement('button');
  saveButton.type = 'button';
  saveButton.className = 'button button-primary';
  saveButton.textContent = 'Save feedback';
  saveButton.addEventListener('click', async () => {
    const rating = ratingSelect.value === '' ? null : Number(ratingSelect.value);
    await recordFeedback(recommendation.id, feedbackSelect.value, rating, feedbackResult);
  });

  card.append(title, artist, meta, links, explanation, snapshotDetails, feedbackLabel, feedbackSelect, ratingLabel, ratingSelect, saveButton, feedbackResult);
  return card;
}

function renderDigest(digest) {
  state.displayedDigestWeek = digest.week;
  byId('digest-week').value = digest.week;
  const container = byId('digest-result');
  container.replaceChildren();
  const heading = document.createElement('p');
  heading.className = 'panel-copy';
  heading.textContent = `Week ${digest.week} · ${digest.recommendations?.length || 0} picks · digest ${digest.id}`;
  container.append(heading);
  if (!digest.recommendations?.length) {
    const empty = document.createElement('p');
    empty.className = 'muted';
    empty.textContent = 'This digest is empty. The server returned a valid persisted digest.';
    container.append(empty);
    return;
  }
  const cards = document.createElement('div');
  cards.className = 'digest-list';
  digest.recommendations.forEach((recommendation) => cards.append(renderPick(recommendation)));
  container.append(cards);
}

async function generateDigest() {
  const week = byId('digest-week').value.trim();
  if (!/^\d{4}-W\d{2}$/.test(week)) {
    setMessage('generation-result', 'Enter an ISO week like 2026-W41.', 'error');
    return;
  }
  setMessage('generation-result', 'Generating from the configured offline input…');
  try {
    const { data } = await request('/api/dev/manual-test/digests', { method: 'POST', body: { week } });
    if (data.status === 'generated' || data.status === 'existing') {
      setMessage('generation-result', `Server result: ${data.status}.`, 'success');
      renderDigest(data.digest);
    } else {
      setMessage('generation-result', `Server result: ${data.status}. ${data.reason || ''}`, 'error');
      byId('digest-result').textContent = JSON.stringify({ reason: data.reason, gaps: data.gaps }, null, 2);
    }
    await refreshStatus();
  } catch (error) {
    setMessage('generation-result', error.message, 'error');
  }
}

async function loadLatestDigest() {
  setMessage('generation-result', 'Loading the latest persisted digest…');
  try {
    const { data } = await request('/api/digests/latest');
    renderDigest(data);
    setMessage('generation-result', 'Latest persisted digest loaded.', 'success');
  } catch (error) {
    byId('digest-result').textContent = error.message;
    setMessage('generation-result', error.message, 'error');
  }
}

async function recordFeedback(recommendationId, feedback, rating, output) {
  const payload = { feedback };
  if (rating !== null) payload.rating = rating;
  output.textContent = 'Saving feedback…';
  try {
    await request(`/api/digests/recommendations/${encodeURIComponent(recommendationId)}/feedback`, { method: 'POST', body: payload });
    output.textContent = 'Saved. Reloading stored digest…';
    await reloadDisplayedDigest();
    output.dataset.kind = 'success';
  } catch (error) {
    output.textContent = error.message;
    output.dataset.kind = 'error';
  }
}

async function reloadDisplayedDigest() {
  if (!state.displayedDigestWeek) throw new Error('Load a digest before saving feedback.');
  const { data } = await request('/api/dev/manual-test/digests', {
    method: 'POST', body: { week: state.displayedDigestWeek }
  });
  if (data.status !== 'existing' && data.status !== 'generated') {
    throw new Error(`Could not reload the displayed digest: ${data.reason || data.status}.`);
  }
  renderDigest(data.digest);
  setMessage('generation-result', `Week ${data.digest.week} reloaded after feedback.`, 'success');
}

async function loadExport() {
  try {
    const { data } = await request('/api/export/my-data');
    byId('export-preview').textContent = JSON.stringify(data, null, 2);
    setMessage('export-result', `Export ${data.exportVersion} loaded (${data.digests?.length || 0} digests).`, 'success');
  } catch (error) {
    setMessage('export-result', error.message, 'error');
  }
}

async function downloadExport() {
  try {
    const { response } = await request('/api/export/my-data?download=true', { rawResponse: true });
    if (!response.ok) throw new Error(`The server returned ${response.status}.`);
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'liner-notes-manual-qa-export.json';
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    setMessage('export-result', 'Export download started.', 'success');
  } catch (error) {
    setMessage('export-result', error.message, 'error');
  }
}

async function deleteAccount() {
  if (byId('delete-confirmation').value !== 'DELETE ACCOUNT') {
    setMessage('delete-result', 'Type DELETE ACCOUNT before continuing.', 'error');
    return;
  }
  if (!window.confirm('Permanently delete this disposable QA account and its account-owned records?')) return;
  try {
    const { response } = await request('/api/subscribers/me', { method: 'DELETE', rawResponse: true });
    const cleanup = response.status === 200 ? await response.json() : null;
    const deletedAccountToken = state.accessToken;
    clearSession();
    setMessage('delete-result', 'Account deleted. Checking that protected access is denied…', 'success');
    try {
      if (!deletedAccountToken) throw new Error('No account token was available for the deletion check.');
      const headers = new Headers({ Accept: 'application/json', Authorization: `Bearer ${deletedAccountToken}` });
      const probe = await fetch('/api/auth/me', { method: 'GET', headers, credentials: 'omit', cache: 'no-store' });
      logActivity('GET', '/api/auth/me', probe.status);
      if (probe.status === 401) setMessage('delete-result', 'Account deleted; its prior bearer token now receives HTTP 401.', 'success');
      else setMessage('delete-result', `Account deletion check expected HTTP 401, received HTTP ${probe.status}.`, 'error');
    } catch (error) {
      setMessage('delete-result', `Account deleted, but token revocation could not be verified: ${error.message}`, 'error');
    }
    if (cleanup?.localCopiesDeleted === false) {
      const issues = Array.isArray(cleanup.issues) ? cleanup.issues.join(', ') : 'unknown cleanup issue';
      setMessage('delete-result', `Account deleted; local copy cleanup incomplete: ${issues}`, 'error');
    }
    byId('profile-result').textContent = 'Deleted account session cleared.';
  } catch (error) {
    setMessage('delete-result', error.message, 'error');
  }
}

function setCurrentIsoWeek() {
  const now = new Date();
  const thursday = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  thursday.setUTCDate(thursday.getUTCDate() - ((thursday.getUTCDay() + 6) % 7) + 3);
  const firstThursday = new Date(Date.UTC(thursday.getUTCFullYear(), 0, 4));
  firstThursday.setUTCDate(firstThursday.getUTCDate() - ((firstThursday.getUTCDay() + 6) % 7) + 3);
  const week = 1 + Math.round((thursday - firstThursday) / 604800000);
  byId('digest-week').value = `${thursday.getUTCFullYear()}-W${String(week).padStart(2, '0')}`;
}

byId('register-form').addEventListener('submit', handleRegister);
byId('login-form').addEventListener('submit', handleLogin);
byId('signout-button').addEventListener('click', () => { clearSession(); announce('Signed out in this tab.'); });
byId('refresh-status-button').addEventListener('click', refreshStatus);
byId('profile-button').addEventListener('click', loadProfile);
byId('refresh-session-button').addEventListener('click', rotateSession);
byId('save-seeds-button').addEventListener('click', () => saveSeeds());
byId('invalid-seed-button').addEventListener('click', () => saveSeeds(true));
byId('load-signals-button').addEventListener('click', loadSignals);
byId('reconcile-button').addEventListener('click', reconcileStorage);
byId('generate-button').addEventListener('click', generateDigest);
byId('load-digest-button').addEventListener('click', loadLatestDigest);
byId('load-export-button').addEventListener('click', loadExport);
byId('download-export-button').addEventListener('click', downloadExport);
byId('delete-account-button').addEventListener('click', deleteAccount);
byId('delete-confirmation').addEventListener('input', (event) => {
  byId('delete-account-button').disabled = !state.accessToken || event.currentTarget.value !== 'DELETE ACCOUNT';
});
byId('clear-activity-button').addEventListener('click', () => { state.activity = []; renderActivity(); });
document.querySelectorAll('[data-check]').forEach((checkbox) => checkbox.addEventListener('change', () => {
  announce(`${checkbox.parentElement.textContent.trim()} ${checkbox.checked ? 'marked reviewed' : 'cleared'}.`);
}));

updateSessionUi();
setCurrentIsoWeek();
refreshStatus();
