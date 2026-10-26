export interface RecentMusic {
  sequence: number;
  recordingId: string;
  title: string;
  artistName: string;
  origin: string;
  attribution: string;
}

export interface RecentMusicPage {
  items: RecentMusic[];
  nextCursor: number | null;
}

export class FeedError extends Error {
  constructor(public readonly status: number, public readonly retryAt: number | null = null) {
    super(status === 404 ? 'Public recommendations are not available right now.'
      : status === 429 ? 'Please wait a moment before trying again.'
      : 'Recent recommendations could not be loaded. Please try again.');
  }
}

export async function readRecent(signal: AbortSignal): Promise<RecentMusicPage> {
  const response = await fetch('/api/discovery/recent?limit=4', {
    signal, credentials: 'omit', cache: 'no-store', headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    const retry = response.headers.get('Retry-After')?.trim();
    const date = retry && /^\d+$/.test(retry) ? Date.now() + Number(retry) * 1000 : Date.parse(retry ?? '');
    throw new FeedError(response.status, Number.isFinite(date) ? date : null);
  }
  if (!response.headers.get('Content-Type')?.includes('application/json')) throw new FeedError(502);
  const page: unknown = await response.json();
  if (!isRecentPage(page)) throw new FeedError(502);
  return page;
}

function isRecentPage(value: unknown): value is RecentMusicPage {
  if (!value || typeof value !== 'object' || !('items' in value) || !Array.isArray(value.items)
    || !('nextCursor' in value) || !(value.nextCursor === null || Number.isSafeInteger(value.nextCursor))) return false;
  return value.items.length <= 4 && value.items.every(item => item && typeof item === 'object'
    && Number.isSafeInteger(item.sequence) && item.sequence > 0
    && ['recordingId', 'title', 'artistName', 'origin', 'attribution'].every(key => typeof item[key] === 'string'));
}
