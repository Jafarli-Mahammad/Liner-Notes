import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { Outlet } from 'react-router';
import { api, ApiError, identityEpoch, isAbort, useIdentity } from '../api/client';
import type { Answers, Batch, Favorite, Feedback, Job, Overview, Pick, Session } from '../api/models';
import { Notice } from '../components/Notice';

interface PersonalData { session: Session; overview: Overview; epoch: number; }
interface PersonalContext {
  data: PersonalData | null; loading: boolean; error: unknown; busy: boolean;
  reload: () => Promise<PersonalData>;
  ensure: () => Promise<PersonalData>;
  exclusive: <T>(operation: () => Promise<T>) => Promise<T>;
  write: <T>(path: string, body?: unknown, method?: string) => Promise<T>;
  saveTaste: (answers: Answers, favorites: Favorite[], tags: string[]) => Promise<void>;
  generate: () => Promise<Job>;
  feedback: (pick: Pick, value: Feedback, rating: number | null, comment: string) => Promise<void>;
  batch: (id: string, signal?: AbortSignal) => Promise<Batch>;
}
const Context = createContext<PersonalContext | null>(null);
export function usePersonal() { const value = useContext(Context); if (!value) throw new Error('Personal context missing'); return value; }

export function PersonalLayout() {
  const identity = useIdentity();
  const [data, setData] = useState<PersonalData | null>(null), [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null), [busy, setBusy] = useState(false);
  const current = useRef<PersonalData | null>(null), locked = useRef(false), mounted = useRef(true);
  const batches = useRef(new Map<string, Batch>()), bootstrap = useRef<Promise<PersonalData> | null>(null);
  const intent = useRef<{ fingerprint: string; key: string; previousJobId: string | null } | null>(null);
  const put = (value: PersonalData) => { current.current = value; if (mounted.current) setData(value); };
  const reload = useCallback(async () => {
    const epoch = identityEpoch();
    const session = await api<Session>('/api/discovery/session');
    const overview = await api<Overview>('/api/discovery');
    batches.current.clear();
    if (intent.current && overview.page?.currentJob && overview.page.currentJob.id !== intent.current.previousJobId) intent.current = null;
    if (epoch !== identityEpoch()) throw new DOMException('Session changed', 'AbortError');
    const value = { session, overview, epoch }; put(value); setError(null); return value;
  }, []);
  useEffect(() => {
    mounted.current = true; current.current = null; setData(null); batches.current.clear(); intent.current = null;
    setLoading(true); setError(null);
    const operation = reload(); bootstrap.current = operation;
    operation.catch(e => { if (!isAbort(e) && mounted.current) setError(e); }).finally(() => { if (mounted.current && bootstrap.current === operation) setLoading(false); });
    return () => { mounted.current = false; };
  }, [identity.epoch, reload]);

  async function ensure() {
    let value = current.current ?? await (bootstrap.current ?? reload());
    if (value.epoch !== identityEpoch()) throw new DOMException('Session changed', 'AbortError');
    if (!value.session.profile) {
      await api('/api/discovery/session', { method: 'POST', csrf: value.session.csrfToken });
      value = await reload();
    }
    return value;
  }
  async function exclusive<T>(operation: () => Promise<T>) {
    if (locked.current) throw new ApiError(409, 'Another change is still being saved. Please wait.');
    locked.current = true; setBusy(true);
    try { return await operation(); } finally { locked.current = false; if (mounted.current) setBusy(false); }
  }
  async function write<T>(path: string, body?: unknown, method = 'POST') {
    const value = await ensure();
    if (value.epoch !== identityEpoch()) throw new DOMException('Session changed', 'AbortError');
    return api<T>(path, { method, body, csrf: value.session.csrfToken });
  }
  function revision(value: number) {
    const saved = current.current;
    if (!saved) return;
    put({ ...saved, session: { ...saved.session, profile: saved.session.profile ? { ...saved.session.profile, revision: value } : null },
      overview: { ...saved.overview, page: saved.overview.page ? { ...saved.overview.page, revision: value } : null } });
  }
  const value: PersonalContext = {
    data, loading, error, busy, reload, ensure, exclusive, write,
    async saveTaste(answers, favorites, tags) {
      await exclusive(async () => {
        const saved = await ensure();
        const result = await write<{ revision: number }>('/api/discovery/onboarding', {
          revision: saved.overview.page?.revision ?? saved.session.profile!.revision, answers,
          selection: { catalogRecordIds: favorites.flatMap(f => f.catalogRecordId ? [f.catalogRecordId] : []),
            legacySignalIds: favorites.flatMap(f => !f.catalogRecordId && f.signalId ? [f.signalId] : []), tags },
        }, 'PUT');
        revision(result.revision); await reload();
      });
    },
    async generate() {
      return exclusive(async () => {
        const saved = await ensure();
        const body = { revision: saved.overview.page?.revision ?? saved.session.profile!.revision };
        const fingerprint = JSON.stringify(body);
        if (intent.current?.fingerprint !== fingerprint) intent.current = { fingerprint, key: crypto.randomUUID(), previousJobId: saved.overview.page?.currentJob?.id ?? null };
        const job = await write<Job>('/api/discovery/batches', { ...body, requestKey: intent.current.key });
        intent.current = null; await reload(); return job;
      });
    },
    async feedback(pick, feedback, rating, comment) {
      await exclusive(async () => {
        const saved = await ensure();
        const result = await write<{ revision: number }>(`/api/discovery/recommendations/${encodeURIComponent(pick.id)}/feedback`, {
          revision: saved.overview.page?.revision ?? saved.session.profile!.revision, feedback, rating, comment: comment || null,
        });
        revision(result.revision); batches.current.clear();
      });
    },
    async batch(id, signal) {
      const cached = batches.current.get(id); if (cached) return cached;
      const epoch = identityEpoch();
      const result = await api<Batch>(`/api/discovery/batches/${encodeURIComponent(id)}`, { signal });
      if (epoch === identityEpoch() && !signal?.aborted) batches.current.set(id, result);
      return result;
    },
  };
  return <Context.Provider value={value}>{loading || (data !== null && data.epoch !== identity.epoch) ? <main id="main-content" className="page-container personal-main"><p role="status">Loading your discoveries…</p></main>
    : error ? <main id="main-content" className="page-container personal-main"><Notice error={error} onRetry={() => { setLoading(true); reload().catch(setError).finally(() => setLoading(false)); }} /></main>
    : <Outlet />}</Context.Provider>;
}
