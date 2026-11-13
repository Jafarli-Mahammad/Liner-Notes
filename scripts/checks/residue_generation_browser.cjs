// Bounded current-provider sequential-request probe via Playwright MCP. Two disposable guests, one tag, no retries.
async (page) => {
  const contexts = [], sessions = [], results = [];
  const browser = page.context().browser();
  const call = (p, path, method = 'GET', body, csrf) => p.evaluate(async ({path,method,body,csrf}) => {
    const r = await fetch(path, {method,headers:{...(body?{'Content-Type':'application/json'}:{}),...(csrf?{'X-CSRF-TOKEN':csrf}:{})},body:body?JSON.stringify(body):undefined});
    const text=await r.text(); let data; try { data=text?JSON.parse(text):null; } catch { data=null; }
    return {status:r.status,data};
  },{path,method,body,csrf});
  try {
    for (const phase of ['first-request','second-request']) {
      const context=await browser.newContext(); contexts.push(context);
      const p=await context.newPage(); await p.goto('http://localhost:5086/taste');
      await p.getByRole('heading',{name:'Your taste',exact:true}).waitFor();
      const csrf=(await call(p,'/api/discovery/session')).data.csrfToken;
      const start=await call(p,'/api/discovery/session','POST',undefined,csrf);
      if(start.status!==200) throw new Error(`Guest start ${start.status}`);
      sessions.push({p,csrf});
      const save=await call(p,'/api/discovery/onboarding','PUT',{revision:0,
        selection:{catalogRecordIds:[],tags:['jazz']},answers:{version:'taste-onboarding-v1',step:'complete',goal:'mixture',exploration:'balance',vocals:'no-preference',allStyles:false}},csrf);
      if(save.status!==200) throw new Error(`Taste save ${save.status}`);
      const begin=Date.now();
      const submitted=await call(p,'/api/discovery/batches','POST',{revision:save.data.revision,requestKey:crypto.randomUUID()},csrf);
      if(submitted.status!==202) throw new Error(`Submission ${submitted.status}`);
      let job=submitted.data;
      while(['Pending','Running','Retryable'].includes(job.state) && Date.now()-begin<55000) {
        await p.waitForTimeout(1000);
        const status=await call(p,`/api/discovery/jobs/${job.id}`);
        if(status.status!==200) throw new Error(`Status ${status.status}`);
        job=status.data;
      }
      const result={phase,elapsedMs:Date.now()-begin,job};
      if(job.state==='Completed' && job.batchId) {
        const batch=await call(p,`/api/discovery/batches/${job.batchId}`);
        if(batch.status!==200) throw new Error(`Batch ${batch.status}`);
        result.batch=batch.data;
        await p.goto(`http://localhost:5086/history/${job.batchId}`);
        await p.getByRole('heading').first().waitFor();
        await p.locator('article.personal-pick h2').first().waitFor();
        result.renderedPickTitles=await p.locator('article.personal-pick h2').allTextContents();
        if(result.renderedPickTitles.length!==batch.data.picks.length || batch.data.picks.some(pick=>!result.renderedPickTitles.includes(pick.title))) throw new Error('Rendered picks differ from stored API batch');
        const exported=await call(p,'/api/discovery/session/data');
        if(exported.status!==200) throw new Error('Own receipt export failed');
        result.exportedReceipts=exported.data.discovery.recommendations;
      }
      results.push(result);
      if(job.state!=='Completed') break; // no repeat of a failed or timed-out acquisition
    }
    return {version:'residue-live-probe-v1',results,
      limit:'One jazz seed, at most two submissions; cache state and upstream samples are not controlled; this is not a measured cold/warm speedup.'};
  } finally {
    const statuses=[];
    for(const s of sessions) statuses.push((await call(s.p,'/api/discovery/session','DELETE',undefined,s.csrf)).status);
    for(const c of contexts) await c.close();
    if(statuses.some(s=>s!==204)) throw new Error(`Cleanup failed: ${statuses}`);
  }
}
