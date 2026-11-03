// Run through Playwright MCP against the disposable synthetic guest_discovery_demo host.
async (page) => {
  const base = 'http://localhost:5096';
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
    const answers={version:'taste-onboarding-v1',step:'complete',goal:'mixture',exploration:'balance',vocals:'no-preference',allStyles:false};
    const save=async(s,tag)=>call(s.p,'/api/discovery/onboarding','PUT',{revision:0,answers,selection:{catalogRecordIds:[],tags:[tag]}},s.csrf);
    const aSaved=await save(a,'rock'),bSaved=await save(b,'jazz');
    check(aSaved.status===200&&bSaved.status===200,'Independent listeners save distinct preferences');
    const aBatch=await generate(a,aSaved.data.revision),bBatch=await generate(b,bSaved.data.revision);
    check(aBatch.picks.length===5&&bBatch.picks.length===5,'Both listeners receive own fixture batches');
    await a.p.goto(base+'/history/'+aBatch.id);await a.p.locator('article.personal-pick h2').first().waitFor();
    check(await a.p.locator('article.personal-pick h2').count()===5,'Actual application renders stored recommendations');
    const exportData=async(s)=>(await call(s.p,'/api/discovery/session/data')).data.discovery;
    const bBefore=await exportData(b);const id=aBatch.picks[0].id;
    const update=async(s,revision,feedback,rating,comment)=>call(s.p,`/api/discovery/recommendations/${id}/feedback`,'POST',{revision,feedback,rating,comment},s.csrf);
    const like=await update(a,aSaved.data.revision,'Liked',9,null);
    check(like.status===200,'Typed positive feedback persists');
    const musical=(await exportData(a)).recommendations.find(r=>r.id===id);
    check(musical.feedback==='Liked'&&musical.rating===9&&musical.feedbackGivenAt,'Musical state and timestamp export');
    const note=await update(a,like.data.revision,'Liked',9,'Private uninterpreted test note');
    check(note.status===200&&note.data.revision===like.data.revision,'Note edit preserves taste revision');
    const noted=(await exportData(a)).recommendations.find(r=>r.id===id);
    check(noted.feedbackGivenAt===musical.feedbackGivenAt,'Note edit does not refresh musical recency');
    check(noted.feedbackComment==='Private uninterpreted test note'&&noted.lastModifiedAt>=musical.lastModifiedAt,'Audit timestamp and private note remain exportable');
    const dislike=await update(a,note.data.revision,'Disliked',2,null);
    check(dislike.status===200&&dislike.data.revision>note.data.revision,'Dislike replaces earlier like');
    const rejected=await update(b,bSaved.data.revision,'Disliked',2,null);
    check(rejected.status===404,'Foreign feedback mutation denied');
    const unlike=await update(a,dislike.data.revision,'Liked',8,null);
    check(unlike.status===200,'Dislike can be reversed');
    const known=await update(a,unlike.data.revision,'AlreadyKnown',null,null);
    check(known.status===200,'Known state remains distinct from dislike');
    const cleared=await update(a,known.data.revision,'None',null,null);
    check(cleared.status===200,'Clearing typed feedback persists');
    const finalA=await exportData(a),clearedPick=finalA.recommendations.find(r=>r.id===id);
    check(clearedPick.feedback==='None'&&clearedPick.rating===null&&clearedPick.feedbackComment===null,'Cleared feedback has no retained vote or note');
    check(finalA.recommendedHistory.length>0,'Clearing feedback preserves recommendation history');
    check((await call(b.p,'/api/discovery/batches/'+aBatch.id)).status===404,'Foreign batch denied');
    check(JSON.stringify(bBefore)==JSON.stringify(await exportData(b)),'Other listener taste, feedback, and receipts remain unchanged');
    check((await call(a.p,'/api/discovery/session','DELETE',undefined,a.csrf)).status===204,'First listener deletion succeeds');a.cleared=true;
    check(JSON.stringify(bBefore)==JSON.stringify(await exportData(b)),'Other listener survives deletion unchanged');
    return {version:'residue-phase3-browser-v1',checks,receipts:finalA.recommendations,
      limit:'Synthetic application correctness only; no founder data or subjective quality claim.'};
  } finally {
    const cleanup=[];for(const s of sessions) if(!s.cleared) cleanup.push((await call(s.p,'/api/discovery/session','DELETE',undefined,s.csrf)).status);
    for(const c of contexts) await c.close();if(cleanup.some(x=>x!==204)) throw new Error('Guest cleanup incomplete');
  }
}
