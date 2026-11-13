// Run with Playwright MCP against the disposable Synthetic host; never a personal account.
async (page) => {
  const base = 'http://localhost:5100', checks = [], sessions = [], batches = [];
  const browser = page.context().browser();
  const check = (ok, name) => { if (!ok) throw new Error(name); checks.push(name); };
  const call = (p, path, method = 'GET', body, csrf) => p.evaluate(async ({ path, method, body, csrf }) => {
    if (!window.__phase6Token && document.cookie.includes('liner.signed-in=')) {
      const restored = await fetch('/api/auth/session', { method: 'POST', headers: { 'X-Liner-Session': '1' } });
      if (restored.ok) window.__phase6Token = (await restored.json()).accessToken;
    }
    const response = await fetch(path, { method, headers: { 'X-Liner-Session': '1', ...(window.__phase6Token ? { Authorization: 'Bearer ' + window.__phase6Token } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}) }, body: body ? JSON.stringify(body) : undefined });
    const text = await response.text(), data = text ? JSON.parse(text) : null;
    if (data?.accessToken) { window.__phase6Token = data.accessToken; delete data.accessToken; delete data.refreshToken; }
    return { status: response.status, data };
  }, { path, method, body, csrf });
  const start = async () => {
    const context = await browser.newContext();
    await context.route('**/*', route => new URL(route.request().url()).origin === base ? route.continue() : route.abort());
    const p = await context.newPage(); await p.goto(base + '/taste');
    await p.getByRole('heading', { name: 'Your taste', exact: true }).waitFor();
    const s = { context, p, csrf: (await call(p, '/api/discovery/session')).data.csrfToken, revision: 0, account: false, cleared: false };
    sessions.push(s);
    check((await call(p, '/api/discovery/session', 'POST', undefined, s.csrf)).status === 200, 'Controlled guest created');
    return s;
  };
  const refresh = async s => { s.csrf = (await call(s.p, '/api/discovery/session')).data.csrfToken; s.revision = (await call(s.p, '/api/discovery')).data.page?.revision ?? 0; };
  const save = async (s, ids, tags, exploration = 'balance') => {
    const result = await call(s.p, '/api/discovery/onboarding', 'PUT', { revision: s.revision, selection: { catalogRecordIds: ids, tags }, answers: { version: 'taste-onboarding-v1', step: 'complete', goal: 'mixture', exploration, vocals: 'no-preference', allStyles: false } }, s.csrf);
    check(result.status === 200, 'Taste save accepted: ' + result.status); s.revision = result.data.revision;
  };
  const generate = async s => {
    await s.p.waitForTimeout(1100);
    const begun = Date.now();
    const accepted = await call(s.p, '/api/discovery/batches', 'POST', { revision: s.revision, requestKey: crypto.randomUUID() }, s.csrf);
    check(accepted.status === 202, 'Generation accepted: ' + accepted.status);
    let job = accepted.data; const deadline = Date.now() + 55000;
    while (['Pending', 'Running', 'Retryable'].includes(job.state) && Date.now() < deadline) { await s.p.waitForTimeout(500); job = (await call(s.p, '/api/discovery/jobs/' + job.id)).data; }
    check(job.state === 'Completed', 'Generation completed: ' + job.state + '/' + job.reason);
    const batch = (await call(s.p, '/api/discovery/batches/' + job.batchId)).data;
    batches.push({ elapsedMs: Date.now() - begun, picks: batch.picks.length, batch }); return batch;
  };
  const register = async (s, preserve) => {
    const registered = await call(s.p, '/api/auth/register', 'POST', { userName: 'phase6' + crypto.randomUUID().replaceAll('-', '').slice(0, 12), email: 'phase6+' + crypto.randomUUID() + '@example.invalid', password: 'Qa!' + crypto.randomUUID() + 'x7', timeZone: 'UTC', deliveryDay: 0, deliveryHourUtc: 8, preserveGuestProgress: preserve, weeklyEmailOptIn: false }, s.csrf);
    check(registered.status === 201, 'Controlled account registered: ' + registered.status);
    s.account = true; await refresh(s);
  };
  const exported = async s => { const r = await call(s.p, '/api/export/my-data'); check(r.status === 200, 'Account export readable'); return r.data; };
  const feedback = async (s, id, value, comment) => {
    const r = await call(s.p, '/api/discovery/recommendations/' + id + '/feedback', 'POST', { revision: s.revision, feedback: value, rating: null, comment }, s.csrf);
    check(r.status === 200, 'Feedback accepted: ' + r.status); await refresh(s);
  };
  try {
    const a = await start(), b = await start(), c = await start();
    check((await call(a.p, '/api/discovery')).data.evidenceOrigin === 'Synthetic', 'Host explicitly labels fixture evidence');
    const catalog = (await call(a.p, '/api/catalog/search?query=Synthetic&kind=Track&limit=10&offset=0')).data.items;
    check(catalog.length > 0, 'Canonical fixture catalog search'); const favorite = catalog[0].id;
    await save(a, [favorite], ['electronic', 'ambient']); const first = await generate(a);
    check(first.picks.length > 0 && first.picks.length < 5, 'Favorite exclusion leaves supported short batch');
    await register(a, true);
    check((await call(a.p, '/api/discovery/batches/' + first.id)).status === 200, 'Guest history survives account claim');
    check(!(await a.context.cookies()).some(cookie => cookie.name === 'liner.guest'), 'Claim revokes guest cookie');
    await register(b, false); await save(b, [], ['instrumental']); const other = await generate(b);
    await save(c, [], ['classical']); const empty = await generate(c);
    check(empty.picks.length === 0, 'Unconnected pool abstains');
    const beforeB = JSON.stringify((await exported(b)).discovery);
    check((await call(b.p, '/api/discovery/batches/' + first.id)).status === 404, 'Foreign batch denied');
    const pick = first.picks[0]; const revision = a.revision;
    await feedback(a, pick.id, 'Liked', 'controlled note');
    check(a.revision > revision, 'Musical feedback advances revision'); const likedRevision = a.revision;
    await feedback(a, pick.id, 'Liked', 'controlled note changed');
    check(a.revision === likedRevision, 'Note edit preserves musical revision');
    await feedback(a, pick.id, 'None', null); check(a.revision > likedRevision, 'Feedback reversal advances revision');
    await feedback(a, pick.id, 'AlreadyKnown', null);
    const repeated = await generate(a); check(repeated.picks.length === 0, 'History does not repeat prior picks');
    await save(a, [], ['electronic', 'ambient']);
    check((await call(a.p, '/api/discovery')).data.page.favorites.length === 0, 'Favorite removal persists');
    const removed = await generate(a); check(removed.picks.length === 1, 'Removed favorite becomes eligible while history stays excluded');
    check(JSON.stringify((await exported(b)).discovery) === beforeB, 'Other account remains unchanged');
    const guestCookie = (await c.context.cookies()).find(cookie => cookie.name === 'liner.guest');
    check(!!guestCookie, 'Guest credential exists only in browser'); await b.context.addCookies([guestCookie]);
    check(JSON.stringify((await exported(b)).discovery) === beforeB, 'Authenticated identity wins over guest cookie');
    await a.p.setViewportSize({ width: 390, height: 844 }); await a.p.emulateMedia({ reducedMotion: 'reduce' });
    await a.p.goto(base + '/discover'); await a.p.getByText('Showing 1 of 5 requested picks.', { exact: false }).waitFor();
    check(await a.p.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Narrow reduced-motion shortfall view fits');
    const data = await exported(a);
    check(data.discovery.recommendations.every(r => r.scoreBreakdown.snapshot?.evidence?.selection || r.scoreBreakdown.Snapshot?.Evidence?.Selection), 'Export contains stored selection receipts');
    // Keep browser credentials in this context only. The driver restarts only its own QA host before resuming.
    for (const [role, s] of [['a', a], ['b', b], ['c', c]]) await s.p.evaluate(state => sessionStorage.setItem('phase6', JSON.stringify(state)), { role, revision: s.revision, batch: role === 'a' ? removed.id : role === 'b' ? other.id : empty.id });
    return { version: 'residue-phase6-browser-v1', checks, batches: batches.map(b => ({ elapsedMs: b.elapsedMs, picks: b.picks })), receiptRecommendations: data.discovery.recommendations.map(r => ({ id: r.id, scoreBreakdown: r.scoreBreakdown })) };
  } catch (error) {
    for (const s of sessions) { try { await call(s.p, s.account ? '/api/subscribers/me' : '/api/discovery/session', 'DELETE', undefined, s.csrf); } catch {} await s.context.close(); }
    throw error;
  }
}
