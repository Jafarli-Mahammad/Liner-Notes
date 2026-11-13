// Execute this function through Playwright MCP browser_run_code_unsafe against the existing loopback beta.
// Creates only disposable guest taste profiles, never requests provider acquisition, and clears guests in finally.
async (page) => {
  const browser = page.context().browser();
  const contexts = [];
  const sessions = [];
  const checks = [];
  const request = (p, path, method = 'GET', body, csrf) => p.evaluate(async ({ path, method, body, csrf }) => {
    const start = performance.now();
    const r = await fetch(path, { method, headers: { ...(body ? { 'Content-Type': 'application/json' } : {}), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}) }, body: body ? JSON.stringify(body) : undefined });
    const text = await r.text();
    let data; try { data = text ? JSON.parse(text) : null; } catch { data = null; }
    return { status: r.status, data, noStore: r.headers.get('cache-control')?.includes('no-store') ?? false, ms: performance.now() - start };
  }, { path, method, body, csrf });
  const expect = (value, name) => { if (!value) throw new Error(name); checks.push(name); };
  try {
    for (const tag of ['jazz', 'rock', 'classical']) {
      const context = await browser.newContext(); contexts.push(context);
      const p = await context.newPage(); await p.goto('http://localhost:5086/taste');
      await p.getByRole('heading', { name: 'Your taste', exact: true }).waitFor();
      const boot = await request(p, '/api/discovery/session');
      expect(boot.status === 200 && boot.data.profile === null, `${tag}: isolated initial session`);
      const csrf = boot.data.csrfToken;
      const started = await request(p, '/api/discovery/session', 'POST', undefined, csrf);
      expect(started.status === 200, `${tag}: guest creation (status ${started.status})`);
      sessions.push({ p, csrf });
      const saved = await request(p, '/api/discovery/onboarding', 'PUT', {
        revision: 0, selection: { catalogRecordIds: [], tags: [tag] },
        answers: { version: 'taste-onboarding-v1', step: 'complete', goal: 'mixture', exploration: 'balance', vocals: 'no-preference', allStyles: false }
      }, csrf);
      expect(saved.status === 200, `${tag}: save own taste`);
    }
    const [a, b, c] = sessions;
    const read = s => request(s.p, '/api/discovery/session/data');
    const before = await read(b);
    expect(before.status === 200 && before.noStore, 'B export is private and readable');
    const foreignId = before.data.discovery.profile.id;
    const failedSave = await request(a.p, '/api/discovery/onboarding', 'PUT', { revision: 1,
      profileId: foreignId, selection: { catalogRecordIds: [], tags: ['ambient'] },
      answers: { version: 'taste-onboarding-v1', step: 'complete', goal: 'different-styles', exploration: 'surprise', vocals: 'instrumental', allStyles: true }
    }, a.csrf);
    expect(failedSave.status === 200, 'A save ignores an untrusted foreign profile field');
    const responses = await Promise.all([read(a), read(b), read(c)]);
    // Full private export should be stable except listener activity timestamps.
    const clean = x => JSON.parse(JSON.stringify(x, (k,v) => ['lastActiveAtUtc','expiresAtUtc'].includes(k) ? undefined : v));
    expect(JSON.stringify(clean(before.data)) === JSON.stringify(clean(responses[1].data)), 'A preference change leaves B export unchanged');
    expect(!JSON.stringify(responses[1].data).includes('ambient'), 'A tag absent from B private export');
    expect(!JSON.stringify(responses[2].data).includes('ambient'), 'A tag absent from C private export');
    await b.p.reload();
    await b.p.getByRole('heading', { name: 'Your taste', exact: true }).waitFor();
    expect((await b.p.locator('h1').allTextContents()).some(t => /taste/i.test(t)), 'B taste UI renders after other-user mutation');
    const owned = await request(b.p, '/api/discovery');
    expect(owned.status === 200 && owned.noStore, 'B overview is private');
    const foreignJob = await request(a.p, '/api/discovery/jobs/00000000-0000-0000-0000-000000000001');
    expect(foreignJob.status === 404, 'Unknown job is not exposed');
    return { version: 'residue-browser-isolation-v1', checks, exportReadMs: responses.map(r => r.ms),
      scope: 'Three disposable guests on running beta; real persistence and UI, no new music acquisition or subjective quality proof.' };
  } finally {
    const cleanup = [];
    for (const s of sessions) cleanup.push((await request(s.p, '/api/discovery/session', 'DELETE', undefined, s.csrf)).status);
    for (const context of contexts) await context.close();
    if (cleanup.some(status => status !== 204)) throw new Error(`Guest cleanup incomplete: ${cleanup.join(',')}`);
  }
}
