// Liner Notes Web Client
const API_BASE = '/api';

const state = {
  token: null,
  refreshToken: null,
  sessionEpoch: 0,
  authAttemptId: 0,
  user: null,
  selectedTags: new Set(),
  activeTab: 'discovery'
};

// Remove credentials persisted by older versions; sessions now live only in this tab's memory.
localStorage.removeItem('linernotes_token');
let refreshInFlight = null;

function acceptSession(data) {
  state.sessionEpoch++;
  state.authAttemptId++;
  state.token = data.accessToken;
  state.refreshToken = data.refreshToken;
  state.user = data.user;
  window.discoveryApp?.sessionChanged(true);
}

async function refreshSession(epoch) {
  if (refreshInFlight) return refreshInFlight;
  const operation = (async () => {
    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST', credentials: 'omit', cache: 'no-store',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ accessToken: state.token, refreshToken: state.refreshToken })
    });
    if (state.sessionEpoch !== epoch) throw new Error('Session changed.');
    if (!res.ok) { logout(); throw new Error('Please sign in again.'); }
    const data = await res.json();
    if (state.sessionEpoch !== epoch) throw new Error('Session changed.');
    state.token = data.accessToken;
    state.refreshToken = data.refreshToken;
  })();
  refreshInFlight = operation;
  try { await operation; }
  finally { if (refreshInFlight === operation) refreshInFlight = null; }
}

async function apiFetch(url, options = {}) {
  if (!url.startsWith(`${API_BASE}/`)) throw new Error('Invalid API address.');
  const epoch = state.sessionEpoch;
  const token = state.token;
  const send = () => fetch(url, { ...options, credentials: options.credentials ?? 'omit', cache: 'no-store',
    headers: { ...options.headers, ...(state.token ? { Authorization: `Bearer ${state.token}` } : {}) } });
  let response = await send();
  if (epoch !== state.sessionEpoch) throw new Error('Session changed.');
  if (response.status === 401 && state.refreshToken) {
    if (token === state.token) await refreshSession(epoch);
    if (epoch !== state.sessionEpoch) throw new Error('Session changed.');
    response = await send();
    if (epoch !== state.sessionEpoch) throw new Error('Session changed.');
  }
  if (response.status === 401 && state.token) logout();
  return response;
}

function escapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

const sampleGuestPicks = [
  {
    id: "00000000-0000-0000-0000-000000000001",
    title: "Ageispolis",
    artist: "Aphex Twin",
    album: "Selected Ambient Works 85-92",
  },
  {
    id: "00000000-0000-0000-0000-000000000002",
    title: "A Forest",
    artist: "The Cure",
    album: "Seventeen Seconds",
  },
  {
    id: "00000000-0000-0000-0000-000000000003",
    title: "Yolculuk",
    artist: "Altın Gün",
    album: "Gece",
  }
];

function buildDeepLinks(artist, title) {
  const q = encodeURIComponent(`${artist} ${title}`);
  return `
    <div class="deep-links">
      <a class="deep-link youtube" href="https://www.youtube.com/results?search_query=${q}" target="_blank" rel="noopener">
        ▶ YouTube
      </a>
      <a class="deep-link spotify" href="https://open.spotify.com/search/${q}" target="_blank" rel="noopener">
        🟢 Spotify
      </a>
      <a class="deep-link bandcamp" href="https://bandcamp.com/search?q=${q}" target="_blank" rel="noopener">
        🎵 Bandcamp
      </a>
      <a class="deep-link apple" href="https://music.apple.com/search?term=${q}" target="_blank" rel="noopener">
        🍎 Apple Music
      </a>
    </div>
  `;
}

function renderScoreBreakdown(item) {
  const score = Number(item.score) || 0;
  const tagScore = Number(item.tagScore) || 0;
  const novelty = Number(item.noveltyBoost) || 0;
  const feedbackPenalty = Number(item.feedbackPenalty) || 0;
  const tags = Array.isArray(item.topTags) ? item.topTags : [];

  return `
    <details class="reason-details">
      <summary>Why this was picked <span class="score-pill">Overall score ${score.toFixed(3)}</span></summary>
      <div class="breakdown-box">
        <strong>Score details saved with this pick</strong>
        <div class="breakdown-grid">
          <div class="breakdown-item"><span class="label">Taste match</span><span class="val">${tagScore.toFixed(3)}</span></div>
          <div class="breakdown-item"><span class="label">Novelty adjustment</span><span class="val">${novelty >= 0 ? '+' : ''}${novelty.toFixed(3)}</span></div>
          <div class="breakdown-item"><span class="label">Feedback adjustment</span><span class="val">${feedbackPenalty > 0 ? '-' : ''}${feedbackPenalty.toFixed(3)}</span></div>
        </div>
        ${tags.length ? `<p class="matched-tags"><strong>Matched styles:</strong> ${tags.map(escapeHtml).join(', ')}</p>` : ''}
      </div>
    </details>
  `;
}

function renderTrackCard(track, isGuest = false) {
  const safeId = escapeHtml(track.id);
  const safeTitle = escapeHtml(track.title);
  const safeArtist = escapeHtml(track.artist);
  const safeAlbum = escapeHtml(track.album || '');
  const feedbackControls = isGuest ? '' : `
    <div class="feedback-actions" aria-label="Rate this recommendation">
      <button class="btn-feedback liked ${track.feedback === 'Liked' ? 'active' : ''}" data-action="feedback" data-id="${safeId}" data-feedback="Liked" data-guest="false">I like this</button>
      <button class="btn-feedback disliked ${track.feedback === 'Disliked' ? 'active' : ''}" data-action="feedback" data-id="${safeId}" data-feedback="Disliked" data-guest="false">Not for me</button>
      <button class="btn-feedback known ${track.feedback === 'AlreadyKnown' ? 'active' : ''}" data-action="feedback" data-id="${safeId}" data-feedback="AlreadyKnown" data-guest="false">Already know it</button>
    </div>
    <div class="rating-bar" aria-label="Rate this pick from 1 to 10">
      <span class="rating-label">Rate this pick</span>
      ${[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(rating => `
        <button class="btn-rating-num ${track.rating === rating ? 'active' : ''}" data-action="feedback" data-id="${safeId}" data-feedback="${rating >= 7 ? 'Liked' : rating <= 3 ? 'Disliked' : 'None'}" data-rating="${rating}" data-guest="false" aria-label="Rate ${rating} out of 10">${rating}</button>
      `).join('')}
    </div>
  `;

  return `
    <article class="card track-card" id="card-${safeId}">
      <div class="track-card-head">
        <div class="track-heading">
          <h3 class="card-title">${safeTitle}</h3>
          <div class="track-artist">${safeArtist}</div>
          ${safeAlbum ? `<div class="track-meta">${safeAlbum}</div>` : ''}
        </div>
        ${isGuest ? '<div class="track-labels"><span class="preview-pill">Example only</span></div>' : ''}
      </div>
      ${buildDeepLinks(track.artist, track.title)}
      ${isGuest ? '<p class="action-note">Feedback can be saved to your account on generated recommendations.</p>' : renderScoreBreakdown(track)}
      ${feedbackControls}
    </article>
  `;
}

async function loadDiscovery() {
  if (window.discoveryApp && await window.discoveryApp.renderDiscovery()) return;
  const container = document.getElementById('discovery-container');
  const epoch = state.sessionEpoch;

  if (!state.token) {
    container.innerHTML = `
      <div class="notice notice-preview">
        <strong>Preview examples only</strong>
        <p>These sample tracks are fixed demo content, not recommendations generated from your taste. Create an account and add 3–5 artists or styles to save a profile.</p>
        <p><a href="#tab-auth" data-action="tab" data-tab="auth">Create an account</a> or <a href="#tab-seeding" data-action="tab" data-tab="seeding">set up your taste profile</a>.</p>
      </div>
      <div class="content-stack">${sampleGuestPicks.map(pick => renderTrackCard(pick, true)).join('')}</div>
    `;
    return;
  }

  container.innerHTML = '<p role="status">Loading your weekly picks…</p>';
  try {
    const response = await apiFetch(`${API_BASE}/digests/latest`, {
      headers: { Authorization: `Bearer ${state.token}` }
    });
    if (epoch !== state.sessionEpoch) return;

    if (response.status === 404) {
      container.innerHTML = `
        <div class="notice">
          <strong>No weekly picks yet</strong>
          <p>Add a few artists or styles to your taste profile. A digest has not been generated for this account yet.</p>
          <p><button class="button button-primary" type="button" data-action="tab" data-tab="seeding">Set up your taste profile</button></p>
        </div>
      `;
      return;
    }
    if (!response.ok) throw new Error('Could not load your weekly picks.');

    const digest = await response.json();
    if (epoch !== state.sessionEpoch) return;
    const items = digest.recommendations.map(recommendation => ({
      id: recommendation.id,
      title: recommendation.track?.title || 'Unknown track',
      artist: recommendation.track?.artistName || 'Unknown artist',
      album: recommendation.track?.albumTitle || '',
      score: recommendation.scoreBreakdown?.finalScore || 0,
      tagScore: recommendation.scoreBreakdown?.tagSimilarityScore || 0,
      noveltyBoost: recommendation.scoreBreakdown?.noveltyBoost || 0,
      feedbackPenalty: recommendation.scoreBreakdown?.feedbackPenalty || 0,
      topTags: (recommendation.scoreBreakdown?.matchedTags || []).map(tag => tag.tagName),
      feedback: recommendation.feedback,
      rating: recommendation.rating
    }));

    container.innerHTML = `
      <p class="data-meta">Week of ${escapeHtml(digest.week)} · ${items.length} ${items.length === 1 ? 'pick' : 'picks'}</p>
      <div class="content-stack">${items.map(pick => renderTrackCard(pick)).join('')}</div>
    `;
  } catch (error) {
    if (epoch !== state.sessionEpoch) return;
    container.innerHTML = `<div class="notice notice-error">${escapeHtml(error.message)}</div>`;
  }
}

async function submitFeedback(recommendationId, feedbackType, rating = null, isGuest = false) {
  if (isGuest) {
    const card = document.getElementById(`card-${recommendationId}`);
    if (card) {
      card.querySelectorAll('.btn-feedback').forEach(b => b.classList.remove('active'));
      const btn = card.querySelector(`.btn-feedback.${feedbackType.toLowerCase() === 'alreadyknown' ? 'known' : feedbackType.toLowerCase()}`);
      if (btn) btn.classList.add('active');
      setRatingSelection(card, rating);
    }
    return;
  }

  const epoch = state.sessionEpoch;
  try {
    const payload = { feedback: feedbackType };
    if (rating !== null) payload.rating = rating;

    const res = await apiFetch(`${API_BASE}/digests/recommendations/${recommendationId}/feedback`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.token}`
      },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      let detail = '';
      try {
        const problem = await res.json();
        detail = problem.detail || problem.title || '';
      } catch { /* Non-JSON error responses use the status fallback. */ }
      throw new Error(detail || `The server returned ${res.status}.`);
    }

    const card = document.getElementById(`card-${recommendationId}`);
    if (card) {
      card.querySelectorAll('.btn-feedback').forEach(b => b.classList.remove('active'));
      const btn = card.querySelector(`.btn-feedback.${feedbackType.toLowerCase() === 'alreadyknown' ? 'known' : feedbackType.toLowerCase()}`);
      if (btn) btn.classList.add('active');
      setRatingSelection(card, rating);
    }
  } catch (err) {
    if (epoch !== state.sessionEpoch) return;
    alert(`Feedback was not saved: ${err.message} Please try again.`);
  }
}

function setRatingSelection(card, rating) {
  card.querySelectorAll('.btn-rating-num').forEach(button => {
    const selected = rating !== null && Number(button.textContent.trim()) === rating;
    button.classList.toggle('active', selected);
  });
}

function feedbackForRating(card, feedback) {
  return card?.querySelector('.btn-feedback.known.active') ? 'AlreadyKnown' : feedback;
}

function handleFeedbackAction(data) {
  const rating = data.rating ? Number(data.rating) : null;
  const card = rating === null ? null : document.getElementById(`card-${data.id}`);
  const feedback = rating === null ? data.feedback : feedbackForRating(card, data.feedback);
  return submitFeedback(data.id, feedback, rating, data.guest === 'true');
}

function toggleTag(tag) {
  if (state.selectedTags.has(tag)) {
    state.selectedTags.delete(tag);
  } else {
    if (getSeedCount() >= 5) {
      alert('Choose no more than 5 artists and styles altogether.');
      return;
    }
    state.selectedTags.add(tag);
  }
  updateTagUI();
}

function getSeedCount() {
  const artists = document.getElementById('seed-artists').value
    .split(',')
    .map(artist => artist.trim())
    .filter(Boolean);
  return state.selectedTags.size + artists.length;
}

function updateTagUI() {
  document.querySelectorAll('.tag-btn').forEach(button => {
    const selected = state.selectedTags.has(button.dataset.tag);
    button.classList.toggle('selected', selected);
    button.setAttribute('aria-pressed', String(selected));
  });

  const count = getSeedCount();
  const countLabel = document.getElementById('seed-count');
  countLabel.textContent = `${count} of 3–5 selected`;
  countLabel.dataset.overLimit = String(count > 5);
}

function setSeedStatus(message, kind = '') {
  const status = document.getElementById('seed-status');
  status.textContent = message;
  status.classList.toggle('is-error', kind === 'error');
  status.classList.toggle('is-success', kind === 'success');
}

function problemMessage(problem, fallback) {
  const errors = Object.values(problem.errors ?? {}).flat().filter(value => typeof value === 'string');
  return errors.length ? errors.join(' ') : problem.detail || problem.title || fallback;
}

async function submitSeeds() {
  if (!state.token) {
    setSeedStatus('Sign in or create an account to save your taste profile.', 'error');
    switchTab('auth');
    return;
  }

  const artists = document.getElementById('seed-artists').value
    .split(',')
    .map(artist => artist.trim())
    .filter(Boolean)
    .map(artistName => ({ artistName, weight: 1.0 }));
  const tags = Array.from(state.selectedTags).map(tag => ({ tag, weight: 1.0 }));
  const total = tags.length + artists.length;

  if (total < 3 || total > 5) {
    setSeedStatus(`Choose 3–5 artists and styles altogether. You have ${total}.`, 'error');
    return;
  }

  setSeedStatus('Saving your taste profile…');
  try {
    const response = await apiFetch(`${API_BASE}/taste/seed`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${state.token}`
      },
      body: JSON.stringify({ tags, artists })
    });

    if (response.ok) {
      setSeedStatus('Your taste profile was saved.', 'success');
      return;
    }

    const problem = await response.json();
    setSeedStatus(problemMessage(problem, 'Your taste profile could not be saved.'), 'error');
  } catch (error) {
    setSeedStatus(`Your taste profile could not be saved: ${error.message}`, 'error');
  }
}

async function loadYourData() {
  if (window.discoveryApp && await window.discoveryApp.renderData()) return;
  const container = document.getElementById('export-container');
  const epoch = state.sessionEpoch;
  if (!state.token) {
    container.innerHTML = `
      <div class="notice">
        <strong>Sign in to see your saved information</strong>
        <p>Your taste profile, recommendation history, and export are available after you sign in.</p>
        <p><a href="#tab-auth" data-action="tab" data-tab="auth">Go to your account</a></p>
      </div>
    `;
    return;
  }

  container.innerHTML = '<p role="status">Loading your saved information…</p>';
  try {
    const response = await apiFetch(`${API_BASE}/export/my-data`, {
      headers: { Authorization: `Bearer ${state.token}` }
    });
    if (!response.ok) throw new Error('Could not load your saved information.');
    const data = await response.json();
    if (epoch !== state.sessionEpoch) return;

    container.innerHTML = `
      <div class="surface">
        <div class="data-toolbar">
          <div><strong>Data export</strong><div class="data-meta">Version ${escapeHtml(data.exportVersion)} · Created ${escapeHtml(new Date(data.exportedAtUtc).toLocaleString())}</div></div>
          <button class="button button-primary" data-action="downloadExport">Download JSON</button>
        </div>
        <p class="data-description">The export includes your account profile, taste records, recommendation history, and sanitized local email copies. Credentials and authentication internals are excluded.</p>
        <pre id="json-dump-pre" class="json-dump"></pre>
        <section class="danger-zone" aria-labelledby="danger-heading">
          <h3 id="danger-heading">Delete your account</h3>
          <p>This removes your account and stored taste data. Some local email copies may require separate cleanup. Shared music catalog records and backups are not erased.</p>
          <button class="btn-danger" data-action="deleteAccount">Delete account</button>
        </section>
      </div>
    `;
    const preview = document.getElementById('json-dump-pre');
    if (preview) preview.textContent = JSON.stringify(data, null, 2);
  } catch (error) {
    if (epoch !== state.sessionEpoch) return;
    container.innerHTML = `<div class="notice notice-error">${escapeHtml(error.message)}</div>`;
  }
}

async function downloadExport() {
  if (!state.token) return;
  const epoch = state.sessionEpoch;
  try {
    const res = await apiFetch(`${API_BASE}/export/my-data?download=true`, {
      headers: { 'Authorization': `Bearer ${state.token}` }
    });
    if (!res.ok) throw new Error('Failed to retrieve authenticated export file');
    const blob = await res.blob();
    if (epoch !== state.sessionEpoch) return;
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `linernotes-data-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    a.remove();
  } catch (err) {
    if (epoch !== state.sessionEpoch) return;
    alert('Export download failed: ' + err.message);
  }
}

async function deleteAccount() {
  if (!confirm("Are you sure you want to delete your account? This action cannot be undone.")) return;

  try {
    const res = await apiFetch(`${API_BASE}/subscribers/me`, {
      method: 'DELETE',
      headers: { 'Authorization': `Bearer ${state.token}` }
    });

    if (res.ok) {
      const cleanup = res.status === 200 ? await res.json() : null;
      alert(cleanup?.localCopiesDeleted === false
        ? `Account deleted. Local copy cleanup incomplete: ${cleanup.issues.join(', ')}`
        : 'Your account has been deleted.');
      logout();
    } else {
      alert('Failed to delete account.');
    }
  } catch (err) {
    alert('Error: ' + err.message);
  }
}

async function handleRegister(e) {
  e.preventDefault();
  const attemptId = ++state.authAttemptId;
  const userName = document.getElementById('reg-username').value;
  const email = document.getElementById('reg-email').value;
  const password = document.getElementById('reg-password').value;

  try {
    const guest = await window.discoveryApp?.registrationOptions() ?? {};
    const res = await fetch(`${API_BASE}/auth/register`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', ...(guest.csrf ? { 'X-CSRF-TOKEN': guest.csrf } : {}) },
      body: JSON.stringify({ userName, email, password, preserveGuestProgress: guest.preserve ?? false,
        weeklyEmailOptIn: document.getElementById('reg-weekly-email').checked })
    });
    if (attemptId !== state.authAttemptId) return;

    const data = await res.json();
    if (attemptId !== state.authAttemptId) return;
    if (!res.ok) {
      alert('Registration failed: ' + problemMessage(data, 'Check credentials.'));
      return;
    }

    acceptSession(data);
    updateAuthUI();
    switchTab('seeding');
  } catch (err) {
    if (attemptId !== state.authAttemptId) return;
    alert('Registration error: ' + err.message);
  }
}

async function handleLogin(e) {
  e.preventDefault();
  const attemptId = ++state.authAttemptId;
  const email = document.getElementById('login-email').value;
  const password = document.getElementById('login-password').value;

  try {
    const res = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });
    if (attemptId !== state.authAttemptId) return;

    const data = await res.json();
    if (attemptId !== state.authAttemptId) return;
    if (!res.ok) {
      alert('Login failed: ' + problemMessage(data, 'Invalid credentials.'));
      return;
    }

    acceptSession(data);
    updateAuthUI();
    switchTab('discovery');
  } catch (err) {
    if (attemptId !== state.authAttemptId) return;
    alert('Login error: ' + err.message);
  }
}

function logout() {
  state.sessionEpoch++;
  state.authAttemptId++;
  state.token = null;
  state.refreshToken = null;
  refreshInFlight = null;
  state.user = null;
  window.discoveryApp?.sessionChanged();
  localStorage.removeItem('linernotes_token');
  document.getElementById('export-container').replaceChildren();
  document.getElementById('discovery-container').replaceChildren();
  updateAuthUI();
  switchTab('discovery');
}

function updateAuthUI() {
  const status = document.getElementById('auth-status');
  if (state.token) {
    status.innerHTML = `<div class="account-state"><span class="account-state-dot" aria-hidden="true"></span><span>Signed in</span><button class="text-button" data-action="logout">Sign out</button></div>`;
  } else {
    status.innerHTML = `<a href="#tab-auth" data-action="tab" data-tab="auth">Sign in or create an account</a>`;
  }
}

function switchTab(tabName) {
  window.discoveryApp?.leave();
  document.querySelector('.page-heading').hidden = tabName !== 'discovery';
  state.activeTab = tabName;
  document.querySelectorAll('.tab-pane').forEach(panel => {
    const active = panel.id === `tab-${tabName}`;
    panel.classList.toggle('active', active);
    panel.hidden = !active;
  });
  document.querySelectorAll('[data-nav-tab]').forEach(tab => {
    const active = tab.dataset.tab === tabName;
    tab.classList.toggle('active', active);
    if (active) tab.setAttribute('aria-current', 'page');
    else tab.removeAttribute('aria-current');
  });
  if (tabName === 'discovery') loadDiscovery();
  if (tabName === 'your-data') loadYourData();
  if (tabName === 'seeding') window.discoveryApp?.renderTaste();
}

document.addEventListener('click', event => {
  const target = event.target.closest('[data-action]');
  if (!target) return;
  event.preventDefault();
  const data = target.dataset;
  switch (data.action) {
    case 'tab': switchTab(data.tab); break;
    case 'tag': toggleTag(data.tag); break;
    case 'seeds': submitSeeds(); break;
    case 'feedback': handleFeedbackAction(data); break;
    case 'downloadExport': downloadExport(); break;
    case 'deleteAccount': deleteAccount(); break;
    case 'logout': logout(); break;
  }
});

window.addEventListener('DOMContentLoaded', () => {
  document.getElementById('register-form').addEventListener('submit', handleRegister);
  document.getElementById('login-form').addEventListener('submit', handleLogin);
  document.getElementById('seed-artists').addEventListener('input', updateTagUI);
  updateTagUI();
  updateAuthUI();
  loadDiscovery();
});
