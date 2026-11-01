import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { MusicLinks } from '../../components/MusicLinks';
import { Artwork } from '../../components/Artwork';
import { ArrowIcon } from '../../components/ArrowIcon';
import { FeedError, readRecent } from '../../api/recent';
import { api, ApiError, useIdentity } from '../../api/client';
import type { Overview, Batch } from '../../api/models';
import type { RecentMusic } from '../../api/recent';

type FeedState = { kind: 'loading' } | { kind: 'ready'; items: RecentMusic[] } | { kind: 'error'; error: FeedError };

function MusicCard({ item, index }: { item: RecentMusic; index: number }) {
  return <article className="music-card">
    <Artwork id={item.recordingId} eager={index === 0} />
    <h2 className="music-title">{item.title}</h2>
    <p className="music-artist">{item.artistName}</p>
    <MusicLinks pick={item} compact />
    {item.batchId && <Link className="text-link" to={`/history/${item.batchId}`}>View discovery →</Link>}
    <p className="music-attribution">{item.attribution}{item.origin === 'Synthetic' ? ' · Synthetic test data' : ''}</p>
  </article>;
}

function FeedSkeleton() {
  return <div className="music-grid" aria-hidden="true">{[0, 1, 2, 3].map(key => <div className="skeleton-card" key={key}>
    <div className="artwork-frame"><div className="artwork-square skeleton-block" /></div>
    <div className="music-title skeleton-block skeleton-title" /><div className="music-artist skeleton-block skeleton-artist" />
    <div className="skeleton-services">{[0, 1, 2, 3].map(service => <span className="skeleton-block" key={service} />)}</div>
    <div className="skeleton-block skeleton-credit" />
  </div>)}</div>;
}

export function Home() {
  const identity = useIdentity();
  return <HomeFeed key={identity.epoch} personal={!!identity.user} />;
}
function HomeFeed({ personal }: { personal: boolean }) {
  const [state, setState] = useState<FeedState>({ kind: 'loading' });
  const [request, setRequest] = useState(0);
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    async function read() {
      if (!personal) return readRecent(controller.signal);
      const overview = await api<Overview>('/api/discovery', { signal: controller.signal });
      const summaries = [...(overview.page?.batches ?? [])]
        .sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc));
      const items: RecentMusic[] = [];
      for (const summary of summaries.slice(0, 4)) {
        const batch = await api<Batch>(`/api/discovery/batches/${encodeURIComponent(summary.id)}`, { signal: controller.signal });
        items.push(...[...batch.picks].sort((a, b) => a.rank - b.rank).map(pick => ({
          sequence: pick.id, recordingId: pick.mbid ?? '', title: pick.title, artistName: pick.artistName,
          origin: 'Personal', attribution: pick.sources.join(' · '), batchId: batch.id,
          spotifyUrl: pick.spotifyUrl, youtubeUrl: pick.youtubeUrl,
        })));
        if (items.length >= 4) break;
      }
      return { items: items.slice(0, 4), nextCursor: null };
    }
    read().then(page => {
      if (!controller.signal.aborted) setState({ kind: 'ready', items: page.items });
    }).catch(error => {
      if (!controller.signal.aborted) {
        setNow(Date.now());
        setState({ kind: 'error', error: error instanceof FeedError ? error : new FeedError(error instanceof ApiError ? error.status : 0, error instanceof ApiError ? error.retryAt : null) });
      }
    });
    return () => controller.abort();
  }, [request, personal]);
  const retryAt = state.kind === 'error' ? state.error.retryAt : null;
  const waiting = retryAt !== null && retryAt > now;
  useEffect(() => {
    if (!retryAt || !waiting) return;
    const timer = window.setTimeout(() => setNow(Date.now()), 1000);
    return () => window.clearTimeout(timer);
  }, [retryAt, waiting, now]);
  return <main id="main-content" className="page-container home-main" tabIndex={-1}>
    <section aria-labelledby="recent-heading" aria-busy={state.kind === 'loading'}>
      <div className="section-heading"><h1 id="recent-heading">Recent recommendations</h1><span className="editorial-meta">{personal ? <Link to="/history">View all history →</Link> : 'Public feed'}</span></div>
      <p className="sr-only" role="status">{state.kind === 'loading' ? 'Loading recent recommendations…'
        : state.kind === 'ready' ? state.items.length ? `${state.items.length} recent recommendations loaded.` : personal ? 'No saved recommendations yet.' : 'No public recommendations yet.' : ''}</p>
      {state.kind === 'loading' && <FeedSkeleton />}
      {state.kind === 'ready' && (state.items.length ? <div className="music-grid">{state.items.map((item, index) => <MusicCard key={item.sequence} item={item} index={index} />)}</div>
        : <div className="feed-message"><h2>{personal ? 'Your discoveries start here' : 'No public recommendations yet'}</h2><p>{personal ? 'Find recommendations to start your personal history. Your saved picks will appear here.' : 'Recent discoveries will appear here when they’re available.'}</p>{personal && <Link className="text-link" to="/discover">Find recommendations →</Link>}</div>)}
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
