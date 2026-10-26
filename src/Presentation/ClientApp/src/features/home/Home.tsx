import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { Artwork } from '../../components/Artwork';
import { ArrowIcon } from '../../components/ArrowIcon';
import { FeedError, readRecent } from '../../api/recent';
import type { RecentMusic } from '../../api/recent';

type FeedState = { kind: 'loading' } | { kind: 'ready'; items: RecentMusic[] } | { kind: 'error'; error: FeedError };

function MusicCard({ item, index }: { item: RecentMusic; index: number }) {
  return <article className="music-card">
    <Artwork id={item.recordingId} eager={index === 0} />
    <h2 className="music-title">{item.title}</h2>
    <p className="music-artist">{item.artistName}</p>
    <p className="music-attribution">{item.attribution}{item.origin === 'Synthetic' ? ' · Synthetic test data' : ''}</p>
  </article>;
}

function FeedSkeleton() {
  return <div className="music-grid" aria-hidden="true">{[0, 1, 2, 3].map(key => <div className="skeleton-card" key={key}>
    <div className="artwork-frame"><div className="artwork-square skeleton-block" /></div>
    <div className="skeleton-line" /><div className="skeleton-line skeleton-short" />
  </div>)}</div>;
}

export function Home() {
  const [state, setState] = useState<FeedState>({ kind: 'loading' });
  const [request, setRequest] = useState(0);
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    readRecent(controller.signal).then(page => {
      if (!controller.signal.aborted) setState({ kind: 'ready', items: page.items });
    }).catch(error => {
      if (!controller.signal.aborted) {
        setNow(Date.now());
        setState({ kind: 'error', error: error instanceof FeedError ? error : new FeedError(0) });
      }
    });
    return () => controller.abort();
  }, [request]);
  const retryAt = state.kind === 'error' ? state.error.retryAt : null;
  const waiting = retryAt !== null && retryAt > now;
  useEffect(() => {
    if (!retryAt || !waiting) return;
    const timer = window.setTimeout(() => setNow(Date.now()), 1000);
    return () => window.clearTimeout(timer);
  }, [retryAt, waiting, now]);
  return <main id="main-content" className="page-container home-main" tabIndex={-1}>
    <section aria-labelledby="recent-heading">
      <div className="section-heading"><h1 id="recent-heading">Recent recommendations</h1><span className="editorial-meta">Public feed</span></div>
      <p className="sr-only" role="status">{state.kind === 'loading' ? 'Loading recent recommendations…'
        : state.kind === 'ready' ? state.items.length ? `${state.items.length} recent recommendations loaded.` : 'No public recommendations yet.' : ''}</p>
      {state.kind === 'loading' && <FeedSkeleton />}
      {state.kind === 'ready' && (state.items.length ? <div className="music-grid">{state.items.map((item, index) => <MusicCard key={item.sequence} item={item} index={index} />)}</div>
        : <div className="feed-message"><h2>No public recommendations yet</h2><p>Recent discoveries will appear here when they’re available.</p></div>)}
      {state.kind === 'error' && <div className="feed-message"><h2>Recommendations are taking a moment</h2>
        <p role="alert">{state.error.message}</p>
        <button className="button button-secondary" type="button" disabled={waiting} onClick={() => { setNow(Date.now()); setRequest(value => value + 1); }}>
          {waiting ? `Try again in ${Math.ceil((retryAt! - now) / 1000)}s` : 'Try again'}
        </button>
      </div>}
    </section>
    <section className="discovery-callout" aria-label="Personal discovery">
      <div><h2>Great music, explained.</h2><p>A few genuinely good recommendations, shaped by what you love and why it resonates.</p></div>
      <Link className="button button-primary" to="/discover">Start discovering <ArrowIcon /></Link>
    </section>
  </main>;
}
