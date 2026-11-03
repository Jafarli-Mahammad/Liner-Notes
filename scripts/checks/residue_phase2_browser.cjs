// Run through Playwright MCP against the disposable synthetic guest_discovery_demo host.
async (page) => {
  const base = 'http://localhost:5094';
  const browser = page.context().browser(), contexts = [], sessions = [], checks = [];
  const check = (value, message) => { if (!value) throw new Error(message); checks.push(message); };
  const call = (p, path, method='GET', body, csrf) => p.evaluate(async ({path,method,body,csrf}) => {
    const r = await fetch(path,{method,headers:{...(body?{'Content-Type':'application/json'}:{}),...(csrf?{'X-CSRF-TOKEN':csrf}:{})},body:body?JSON.stringify(body):undefined});
    const text=await r.text();return {status:r.status,data:text?JSON.parse(text):null};
  },{path,method,body,csrf});
  const start = async () => {
    const context=await browser.newContext(); contexts.push(context);
    await context.route('**/*',r=>new URL(r.request().url()).origin===base?r.continue():r.abort());
    const p=await context.newPage();await p.goto(base+'/taste');await p.getByRole('heading',{name:'Your taste',exact:true}).waitFor();
    const boot=await call(p,'/api/discovery/session');check(!boot.data.profile,'Fresh isolated browser profile');
    const csrf=boot.data.csrfToken;const created=await call(p,'/api/discovery/session','POST',undefined,csrf);check(created.status===200,'Guest created');
    const session={p,csrf,cleared:false};sessions.push(session);return session;
  };
  const generate = async (s,revision) => {
    const accepted=await call(s.p,'/api/discovery/batches','POST',{revision,requestKey:crypto.randomUUID()},s.csrf);
    check(accepted.status===202,'Generation accepted');let job=accepted.data;const begun=Date.now();
    while(['Pending','Running','Retryable'].includes(job.state)&&Date.now()-begun<55000) {
      await s.p.waitForTimeout(1000);job=(await call(s.p,`/api/discovery/jobs/${job.id}`)).data;
    }
    check(job.state==='Completed',`Generation completed (${job.state})`);
    const result=await call(s.p,`/api/discovery/batches/${job.batchId}`);check(result.status===200,'Own batch readable');return result.data;
  };
  try {
    const a=await start(),b=await start();
    check((await call(a.p,'/api/discovery')).data.evidenceOrigin==='Synthetic','Explicit synthetic application; no live-provider acquisition');
    const items=[];
    for(const [kind,pages] of [['Artist',5],['Album',2],['Track',1]]) for(let offset=0;offset<pages*10;offset+=10) {
      const found=await call(a.p,`/api/catalog/search?query=Synthetic&kind=${kind}&limit=10&offset=${offset}`);
      check(found.status===200,`${kind} catalog page`);items.push(...(kind==='Track'?found.data.items.slice(0,2):found.data.items));
    }
    check(items.length===72,'Seventy-two artist/album/recording favorites available');
    const answers={version:'taste-onboarding-v1',step:'complete',goal:'mixture',exploration:'balance',vocals:'no-preference',allStyles:false};
    const saved=await call(a.p,'/api/discovery/onboarding','PUT',{revision:0,answers,selection:{catalogRecordIds:items.map(x=>x.id),tags:[]}},a.csrf);
    check(saved.status===200,'Save all mixed favorites');
    await a.p.reload();await a.p.getByRole('heading',{name:'Your taste',exact:true}).waitFor();
    check(await a.p.locator('.favorite-row').count()===72,'Taste UI retains every saved favorite');
    const aBatch=await generate(a,saved.data.revision);
    check(aBatch.picks.length===3,'Favorite recordings suppressed without padding');
    const favoriteRecordings=new Set(items.filter(x=>x.kind==='Track').map(x=>x.canonicalId));
    check(aBatch.picks.every(p=>!favoriteRecordings.has(p.mbid)),'No saved recording recommended back to owner');
    await a.p.goto(base+'/history/'+aBatch.id);await a.p.locator('article.personal-pick h2').first().waitFor();
    check(await a.p.locator('article.personal-pick h2').count()===3,'Selected picks render in actual application');
    const aExport=(await call(a.p,'/api/discovery/session/data')).data.discovery;
    check(aExport.tasteSignals.length===72,'All favorites export');
    for(const pick of aExport.recommendations) {
      const evidence=pick.scoreBreakdown.snapshot.evidence;
      check(evidence.favoriteEvidence.length===72&&evidence.favoriteEvidence.every(s=>s.state==='supported'),'Receipt accounts for every fresh favorite');
      check(evidence.tasteEvidenceCount===72&&evidence.acquisitionWindow.length===20,'Evidence independent of twenty-request candidate window');
    }
    const disliked=await call(a.p,`/api/discovery/recommendations/${aBatch.picks[0].id}/feedback`,'POST',{revision:saved.data.revision,feedback:'Disliked',rating:2,comment:null},a.csrf);
    check(disliked.status===200,'Owner feedback persists through existing path');
    const bSaved=await call(b.p,'/api/discovery/onboarding','PUT',{revision:0,answers,selection:{catalogRecordIds:[],tags:['electronic']}},b.csrf);
    check(bSaved.status===200,'Independent listener saves own taste');const bBatch=await generate(b,bSaved.data.revision);
    check(bBatch.picks.length===5,'Other listener unaffected by favorites/history/dislike exclusions');
    check((await call(b.p,'/api/discovery/batches/'+aBatch.id)).status===404,'Foreign batch denied');
    const bBefore=(await call(b.p,'/api/discovery/session/data')).data.discovery;
    check(bBefore.recommendations.every(p=>p.scoreBreakdown.snapshot.evidence.favoriteEvidence.length===0),'No favorite evidence leaks into other listener receipts');
    check((await call(a.p,'/api/discovery/session','DELETE',undefined,a.csrf)).status===204,'First guest deletion succeeds');a.cleared=true;
    const bAfter=(await call(b.p,'/api/discovery/session/data')).data.discovery;
    check(JSON.stringify(bBefore.recommendations)===JSON.stringify(bAfter.recommendations),'Other listener receipts survive first listener deletion');
    return {version:'residue-phase2-browser-v1',checks,ownedFavoriteCount:72,candidateWindow:20,aPicks:aBatch.picks.length,bPicks:bBatch.picks.length,
      receipts:aExport.recommendations,limit:'Synthetic application correctness only; no subjective quality claim or founder inputs.'};
  } finally {
    const cleanup=[];for(const s of sessions) if(!s.cleared) cleanup.push((await call(s.p,'/api/discovery/session','DELETE',undefined,s.csrf)).status);
    for(const c of contexts) await c.close();if(cleanup.some(x=>x!==204)) throw new Error('Guest cleanup incomplete');
  }
}
