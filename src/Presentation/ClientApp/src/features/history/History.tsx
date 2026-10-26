import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { usePersonal } from '../../app/Personal';
import type { Batch } from '../../api/models';
import { isAbort } from '../../api/client';
import { Artwork } from '../../components/Artwork';
import { Notice } from '../../components/Notice';

interface Detail { batch?: Batch; error?: unknown; }
export default function History() {
  const personal = usePersonal(), summaries = personal.data?.overview.page?.batches ?? [];
  const [details, setDetails] = useState<Record<string, Detail>>({}), [limit, setLimit] = useState(4), [retry, setRetry] = useState(0);
  const read = useRef(personal.batch); read.current = personal.batch;
  const signature = summaries.slice(0, limit).map(summary => summary.id).join('|');
  const revision = personal.data?.overview.page?.revision;
  useEffect(() => {
    const controller = new AbortController(), ids = signature ? signature.split('|') : [];
    let next = 0;
    setDetails({});
    async function worker() {
      while (!controller.signal.aborted && next < ids.length) {
        const id = ids[next++];
        try { const batch = await read.current(id, controller.signal); if (!controller.signal.aborted) setDetails(value => ({ ...value, [id]: { batch } })); }
        catch (error) { if (!isAbort(error) && !controller.signal.aborted) setDetails(value => ({ ...value, [id]: { error } })); }
      }
    }
    void Promise.all([worker(), worker()]);
    return () => controller.abort();
  }, [signature, revision, retry]);
  const guest = personal.data?.session.profile?.isGuest ?? true;
  return <main id="main-content" className="page-container personal-main history-main" tabIndex={-1}>
    <header className="history-heading"><div><p className="editorial-meta coral-meta">Past finds</p><h1>Discovery history</h1></div><Link className="button button-secondary" to="/discover">Discover again <span aria-hidden="true">→</span></Link></header>
    {summaries.length ? <div className="history-grid">{summaries.slice(0, limit).map(summary => {
      const detail = details[summary.id], batch = detail?.batch;
      return <article className="history-card" key={summary.id}><div className="history-card-heading"><div><h2>{new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'long', day: 'numeric' }).format(new Date(summary.createdAtUtc))}</h2>
        {batch ? <p>{String(batch.picks.length).padStart(2, '0')} recommendations</p> : <p className="field-help" role="status">Loading recommendations…</p>}</div><span className="session-badge">{guest ? 'Guest session' : 'Your account'}</span></div>
        {batch ? <div className="history-preview"><div className="history-artwork">{batch.picks.slice(0, 3).map(pick => <Artwork key={pick.id} id={pick.mbid ?? ''} />)}</div>
          <ul className="history-track-list">{batch.picks.slice(0, 3).map(pick => <li key={pick.id}><span><strong>{pick.title}</strong> <span className="history-artist">· {pick.artistName}</span></span>
            <span className={`feedback-label${pick.feedback === 'Liked' ? ' feedback-liked' : ''}`}>{pick.feedback === 'Liked' ? '♡ Liked' : pick.feedback === 'AlreadyKnown' ? '✓ Known' : pick.feedback === 'Disliked' ? '× Not for me' : pick.rating ? `${pick.rating}/10` : 'Not rated'}</span></li>)}</ul></div>
          : detail?.error ? <Notice error={detail.error} onRetry={() => setRetry(value => value + 1)} /> : <div className="history-skeleton" aria-hidden="true"><div /><div /></div>}
        <div className="history-card-footer"><Link to={`/history/${summary.id}`}>View recommendations <span aria-hidden="true">→</span></Link></div>
      </article>;
    })}</div> : <div className="feed-message"><h2>Your history starts with a discovery</h2><p>Completed recommendation sessions will appear here.</p><Link className="text-link" to="/discover">Find your next favorites →</Link></div>}
    {summaries.length > limit && <button className="button button-secondary" type="button" onClick={() => setLimit(value => value + 4)}>Show more recent sessions</button>}
    {guest && <section className="guest-history-note"><svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" aria-hidden="true"><path d="M3 11a9 9 0 1 1 3 7M3 4v7h7M12 7v5l3 2" /></svg><h2>Browsing as a guest</h2><p>Guest discoveries are stored on the server and expire after seven inactive days. Creating an account lets you keep your saved discoveries across devices.</p><Link className="button button-secondary" to="/account?mode=register">Create free account</Link></section>}
    {!!summaries.length && <p className="history-limit field-help">Showing the latest {summaries.length} completed {summaries.length === 1 ? 'session' : 'sessions'}. Up to ten recent sessions are available here.</p>}
  </main>;
}
