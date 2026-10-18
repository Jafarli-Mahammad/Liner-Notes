// Guest and account discovery share the same profile API. No provider calls happen in this client.
window.discoveryApp = (() => {
  const html = escapeHtml;
  let enabled = null, session = null, page = null, styles = [], evidenceOrigin = null;
  let favorites = [], tags = new Set(), dirty = false, pendingSubmission = null;
  let version = 0, timer = null, countdown = null, searchTimer = null, searchAbort = null;
  let hits = [], activeHit = -1, searchVersion = 0;

  function leave() {
    version++;
    clearTimeout(timer); clearTimeout(searchTimer); clearInterval(countdown);
    searchAbort?.abort(); searchVersion++;
  }
  function sessionChanged() {
    leave(); session = null; page = null; dirty = false; favorites = []; tags.clear(); pendingSubmission = null;
    document.querySelectorAll('[data-discovery-root]').forEach(node => node.replaceChildren());
  }
  function message(text, error = false) {
    const target = document.querySelector('.tab-pane.active [data-discovery-message]');
    if (!target) return;
    target.textContent = text;
    target.classList.toggle('discovery-error', error);
    target.setAttribute('role', error ? 'alert' : 'status');
  }
  function errorMessage(status) {
    return ({ 400: 'Check your selections and try again.', 401: 'Your session has expired. Start again or sign in.',
      403: 'Reload the page before trying again.', 409: 'Your profile or request changed. Reload your discoveries to continue.',
      429: 'Please wait a moment before trying again.',
      503: 'New requests are temporarily paused. Your saved results are still available.' })[status]
      ?? 'That request could not finish. Please try again.';
  }
  async function request(path, { method = 'GET', body, signal } = {}) {
    const headers = {};
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = session?.csrfToken ?? '';
    const response = await apiFetch(`${API_BASE}${path}`, { method, credentials: 'same-origin', headers,
      signal, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
    if (!response.ok) {
      const seconds = Math.min(600, Math.max(1, Number(response.headers.get('Retry-After')) || 1));
      if (response.status === 429 && page) page.nextRequestAtUtc = new Date(Date.now() + seconds * 1000).toISOString();
      const error = new Error(errorMessage(response.status)); error.status = response.status; throw error;
    }
    return response.status === 204 ? null : response.json();
  }
  async function bootstrap() {
    try { session = await request('/discovery/session'); enabled = true; }
    catch (error) { if (error.status === 404) { enabled = false; return false; } throw error; }
    document.getElementById('reg-preserve-label').hidden = !session.profile?.isGuest;
    const overview = await request('/discovery');
    page = overview.page; styles = overview.styles; evidenceOrigin = overview.evidenceOrigin;
    if (!dirty) {
      favorites = (page?.favorites ?? []).map(f => ({ id: f.catalogRecordId, name: f.name, artistName: f.artistName, kind: f.kind }));
      tags = new Set(page?.tags ?? []);
    }
    return true;
  }
  async function ensureProfile() {
    if (!session) await bootstrap();
    if (!session.profile) {
      await request('/discovery/session', { method: 'POST' });
      await bootstrap();
    }
  }
  function root(id) { return document.getElementById(id); }
  function shell() {
    return '<p data-discovery-message class="discovery-message" role="status" aria-live="polite"></p>';
  }
  function importNotice() {
    return session?.guestImportAvailable ? `<div class="notice"><strong>You also have guest discoveries</strong>
      <p>Import that taste and history into this account when you are ready.</p>
      <button class="button button-secondary" data-discovery-action="import">Import guest progress</button></div>` : '';
  }
  async function renderDiscovery() {
    leave(); const current = version, target = root('discovery-container');
    try {
      if (!await bootstrap()) return false;
      if (current !== version) return true;
      target.setAttribute('data-discovery-root', '');
      target.innerHTML = shell() + importNotice() + `<div id="discovery-progress" role="status" aria-live="polite"></div>
        <div id="discovery-results"></div>`;
      if (!session.profile) {
        root('discovery-results').innerHTML = `<div class="surface discovery-welcome"><h3>Find something you’ll want to hear.</h3>
          <p>Pick a few favorite artists, albums or styles. Get your first discoveries here, without an account.</p>
          ${evidenceOrigin === 'Synthetic' ? '<p class="notice notice-preview">Demo mode uses synthetic music data.</p>' : ''}
          <button class="button button-primary" data-discovery-action="edit">Try as a guest</button>
          <p class="field-help">Guest taste and results expire after seven inactive days. You can export or clear them any time.</p></div>`;
      } else if (page.batches.length) {
        await showBatch(page.batches[0].id, current);
      } else {
        root('discovery-results').innerHTML = `<div class="surface"><h3>Your first discoveries start with your taste</h3>
          <p>Choose artists, albums or styles to get started.</p>
          <button class="button button-primary" data-discovery-action="edit">Choose my favorites</button></div>`;
      }
      if (page?.currentJob && ['Pending', 'Running', 'Retryable'].includes(page.currentJob.state))
        pollGeneration(page.currentJob.id, current);
      return true;
    } catch (error) {
      if (current !== version) return true;
      target.innerHTML = shell() + '<button class="button button-secondary" data-discovery-action="reload">Try again</button>';
      message(error.message, true); return true;
    }
  }
  function progress(job) {
    const copy = ({ queued: 'Your discoveries are queued.', waiting_for_music: 'Getting music for your taste…',
      preparing: 'Finding and checking your picks…', persisting: 'Saving your discoveries…', retrying: 'The music source needs a moment. Your request is still queued.' })[job.progressCode]
      ?? (job.state === 'Retryable' ? 'Waiting to retry the music source…' : 'Finding music for your taste…');
    const target = root('discovery-progress');
    if (target) { target.textContent = copy; target.className = 'notice'; }
  }
  async function pollGeneration(id, current) {
    if (current !== version || state.activeTab !== 'discovery') return;
    try {
      const job = await request(`/discovery/jobs/${encodeURIComponent(id)}`);
      if (current !== version) return;
      if (job.state === 'Completed') {
        await bootstrap();
        root('discovery-progress').replaceChildren(); root('discovery-progress').className = '';
        if (job.batchId) await showBatch(job.batchId, current);
        return;
      }
      if (['Failed', 'Cancelled'].includes(job.state)) {
        root('discovery-progress').replaceChildren(); root('discovery-progress').className = '';
        message(job.state === 'Cancelled' ? 'Your taste changed, so that request was cancelled. Choose your next request when ready.'
          : 'The music source could not complete this request. Your saved picks are still here. Try again later.', true);
        await bootstrap(); renderMoreControls(); return;
      }
      progress(job);
      timer = setTimeout(() => pollGeneration(id, current), 2000);
    } catch (error) {
      if (current !== version) return;
      message(error.message, true);
      timer = setTimeout(() => pollGeneration(id, current), 5000);
    }
  }
  function pickCard(pick) {
    return `<article class="surface discovery-pick" data-pick="${html(pick.id)}" data-current-feedback="${html(pick.feedback)}">
      <div class="discovery-pick-heading"><span class="discovery-rank" aria-label="Pick ${pick.rank}">${pick.rank}</span>
        <div><h3>${html(pick.title)}</h3><p>${html(pick.artistName)}${pick.album ? ` — ${html(pick.album)}` : ''}</p></div></div>
      <p class="discovery-explanation">${html(pick.explanation)}</p>
      ${pick.matchedTags.length ? `<p class="field-help">Shared styles: ${pick.matchedTags.map(html).join(', ')}</p>` : ''}
      ${buildDeepLinks(pick.artistName, pick.title)}
      <fieldset class="discovery-feedback"><legend>How does this pick fit?</legend><div class="action-row">
        ${[['Liked', 'Like'], ['Disliked', 'Pass'], ['AlreadyKnown', 'Already know it']].map(([value, label]) =>
          `<button type="button" class="button button-secondary" data-discovery-action="feedback" data-feedback="${value}" aria-pressed="${pick.feedback === value}">${label}</button>`).join('')}
        <button type="button" class="button button-quiet" data-discovery-action="feedback" data-feedback="None" data-clear="true">Clear feedback</button></div>
        <div class="discovery-feedback-fields"><label>Rating <select data-discovery-rating aria-label="Rating for ${html(pick.title)}">
          <option value="">No rating</option>${Array.from({ length: 10 }, (_, i) => `<option value="${i + 1}"${pick.rating === i + 1 ? ' selected' : ''}>${i + 1}/10</option>`).join('')}
        </select></label><label>A note <span class="optional-label">Optional</span><input data-discovery-comment maxlength="1000" value="${html(pick.comment ?? '')}" aria-label="Note for ${html(pick.title)}"></label>
        <button class="button button-secondary" data-discovery-action="feedback" data-save="true">Save rating and note</button></div>
      </fieldset><p class="field-help">Music evidence: ${pick.sources.map(html).join(', ') || 'Stored source evidence'}</p></article>`;
  }
  async function showBatch(id, current = version) {
    const batch = await request(`/discovery/batches/${encodeURIComponent(id)}`);
    if (current !== version) return;
    root('discovery-results').innerHTML = `${batch.coverage?.Origin === 'Synthetic' ? '<div class="notice notice-preview"><strong>Demo recommendations</strong><p>These use synthetic music data to demonstrate the flow.</p></div>' : ''}
      <div class="discovery-batch-heading"><h3>Picked for your taste</h3><time datetime="${html(batch.createdAtUtc)}">${html(new Date(batch.createdAtUtc).toLocaleDateString())}</time></div>
      ${batch.picks.length ? batch.picks.map(pickCard).join('') : '<div class="surface"><h3>No new supported picks yet</h3><p>Try different favorites or styles. We will not repeat your previous picks just to fill the list.</p></div>'}
      ${batch.coverage?.Gaps?.length ? '<p class="field-help">Some selections had limited source data for this batch.</p>' : ''}
      <div id="discovery-more-controls"></div>
      ${page.batches.length > 1 ? `<details class="surface"><summary>Earlier discoveries</summary><div class="action-row">${page.batches.map(b =>
        `<button class="button button-secondary" data-discovery-action="history" data-id="${html(b.id)}">${html(new Date(b.createdAtUtc).toLocaleString())}</button>`).join('')}</div></details>` : ''}`;
    renderMoreControls();
  }
  function renderMoreControls() {
    const target = root('discovery-more-controls'); if (!target) return;
    target.innerHTML = '<div class="action-row"><button id="discovery-more" class="button button-primary" data-discovery-action="more">Discover more</button><button class="button button-secondary" data-discovery-action="edit">Edit my taste</button></div>';
    clearInterval(countdown);
    const update = () => {
      const button = root('discovery-more'); if (!button) return;
      const remaining = Math.max(0, Math.ceil((Date.parse(page?.nextRequestAtUtc ?? '') - Date.now()) / 1000)) || 0;
      const busy = page?.currentJob && ['Pending', 'Running', 'Retryable'].includes(page.currentJob.state);
      button.disabled = remaining > 0 || Boolean(busy);
      button.textContent = busy ? 'Discoveries in progress' : remaining ? `Discover more in ${remaining}s` : 'Discover more';
    };
    update(); countdown = setInterval(update, 1000);
  }
  async function renderTaste() {
    leave(); const current = version, target = root('tab-seeding');
    try {
      if (!await bootstrap() || current !== version) return;
      target.setAttribute('data-discovery-root', '');
      target.innerHTML = `<div class="section-heading"><div><h2>What do you love listening to?</h2><p>A few favorites are enough to get started. Choose up to five artists or albums, plus up to five styles.</p></div></div>
        <div class="surface discovery-editor">${shell()}<div class="discovery-search-row"><label>Search for
          <select id="discovery-kind"><option value="Artist">Artists</option><option value="Album">Albums</option></select></label>
          <label class="discovery-search-label" for="discovery-search">A favorite artist or album
          <input id="discovery-search" type="search" maxlength="128" autocomplete="off" role="combobox" aria-autocomplete="list" aria-controls="discovery-hits" aria-expanded="false" placeholder="Start typing a name"></label></div>
        <p id="discovery-search-status" class="field-help" role="status" aria-live="polite">Search the prepared catalog. Typing stays local to this app.</p>
        <ul id="discovery-hits" class="discovery-hits" role="listbox" aria-label="Matching artists and albums" hidden></ul>
        <div class="action-row"><button class="button button-secondary" data-discovery-action="lookup">Search more online</button>
          <button id="discovery-local-more" class="button button-quiet" data-discovery-action="local-more" hidden>More local matches</button></div>
        <p class="field-help">Missing someone? “Search more online” checks the music catalog provider only when you ask.</p>
        <div id="discovery-favorites" class="discovery-favorites" aria-label="Selected favorites"></div>
        <fieldset class="discovery-styles"><legend>Styles you enjoy <span class="optional-label">Optional</span></legend><div class="tag-cloud">
        ${styles.map(tag => `<button type="button" class="tag-btn" data-discovery-action="tag" data-style="${html(tag)}" aria-pressed="${tags.has(tag)}">${html(tag)}</button>`).join('')}</div></fieldset>
        <p id="discovery-selection-count" class="field-help" aria-live="polite"></p>
        <button class="button button-primary" data-discovery-action="generate">Find my music</button>
        <p class="field-help">${session.profile?.isGuest || !session.profile ? 'Your discoveries appear here. No account is required.' : 'Your new taste will apply to this request.'}</p></div>`;
      updateSelections();
      root('discovery-search').addEventListener('input', queueSearch);
      root('discovery-search').addEventListener('keydown', searchKeys);
      root('discovery-kind').addEventListener('change', queueSearch);
    } catch (error) { if (current === version) { target.innerHTML = shell(); message(error.message, true); } }
  }
  function updateSelections() {
    const target = root('discovery-favorites'); if (!target) return;
    target.innerHTML = favorites.map((item, index) => `<span class="discovery-selection"><span>${html(item.name)}${item.artistName ? ` <small>by ${html(item.artistName)}</small>` : ''}${!item.id ? ' <small>Find a catalog match before saving</small>' : ''}</span>
      <button type="button" data-discovery-action="remove" data-index="${index}" aria-label="Remove ${html(item.name)}">×</button></span>`).join('');
    root('discovery-selection-count').textContent = `${favorites.length}/5 favorites and ${tags.size}/5 styles selected.`;
  }
  function queueSearch() {
    clearTimeout(searchTimer); searchAbort?.abort(); searchVersion++;
    root('discovery-local-more').hidden = true;
    if (root('discovery-search').value.trim().length < 2) { showHits([]); root('discovery-search-status').textContent = 'Type at least two letters to search.'; return; }
    searchTimer = setTimeout(() => searchLocal(0), 150);
  }
  function showHits(items) {
    hits = items; activeHit = -1;
    const target = root('discovery-hits'); if (!target) return;
    target.innerHTML = items.map((item, i) => `<li id="discovery-hit-${i}" role="option" aria-selected="false" data-discovery-action="pick" data-index="${i}">
      <strong>${html(item.name)}</strong><span>${html([item.artistName, item.disambiguation, item.firstReleaseDate?.slice(0, 4)].filter(Boolean).join(' — '))}</span>
      ${item.tags?.length ? `<small>${item.tags.slice(0, 4).map(html).join(', ')}</small>` : ''}${item.origin === 'Synthetic' ? '<small>Demo catalog entry</small>' : ''}</li>`).join('');
    target.hidden = !items.length;
    root('discovery-search').setAttribute('aria-expanded', String(Boolean(items.length)));
    root('discovery-search').removeAttribute('aria-activedescendant');
  }
  async function searchLocal(offset) {
    const input = root('discovery-search'); if (!input) return;
    const query = input.value.trim(), kind = root('discovery-kind').value, current = ++searchVersion;
    searchAbort?.abort(); searchAbort = new AbortController();
    root('discovery-search-status').textContent = 'Searching the local catalog…';
    try {
      const response = await fetch(`/api/catalog/search?query=${encodeURIComponent(query)}&kind=${kind}&limit=10&offset=${offset}`, { signal: searchAbort.signal, credentials: 'omit' });
      if (!response.ok) throw new Error(errorMessage(response.status));
      const result = await response.json(); if (current !== searchVersion) return;
      showHits(result.items);
      root('discovery-search-status').textContent = result.items.length ? `${result.items.length} local matches. Use the arrow keys and Enter to choose.` : 'No local matches. You can search more online.';
      root('discovery-local-more').hidden = !result.hasMore;
      root('discovery-local-more').dataset.offset = String(offset + 10);
    } catch (error) { if (current === searchVersion && error.name !== 'AbortError') root('discovery-search-status').textContent = error.message; }
  }
  function searchKeys(event) {
    if (event.key === 'Escape') { showHits([]); return; }
    if (!hits.length) return;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault(); activeHit = (activeHit + (event.key === 'ArrowDown' ? 1 : -1) + hits.length) % hits.length;
      root('discovery-hits').querySelectorAll('[role="option"]').forEach((node, i) => node.setAttribute('aria-selected', String(i === activeHit)));
      root('discovery-search').setAttribute('aria-activedescendant', `discovery-hit-${activeHit}`);
      root(`discovery-hit-${activeHit}`).scrollIntoView({ block: 'nearest' });
    } else if (event.key === 'Enter' && activeHit >= 0) { event.preventDefault(); chooseHit(activeHit); }
  }
  function chooseHit(index) {
    const item = hits[index]; if (!item) return;
    if (favorites.length >= 5) { message('Choose up to five artists or albums.', true); return; }
    if (!favorites.some(f => f.id === item.id || (f.canonicalId && f.canonicalId === item.canonicalId))) {
      favorites.push(item); dirty = true; updateSelections();
    }
    showHits([]); root('discovery-search').value = ''; root('discovery-search').focus();
    root('discovery-search-status').textContent = 'Added to your favorites.';
  }
  async function lookup() {
    const query = root('discovery-search').value.trim(), kind = root('discovery-kind').value;
    if (query.length < 2) { message('Enter at least two letters before searching more.', true); return; }
    const current = version, queryVersion = ++searchVersion;
    clearTimeout(searchTimer); searchAbort?.abort();
    await ensureProfile();
    const job = await request('/catalog/lookup', { method: 'POST', body: { query, kind, requestKey: crypto.randomUUID() } });
    if (current !== version || queryVersion !== searchVersion) return;
    root('discovery-search-status').textContent = 'Searching the music catalog provider…';
    const poll = async () => {
      if (current !== version || queryVersion !== searchVersion) return;
      try {
        const result = await request(`/catalog/lookups/${encodeURIComponent(job.id)}`);
        if (current !== version || queryVersion !== searchVersion) return;
        if (result.state === 'Completed') {
          showHits(result.items ?? []);
          root('discovery-search-status').textContent = result.errorCode ? 'Those search matches are no longer available. Search again.'
            : result.items?.length ? 'Choose a match from the catalog provider.' : 'The provider found no matches. Try another spelling.';
        } else if (['Failed', 'Cancelled'].includes(result.state)) {
          root('discovery-search-status').textContent = 'The provider search could not finish. Try again later.';
        } else timer = setTimeout(poll, 2000);
      } catch (error) { if (current === version && queryVersion === searchVersion) root('discovery-search-status').textContent = error.message; }
    };
    await poll();
  }
  async function submit(selection) {
    const current = version;
    if (selection && (!favorites.length && !tags.size)) throw new Error('Choose at least one artist, album or style.');
    if (selection && favorites.some(item => !item.id)) throw new Error('Find a catalog match for each saved artist, or remove that name before saving new taste.');
    await ensureProfile();
    const body = { revision: page?.revision ?? session.profile.revision,
      ...(selection ? { selection: { catalogRecordIds: favorites.map(item => item.id), tags: [...tags] } } : {}) };
    const fingerprint = JSON.stringify(body);
    if (pendingSubmission?.fingerprint !== fingerprint) pendingSubmission = { fingerprint, body: { ...body, requestKey: crypto.randomUUID() } };
    const job = await request('/discovery/batches', { method: 'POST', body: pendingSubmission.body });
    pendingSubmission = null; dirty = false;
    if (current === version) switchTab('discovery'); // Overview resumes this exact durable request; visiting never creates another batch.
    return job;
  }
  async function feedback(button) {
    const card = button.closest('[data-pick]');
    const clear = button.dataset.clear === 'true';
    const value = button.dataset.save === 'true' ? card.dataset.currentFeedback : button.dataset.feedback;
    const result = await request(`/discovery/recommendations/${encodeURIComponent(card.dataset.pick)}/feedback`, {
      method: 'POST', body: { revision: page.revision, feedback: value,
        rating: clear ? null : Number(card.querySelector('[data-discovery-rating]').value) || null,
        comment: clear ? null : card.querySelector('[data-discovery-comment]').value || null } });
    page.revision = result.revision;
    card.dataset.currentFeedback = value;
    card.querySelectorAll('[data-feedback]').forEach(node => node.setAttribute('aria-pressed', String(!clear && node.dataset.feedback === value)));
    if (clear) { card.querySelector('[data-discovery-rating]').value = ''; card.querySelector('[data-discovery-comment]').value = ''; }
    message('Feedback saved. Your next request will use it.');
  }
  async function renderData() {
    const current = version;
    try {
      if (!await bootstrap() || !session.profile?.isGuest) return false;
      if (current !== version) return true;
      const target = root('export-container'); target.setAttribute('data-discovery-root', '');
      target.innerHTML = shell() + `<div class="surface"><h3>Your guest data</h3><p>Your taste, saved picks, explanations and feedback belong to this guest session.</p>
        <p>Expires after inactivity: <time datetime="${html(session.profile.expiresAtUtc)}">${html(new Date(session.profile.expiresAtUtc).toLocaleString())}</time>.</p>
        <div class="action-row"><button class="button button-primary" data-discovery-action="export">Download my data</button>
          <button class="button button-secondary" data-discovery-action="clear">Clear guest data</button>
          <button class="button button-secondary" data-action="tab" data-tab="auth">Keep it with an account</button></div></div>`;
      return true;
    } catch (error) { if (current === version) { root('export-container').innerHTML = shell(); message(error.message, true); } return true; }
  }
  async function registrationOptions() {
    if (!await bootstrap() || state.token || !session.profile?.isGuest || !root('reg-preserve').checked) return {};
    return { preserve: true, csrf: session.csrfToken };
  }
  document.addEventListener('click', async event => {
    const button = event.target.closest('[data-discovery-action]'); if (!button) return;
    event.preventDefault();
    const action = button.dataset.discoveryAction;
    const clickedVersion = version;
    try {
      if (action === 'edit') { switchTab('seeding'); return; }
      if (action === 'pick') { chooseHit(Number(button.dataset.index)); return; }
      if (action === 'tag') {
        const tag = button.dataset.style;
        if (tags.has(tag)) tags.delete(tag);
        else if (tags.size < 5) tags.add(tag);
        else { message('Choose up to five styles.', true); return; }
        dirty = true; button.setAttribute('aria-pressed', String(tags.has(tag))); updateSelections(); return;
      }
      if (action === 'remove') { favorites.splice(Number(button.dataset.index), 1); dirty = true; updateSelections(); return; }
      button.disabled = true;
      if (action === 'lookup') await lookup();
      if (action === 'local-more') await searchLocal(Number(button.dataset.offset));
      if (action === 'generate') await submit(true);
      if (action === 'more') await submit(false);
      if (action === 'reload') await renderDiscovery();
      if (action === 'history') await showBatch(button.dataset.id);
      if (action === 'feedback') await feedback(button);
      if (action === 'import' && confirm('Import your guest taste and recommendation history into this account?')) {
        await request('/discovery/session/import', { method: 'POST' }); dirty = false; await renderDiscovery();
      }
      if (action === 'clear' && confirm('Permanently clear this guest taste, recommendations and feedback?')) {
        await request('/discovery/session', { method: 'DELETE' }); sessionChanged(); switchTab('discovery');
      }
      if (action === 'export') {
        const data = await request('/discovery/session/data');
        const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
        const link = document.createElement('a'); link.href = url; link.download = 'liner-notes-guest.json'; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      }
    } catch (error) { message(error.message, true); }
    finally {
      if (button.isConnected && clickedVersion === version) button.disabled = false;
      if (action === 'more' && clickedVersion === version) renderMoreControls();
    }
  });
  return { get enabled() { return enabled; }, renderDiscovery, renderTaste, renderData, registrationOptions, sessionChanged, leave };
})();
