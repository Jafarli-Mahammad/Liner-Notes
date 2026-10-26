import { useEffect, useState } from 'react';
import { api, errorText, isAbort } from '../../api/client';
import type { TagPage } from '../../api/models';

export function TagPicker({ selected, onChange }: { selected: string[]; onChange: (tags: string[]) => void }) {
  const [query, setQuery] = useState(''), [category, setCategory] = useState(''), [browse, setBrowse] = useState(false);
  const [result, setResult] = useState<TagPage | null>(null), [offset, setOffset] = useState(0), [message, setMessage] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      api<TagPage>(`/api/discovery/onboarding/tags?query=${encodeURIComponent(query)}&category=${category}&offset=${offset}&limit=4`, { signal: controller.signal })
        .then(data => { if (!controller.signal.aborted) { setResult(data); setMessage(data.items.length ? '' : 'No matching styles. Try another term.'); } }).catch(e => { if (!isAbort(e)) setMessage(errorText(e)); });
    }, query ? 150 : 0);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [query, category, offset]);
  return <div className="editor-panel tags-panel"><p className="panel-intro">Add a few descriptive tags to narrow down the vibe.</p>
    <div className="tag-cloud">{selected.map(tag => <button className="selected-tag" type="button" key={tag} onClick={() => onChange(selected.filter(value => value !== tag))} aria-label={`Remove style ${tag}`}>{tag.charAt(0).toUpperCase() + tag.slice(1)}<span aria-hidden="true">×</span></button>)}
      <button type="button" className="add-tag" onClick={() => setBrowse(value => !value)} aria-expanded={browse}>＋ Add tag</button>
    </div>
    {browse && <div className="tag-search-fields"><label>Find styles<input type="search" value={query} maxLength={128} onChange={e => { setQuery(e.target.value); setOffset(0); }} /></label><label>Browse<select value={category} onChange={e => { setCategory(e.target.value); setOffset(0); }}><option value="">All styles</option><option value="genre">Genres</option><option value="mood">Moods</option><option value="sound">Sounds</option></select></label></div>}
    <div className="tag-suggestions"><span className="editorial-meta">Suggestions</span><div className="tag-cloud">
      {result?.items.map(tag => <button type="button" className="suggested-tag" key={tag.id} aria-pressed={selected.includes(tag.id)} onClick={() => onChange(selected.includes(tag.id) ? selected.filter(value => value !== tag.id) : [...selected, tag.id])}>{tag.label}</button>)}
    </div>{browse && result?.hasMore && <button type="button" className="text-button" onClick={() => setOffset(value => value + 4)}>Browse more styles</button>}
      {!!offset && <button type="button" className="text-button" onClick={() => setOffset(0)}>First suggestions</button>}
    <p className="field-help" role="status">{message}</p></div>
  </div>;
}
