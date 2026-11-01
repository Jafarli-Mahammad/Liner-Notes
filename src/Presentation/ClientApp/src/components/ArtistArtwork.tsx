import { useEffect, useState } from 'react';
import { retryDeadline } from '../api/client';

interface Photo { imageUrl: string; sourceUrl: string; credit: string; license: string; licenseUrl: string; }
export function ArtistArtwork({ id, name }: { id?: string | null; name: string }) {
  const [photo, setPhoto] = useState<Photo | null>(null);
  useEffect(() => {
    setPhoto(null);
    if (!id || !/^[0-9a-f-]{36}$/i.test(id)) return;
    const controller = new AbortController();
    const deadline = Date.now() + 90_000;
    let timer: ReturnType<typeof setTimeout>;
    async function load() {
      try {
        const response = await fetch(`/api/artwork/artists/${id}`, { credentials: 'omit', cache: 'no-store',
          signal: AbortSignal.any([controller.signal, AbortSignal.timeout(15_000)]) });
        if (response.ok) {
          const value: Photo = await response.json();
          if (!controller.signal.aborted && value.imageUrl === `/api/artwork/artists/${id}/image`
            && value.sourceUrl.startsWith('https://commons.wikimedia.org/')) setPhoto(value);
          return;
        }
        if (response.status !== 503 && response.status !== 429) return;
        const delay = Math.max(3000, (retryDeadline(response.headers.get('Retry-After')) ?? 0) - Date.now());
        if (!controller.signal.aborted && Date.now() + delay < deadline) timer = setTimeout(load, delay);
      } catch { /* A photo failure never blocks editing or saving taste. */ }
    }
    void load();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [id]);
  const initials = name.trim().split(/\s+/).slice(0, 2).map(word => [...word][0]).join('');
  return photo ? <a className="artist-photo" href={photo.sourceUrl} target="_blank" rel="noreferrer"
    title={`Photo: ${photo.credit} · ${photo.license}`} aria-label={`Photo: ${photo.credit} · ${photo.license}. Source and license details on Wikimedia Commons.`}>
    <img src={photo.imageUrl} alt="" width="48" height="48" loading="lazy" decoding="async" onError={() => setPhoto(null)} />
  </a> : <span className="artist-initials" role="img" aria-label={`Photo unavailable for ${name}`}>{initials}</span>;
}
