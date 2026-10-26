// Deterministic HTTP/timer regression checks for the actual discovery client; no network.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../src/Presentation/wwwroot/discovery.js'), 'utf8');
const started = Date.parse('2026-10-24T12:00:00Z');

async function client(status, retryAfter) {
  let now = started, sequence = 0;
  const timers = new Map(), polls = [], elements = new Map();
  class Clock extends Date { static now() { return now; } }
  const state = { activeTab: 'discovery' };
  const sandbox = {
    window: {}, state, API_BASE: '/api', escapeHtml: String, Date: Clock,
    document: {
      addEventListener() {}, querySelector: () => null,
      getElementById(id) {
        if (!elements.has(id)) elements.set(id, { setAttribute() {}, replaceChildren() {}, classList: { toggle() {} } });
        return elements.get(id);
      }
    },
    setTimeout(fn, delay) { const id = ++sequence; timers.set(id, { fn, at: now + delay }); return id; },
    clearTimeout: id => timers.delete(id), clearInterval() {},
    async apiFetch(url) {
      if (url.endsWith('/session')) return Response.json({ profile: { id: 'listener', isGuest: true } });
      if (url.endsWith('/discovery')) return Response.json({ page: { batches: [], currentJob: { id: 'job', state: 'Running' } }, styles: [] });
      assert.ok(url.endsWith('/jobs/job'));
      polls.push(now - started);
      if (polls.length === 1 && status !== 200)
        return new Response('', { status, headers: retryAfter === null ? {} : { 'Retry-After': retryAfter } });
      return Response.json({ state: 'Running' });
    }
  };
  vm.runInNewContext(source, sandbox);
  await sandbox.window.discoveryApp.renderDiscovery();
  await new Promise(setImmediate);
  return {
    polls, state, app: sandbox.window.discoveryApp,
    async advance(milliseconds) {
      const end = now + milliseconds;
      while (true) {
        const next = [...timers].sort((a, b) => a[1].at - b[1].at)[0];
        if (!next || next[1].at > end) break;
        const [id, timer] = next; timers.delete(id); now = timer.at;
        await timer.fn(); await new Promise(setImmediate);
      }
      now = end;
    }
  };
}

(async () => {
  for (const [status, header, expected] of [
    [429, '60', 60000], [503, new Date(started + 60000).toUTCString(), 60000],
    [429, null, 5000], [503, 'invalid', 5000], [429, '-10', 5000],
    [503, 'Infinity', 5000], [503, new Date(started - 1000).toUTCString(), 1000],
    [200, null, 2000], [503, '3000000', 3000000000]
  ]) {
    const test = await client(status, header);
    await test.advance(expected - 1);
    assert.deepEqual(test.polls, [0], `${status}/${header}: premature retry`);
    await test.advance(1);
    assert.deepEqual(test.polls, [0, expected], `${status}/${header}: missing retry`);
    await test.advance(2000);
    assert.deepEqual(test.polls, [0, expected, expected + 2000], 'successful polling resumes at two seconds');
    test.app.leave(); await test.advance(60000);
    assert.equal(test.polls.length, 3, 'leaving cancels polling');
  }
  const background = await client(429, '60');
  background.state.activeTab = 'taste'; await background.advance(60000);
  assert.deepEqual(background.polls, [0], 'a background tab must not resume polling');
  console.log('PASS discovery polling: Retry-After seconds/date, fallback, long delays, normal cadence and cancellation');
})().catch(error => { console.error(error); process.exitCode = 1; });
