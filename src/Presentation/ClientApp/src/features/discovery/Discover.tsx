import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { usePersonal } from '../../app/Personal';
import { apiResponse, ApiError, isAbort, retryDeadline } from '../../api/client';
import { unfinished } from '../../api/models';
import type { Batch, Job, Pick } from '../../api/models';
import { Artwork } from '../../components/Artwork';
import { FeedbackControls } from '../../components/FeedbackControls';
import { MusicLinks } from '../../components/MusicLinks';
import { Notice } from '../../components/Notice';
import { useRetryDelay } from '../../hooks/useRetryDelay';

function PickContent({ pick, featured, onSaved }: { pick: Pick; featured?: boolean; onSaved: (pick: Pick) => void }) {
  return <article className={`personal-pick${featured ? ' featured-pick' : ''}`}>
    <div className={featured ? 'featured-art' : ''}><Artwork id={pick.mbid ?? ''} eager={featured} /></div>
    <h2>{pick.title}</h2><p className="pick-artist">{pick.artistName}</p>
    {!!pick.matchedTags.length && <div className="pick-tags">{pick.matchedTags.map(tag => <span className="soft-tag" key={tag}>{tag}</span>)}</div>}
    {featured && <span className="editorial-meta pick-reason-label">Why this fits</span>}
    <p className="pick-explanation">{pick.explanation}</p>
    <MusicLinks pick={pick} compact={!featured} />
    {featured ? <FeedbackControls pick={pick} onSaved={onSaved} /> : <details className="compact-feedback"><summary>Give feedback</summary><FeedbackControls pick={pick} onSaved={onSaved} /></details>}
    {!!pick.sources.length && <details className="source-note"><summary>Music evidence</summary><p>{pick.sources.join(', ')}</p></details>}
  </article>;
}

export default function Discover() {
  const personal = usePersonal(), { batchId } = useParams();
  const page = personal.data?.overview.page;
  const id = batchId ?? page?.batches[0]?.id;
  const [batch, setBatch] = useState<Batch | null>(null), [loading, setLoading] = useState(false), [error, setError] = useState<unknown>(null);
  const [readVersion, setReadVersion] = useState(0);
  const [job, setJob] = useState<Job | null>(page?.currentJob ?? null), [now, setNow] = useState(Date.now());
  const retryDelay = useRetryDelay(error);
  const reloadRef = useRef(personal.reload); reloadRef.current = personal.reload;
  const readBatch = useRef(personal.batch); readBatch.current = personal.batch;
  useEffect(() => {
    const controller = new AbortController(); setError(null); setBatch(null);
    if (id) { setLoading(true); readBatch.current(id, controller.signal).then(value => { if (!controller.signal.aborted) setBatch(value); }).catch(e => { if (!isAbort(e)) setError(e); }).finally(() => { if (!controller.signal.aborted) setLoading(false); }); }
    return () => controller.abort();
  }, [id, readVersion]);
  useEffect(() => { setJob(page?.currentJob ?? null); }, [page?.currentJob?.id, page?.currentJob?.state]);
  const active = !batchId && unfinished(job), next = page?.nextRequestAtUtc ? Date.parse(page.nextRequestAtUtc) : 0;
  useEffect(() => {
    if (!active && !(next > now)) return;
    const timer = setTimeout(() => setNow(Date.now()), 1000); return () => clearTimeout(timer);
  }, [active, next, now]);
  useEffect(() => {
    if (!active || !job) return;
    const controller = new AbortController(); let timer: ReturnType<typeof setTimeout>;
    let deadline = 0;
    const schedule = (time: number) => { deadline = time; timer = setTimeout(poll, Math.min(Math.max(time - Date.now(), 0), 2_147_483_647)); };
    const poll = async () => {
      if (controller.signal.aborted) return;
      if (document.hidden) { schedule(Math.max(deadline, Date.now() + 2000)); return; }
      if (deadline > Date.now()) { schedule(deadline); return; }
      try {
        const response = await apiResponse(`/api/discovery/jobs/${encodeURIComponent(job.id)}`, { signal: controller.signal });
        const status: Job = await response.json(); if (controller.signal.aborted) return; setJob(status);
        if (unfinished(status)) schedule(Math.max(Date.now() + 1000, retryDeadline(response.headers.get('Retry-After')) ?? 0));
        else if (status.state !== 'Completed') setError(new Error(status.errorCode === 'job_timeout'
          ? 'The music source took too long. Your saved picks are safe. You can request discoveries again.'
          : status.state === 'Cancelled' ? 'Your taste changed and that request was cancelled. You can request more discoveries when ready.'
            : 'The music source could not complete that request. Your saved picks are still here.'));
      } catch (e) {
        if (controller.signal.aborted) return;
        setError(e);
        if (e instanceof ApiError && [401, 403, 404].includes(e.status)) { setJob(null); return; }
        schedule(Math.max(Date.now() + 5000, e instanceof ApiError ? e.retryAt ?? 0 : 0));
      }
    };
    schedule(Date.now() + 1000);
    return () => { controller.abort(); clearTimeout(timer); };
  }, [active, job?.id]);
  useEffect(() => {
    if (batchId || !job || unfinished(job)) return;
    let cancelled = false, timer: ReturnType<typeof setTimeout>;
    let attempts = 0;
    const refresh = async () => {
      try { await reloadRef.current(); if (!cancelled && job.state === 'Completed') setError(null); }
      catch (e) {
        if (cancelled || isAbort(e)) return;
        setError(e);
        if (++attempts < 4 && !(e instanceof ApiError && [401, 403, 404].includes(e.status)))
          timer = setTimeout(refresh, Math.max(attempts * 2000, e instanceof ApiError ? (e.retryAt ?? 0) - Date.now() : 0));
      }
    };
    void refresh();
    return () => { cancelled = true; clearTimeout(timer); };
  }, [batchId, job?.id, job?.state, readVersion]);
  const hasTaste = !!(page?.favorites.length || page?.tags.length || page?.onboarding?.allStyles);
  const disabled = personal.busy || active || next > now || retryDelay > 0;
  async function generate() { setError(null); try { setJob(await personal.generate()); } catch (e) { setError(e); } }
  async function refreshSaved() { try { await personal.reload(); setError(null); setReadVersion(value => value + 1); } catch (e) { setError(e); } }
  function saved(pick: Pick) { setBatch(value => value ? { ...value, picks: value.picks.map(p => p.id === pick.id ? pick : p) } : value); }
  const first = batch?.picks[0];
  const progress: Record<string, string> = { queued: 'Your discoveries are queued.', waiting_for_music: 'Getting music for your taste…', preparing: 'Finding and checking your picks…', persisting: 'Saving your discoveries…', waiting_to_retry: 'The music source needs a moment. Your request will retry automatically.' };
  return <main id="main-content" className="page-container personal-main discover-main" tabIndex={-1}>
    {batchId && <Link className="back-link" to="/history">← Discovery history</Link>}
    <section className="discovery-hero">
      <div className="discovery-intro"><p className="editorial-meta coral-meta">{batchId ? 'Past discovery' : 'Your discovery'}</p>
        <h1>{batchId ? 'A discovery worth revisiting.' : 'Meet your next obsession.'}</h1>
        <p className="intro-copy">A few genuinely good finds, tuned to your taste — thoughtful connections between what you already love and sounds worth hearing next.</p>
        {hasTaste ? <button className="button button-primary discovery-cta" type="button" disabled={disabled} onClick={generate}>{personal.busy ? 'Requesting discoveries…' : active ? 'Finding your next favorites…' : next > now ? `Ready in ${Math.ceil((next - now) / 1000)}s` : 'Find my next favorites'}</button>
          : <Link className="button button-primary discovery-cta" to="/taste">Find my next favorites</Link>}
        <div className="taste-summary"><span className="editorial-meta">Based on your taste</span>
          {hasTaste ? <div className="taste-strip">{page?.favorites.slice(0, 4).map(f => <span className="taste-chip" key={f.catalogRecordId ?? f.signalId}>{f.name}</span>)}{page?.tags.slice(0, 4).map(tag => <span className="taste-chip" key={tag}>{tag}</span>)}{page?.onboarding?.allStyles && <span className="taste-chip">All styles welcome</span>}</div>
            : <p className="field-help">Start with artists, albums, tracks or styles you love. Every preference is optional.</p>}
          <Link className="text-link" to="/taste">Tune your taste</Link>
        </div>
        {active && <Notice>{progress[job!.progressCode] ?? 'Finding music for your taste…'}</Notice>}
        {!!error && <Notice error={error} onRetry={refreshSaved} />}
        {personal.data?.overview.evidenceOrigin !== 'Live' && <p className="field-help">{personal.data?.overview.evidenceOrigin === 'Synthetic' ? 'Synthetic test data' : 'Recorded evidence'}</p>}
      </div>
      <div>{first ? <PickContent key={first.id} pick={first} featured onSaved={saved} /> : <div className="discover-empty">
        <div className="empty-sleeve" aria-hidden="true"><span /></div>
        <h2>{loading ? 'Opening your discoveries…' : batch ? 'No picks in this batch' : 'Your next discovery starts here'}</h2>
        <p>{loading ? 'Reading your saved recommendations.' : batch ? 'The available evidence was too limited for this batch. Try adjusting your taste.' : 'Choose a little music you love, then find a few tracks worth hearing next.'}</p>
        {!hasTaste && <Link className="text-link" to="/">Explore recent recommendations</Link>}
      </div>}</div>
    </section>
    {batch?.coverage?.Gaps?.length ? <Notice>Some selections had limited source evidence. Your saved explanations show the connections available.</Notice> : null}
    {!!batch && batch.picks.length > 1 && <section className="secondary-picks"><div className="more-heading"><h2>More finds for you</h2><span className="editorial-meta">{String(batch.picks.length - 1).padStart(2, '0')} more</span></div>
      <div className="personal-picks-grid">{batch.picks.slice(1).map(pick => <PickContent key={pick.id} pick={pick} onSaved={saved} />)}</div>
      {!batchId && <div className="discover-more"><button className="button button-secondary" disabled={disabled} onClick={generate} type="button">Find more recommendations <span aria-hidden="true">↻</span></button><p className="field-help">New suggestions may take a moment to generate.</p></div>}
    </section>}
  </main>;
}
