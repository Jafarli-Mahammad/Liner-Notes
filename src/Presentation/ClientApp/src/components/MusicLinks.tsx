function storedLink(value: string | null, hosts: string[]) {
  if (!value) return null;
  try { const uri = new URL(value); return uri.protocol === 'https:' && !uri.username && !uri.password && hosts.includes(uri.hostname) ? uri.href : null; } catch { return null; }
}
function ServiceLogo({ service }: { service: string }) {
  return <svg className="service-logo" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {service === 'spotify' ? <><circle cx="12" cy="12" r="9.5" /><path d="M6.5 9c4-1.2 8-.8 11 1M7.5 12.5c3-1 6.5-.6 9 1M8.5 16c2.5-.7 4.5-.4 6.5.7" /></>
      : service === 'apple' ? <><path d="M15 3c0 2-1.5 3.5-3 3.5C12 4.5 13.5 3 15 3ZM16 7c-2-1-3 .5-4 .5S9 6 7 7c-4 2-2 9 1 12 1.5 1.5 2.5 0 4 0s2.5 1.5 4 0c1-1 2-3 2.5-4.5-3-1-3.5-4.5-.5-6.5Z" /></>
      : service === 'youtube' ? <><rect x="2" y="5" width="20" height="14" rx="4" /><path d="m10 9 5 3-5 3Z" /></>
      : <path d="M3 17 8 7h13l-5 10Z" />}
  </svg>;
}
type MusicMetadata = { artistName: string; title: string; spotifyUrl?: string | null; youtubeUrl?: string | null };
export function MusicLinks({ pick, compact = false }: { pick: MusicMetadata; compact?: boolean }) {
  const query = encodeURIComponent(`${pick.artistName} ${pick.title}`.trim());
  const spotify = storedLink(pick.spotifyUrl ?? null, ['open.spotify.com']);
  const youtube = storedLink(pick.youtubeUrl ?? null, ['www.youtube.com', 'youtube.com', 'youtu.be']);
  if (!pick.artistName.trim() && !pick.title.trim()) return null;
  const services = [
    { id: 'spotify', name: 'Spotify', url: spotify ?? `https://open.spotify.com/search/${query}`, direct: !!spotify },
    { id: 'apple', name: 'Apple Music', url: `https://music.apple.com/search?term=${query}` },
    { id: 'youtube', name: 'YouTube', url: youtube ?? `https://www.youtube.com/results?search_query=${query}`, direct: !!youtube },
    { id: 'bandcamp', name: 'Bandcamp', url: `https://bandcamp.com/search?q=${query}` },
  ];
  return <div className={`music-links${compact ? ' music-links-compact' : ''}`}>
    {services.map(service => {
      const label = `${service.direct ? 'Open' : 'Search'} ${service.name}`;
      return <a key={service.id} className={`music-link-${service.id}`} href={service.url} target="_blank" rel="noopener noreferrer" aria-label={`${label}: ${pick.artistName} — ${pick.title} (opens in a new tab)`} title={label}>
        <ServiceLogo service={service.id} /><span className={compact ? 'sr-only' : undefined}>{label}</span>
      </a>;
    })}
  </div>;
}
