// Run through Playwright MCP against the disposable Synthetic host only.
async (page) => {
  const base = 'http://localhost:5098', checks = [], sessions = [];
  const browser = page.context().browser();
  const check = (condition, name) => { if (!condition) throw new Error(name); checks.push(name); };
  const call = (p, path, method = 'GET', body, csrf) => p.evaluate(async ({ path, method, body, csrf }) => {
    const r = await fetch(path, { method, headers: { ...(body ? { 'Content-Type': 'application/json' } : {}), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}) }, body: body ? JSON.stringify(body) : undefined });
    const text = await r.text(); return { status: r.status, data: text ? JSON.parse(text) : null };
  }, { path, method, body, csrf });
  const start = async () => {
    const context = await browser.newContext();
    await context.route('**/*', r => new URL(r.request().url()).origin === base ? r.continue() : r.abort());
    const p = await context.newPage(); await p.goto(base + '/taste');
    await p.getByRole('heading', { name: 'Your taste', exact: true }).waitFor();
    const csrf = (await call(p, '/api/discovery/session')).data.csrfToken;
    check((await call(p, '/api/discovery/session', 'POST', undefined, csrf)).status === 200, 'Isolated guest created');
    const s = { context, p, csrf, revision: 0, cleared: false }; sessions.push(s); return s;
  };
  const save = async (s, tag, exploration) => {
    const result = await call(s.p, '/api/discovery/onboarding', 'PUT', { revision: s.revision,
      selection: { catalogRecordIds: [], tags: [tag] }, answers: { version: 'taste-onboarding-v1', step: 'complete',
        goal: 'mixture', exploration, vocals: 'no-preference', allStyles: false } }, s.csrf);
    check(result.status === 200, 'Preferences saved'); s.revision = result.data.revision;
  };
  const generate = async s => {
    const accepted = await call(s.p, '/api/discovery/batches', 'POST', { revision: s.revision, requestKey: crypto.randomUUID() }, s.csrf);
    check(accepted.status === 202, 'Generation accepted'); let job = accepted.data;
    const until = Date.now() + 55000;
    while (['Pending', 'Running', 'Retryable'].includes(job.state) && Date.now() < until) {
      await s.p.waitForTimeout(500); job = (await call(s.p, '/api/discovery/jobs/' + job.id)).data;
    }
    check(job.state === 'Completed', 'Bounded generation completed: ' + job.state);
    const result = await call(s.p, '/api/discovery/batches/' + job.batchId);
    check(result.status === 200, 'Owned batch readable'); return result.data;
  };
  const exportData = async s => (await call(s.p, '/api/discovery/session/data')).data.discovery;
  try {
    const a = await start(), b = await start();
    check((await call(a.p, '/api/discovery')).data.evidenceOrigin === 'Synthetic', 'Explicit fixture host; no live-provider or founder data');
    await save(a, 'electronic', 'close'); await save(b, 'classical', 'balance');
    const close = await generate(a), sparse = await generate(b);
    check(close.picks.length === 3, 'Close acquisition cap yields three supported candidates');
    check(close.coverage.Selection.Shortfall === 2, 'Shortfall is stored');
    check(close.coverage.Acquisition.every(r => r.Limit === 3), 'Effective public acquisition limit is recorded');
    check(sparse.picks.length === 0, 'Unconnected candidates do not pad an empty batch');
    check(sparse.coverage.Selection.Decisions.every(d => d.Reason === 'insufficient_connection'), 'Abstention reasons persist for the whole pool');
    const beforeB = await exportData(b);
    await a.p.goto(base + '/discover');
    await a.p.getByText('Showing 3 of 5 requested picks.', { exact: false }).waitFor();
    check(await a.p.getByRole('button', { name: /Find more recommendations/ }).count() === 1, 'Short batch keeps a regeneration action');
    await b.p.goto(base + '/discover');
    await b.p.getByRole('heading', { name: 'No picks in this batch', exact: true }).waitFor();
    check(await b.p.getByRole('button', { name: /Find more recommendations/ }).count() === 1, 'Empty batch keeps a regeneration action');
    await save(a, 'electronic', 'surprise'); const expanded = await generate(a);
    check(expanded.coverage.Acquisition.every(r => r.Limit === 10), 'Surprise changes acquisition under the same provider cap');
    check(expanded.picks.length === 2, 'History suppresses the original three while the wider pool supplies two new picks');
    check(expanded.coverage.Selection.Decisions.filter(d => d.Reason === 'excluded').length === 3, 'Selection receipt reconstructs history suppression');
    check(expanded.picks.every(p => !close.picks.some(old => old.mbid === p.mbid)), 'Earlier recordings are not repeated');
    check(JSON.stringify(beforeB) === JSON.stringify(await exportData(b)), 'Changing one listener leaves the other listener unchanged');
    check((await call(b.p, '/api/discovery/batches/' + expanded.id)).status === 404, 'Foreign selection receipt denied');
    await a.p.setViewportSize({ width: 390, height: 844 }); await a.p.emulateMedia({ reducedMotion: 'reduce' });
    await a.p.goto(base + '/discover'); await a.p.getByText('Showing 2 of 5 requested picks.', { exact: false }).waitFor();
    check(await a.p.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Shortfall and explanations fit a narrow reduced-motion view');
    const data = await exportData(a);
    check(data.recommendations.every(r => r.scoreBreakdown.snapshot?.evidence?.selection || r.scoreBreakdown.Snapshot?.Evidence?.Selection), 'Export carries full selection receipts');
    for (const s of sessions) { check((await call(s.p, '/api/discovery/session', 'DELETE', undefined, s.csrf)).status === 204, 'Disposable guest deletion succeeds'); s.cleared = true; }
    return { version: 'residue-phase5-browser-v1', checks, counts: { close: close.picks.length, sparse: sparse.picks.length, expanded: expanded.picks.length },
      picks: data.recommendations.map(r => ({ id: r.id, scoreBreakdown: r.scoreBreakdown })), limitation: 'Synthetic application correctness; subjective enjoyment and production provider coverage remain unverified.' };
  } finally {
    for (const s of sessions) { if (!s.cleared) await call(s.p, '/api/discovery/session', 'DELETE', undefined, s.csrf); await s.context.close(); }
  }
}
