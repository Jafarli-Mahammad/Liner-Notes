import { useEffect, useId, useRef, useState } from 'react';
import { api, ApiError, apiResponse, errorText, isAbort, retryDeadline } from '../../api/client';
import type { CatalogItem, CatalogPage, Favorite, Lookup } from '../../api/models';
import { usePersonal } from '../../app/Personal';

export function CatalogPicker({ onChoose }: { onChoose: (favorite: Favorite) => void }) {
  const personal = usePersonal();
  const [query, setQuery] = useState(''), [kind, setKind] = useState('Artist'), [hits, setHits] = useState<CatalogItem[]>([]);
  const [hasMore, setHasMore] = useState(false), [offset, setOffset] = useState(0), [active, setActive] = useState(-1);
  const [open, setOpen] = useState(false), [message, setMessage] = useState(''), [lookupBusy, setLookupBusy] = useState(false);
  const version = useRef(0), controller = useRef<AbortController | null>(null), input = useRef<HTMLInputElement>(null);
  const lookupKey = useRef<{ fingerprint: string; key: string } | null>(null);
  const id = useId();
  useEffect(() => {
    const current = ++version.current, abort = new AbortController(); controller.current?.abort(); controller.current = abort;
    setLookupBusy(false); setActive(-1); setHits([]); setHasMore(false); setOffset(0);
    if (query.trim().length < 2) { setMessage(''); setOpen(false); return () => abort.abort(); }
    setMessage('Searching the local catalog…'); setOpen(true);
    const timer = setTimeout(() => search(0, abort.signal, current), 180);
    return () => { clearTimeout(timer); abort.abort(); };
  }, [query, kind]);
  async function search(start: number, signal: AbortSignal, current: number) {
    try {
      const result = await api<CatalogPage>(`/api/catalog/search?query=${encodeURIComponent(query.trim())}&kind=${kind}&limit=10&offset=${start}`, { signal });
      if (current !== version.current || signal.aborted) return;
      setHits(result.items); setHasMore(result.hasMore); setOffset(start); setActive(-1);
      setMessage(result.items.length ? 'Choose a match. Use the arrow keys and Enter.' : 'No local matches. You can search the music source explicitly.');
    } catch (e) { if (!isAbort(e) && current === version.current) setMessage(errorText(e)); }
  }
  async function lookup() {
    if (query.trim().length < 2 || lookupBusy) return;
    const current = ++version.current, abort = new AbortController(); controller.current?.abort(); controller.current = abort;
    setLookupBusy(true); setMessage('Searching the music source…');
    const fingerprint = JSON.stringify({ query: query.trim(), kind });
    if (lookupKey.current?.fingerprint !== fingerprint) lookupKey.current = { fingerprint, key: crypto.randomUUID() };
    try {
      const job = await personal.exclusive(async () => personal.write<Lookup>('/api/catalog/lookup', { query: query.trim(), kind, requestKey: lookupKey.current!.key }));
      let status = job, deadline = Date.now() + 2000;
      while (!abort.signal.aborted && current === version.current) {
        if (status.state === 'Completed') {
          setHits(status.items ?? []); setHasMore(false); setActive(-1); lookupKey.current = null;
          setMessage(status.errorCode ? 'Those matches are no longer available. Search again.' : status.items?.length ? 'Choose a match from the music source.' : 'The source found no matches. Try another spelling.'); return;
        }
        if (['Failed', 'Cancelled'].includes(status.state)) { lookupKey.current = null; setMessage('The music source could not complete this search. Try again later.'); return; }
        await new Promise<void>((resolve, reject) => {
          const timer = setTimeout(done, Math.min(Math.max(0, deadline - Date.now()), 2_147_483_647));
          function done() { abort.signal.removeEventListener('abort', cancel); resolve(); }
          function cancel() { clearTimeout(timer); reject(new DOMException('Search changed', 'AbortError')); }
          abort.signal.addEventListener('abort', cancel, { once: true });
        });
        if (document.hidden || deadline > Date.now()) { deadline = Math.max(deadline, Date.now() + 2000); continue; }
        try {
          const response = await apiResponse(`/api/catalog/lookups/${encodeURIComponent(job.id)}`, { signal: abort.signal });
          status = await response.json(); deadline = Math.max(Date.now() + 2000, retryDeadline(response.headers.get('Retry-After')) ?? 0);
        } catch (e) {
          if (e instanceof ApiError && [429, 503].includes(e.status)) { setMessage(errorText(e)); deadline = Math.max(Date.now() + 5000, e.retryAt ?? 0); }
          else throw e;
        }
      }
    } catch (e) { if (!isAbort(e) && current === version.current) setMessage(errorText(e)); }
    finally { if (current === version.current) setLookupBusy(false); }
  }
  function choose(item: CatalogItem) {
    onChoose({ catalogRecordId: item.id, canonicalId: item.canonicalId, name: item.name, artistName: item.artistName,
      kind: item.kind, disambiguation: item.disambiguation, releases: item.releases });
    setQuery(''); setHits([]); setOpen(false); setMessage('Added to your music.'); input.current?.focus();
  }
  return <div className="catalog-picker"><div className="catalog-search">
    <span className="search-symbol" aria-hidden="true"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 4 4" /></svg></span>
    <label className="sr-only" htmlFor={`${id}-search`}>Search artists, albums, or tracks</label>
    <input id={`${id}-search`} ref={input} type="search" role="combobox" autoComplete="off" placeholder="Search artists, albums, or tracks…" maxLength={128}
      aria-autocomplete="list" aria-expanded={open} aria-controls={`${id}-results`} aria-describedby={`${id}-status`}
      aria-activedescendant={active >= 0 ? `${id}-option-${active}` : undefined} value={query} onChange={e => setQuery(e.target.value)}
      onFocus={() => { if (query.trim().length >= 2) setOpen(true); }}
      onKeyDown={e => {
        if (e.key === 'Escape') { setOpen(false); setActive(-1); }
        if (['ArrowDown', 'ArrowUp'].includes(e.key) && hits.length) { e.preventDefault(); setOpen(true); setActive(value => (value + (e.key === 'ArrowDown' ? 1 : -1) + hits.length) % hits.length); }
        if (e.key === 'Enter' && open && active >= 0 && hits[active]) { e.preventDefault(); choose(hits[active]); }
      }} />
    <label className="sr-only" htmlFor={`${id}-kind`}>Music type</label><select id={`${id}-kind`} value={kind} onChange={e => setKind(e.target.value)}><option value="Artist">Artists</option><option value="Album">Albums</option><option value="Track">Tracks</option></select>
  </div>
    <p id={`${id}-status`} className="sr-only" role="status">{message}</p>
    {open && <div className="catalog-popover"><ul id={`${id}-results`} role="listbox" aria-label="Music search results">
      {hits.map((item, index) => <li role="option" aria-selected={index === active} key={item.id} id={`${id}-option-${index}`} onClick={() => choose(item)} onMouseMove={() => setActive(index)}>
        <strong>{item.name}</strong><span>{[item.artistName, item.disambiguation, item.firstReleaseDate].filter(Boolean).join(' · ') || item.kind}</span><small>{item.attribution}</small>
      </li>)}
    </ul><p className="field-help">{message}</p><div className="action-row">
      {hasMore && <button type="button" className="text-button" onClick={() => search(offset + 10, controller.current!.signal, version.current)}>More local matches</button>}
      <button type="button" className="text-button" disabled={lookupBusy || personal.busy} onClick={lookup}>{lookupBusy ? 'Searching music source…' : 'Search music source'}</button>
      <button type="button" className="text-button" onClick={() => setOpen(false)}>Close results</button>
    </div></div>}
  </div>;
}
