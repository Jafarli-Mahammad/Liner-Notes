import { chromium } from '@playwright/test';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const project = fileURLToPath(new URL('../', import.meta.url));
const repository = resolve(project, '../../..');
const output = resolve(repository, 'design/superdesign/home-checkpoint');
const covers = process.env.LINER_REFERENCE_COVERS;
if (!covers) throw new Error('Set LINER_REFERENCE_COVERS to a temporary directory containing reference 0.jpg–3.jpg.');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.LINER_BROWSER_EXECUTABLE });
const origin = process.env.LINER_HOME_URL ?? 'http://localhost:5080';
const items = ['Heaven or Las Vegas', 'Violator', 'Bloom', 'Ambient 1: Music for Airports'].map((title, index) => ({
  sequence: index + 1, recordingId: `10000000-0000-4000-8000-00000000000${index}`, title,
  artistName: ['Cocteau Twins', 'Depeche Mode', 'Beach House', 'Brian Eno'][index],
  origin: 'Public', attribution: 'MusicBrainz · CC0-1.0',
}));

const measurements = [];
for (const width of [390, 768, 1440]) {
  const reference = await browser.newPage({ viewport: { width, height: 1000 }, deviceScaleFactor: 1 });
  await reference.goto(new URL('../../../../design/superdesign/phase-1/home.html', import.meta.url).href, { waitUntil: 'networkidle' });
  await reference.evaluate(() => document.fonts.ready);
  for (const img of await reference.locator('img').all()) await img.scrollIntoViewIfNeeded();
  await reference.evaluate(() => scrollTo(0, 0));
  await reference.screenshot({ path: resolve(output, `reference-${width}.png`), fullPage: true });
  const actual = await browser.newPage({ viewport: { width, height: 1000 }, deviceScaleFactor: 1 });
  await actual.route('**/api/discovery/recent?limit=4', route => route.fulfill({ json: { items, nextCursor: null } }));
  await actual.route('**/api/artwork/recordings/*', async route => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1);
    const index = items.findIndex(item => item.recordingId === id);
    await route.fulfill({ contentType: 'image/jpeg', body: await readFile(resolve(covers, `${index}.jpg`)) });
  });
  await actual.goto(origin, { waitUntil: 'networkidle' });
  await actual.evaluate(() => document.fonts.ready);
  for (const img of await actual.locator('.music-card img').all()) await img.scrollIntoViewIfNeeded();
  await actual.waitForFunction(() => document.querySelectorAll('.artwork-ready').length === 4);
  await actual.evaluate(() => scrollTo(0, 0));
  // Let pointer hover leave the scrolled cards before taking the static comparison.
  await actual.mouse.move(0, 0);
  await actual.waitForTimeout(450);
  await actual.screenshot({ path: resolve(output, `controlled-data-${width}.png`), fullPage: true });
  async function geometry(page, isReference) {
    return page.evaluate(ref => {
      const rect = selector => {
        const el = document.querySelector(selector); if (!el) return null;
        const r = el.getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height };
      };
      const heading = document.querySelector('h1'); const style = getComputedStyle(heading);
      return {
        logo: rect(ref ? '#nav-brand img' : '.brand img'), heading: rect('h1'),
        firstFrame: rect(ref ? 'main section:first-child .grid > div:first-child > div' : '.artwork-frame'),
        callout: rect(ref ? 'section[aria-label="Personal music discovery"]' : '.discovery-callout'),
        headingStyle: { fontFamily: style.fontFamily, size: style.fontSize, weight: style.fontWeight, tracking: style.letterSpacing },
        background: getComputedStyle(document.body).backgroundColor,
        overflow: document.documentElement.scrollWidth > innerWidth,
      };
    }, isReference);
  }
  measurements.push({ width, scope: 'Production build with explicitly intercepted reference-only data; not live recommendations',
    reference: await geometry(reference, true), implementation: await geometry(actual, false) });
  await reference.close(); await actual.close();
}
await writeFile(resolve(output, 'visual-comparison.json'), JSON.stringify(measurements, null, 2) + '\n');
await browser.close();
console.log('Saved reference and controlled-data screenshots plus geometry at 390/768/1440. Live screenshots are produced separately by the browser tests.');
