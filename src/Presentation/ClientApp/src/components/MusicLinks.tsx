import type { Pick } from '../api/models';
import { ArrowIcon } from './ArrowIcon';

function storedLink(value: string | null, hosts: string[]) {
  if (!value) return null;
  try { const uri = new URL(value); return uri.protocol === 'https:' && !uri.username && !uri.password && hosts.includes(uri.hostname) ? uri.href : null; } catch { return null; }
}
export function MusicLinks({ pick, compact = false }: { pick: Pick; compact?: boolean }) {
  const query = encodeURIComponent(`${pick.artistName} ${pick.title}`);
  const spotify = storedLink(pick.spotifyUrl, ['open.spotify.com']);
  const youtube = storedLink(pick.youtubeUrl, ['www.youtube.com', 'youtube.com', 'youtu.be']);
  return <div className={`music-links${compact ? ' music-links-compact' : ''}`}>
    <a className="music-link-spotify" href={spotify ?? `https://open.spotify.com/search/${query}`} target="_blank" rel="noopener noreferrer">{spotify ? 'Open Spotify' : 'Search Spotify'}<ArrowIcon /></a>
    {!compact && <><a className="music-link-apple" href={`https://music.apple.com/search?term=${query}`} target="_blank" rel="noopener noreferrer">Search Apple Music<ArrowIcon /></a>
      <a className="music-link-youtube" href={youtube ?? `https://www.youtube.com/results?search_query=${query}`} target="_blank" rel="noopener noreferrer">{youtube ? 'Open YouTube' : 'Search YouTube'}<ArrowIcon /></a>
      <details><summary>More places to listen</summary><div className="extra-links"><a href={`https://bandcamp.com/search?q=${query}`} target="_blank" rel="noopener noreferrer">Search Bandcamp</a></div></details></>}
  </div>;
}
