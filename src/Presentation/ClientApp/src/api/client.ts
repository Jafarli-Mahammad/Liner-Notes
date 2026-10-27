import { useSyncExternalStore } from 'react';
import type { AuthResponse, Subscriber } from './models';

let credentials: AuthResponse | null = null;
let identity = { epoch: 0, user: null as Subscriber | null };
let attempt = 0;
let refreshing: Promise<void> | null = null;
const listeners = new Set<() => void>();
export function useIdentity() { return useSyncExternalStore(callback => { listeners.add(callback); return () => { listeners.delete(callback); }; }, () => identity); }
export function identityEpoch() { return identity.epoch; }
export function signOut() { credentials = null; refreshing = null; attempt++; identity = { epoch: identity.epoch + 1, user: null }; listeners.forEach(notify => notify()); }

export class ApiError extends Error {
  constructor(public status: number, message: string, public retryAt: number | null = null, public fields: Record<string, string[]> = {}) { super(message); }
}
export function retryDeadline(value: string | null) {
  const raw = value?.trim();
  const time = raw && /^\d+$/.test(raw) ? Date.now() + Number(raw) * 1000 : Date.parse(raw ?? '');
  return Number.isFinite(time) && Math.abs(time) <= 8_640_000_000_000_000 ? time : null;
}
export function errorText(error: unknown) {
  if (error instanceof DOMException && error.name === 'TimeoutError') return 'The request took too long. Your saved information is safe. Please try again.';
  return error instanceof Error ? error.message : 'That request could not finish. Please try again.';
}
export function isAbort(error: unknown) { return error instanceof DOMException && error.name === 'AbortError'; }

async function check(response: Response) {
  if (response.ok) return response;
  let problem: { errors?: Record<string, string[]> } = {};
  if (response.headers.get('Content-Type')?.includes('json')) {
    try { problem = await response.json(); } catch { /* Keep status-based recovery when the body is incomplete. */ }
  }
  const messages: Record<number, string> = {
    400: 'Check your answers and try again.', 401: 'Your session has expired. Please sign in or start again.',
    403: 'Request verification expired. Refresh this page before trying again.', 404: 'This information is no longer available.',
    409: 'Your taste or request changed. Refresh the saved information before trying again.',
    429: 'Please wait before trying again.', 503: 'New requests are temporarily paused. Saved discoveries are still available.',
  };
  throw new ApiError(response.status, messages[response.status] ?? 'That request could not finish. Please try again.', retryDeadline(response.headers.get('Retry-After')), problem.errors ?? {});
}

async function refresh(epoch: number) {
  if (refreshing) return refreshing;
  const saved = credentials;
  const operation = (async () => {
    if (!saved) return;
    const response = await fetch('/api/auth/refresh', { method: 'POST', cache: 'no-store', credentials: 'omit', signal: AbortSignal.timeout(30_000),
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ accessToken: saved.accessToken, refreshToken: saved.refreshToken }) });
    if (identity.epoch !== epoch) throw new DOMException('Session changed', 'AbortError');
    if (!response.ok) { signOut(); throw new ApiError(401, 'Please sign in again.'); }
    const next: AuthResponse = await response.json();
    if (identity.epoch !== epoch) throw new DOMException('Session changed', 'AbortError');
    if (!next.accessToken || !next.refreshToken) { signOut(); throw new ApiError(401, 'Please sign in again.'); }
    credentials = next;
  })();
  refreshing = operation;
  try { await operation; } finally { if (refreshing === operation) refreshing = null; }
}

export interface RequestOptions { method?: string; body?: unknown; signal?: AbortSignal; csrf?: string; }
export async function apiResponse(path: string, options: RequestOptions = {}) {
  if (!path.startsWith('/api/') || path.includes('://')) throw new Error('Invalid API address.');
  const epoch = identity.epoch, previousToken = credentials?.accessToken;
  const timeout = AbortSignal.timeout(30_000);
  const signal = options.signal ? AbortSignal.any([options.signal, timeout]) : timeout;
  const send = () => fetch(path, {
    method: options.method ?? 'GET', signal, credentials: 'same-origin', cache: 'no-store',
    headers: { Accept: 'application/json', ...(options.body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(credentials ? { Authorization: `Bearer ${credentials.accessToken}` } : {}), ...(options.csrf ? { 'X-CSRF-TOKEN': options.csrf } : {}) },
    ...(options.body === undefined ? {} : { body: JSON.stringify(options.body) }),
  });
  let response = await send();
  if (identity.epoch !== epoch) throw new DOMException('Session changed', 'AbortError');
  if (response.status === 401 && credentials) {
    if (credentials.accessToken === previousToken) await refresh(epoch);
    if (identity.epoch !== epoch) throw new DOMException('Session changed', 'AbortError');
    response = await send();
    if (identity.epoch !== epoch) throw new DOMException('Session changed', 'AbortError');
    if (response.status === 401) signOut();
  }
  return check(response);
}
export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const epoch = identity.epoch;
  const response = await apiResponse(path, options);
  if (response.status === 204) return undefined as T;
  if (!response.headers.get('Content-Type')?.includes('json')) throw new ApiError(502, 'The server returned an unreadable response. Please try again.');
  const body = await response.json();
  if (epoch !== identity.epoch) throw new DOMException('Session changed', 'AbortError');
  return body;
}
export async function authenticate(path: 'login' | 'register', body: unknown, csrf?: string) {
  const current = ++attempt, epoch = identity.epoch;
  const result = await api<AuthResponse>(`/api/auth/${path}`, { method: 'POST', body, csrf });
  if (current !== attempt || epoch !== identity.epoch) throw new DOMException('Session changed', 'AbortError');
  if (!result.accessToken || !result.refreshToken || !result.user?.id) throw new ApiError(502, 'The sign-in response was incomplete.');
  credentials = result;
  refreshing = null;
  identity = { epoch: identity.epoch + 1, user: result.user };
  listeners.forEach(notify => notify());
}
