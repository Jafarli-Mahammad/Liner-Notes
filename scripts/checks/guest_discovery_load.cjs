// Synthetic loopback fixture only. Uses Node's standard library; no application dependency.
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const base = new URL(process.env.LINER_GUEST_DEMO_URL || 'http://127.0.0.1:5092');
const output = process.env.LINER_GUEST_LOAD_OUTPUT || '/tmp/liner-guest-load';
const marker = process.env.LINER_GUEST_LOAD_RESTART_MARKER;
assert.ok(process.argv.includes('--approve-synthetic'));
assert.ok(['localhost', '127.0.0.1', '[::1]'].includes(base.hostname));
assert.equal(base.protocol, 'http:');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const percentile = (values, fraction) => [...values].sort((a, b) => a - b)[Math.ceil(values.length * fraction) - 1];
class Session {
  cookies = new Map();
  csrf = null;
  async request(route, method = 'GET', body) {
    const headers = { Origin: base.origin, Cookie: [...this.cookies].map(([k, v]) => `${k}=${v}`).join('; ') };
    if (method !== 'GET') { headers['X-CSRF-TOKEN'] = this.csrf; headers['Content-Type'] = 'application/json'; }
    const response = await fetch(new URL(route, base), { method, headers,
      body: body === undefined ? undefined : JSON.stringify(body), redirect: 'error', signal: AbortSignal.timeout(20_000) });
    for (const cookie of response.headers.getSetCookie()) {
      const pair = cookie.split(';', 1)[0]; const separator = pair.indexOf('=');
      this.cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
    }
    assert.ok(response.ok, `${method} ${route.split('?')[0]} returned ${response.status}`);
    return response.status === 204 ? null : response.json();
  }
}
async function searches(concurrency, iterations) {
  const samples = [];
  const queries = ['synthetic', 'synthetic artist 0', 'synthtic', 'synthetic atlas'];
  await Promise.all(Array.from({ length: concurrency }, async (_, worker) => {
    const client = new Session();
    for (let i = 0; i < iterations; i++) {
      const start = performance.now();
      const result = await client.request(`/api/catalog/search?kind=Artist&query=${encodeURIComponent(queries[(worker + i) % queries.length])}`);
      assert.ok(Array.isArray(result.items));
      samples.push(performance.now() - start);
    }
  }));
  return { concurrency, requests: samples.length, p50Ms: percentile(samples, .5), p95Ms: percentile(samples, .95),
    p99Ms: percentile(samples, .99), maxMs: Math.max(...samples) };
}
async function main() {
  await fs.mkdir(output, { recursive: true, mode: 0o700 });
  const metadata = await new Session().request('/api/discovery');
  assert.equal(metadata.evidenceOrigin, 'Synthetic', 'Load checks must never acquire live recommendations.');
  const report = { evidence: 'Synthetic', search: [], generation: [], restart: Boolean(marker) };
  const clients = [], owners = [];
  try {
    for (let i = 0; i < 50; i++) {
      const client = new Session();
      client.csrf = (await client.request('/api/discovery/session')).csrfToken;
      await client.request('/api/discovery/session', 'POST'); owners.push(client);
      if (i < 5) clients.push(client);
    }
    report.persistedProfiles = owners.length;
    await searches(1, 4);
    report.search.push(await searches(5, 20));
    report.search.push(await searches(20, 10));
    report.search.push(await searches(50, 10));
    console.log('PASS local search at 5, 20 and 50 concurrent clients with 50 persisted profiles');
    for (let i = 0; i < clients.length; i++) {
      const client = clients[i];
      const selection = { catalogRecordIds: [], tags: ['jazz', 'rock', 'ambient', 'electronic', 'indie'].slice(i, i + 1) };
      const body = { revision: 0, requestKey: randomUUID(), selection };
      const start = performance.now();
      const job = await client.request('/api/discovery/batches', 'POST', body);
      assert.equal((await client.request('/api/discovery/batches', 'POST', body)).id, job.id);
      client.job = job; client.started = start;
    }
    if (marker) {
      const statuses = await Promise.all(clients.map(c => c.request(`/api/discovery/jobs/${c.job.id}`)));
      report.unfinishedBeforeRestart = statuses.filter(s => ['Pending', 'Running', 'Retryable'].includes(s.state)).length;
      assert.ok(report.unfinishedBeforeRestart > 0, 'Restart check needs unfinished durable work.');
      await fs.writeFile(marker, JSON.stringify({ unfinished: report.unfinishedBeforeRestart }));
      const deadline = performance.now() + 45_000;
      while (true) {
        try { await fs.access(marker + '.resumed'); break; } catch { /* Host restarts only its disposable container. */ }
        assert.ok(performance.now() < deadline, 'Host did not resume the restart check.');
        await pause(100);
      }
    }
    await Promise.all(clients.map(async client => {
      const deadline = performance.now() + 180_000;
      while (true) {
        const job = await client.request(`/api/discovery/jobs/${client.job.id}`);
        if (job.state === 'Completed') {
          assert.ok(job.batchId);
          await client.request(`/api/discovery/batches/${job.batchId}`);
          report.generation.push({ elapsedMs: performance.now() - client.started, state: job.state });
          break;
        }
        assert.ok(!['Failed', 'Cancelled'].includes(job.state), `Synthetic job ended ${job.state}: ${job.errorCode}`);
        assert.ok(performance.now() < deadline, 'Synthetic generation timed out.');
        await pause(2000);
      }
      // The saved overview must remain readable after completion and restart.
      const overview = await client.request('/api/discovery');
      assert.ok(overview.page);
    }));
    console.log('PASS five durable generation requests, idempotent replay and result retrieval');
  } finally {
    for (const client of owners) await client.request('/api/discovery/session', 'DELETE');
  }
  report.privateGuestsDeleted = owners.length;
  await fs.writeFile(path.join(output, 'load-results.json'), JSON.stringify(report, null, 2));
  console.log('PASS private load-fixture guest cleanup');
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
