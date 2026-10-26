import { chromium } from '@playwright/test';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import { gzipSync } from 'node:zlib';

const browser = await chromium.launch({ executablePath: process.env.LINER_BROWSER_EXECUTABLE });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
await page.addInitScript(() => {
  window.homeMetrics = { cls: 0, lcp: 0, longTasks: 0 };
  new PerformanceObserver(list => { for (const e of list.getEntries()) if (!e.hadRecentInput) window.homeMetrics.cls += e.value; }).observe({ type: 'layout-shift', buffered: true });
  new PerformanceObserver(list => { for (const e of list.getEntries()) window.homeMetrics.lcp = e.startTime; }).observe({ type: 'largest-contentful-paint', buffered: true });
  new PerformanceObserver(list => { window.homeMetrics.longTasks += list.getEntries().length; }).observe({ type: 'longtask', buffered: true });
});
const requests = [];
page.on('request', req => requests.push({ url: req.url(), method: req.method() }));
const response = await page.goto(process.env.LINER_HOME_URL ?? 'http://localhost:5080', { waitUntil: 'networkidle' });
await page.evaluate(() => document.fonts.ready);
await page.waitForTimeout(500);
const assets = [];
for (const file of await readdir('dist/home-assets')) {
  if (!/\.(js|css)$/.test(file)) continue;
  const bytes = await readFile(`dist/home-assets/${file}`);
  assets.push({ file, bytes: bytes.length, gzipBytes: gzipSync(bytes).length });
}
const result = { scope: 'Single cold local Chromium navigation, actual public API, no CPU/network throttling; not a production benchmark',
  browser: browser.version(), viewport: '1440×1000', responseStatus: response.status(), headers: response.headers(), assets, requests,
  measurements: await page.evaluate(() => ({ ...window.homeMetrics,
    paints: performance.getEntriesByType('paint').map(p => ({ name: p.name, milliseconds: p.startTime })),
    resources: performance.getEntriesByType('resource').map(r => ({ url: r.name, milliseconds: r.duration, transferSize: r.transferSize })),
  })),
};
await writeFile('../../../design/superdesign/frontend-review/performance.json', JSON.stringify(result, null, 2) + '\n');
await browser.close();
console.log(JSON.stringify({ browser: result.browser, measurements: result.measurements, assets }));
