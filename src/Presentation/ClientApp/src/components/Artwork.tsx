import { useEffect, useRef, useState } from 'react';

const recordingId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function Artwork({ id, eager = false }: { id: string; eager?: boolean }) {
  const valid = recordingId.test(id) && id !== '00000000-0000-0000-0000-000000000000';
  const [attempt, setAttempt] = useState(0);
  const [status, setStatus] = useState<'loading' | 'ready' | 'missing'>('loading');
  const [visible, setVisible] = useState(false);
  const frame = useRef<HTMLDivElement>(null);
  useEffect(() => { setAttempt(0); setStatus('loading'); }, [id]);
  useEffect(() => {
    const update = () => {
      const rect = frame.current?.getBoundingClientRect();
      setVisible(document.visibilityState === 'visible' && !!rect && rect.top < innerHeight && rect.bottom > 0);
    };
    const observer = new IntersectionObserver(update);
    if (frame.current) observer.observe(frame.current);
    document.addEventListener('visibilitychange', update);
    return () => { observer.disconnect(); document.removeEventListener('visibilitychange', update); };
  }, []);
  useEffect(() => {
    if (!valid || !visible || status !== 'missing' || attempt >= 4) return;
    const timer = window.setTimeout(() => {
      const rect = frame.current?.getBoundingClientRect();
      if (document.visibilityState === 'visible' && rect && rect.top < innerHeight && rect.bottom > 0) {
        setAttempt(value => value + 1);
      }
    }, 30_000);
    return () => window.clearTimeout(timer);
  }, [valid, visible, status, attempt]);
  return <div ref={frame} className={`artwork-frame artwork-${valid ? status : 'missing'}`}>
    <div className="artwork-square">
      <svg className="artwork-placeholder" viewBox="0 0 500 500" aria-hidden="true"><path fill="#e7e3db" d="M0 0h500v500H0z" /><circle cx="250" cy="250" r="140" fill="#ccc6ba" /><circle cx="250" cy="250" r="35" fill="#e7e3db" /></svg>
      {valid && <img src={`/api/artwork/recordings/${id}${attempt ? `?refresh=${attempt}` : ''}`}
        width="500" height="500" alt="" loading={eager ? 'eager' : 'lazy'} fetchPriority={eager ? 'high' : 'auto'}
        decoding="async" referrerPolicy="no-referrer"
        onLoad={event => setStatus(event.currentTarget.naturalWidth === 80 ? 'missing' : 'ready')}
        onError={() => setStatus('missing')} />}
    </div>
    {(status === 'missing' || !valid) && <span className="sr-only">Cover unavailable</span>}
  </div>;
}
