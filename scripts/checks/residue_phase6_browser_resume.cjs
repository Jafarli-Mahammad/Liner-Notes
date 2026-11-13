// Resume the same browser contexts after restarting the disposable host with its existing keys.
async (page) => {
  const checks = [], browser = page.context().browser();
  const check = (ok, name) => { if (!ok) throw new Error(name); checks.push(name); };
  const call = (p, path, method = 'GET', csrf) => p.evaluate(async ({ path, method, csrf }) => {
    const r = await fetch(path, { method, headers: { 'X-Liner-Session': '1', ...(window.__phase6Token ? { Authorization: 'Bearer ' + window.__phase6Token } : {}), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}) } });
    const text = await r.text(), data = text ? JSON.parse(text) : null;
    if (data?.accessToken) { window.__phase6Token = data.accessToken; delete data.accessToken; delete data.refreshToken; }
    return { status: r.status, data };
  }, { path, method, csrf });
  const sessions = [];
  for (const context of browser.contexts()) for (const p of context.pages()) {
    const state = await p.evaluate(() => JSON.parse(sessionStorage.getItem('phase6') || 'null')).catch(() => null);
    if (state) sessions.push({ p, context, ...state });
  }
  check(sessions.length === 3, 'Three controlled contexts survived host restart');
  for (const s of sessions) {
    if (s.role !== 'c') check((await call(s.p, '/api/auth/session', 'POST')).status === 200, 'Protected session restored after host restart');
    const overview = await call(s.p, '/api/discovery');
    check(overview.status === 200 && overview.data.page.revision === s.revision, 'Persisted taste revision restored');
    check((await call(s.p, '/api/discovery/batches/' + s.batch)).status === 200, 'Persisted batch restored');
    if (s.role !== 'c') {
      const exportData = await call(s.p, '/api/export/my-data');
      check(exportData.status === 200 && exportData.data.discovery.batches.length > 0, 'Account export survives host restart');
      const deleted = await call(s.p, '/api/subscribers/me', 'DELETE');
      check(deleted.status === 204, 'Controlled account deleted: ' + deleted.status);
      check((await call(s.p, '/api/auth/me')).status === 401, 'Deleted account cannot authenticate');
    } else {
      const csrf = (await call(s.p, '/api/discovery/session')).data.csrfToken;
      check((await call(s.p, '/api/discovery/session', 'DELETE', csrf)).status === 204, 'Controlled guest cleared');
    }
    await s.context.close();
  }
  return { version: 'residue-phase6-restart-v1', checks };
}
