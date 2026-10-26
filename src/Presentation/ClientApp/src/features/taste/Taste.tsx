import { useEffect, useRef, useState } from 'react';
import { Link, useBeforeUnload, useBlocker, useNavigate } from 'react-router';
import { ApiError } from '../../api/client';
import { emptyAnswers } from '../../api/models';
import type { Answers, Favorite } from '../../api/models';
import { usePersonal } from '../../app/Personal';
import { Notice } from '../../components/Notice';
import { Artwork } from '../../components/Artwork';
import { CatalogPicker } from './CatalogPicker';
import { TagPicker } from './TagPicker';
import { useRetryDelay } from '../../hooks/useRetryDelay';

export default function Taste() {
  const personal = usePersonal(), navigate = useNavigate();
  const page = personal.data?.overview.page;
  const [favorites, setFavorites] = useState<Favorite[]>(page?.favorites ?? []), [tags, setTags] = useState<string[]>(page?.tags ?? []);
  const [answers, setAnswers] = useState<Answers>(page?.onboarding ?? emptyAnswers);
  const [error, setError] = useState<unknown>(null), [message, setMessage] = useState(''), [conflict, setConflict] = useState(false);
  const retryDelay = useRetryDelay(error);
  const fingerprint = JSON.stringify({ favorites, tags, answers });
  const baseline = useRef(fingerprint), dirty = fingerprint !== baseline.current, dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;
  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirtyRef.current && currentLocation.pathname !== nextLocation.pathname);
  useEffect(() => {
    if (blocker.state === 'blocked') {
      if (window.confirm('Leave without saving your taste changes?')) blocker.proceed(); else blocker.reset();
    }
  }, [blocker]);
  useBeforeUnload(event => { if (dirtyRef.current) { event.preventDefault(); event.returnValue = ''; } });
  async function reloadSaved() {
    if (dirty && !window.confirm('Discard this unsaved draft and load your saved taste?')) return;
    try {
      const value = await personal.reload(), saved = value.overview.page;
      const next = { favorites: saved?.favorites ?? [], tags: saved?.tags ?? [], answers: saved?.onboarding ?? emptyAnswers };
      baseline.current = JSON.stringify(next); setFavorites(next.favorites); setTags(next.tags); setAnswers(next.answers); setConflict(false); setError(null);
    } catch (e) { setError(e); }
  }
  async function save(generate: boolean) {
    setError(null); setMessage('');
    try {
      const nextAnswers = { ...answers, step: 'complete' };
      await personal.saveTaste(nextAnswers, favorites, tags);
      baseline.current = JSON.stringify({ favorites, tags, answers: nextAnswers }); dirtyRef.current = false; setAnswers(nextAnswers);
      setMessage('Your taste is saved.');
      if (generate) {
        if (favorites.length || tags.length || answers.allStyles) { await personal.generate(); navigate('/discover'); }
        else navigate('/');
      }
    } catch (e) { setError(e); if (e instanceof ApiError && e.status === 409) setConflict(true); }
  }
  function add(favorite: Favorite) {
    setFavorites(values => values.some(item => item.catalogRecordId === favorite.catalogRecordId || (favorite.canonicalId && favorite.canonicalId === item.canonicalId && item.kind === favorite.kind)) ? values : [...values, favorite]);
  }
  const exploration = ['close', 'balance', 'surprise'].indexOf(answers.exploration ?? 'balance');
  return <main id="main-content" className="page-container personal-main taste-main" tabIndex={-1}>
    <header className="taste-heading"><h1>Your taste</h1><p>Guide your discovery by selecting artists, albums, and genres you already love. We use these signals to find less obvious connections and fresh recommendations.</p></header>
    <div className="taste-layout"><div className="taste-editor">
      <section aria-labelledby="music-you-love"><div className="editor-section-heading"><h2 id="music-you-love">Music you love</h2><span className="editorial-meta">{favorites.length} selected</span></div>
        <CatalogPicker onChoose={add} />
        <div className="editor-panel favorite-list">{favorites.length ? favorites.map((favorite, index) => <div className="favorite-row" key={favorite.catalogRecordId ?? favorite.signalId ?? index}>
          <div className="favorite-summary">{favorite.kind === 'Track' && favorite.canonicalId ? <div className="favorite-cover"><Artwork id={favorite.canonicalId} /></div> : <span className={`favorite-symbol favorite-${favorite.kind.toLowerCase()}`} aria-hidden="true" />}
            <div><p>{favorite.name}</p><span>{favorite.artistName ?? favorite.kind}{favorite.disambiguation ? ` · ${favorite.disambiguation}` : ''}</span>
              {!!favorite.releases?.length && <small>{favorite.releases.slice(0, 2).map(r => r.title).join(' · ')}</small>}</div></div>
          <button type="button" className="remove-favorite" aria-label={`Remove ${favorite.name}`} onClick={() => setFavorites(values => values.filter((_, i) => i !== index))}>×</button>
        </div>) : <p className="field-help">Search for music you love, or start with styles below. Favorites are optional.</p>}</div>
      </section>
      <section aria-labelledby="sounds-and-moods"><div className="editor-section-heading"><h2 id="sounds-and-moods">Sounds &amp; moods</h2><span className="editorial-meta">Optional</span></div><TagPicker selected={tags} onChange={setTags} /></section>
    </div>
    <aside className="taste-preferences" aria-label="Discovery preferences"><div className="editor-panel preferences-panel"><h2><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true"><path d="M4 7h9m4 0h3M4 17h3m4 0h9M13 4v6M7 14v6" /></svg>Discovery goals</h2>
      <div className="exploration-control"><div className="preference-label"><label htmlFor="exploration">Exploration</label><span>{answers.exploration === 'close' ? 'Closer' : answers.exploration === 'surprise' ? 'Exploratory' : answers.exploration === 'balance' ? 'Balanced' : 'Optional'}</span></div>
        <input id="exploration" type="range" min="0" max="2" step="1" value={exploration < 0 ? 1 : exploration} style={{ background: `linear-gradient(to right, var(--ink-main) ${(exploration < 0 ? 1 : exploration) * 50}%, var(--outline) ${(exploration < 0 ? 1 : exploration) * 50}%)` }} aria-valuetext={['Closer to my taste', 'Balanced', 'More exploratory'][exploration < 0 ? 1 : exploration]} onChange={e => setAnswers(value => ({ ...value, exploration: ['close', 'balance', 'surprise'][Number(e.target.value)] }))} />
        <div className="range-labels"><span>Closer to my taste</span><span>More exploratory</span></div>
      </div>
      <fieldset className="vocals-control"><legend>Vocals</legend><div className="preference-options">{[['no-preference', 'Any'], ['instrumental', 'Instrumental'], ['vocals', 'Vocals']].map(([value, label]) => <label key={value}><input type="radio" name="vocals" value={value} checked={answers.vocals === value || (value === 'no-preference' && answers.vocals === 'both')} onChange={() => setAnswers(a => ({ ...a, vocals: value }))} /><span>{label}</span></label>)}</div></fieldset>
      <details className="optional-preferences"><summary>More optional preferences</summary><label>What would you like to discover?<select value={answers.goal ?? ''} onChange={e => setAnswers(a => ({ ...a, goal: e.target.value || null }))}><option value="">No preference</option><option value="skipped">Skipped</option><option value="new-artists">New artists</option><option value="favorites">More like my favorites</option><option value="different-styles">Different styles</option><option value="mixture">A mixture</option></select></label>
        <label className="checkbox-label"><input type="checkbox" checked={answers.allStyles} onChange={e => setAnswers(a => ({ ...a, allStyles: e.target.checked }))} />My taste is a soup — all styles welcome</label>
        <button type="button" className="text-button" onClick={() => setAnswers(a => ({ ...a, goal: 'skipped', exploration: 'skipped', vocals: 'skipped' }))}>Skip optional questions</button>
        <p className="field-help">Preferences use available evidence. Missing vocal information stays unknown. Popularity has no preference or penalty.</p>
      </details>
    </div><div className="taste-actions"><button className="button button-primary" type="button" disabled={personal.busy || conflict || retryDelay > 0} onClick={() => save(true)}>{personal.busy ? 'Saving your taste…' : retryDelay ? `Try again in ${retryDelay}s` : 'Find recommendations'}</button>
      <button className="text-button" type="button" disabled={personal.busy || conflict || retryDelay > 0} onClick={() => save(false)}>Save taste without generating</button>
      <p className="field-help">{personal.data?.session.profile?.isGuest || !personal.data?.session.profile ? <>Guest taste is stored for seven inactive days. <Link to="/account">Create an account</Link> to keep your discoveries across devices.</> : 'Changes are saved to your account when you choose Save or Find recommendations.'}</p>
      <Link className="text-link" to="/">Skip setup and explore recent recommendations</Link>
      {!!error && <Notice error={error} onRetry={reloadSaved} />}<p className="field-help" role="status">{message}</p>
    </div></aside></div>
  </main>;
}
