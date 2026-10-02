// Liner Notes Web Client
const API_BASE = '/api';

const state = {
  token: localStorage.getItem('linernotes_token') || null,
  user: null,
  selectedTags: new Set(),
  activeTab: 'discovery'
};

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
    score: 0.842,
    tagScore: 0.912,
    popularityPenalty: 0.120,
    noveltyBoost: 0.050,
    topTags: ["idm", "ambient techno", "electronic"],
    feedback: null
  },
  {
    id: "00000000-0000-0000-0000-000000000002",
    title: "A Forest",
    artist: "The Cure",
    album: "Seventeen Seconds",
    score: 0.785,
    tagScore: 0.890,
    popularityPenalty: 0.155,
    noveltyBoost: 0.050,
    topTags: ["post-punk", "gothic rock", "new wave"],
    feedback: null
  },
  {
    id: "00000000-0000-0000-0000-000000000003",
    title: "Yolculuk",
    artist: "Altın Gün",
    album: "Gece",
    score: 0.814,
    tagScore: 0.875,
    popularityPenalty: 0.081,
    noveltyBoost: 0.020,
    topTags: ["anatolian rock", "psychedelic rock"],
    feedback: null
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
  return `
    <div class="breakdown-box">
      <strong>Stored Score Breakdown (Residue: Prism Engine)</strong>
      <div class="breakdown-grid">
        <div class="breakdown-item">
          <span class="label">Tag Overlap ($S_{tag}$)</span>
          <span class="val">${item.tagScore ? item.tagScore.toFixed(3) : '0.000'}</span>
        </div>
        <div class="breakdown-item">
          <span class="label">Pop. Penalty ($P_{pop}$)</span>
          <span class="val">-${item.popularityPenalty ? item.popularityPenalty.toFixed(3) : '0.000'}</span>
        </div>
        <div class="breakdown-item">
          <span class="label">Novelty Boost ($A_{nov}$)</span>
          <span class="val">+${item.noveltyBoost ? item.noveltyBoost.toFixed(3) : '0.000'}</span>
        </div>
        <div class="breakdown-item">
          <span class="label">Composite Score</span>
          <span class="val" style="color: var(--accent-primary);">${item.score ? item.score.toFixed(3) : '0.000'}</span>
        </div>
      </div>
      ${item.topTags ? `<div style="margin-top: 0.5rem; color: var(--text-muted); font-size: 0.8rem;">Matched tags: ${item.topTags.map(escapeHtml).join(', ')}</div>` : ''}
    </div>
  `;
}

function renderTrackCard(track, isGuest = false) {
  const safeId = escapeHtml(track.id);
  const safeTitle = escapeHtml(track.title);
  const safeArtist = escapeHtml(track.artist);
  const safeAlbum = escapeHtml(track.album || '');

  return `
    <div class="card" id="card-${safeId}">
      <div class="card-header">
        <div>
          <div class="card-title">${safeTitle}</div>
          <div class="track-artist">${safeArtist}</div>
          <div class="track-meta">${safeAlbum}</div>
        </div>
      </div>
      ${buildDeepLinks(track.artist, track.title)}
      ${renderScoreBreakdown(track)}
      <div class="feedback-actions">
        <button class="btn-feedback liked ${track.feedback === 'Liked' ? 'active' : ''}" onclick="submitFeedback('${safeId}', 'Liked', null, ${isGuest})">
          👍 Like
        </button>
        <button class="btn-feedback disliked ${track.feedback === 'Disliked' ? 'active' : ''}" onclick="submitFeedback('${safeId}', 'Disliked', null, ${isGuest})">
          👎 Dislike
        </button>
        <button class="btn-feedback known ${track.feedback === 'AlreadyKnown' ? 'active' : ''}" onclick="submitFeedback('${safeId}', 'AlreadyKnown', null, ${isGuest})">
          🎧 Already Know This
        </button>
      </div>
      <div class="rating-bar" style="margin-top: 0.75rem; display: flex; align-items: center; gap: 0.25rem; flex-wrap: wrap;">
        <span style="font-size: 0.8rem; color: var(--text-muted); margin-right: 0.35rem;">Rate 1–10:</span>
        ${[1,2,3,4,5,6,7,8,9,10].map(r => `
          <button type="button" class="btn-rating-num ${track.rating === r ? 'active' : ''}" 
                  style="padding: 2px 7px; font-size: 0.75rem; border-radius: 4px; border: 1px solid var(--border-color); background: ${track.rating === r ? 'var(--accent-primary)' : 'var(--bg-secondary)'}; color: ${track.rating === r ? '#fff' : 'var(--text-color)'}; cursor: pointer;"
                  onclick="submitFeedback('${safeId}', '${r >= 7 ? 'Liked' : (r <= 3 ? 'Disliked' : 'None')}', ${r}, ${isGuest})">${r}</button>
        `).join('')}
      </div>
    </div>
  `;
}

async function loadDiscovery() {
  const container = document.getElementById('discovery-container');
  if (!state.token) {
    container.innerHTML = `
      <div style="margin-bottom: 1.5rem; padding: 1rem; background: var(--bg-tertiary); border-radius: var(--radius-sm); border: 1px solid var(--border-color);">
        <strong>Guest Preview Mode</strong>: Below is a sample 3-pick discovery batch generated by our deterministic Residue (Prism) scorer. 
        <a href="#" onclick="switchTab('auth')" style="color: var(--accent-primary); text-decoration: underline;">Create an account</a> to get personalized weekly emails and custom seed preferences.
      </div>
      ${sampleGuestPicks.map(p => renderTrackCard(p, true)).join('')}
    `;
    return;
  }

  container.innerHTML = '<p>Loading your weekly digest...</p>';
  try {
    const res = await fetch(`${API_BASE}/digests/latest`, {
      headers: { 'Authorization': `Bearer ${state.token}` }
    });

    if (res.status === 404) {
      container.innerHTML = `
        <div class="card">
          <h3>No Digest Generated Yet</h3>
          <p style="color: var(--text-muted); margin: 0.5rem 0 1rem 0;">
            Your taste profile is being processed. Complete your taste seeding or link Last.fm/ListenBrainz to prepare your first scheduled weekly delivery.
          </p>
          <button class="btn-primary" onclick="switchTab('seeding')">Go to Taste Seeding</button>
        </div>
      `;
      return;
    }

    if (!res.ok) throw new Error('Failed to load digest');
    const digest = await res.json();

    const items = digest.recommendations.map(r => ({
      id: r.id,
      title: r.track?.title || 'Unknown Track',
      artist: r.track?.artistName || 'Unknown Artist',
      album: r.track?.albumTitle || '',
      score: r.scoreBreakdown?.finalScore || 0,
      tagScore: r.scoreBreakdown?.tagSimilarityScore || 0,
      popularityPenalty: r.scoreBreakdown?.popularityPenalty || 0,
      noveltyBoost: r.scoreBreakdown?.noveltyBoost || 0,
      topTags: (r.scoreBreakdown?.matchedTags || []).map(t => t.tagName),
      feedback: r.feedback,
      rating: r.rating
    }));

    container.innerHTML = `
      <div style="margin-bottom: 1rem; color: var(--text-muted);">
        Showing Weekly Digest for <strong>${escapeHtml(digest.week)}</strong> (${items.length} picks)
      </div>
      ${items.map(p => renderTrackCard(p, false)).join('')}
    `;
  } catch (err) {
    container.innerHTML = `<p style="color: var(--accent-danger);">Error loading digest: ${escapeHtml(err.message)}</p>`;
  }
}

async function submitFeedback(recommendationId, feedbackType, rating = null, isGuest = false) {
  if (isGuest) {
    const card = document.getElementById(`card-${recommendationId}`);
    if (card) {
      card.querySelectorAll('.btn-feedback').forEach(b => b.classList.remove('active'));
      const btn = card.querySelector(`.btn-feedback.${feedbackType.toLowerCase() === 'alreadyknown' ? 'known' : feedbackType.toLowerCase()}`);
      if (btn) btn.classList.add('active');
      if (rating !== null) {
        card.querySelectorAll('.btn-rating-num').forEach(b => {
          const isActive = parseInt(b.textContent.trim()) === rating;
          b.style.background = isActive ? 'var(--accent-primary)' : 'var(--bg-secondary)';
          b.style.color = isActive ? '#fff' : 'var(--text-color)';
        });
      }
    }
    return;
  }

  try {
    const payload = { feedback: feedbackType };
    if (rating !== null) payload.rating = rating;

    const res = await fetch(`${API_BASE}/digests/recommendations/${recommendationId}/feedback`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.token}`
      },
      body: JSON.stringify(payload)
    });

    if (res.ok) {
      const card = document.getElementById(`card-${recommendationId}`);
      if (card) {
        card.querySelectorAll('.btn-feedback').forEach(b => b.classList.remove('active'));
        const btn = card.querySelector(`.btn-feedback.${feedbackType.toLowerCase() === 'alreadyknown' ? 'known' : feedbackType.toLowerCase()}`);
        if (btn) btn.classList.add('active');
        if (rating !== null) {
          card.querySelectorAll('.btn-rating-num').forEach(b => {
            const isActive = parseInt(b.textContent.trim()) === rating;
            b.style.background = isActive ? 'var(--accent-primary)' : 'var(--bg-secondary)';
            b.style.color = isActive ? '#fff' : 'var(--text-color)';
          });
        }
      }
    }
  } catch (err) {
    alert('Feedback submission failed: ' + err.message);
  }
}

function toggleTag(tag) {
  if (state.selectedTags.has(tag)) {
    state.selectedTags.delete(tag);
  } else {
    if (state.selectedTags.size >= 5) {
      alert('You can select up to 5 seed tags.');
      return;
    }
    state.selectedTags.add(tag);
  }
  updateTagUI();
}

function updateTagUI() {
  document.querySelectorAll('.tag-btn').forEach(btn => {
    const tag = btn.dataset.tag;
    if (state.selectedTags.has(tag)) {
      btn.classList.add('selected');
    } else {
      btn.classList.remove('selected');
    }
  });
  document.getElementById('seed-count').innerText = `${state.selectedTags.size} / 3–5 seeds selected`;
}

async function submitSeeds() {
  if (!state.token) {
    alert('Please sign in or register to persist your taste seeds.');
    switchTab('auth');
    return;
  }

  const artistsInput = document.getElementById('seed-artists').value;
  const artists = artistsInput
    .split(',')
    .map(a => a.trim())
    .filter(a => a.length > 0)
    .map(a => ({ artistName: a, initialWeight: 1.0 }));

  const tags = Array.from(state.selectedTags).map(t => ({
    tagName: t,
    initialWeight: 1.0
  }));

  const total = tags.length + artists.length;
  if (total < 3 || total > 5) {
    alert(`Please provide between 3 and 5 total seeds (tags + artists). Currently selected: ${total}`);
    return;
  }

  try {
    const res = await fetch(`${API_BASE}/taste/seed`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${state.token}`
      },
      body: JSON.stringify({ tags, artists })
    });

    if (res.ok) {
      alert('Taste seeds saved successfully! Your profile is configured.');
      switchTab('discovery');
    } else {
      const err = await res.json();
      alert('Error saving seeds: ' + (err.detail || 'Validation failed.'));
    }
  } catch (err) {
    alert('Submission failed: ' + err.message);
  }
}

async function loadYourData() {
  const container = document.getElementById('export-container');
  if (!state.token) {
    container.innerHTML = `
      <p style="color: var(--text-muted);">
        Sign in to view, inspect, or export all data stored about your account verbatim.
      </p>
    `;
    return;
  }

  container.innerHTML = '<p>Retrieving your stored records...</p>';
  try {
    const res = await fetch(`${API_BASE}/export/my-data`, {
      headers: { 'Authorization': `Bearer ${state.token}` }
    });

    if (!res.ok) throw new Error('Failed to retrieve export');
    const data = await res.json();

    container.innerHTML = `
      <div style="margin-bottom: 1rem; display: flex; justify-content: space-between; align-items: center;">
        <div>
          <strong>Export Version:</strong> ${escapeHtml(data.exportVersion)} | 
          <strong>Generated:</strong> ${escapeHtml(new Date(data.exportedAtUtc).toLocaleString())}
        </div>
        <button class="btn-primary" onclick="downloadExport()">Download JSON Export</button>
      </div>
      <pre id="json-dump-pre" class="json-dump"></pre>
      <div style="margin-top: 2rem; padding-top: 1rem; border-top: 1px solid var(--border-color);">
        <h4>Danger Zone</h4>
        <p style="color: var(--text-muted); font-size: 0.85rem; margin-bottom: 0.75rem;">
          Permanently purge your subscriber profile, preferences, and taste signals under the GDPR right to be forgotten.
        </p>
        <button class="btn-danger" onclick="deleteAccount()">Delete My Account</button>
      </div>
    `;

    const pre = document.getElementById('json-dump-pre');
    if (pre) {
      pre.textContent = JSON.stringify(data, null, 2);
    }
  } catch (err) {
    container.innerHTML = `<p style="color: var(--accent-danger);">Error: ${escapeHtml(err.message)}</p>`;
  }
}

async function downloadExport() {
  if (!state.token) return;
  try {
    const res = await fetch(`${API_BASE}/export/my-data?download=true`, {
      headers: { 'Authorization': `Bearer ${state.token}` }
    });
    if (!res.ok) throw new Error('Failed to retrieve authenticated export file');
    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `linernotes-data-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    a.remove();
  } catch (err) {
    alert('Export download failed: ' + err.message);
  }
}

async function deleteAccount() {
  if (!confirm("Are you sure you want to delete your account? This action cannot be undone.")) return;

  try {
    const res = await fetch(`${API_BASE}/subscribers/me`, {
      method: 'DELETE',
      headers: { 'Authorization': `Bearer ${state.token}` }
    });

    if (res.ok) {
      alert('Your account has been deleted.');
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
  const userName = document.getElementById('reg-username').value;
  const email = document.getElementById('reg-email').value;
  const password = document.getElementById('reg-password').value;

  try {
    const res = await fetch(`${API_BASE}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName, email, password })
    });

    const data = await res.json();
    if (!res.ok) {
      alert('Registration failed: ' + (data.detail || data.title || 'Check credentials.'));
      return;
    }

    state.token = data.accessToken;
    state.user = data.user;
    localStorage.setItem('linernotes_token', state.token);
    updateAuthUI();
    switchTab('seeding');
  } catch (err) {
    alert('Registration error: ' + err.message);
  }
}

async function handleLogin(e) {
  e.preventDefault();
  const email = document.getElementById('login-email').value;
  const password = document.getElementById('login-password').value;

  try {
    const res = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    const data = await res.json();
    if (!res.ok) {
      alert('Login failed: ' + (data.detail || 'Invalid credentials.'));
      return;
    }

    state.token = data.accessToken;
    state.user = data.user;
    localStorage.setItem('linernotes_token', state.token);
    updateAuthUI();
    switchTab('discovery');
  } catch (err) {
    alert('Login error: ' + err.message);
  }
}

function logout() {
  state.token = null;
  state.user = null;
  localStorage.removeItem('linernotes_token');
  updateAuthUI();
  switchTab('discovery');
}

function updateAuthUI() {
  const badge = document.getElementById('auth-status');
  if (state.token) {
    badge.innerHTML = `
      <span>Subscriber Active</span> | 
      <a href="#" onclick="logout()" style="color: var(--accent-primary); text-decoration: underline;">Logout</a>
    `;
  } else {
    badge.innerHTML = `
      <a href="#" onclick="switchTab('auth')" style="color: var(--accent-primary); text-decoration: underline;">Sign In / Register</a>
    `;
  }
}

function switchTab(tabName) {
  state.activeTab = tabName;
  document.querySelectorAll('.tab-pane').forEach(el => el.classList.remove('active'));
  document.querySelectorAll('nav button').forEach(el => el.classList.remove('active'));

  const tab = document.getElementById(`tab-${tabName}`);
  if (tab) tab.classList.add('active');

  const navBtn = document.getElementById(`nav-${tabName}`);
  if (navBtn) navBtn.classList.add('active');

  if (tabName === 'discovery') loadDiscovery();
  if (tabName === 'your-data') loadYourData();
}

window.addEventListener('DOMContentLoaded', () => {
  updateAuthUI();
  loadDiscovery();
});
