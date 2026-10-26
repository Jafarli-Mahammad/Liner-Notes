import type { Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import type { Batch, Overview, Session } from '../../src/api/models';

export const batchId = '20000000-0000-4000-8000-000000000001';
export const session: Session = { csrfToken: 'test-csrf', profile: { id: '30000000-0000-4000-8000-000000000001', userId: null, revision: 1, isGuest: true, expiresAtUtc: '2026-11-03T00:00:00Z' }, guestImportAvailable: false };
export const batch: Batch = { id: batchId, kind: 'OnDemand', createdAtUtc: '2026-10-24T10:00:00Z', coverage: null, picks: [
  ['Heaven or Las Vegas', 'Cocteau Twins', 'Dream pop'], ['Violator', 'Depeche Mode', 'Synth-pop'], ['Bloom', 'Beach House', 'Dream pop'], ['Ambient 1: Music for Airports', 'Brian Eno', 'Ambient'],
].map(([title, artistName, tag], i) => ({ id: `40000000-0000-4000-8000-00000000000${i}`, title, artistName, album: null, rank: i + 1,
  explanation: ['A stored connection between your dream-pop favorites and atmospheric guitars.', 'A connection to your saved synth-pop taste.', 'Shared dream-pop styles from your saved favorites.', 'A connection to the ambient styles you selected.'][i],
  matchedTags: [tag], feedback: 'None' as const, rating: null, comment: null, mbid: `10000000-0000-4000-8000-00000000000${i}`, spotifyUrl: null, youtubeUrl: null, sources: ['ListenBrainz'] })) };
export const overview: Overview = { page: { revision: 1,
  favorites: [{ catalogRecordId: '50000000-0000-4000-8000-000000000001', canonicalId: '60000000-0000-4000-8000-000000000001', name: 'Depeche Mode', kind: 'Artist' },
    { catalogRecordId: '50000000-0000-4000-8000-000000000002', name: 'Heaven or Las Vegas', artistName: 'Cocteau Twins', kind: 'Album' },
    { catalogRecordId: '50000000-0000-4000-8000-000000000003', name: 'Beach House', kind: 'Artist' }],
  tags: ['dream pop', 'ambient', 'late night', 'atmospheric'], batches: [{ id: batchId, kind: 'OnDemand', createdAtUtc: batch.createdAtUtc, completedAtUtc: batch.createdAtUtc }], currentJob: null, nextRequestAtUtc: null,
  onboarding: { version: 'taste-onboarding-v1', step: 'complete', goal: null, exploration: 'balance', vocals: 'no-preference', allStyles: false } }, styles: ['ambient'], cooldownSeconds: 30, evidenceOrigin: 'Live' };

export async function personalFixture(page: Page, options: { empty?: boolean } = {}) {
  const saved = structuredClone(overview), picks = structuredClone(batch);
  if (options.empty && saved.page) { saved.page.batches = []; saved.page.favorites = []; saved.page.tags = []; }
  await page.route('**/api/discovery/session', route => route.fulfill({ json: session }));
  await page.route('**/api/discovery', route => route.fulfill({ json: saved }));
  await page.route('**/api/discovery/batches/*', route => route.fulfill({ json: picks }));
  await page.route('**/api/discovery/onboarding/tags?*', route => route.fulfill({ json: { version: 'taste-vocabulary-v1', items: [
    { id: 'shoegaze', label: 'Shoegaze', category: 'genre', aliases: [] }, { id: 'synthpop', label: 'Synth-pop', category: 'genre', aliases: [] },
    { id: 'melancholic', label: 'Melancholic', category: 'mood', aliases: [] }, { id: 'post punk', label: 'Post-punk', category: 'genre', aliases: [] },
  ], hasMore: true } }));
  await page.route('**/api/discovery/onboarding', async route => {
    const body = route.request().postDataJSON();
    if (saved.page) { saved.page.revision++; saved.page.onboarding = body.answers; saved.page.tags = body.selection.tags; saved.page.favorites = saved.page.favorites.filter(f => body.selection.catalogRecordIds.includes(f.catalogRecordId) || body.selection.legacySignalIds.includes(f.signalId)); }
    await route.fulfill({ json: { revision: saved.page?.revision ?? 2 } });
  });
  await page.route('**/api/discovery/recommendations/*/feedback', async route => {
    const body = route.request().postDataJSON();
    const pick = picks.picks.find(p => route.request().url().includes(p.id));
    if (pick) Object.assign(pick, body);
    if (saved.page) saved.page.revision++;
    await route.fulfill({ json: { revision: saved.page?.revision ?? 2 } });
  });
  await page.route('**/api/artwork/recordings/*', async route => {
    const index = batch.picks.findIndex(p => route.request().url().includes(p.mbid!));
    const directory = process.env.LINER_REFERENCE_COVERS;
    if (directory && index >= 0) await route.fulfill({ contentType: 'image/jpeg', body: await readFile(`${directory}/${index}.jpg`) });
    else await route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="500" height="500"><rect width="500" height="500" fill="#DCCFFD"/></svg>' });
  });
  return { overview: saved, batch: picks };
}
