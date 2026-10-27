import { useEffect, useRef, useState } from 'react';
import { retryDeadline } from '../api/client';

const recordingId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function Artwork({ id, eager = false }: { id: string; eager?: boolean }) {
  const valid = recordingId.test(id) && id !== '00000000-0000-0000-0000-000000000000';
  const [status, setStatus] = useState<'loading' | 'ready' | 'missing'>('loading');
  const [source, setSource] = useState<string>();
  const [visible, setVisible] = useState(false);
  const frame = useRef<HTMLDivElement>(null);
  const loaded = useRef<string | null>(null);
  useEffect(() => {
    loaded.current = null; setSource(undefined); setStatus('loading');
  }, [id]);
  useEffect(() => {
    const update = () => {
      const rect = frame.current?.getBoundingClientRect();
      setVisible(document.visibilityState === 'visible' && !!rect && rect.top < innerHeight + 200 && rect.bottom > -200);
    };
    const observer = new IntersectionObserver(update, { rootMargin: '200px' });
    if (frame.current) observer.observe(frame.current);
    document.addEventListener('visibilitychange', update);
    return () => { observer.disconnect(); document.removeEventListener('visibilitychange', update); };
  }, []);
  useEffect(() => {
    if (!valid || !visible || loaded.current === id) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>, attempts = 0;
    const deadline = Date.now() + 10 * 60_000;
    const schedule = (retryAt = 0) => {
      const delay = Math.max(Math.min(2000 * 2 ** Math.min(attempts++, 3), 15_000), retryAt - Date.now());
      if (Date.now() + delay >= deadline) { setStatus('missing'); return; }
      timer = setTimeout(load, delay);
    };
    const load = async () => {
      if (Date.now() >= deadline) { setStatus('missing'); return; }
      try {
        const timeoutMs = Math.max(1, Math.min(20_000, deadline - Date.now()));
        const response = await fetch(`/api/artwork/recordings/${id}`, {
          method: 'HEAD',
          signal: AbortSignal.any([controller.signal, AbortSignal.timeout(timeoutMs)]), credentials: 'omit',
          referrerPolicy: 'no-referrer', priority: eager ? 'high' : 'auto',
        });
        if (controller.signal.aborted) return;
        const state = response.headers.get('X-Artwork-State');
        if (state === 'pending' || [429, 503].includes(response.status)) {
          await response.body?.cancel();
          schedule(retryDeadline(response.headers.get('Retry-After')) ?? 0); return;
        }
        if (!response.ok || state === 'unavailable') { await response.body?.cancel(); setStatus('missing'); return; }
        if (controller.signal.aborted) return;
        loaded.current = id; setSource(`/api/artwork/recordings/${id}`);
      } catch {
        if (!controller.signal.aborted) schedule();
      }
    };
    void load();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [id, valid, visible, eager]);
  return <div ref={frame} className={`artwork-frame artwork-${valid ? status : 'missing'}`}>
    <div className="artwork-square">
      <svg className="artwork-placeholder" viewBox="0 0 500 500" aria-hidden="true"><path fill="#e7e3db" d="M0 0h500v500H0z" /><circle cx="250" cy="250" r="140" fill="#ccc6ba" /><circle cx="250" cy="250" r="35" fill="#e7e3db" /></svg>
      {valid && <img src={source}
        width="500" height="500" alt="" loading={eager ? 'eager' : 'lazy'} fetchPriority={eager ? 'high' : 'auto'}
        decoding="async" referrerPolicy="no-referrer"
        onLoad={() => setStatus('ready')}
        onError={() => setStatus('missing')} />}
    </div>
    {(status === 'missing' || !valid) && <span className="sr-only">Cover unavailable</span>}
  </div>;
}
