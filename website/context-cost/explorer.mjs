import {forecast, resolvePrice, crossing, scenarioOptions} from './model.mjs';
const $=id=>document.getElementById(id), money=n=>n==null?'Unknown':new Intl.NumberFormat('en-US',{style:'currency',currency:'USD',maximumFractionDigits:4}).format(n);
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const colors=['var(--plot-1)','var(--plot-2)','var(--plot-3)','var(--plot-4)'];
let data,prices,preset,exportBundle,comparisonRows,levelInputs={},quotaInputs={};
const fields=[
 ['initialContext','Starting context (tokens)',0,1000000,1,'main','local'],
 ['toolTokensPerTurn','Tool / file growth per turn',0,200000,1,'main','assumptions'],
 ['outputTokensPerTurn','Output tokens / turn',0,100000,1,'main','local'],
 ['tasks','Tasks / decisions',1,200,1,'main','assumptions'],
 ['turnsPerTask','Turns / task',1,50,1,'main','assumptions'],
 ['gapMinutes','Within-task gap (minutes)',0,10080,.1,'main','assumptions'],
 ['taskGapMinutes','Between-task gap (minutes)',0,10080,.1,'main','assumptions'],
 ['restartContext','Fresh context / task',0,1000000,1,'main','local'],
 ['inputTokensPerTurn','Uncached prompt suffix / turn',0,100000,1,'advanced','assumptions'],
 ['sharedPrefixTokens','Reusable prefix on restart',0,1000000,1,'advanced','local'],
 ['initiallyCachedTokens','Initially warm prefix',0,1000000,1,'advanced','assumptions'],
 ['cacheTtlMinutes','Effective TTL (minutes)',0,10080,.1,'advanced','openai-cache'],
 ['cacheWritePerMTokOverride','Custom write USD / MTok',0,1000,.01,'advanced','assumptions'],
 ['minimumCacheTokens','Minimum cacheable prefix',0,1000000,1,'advanced','anthropic-cache'],
 ['compactionThreshold','Compact at tokens (0 = off)',0,1000000,1,'advanced','compaction'],
 ['compactedContext','Context after compaction',0,1000000,1,'advanced','compaction'],
 ['compactionOutputTokens','Compaction output tokens',0,100000,1,'advanced','compaction'],
 ['compactionMinutes','Compaction duration (minutes)',0,1440,.1,'advanced','compaction'],
 ['reasoningTokensPerTurn','Extra billed reasoning / turn',0,100000,1,'advanced','reasoning'],
 ['retainedReasoningTokensPerTurn','Retained reasoning / turn',0,100000,1,'advanced','reasoning'],
 ['successLong','Long-session success probability',0,1,.01,'advanced','assumptions'],
 ['successFresh','Fresh-session success probability',0,1,.01,'advanced','assumptions'],
 ['quotaConversion','Selected model quota % / USD',0,10000,.001,'advanced','assumptions']
];
function download(name,value,type='application/json') {
 const url=URL.createObjectURL(new Blob([value],{type}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
function csv(rows) {const keys=Object.keys(rows[0]??{});return [keys,...rows.map(r=>keys.map(k=>r[k]))].map(row=>row.map(v=>'"'+String(v??'unknown').replaceAll('"','""')+'"').join(',')).join('\n');}
function setupFields() {
 for(const [id,label,min,max,step,group,ref] of fields) {
  const element=document.createElement('label');element.innerHTML=`${esc(label)}<input id="${id}" type="number" min="${min}" max="${max}" step="${step}" ${['successLong','successFresh','quotaConversion','cacheWritePerMTokOverride'].includes(id)?'placeholder="Unknown / not supplied"':'required'}><a class="source-link" data-ref="${ref}" href="#evidence-${ref==='local'?'local-coding':ref}">${ref==='local'?'Local scale / proxy':'Source / assumption'}</a>`;
  if(group==='main' && ['initialContext','tasks','toolTokensPerTurn'].includes(id)) {
   const range=document.createElement('input');range.type='range';range.min=min;range.max=max;range.step=step;range.setAttribute('aria-label',label+' slider');range.dataset.for=id;
   range.addEventListener('input',()=>{$(id).value=range.value;render();});element.append(range);
  }
  $(group+'-inputs').append(element);
 }
}
function setPreset(id) {
 preset=data.presets.find(p=>p.id===id);
 for(const [key,value] of Object.entries(preset.parameters)) if($(key)) $(key).value=value;
 $('gaps').value='';$('successLong').value='';$('successFresh').value='';
 document.querySelectorAll('#presets button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.id===id)));
 $('question').textContent=preset.question;$('sample').innerHTML=esc(preset.parameterNotes)+` <a href="#evidence-${preset.evidenceIds[0]}">Inspect sample</a>`;
 document.querySelectorAll('[data-ref="local"]').forEach(a=>a.href='#evidence-'+preset.evidenceIds[0]);
 render();
}
function configureModel() {
 const m=data.models.find(m=>m.modelId===$('model').value), old=$('reasoning').value;
 $('reasoning').innerHTML=m.reasoningLevels.map(l=>`<option>${esc(l)}</option>`).join('');
 $('reasoning').value=m.reasoningLevels.includes(old)?old:'medium';
 $('quotaConversion').value=quotaInputs[m.modelId]??'';
 configureCache();configureReasoning();
}
function configureReasoning() {
 const saved=levelInputs[$('model').value+'/'+$('reasoning').value]??[0,0];
 $('reasoningTokensPerTurn').value=saved[0];$('retainedReasoningTokensPerTurn').value=saved[1];
}
function configureCache() {
 const policy=data.cachePolicies[$('model').value], mode=$('cacheMode').value;
 $('cacheTtlMinutes').value=mode==='disabled'?0:mode==='hour'?60:policy.ttlMinutes;
 $('minimumCacheTokens').value=policy.minimumCacheTokens;
 $('cacheWritePerMTokOverride').value='';
 $('cacheTtlMinutes').disabled=mode!=='custom';$('cacheWritePerMTokOverride').disabled=mode!=='custom';
}
function optionsFor(model, p) {
 const mode=$('cacheMode').value, provider=data.cachePolicies[model.modelId];
 const o=scenarioOptions({...preset.parameters,...p},model.modelId,provider,$('date').value);
 o.reasoningLevel=$('reasoning').value;
 if(mode==='unknown') o.cacheTtlMinutes=null;
 if(mode==='disabled') o.cacheTtlMinutes=0;
 if(mode==='custom') {o.cacheTtlMinutes=p.cacheTtlMinutes;o.cacheWritePerMTokOverride=p.cacheWritePerMTokOverride;}
 if(mode==='hour') {
  if(model.vendor!=='anthropic') o.cacheTtlMinutes=null;
  else {o.cacheTtlMinutes=60;const rate=resolvePrice(model,Date.parse(o.startUtc));o.cacheWritePerMTokOverride=rate?rate.inputPerMTok*2:null;}
 }
 o.minimumCacheTokens=model.modelId===$('model').value?p.minimumCacheTokens:provider.minimumCacheTokens;
 if($('gaps').value.trim()) o.gapsMinutes=$('gaps').value.split(',').map(Number);
 return o;
}
function chart(id,title,series,assumption,{stack=false,marker=null,xLabel='Turn',rightScale=null,bars=false,markerLabel='Turn'}={}) {
 const el=document.createElement('article');el.className='chart';
 const values=series.flatMap(s=>s.points.map(p=>p.y)).filter(v=>v!=null);
 const maxX=Math.max(1,...series.flatMap(s=>s.points.map(p=>p.x))),maxY=Math.max(.00001,...values)*1.08;
 const x=v=>64+v/maxX*440,y=v=>225-v/maxY*185;
 let svg=`<svg viewBox="0 0 560 280" role="img" aria-label="${esc(title)}; USD vertical axis, ${esc(xLabel)} horizontal axis"><text x="10" y="18">USD</text>`;
 for(let i=0;i<=4;i++) {const v=maxY*i/4;svg+=`<line class="axis" x1="64" x2="504" y1="${y(v)}" y2="${y(v)}"/><text x="58" y="${y(v)+4}" text-anchor="end">${v<1?v.toFixed(3):v.toFixed(1)}</text><text x="${x(maxX*i/4)}" y="247" text-anchor="middle">${Math.round(maxX*i/4).toLocaleString()}</text>`;}
 svg+=`<text x="280" y="273" text-anchor="middle">${esc(xLabel)}</text>`;
 series.forEach((s,i)=>{
  if(stack) {
   const lower=i?series[i-1].points:s.points.map(p=>({...p,y:0}));
   svg+=`<polygon points="${[...s.points,...lower.slice().reverse()].map(p=>`${x(p.x)},${y(p.y)}`).join(' ')}" fill="${colors[i]}" opacity=".8"/>`;
  } else if(bars) {
   s.points.forEach(p=>{if(p.y!=null)svg+=`<rect x="${x(p.x)-35+i*17}" y="${y(p.y)}" width="15" height="${225-y(p.y)}" fill="${colors[i%4]}"/>`;});
  } else svg+=`<polyline points="${s.points.filter(p=>p.y!=null).map(p=>`${x(p.x)},${y(p.y)}`).join(' ')}" fill="none" stroke="${colors[i%4]}" stroke-width="2.5" stroke-dasharray="${i===1?'8 3':i===2?'3 3':i===3?'10 3 2 3':''}"/>`;
 });
 if(marker!=null) svg+=`<line x1="${x(marker)}" x2="${x(marker)}" y1="32" y2="225" stroke="var(--site-nav-text)" stroke-dasharray="3 3"/><text x="${Math.min(x(marker)+5,400)}" y="30">${esc(markerLabel)} ${marker}</text>`;
 if(rightScale) svg+=`<text x="552" y="18" text-anchor="end">${Math.round(maxY/rightScale).toLocaleString()} tokens ↑</text>`;
 svg+='</svg>';
 el.innerHTML=`<h2>${esc(title)}</h2>${svg}<div class="legend">${series.map((s,i)=>`<span style="--line:${colors[i%4]}">${esc(s.name)}</span>`).join('')}</div><p>${esc(assumption)}</p><button type="button">Export chart numbers (CSV)</button>`;
 const rows=series.flatMap((s,si)=>s.points.map((p,pi)=>({series:s.name,x:p.x,usd:stack?p.y-(si?series[si-1].points[pi].y:0):p.y,...(stack?{stackedBoundaryUsd:p.y}:{}),...(p.tokens!=null?{contextTokens:p.tokens}:{}),assumptions:assumption,model:$('model').value,reasoning:$('reasoning').value,priceDate:$('date').value,sessionParameters:JSON.stringify(exportBundle.parameters)})));
 el.querySelector('button').onclick=()=>download(id+'.csv',csv(rows),'text/csv');
 exportBundle.charts[id]={assumption,rows};$('charts').append(el);
}
function render() {
 try {
  $('error').textContent='';
  for(const [id] of fields) if(!$(id).disabled && !$(id).checkValidity()) throw new Error('Check '+$(id).parentElement.firstChild.textContent+'.');
  if(!$('date').checkValidity()) throw new Error('Select a price date.');
  const p=Object.fromEntries(fields.map(([id])=>[id,$(id).value===''?null:Number($(id).value)]));
  if(p.sharedPrefixTokens>Math.min(p.restartContext,p.initialContext)) throw new Error('Shared prefix must fit both initial and fresh context.');
  if(p.initiallyCachedTokens>p.initialContext) throw new Error('Initially warm prefix exceeds starting context.');
  if(p.compactionThreshold>0 && p.compactedContext>=p.compactionThreshold) throw new Error('Compacted context must be below the compaction threshold.');
  if(p.retainedReasoningTokensPerTurn>p.reasoningTokensPerTurn) throw new Error('Retained reasoning cannot exceed billed reasoning.');
  const model=prices.models.find(m=>m.modelId===$('model').value),o=optionsFor(model,p),rate=resolvePrice(model,Date.parse(o.startUtc));
  if(o.gapsMinutes.length!==o.turns || o.gapsMinutes[0]!==0 || o.gapsMinutes.some(g=>!Number.isFinite(g)||g<0)) throw new Error('Provide one nonnegative gap per turn, starting with zero.');
  const long=forecast(o,model),fresh=forecast({...o,initialContext:p.restartContext,initiallyCachedTokens:Math.min(o.initiallyCachedTokens,p.restartContext),restartEveryTurns:p.turnsPerTask,compactionThreshold:0},model);
  $('price-note').innerHTML=rate?`Input / read / write / output: ${[rate.inputPerMTok,rate.cacheReadPerMTok,o.cacheWritePerMTokOverride??rate.cacheWritePerMTok,rate.outputPerMTok].map(money).join(' / ')} per MTok · valid from ${esc(rate.validFrom.slice(0,10))} · verified ${esc(rate.verifiedOn??'unknown')} · ${rate.unconfirmed?'UNCONFIRMED':'catalogue rate'} · <a href="${esc(rate.sourceUrls[0])}">Price source</a>. ${esc(rate.note??'')}`:'Price unknown for this date.';
  const policy=data.models.find(m=>m.modelId===model.modelId);
  $('policy-note').textContent=`Policy status: ${policy.routingStatus}. Reasoning: ${o.reasoningLevel}; additional tokens are user estimates, not a measured effort multiplier. Cache: ${o.cacheTtlMinutes??'unknown'} minutes. ${model.vendor==='openai'?'30-minute expiry is a conservative lower-retention scenario.':''}`;
  if(long.total==null) throw new Error(long.unavailableReason);
  const cross=crossing(long,fresh,p.turnsPerTask),raw=forecast({...o,compactionThreshold:0},model);
  const threshold=p.compactionThreshold || Math.ceil(Math.max(o.initialContext*1.5,p.compactedContext+1));
  const compact=forecast({...o,compactionThreshold:threshold},model);
  exportBundle={parameters:o,preset:preset.id,sample:preset.parameterNotes,price:rate,evidence:data.evidence,long,fresh,compact,charts:{}};
  $('output').hidden=false;
  $('summary').innerHTML=[['Long session',money(long.total)],['Fresh per task',money(fresh.total)],['First task where long costs more',cross??'Not in horizon']].map(([label,value])=>`<div class="card">${esc(label)}<strong>${esc(value)}</strong></div>`).join('');
  const next=forecast({...o,turns:o.turns+1,gapsMinutes:[...o.gapsMinutes,o.turns%p.turnsPerTask===0?p.taskGapMinutes:p.gapMinutes]},model).turns.at(-1);
  $('finding').textContent=`Over ${p.tasks} tasks (${o.turns} turns), keeping the session ${long.total>fresh.total?'adds':'saves'} ${money(Math.abs(long.total-fresh.total))}. The following turn would cost ${money(next.total)}, including growth and any compaction, at the next configured gap.`;
  const assumption=`${model.displayName}, ${o.reasoningLevel}, ${$('date').value}; ${o.turns} turns, ${p.turnsPerTask}/task; start ${o.initialContext} tokens, +${o.toolTokensPerTurn+o.outputTokensPerTurn+o.inputTokensPerTurn+o.retainedReasoningTokensPerTurn}/turn; ${o.cacheTtlMinutes}m effective TTL; gaps ${p.gapMinutes}m within / ${p.taskGapMinutes}m between tasks${$('gaps').value?' (custom per-turn gaps override)':''}; ${o.initiallyCachedTokens} initially warm. ${o.cacheWritePerMTokOverride!=null?'Explicit write override: '+money(o.cacheWritePerMTokOverride)+'/MTok. ':''}Standard text tariff, no context premium or window enforcement. Beyond supported windows these are mathematical extrapolations.`;
  $('assumptions').textContent=assumption+' All chart exports include assumptions; full JSON includes every source and parameter.';
  $('charts').replaceChildren();
  const kinds=['input','cacheRead','cacheWrite','output'];let sums=[0,0,0,0];
  const stacks=kinds.map(k=>({name:k,points:[{x:0,y:0}]}));
  long.turns.forEach(r=>{kinds.forEach((k,i)=>sums[i]+=r.components[k]);let acc=0;stacks.forEach((s,i)=>{acc+=sums[i];s.points.push({x:r.turn,y:acc});});});
  chart('cumulative','01 / Where the dollars go',stacks,assumption+' Stacked cumulative USD; compaction included.',{stack:true});
  const selectedModels=data.models.map(m=>prices.models.find(p=>p.modelId===m.modelId));
  chart('next-turn','02 / Every question costs more',selectedModels.map(m=>({name:m.displayName,points:Array.from({length:21},(_,i)=>{const n=i*25000,r=resolvePrice(m,Date.parse(o.startUtc));return {x:n,y:r?(n*(n>=data.cachePolicies[m.modelId].minimumCacheTokens?r.cacheReadPerMTok:r.inputPerMTok)+(o.outputTokensPerTurn+o.reasoningTokensPerTurn)*r.outputPerMTok)/1e6:null};})})),`Price date ${$('date').value}; fully warm eligible prefix, fixed ${o.outputTokensPerTurn+o.reasoningTokensPerTurn} billed output, no growth suffix. Each model uses its cache minimum. Same token budget does not mean equal quality.`,{xLabel:'Context tokens'});
  chart('strategies','03 / Keep it or start fresh',[{name:'Long session',points:long.turns.map(r=>({x:r.turn,y:r.cumulativeCost}))},{name:'Fresh per task',points:fresh.turns.map(r=>({x:r.turn,y:r.cumulativeCost}))}],assumption+` Fresh rebuild: ${p.restartContext}; shared prefix: ${p.sharedPrefixTokens}; fresh strategy has no compaction. First strict crossover at completed task: ${cross??'none in horizon'}.`,{marker:cross?cross*p.turnsPerTask:null});
  const current=long.turns.at(-1).contextTokens;
  const ttl=o.cacheTtlMinutes;
  const gaps=ttl===0?[0,1,5,30,60]:[...new Set([0,ttl*.25,ttl*.5,Math.max(0,ttl-.001),ttl,ttl*1.5,ttl*2])].sort((a,b)=>a-b);
  chart('expiry','04 / The price of a pause',[{name:'Next call',points:gaps.map(g=>{const r=forecast({...o,turns:2,gapsMinutes:[0,g],initialContext:current,initiallyCachedTokens:current,toolTokensPerTurn:0,outputTokensPerTurn:0,retainedReasoningTokensPerTurn:0,compactionThreshold:0},model);return {x:g,y:r.turns[1].total};})}],`Same ${current} token prefix, ${o.reasoningTokensPerTurn} reasoning output, no other growth or output. ${model.displayName}; ${$('date').value}; ${ttl===0?'caching disabled':('write at effective TTL '+ttl+'m')}. Effective write rate ${money(o.cacheWritePerMTokOverride??rate.cacheWritePerMTok)}/MTok. Boundary is conservative for OpenAI; real entries may survive longer.`,{xLabel:'Minutes since preceding request start'});
  chart('compaction-cost','05 / Does compaction pay?',[{name:'No compaction',points:raw.turns.map(r=>({x:r.turn,y:r.cumulativeCost}))},{name:'With compaction',points:compact.turns.map(r=>({x:r.turn,y:r.cumulativeCost}))}],assumption+` Compact at ${threshold} to ${p.compactedContext}; summary output ${p.compactionOutputTokens}; ${p.compactionMinutes}m compaction; full rewrite. ${p.compactionThreshold?'Uses configured threshold.':'Comparison-only threshold; main forecast remains uncompacted.'}`);
  chart('compaction-context','06 / History grows, then resets',[{name:'Warm replay value of context',points:compact.turns.flatMap(r=>r.compacted?[{x:r.turn,y:r.contextBeforeCompaction*rate.cacheReadPerMTok/1e6,tokens:r.contextBeforeCompaction},{x:r.turn,y:r.contextTokens*rate.cacheReadPerMTok/1e6,tokens:r.contextTokens}]:[{x:r.turn,y:r.contextTokens*rate.cacheReadPerMTok/1e6,tokens:r.contextTokens}])}],`Context valued at ${money(rate.cacheReadPerMTok)}/MTok, ${model.displayName}, ${$('date').value}. This USD axis is replay value, not actual compaction charges. Exact context token counts are in CSV. Threshold ${threshold}, retained ${p.compactedContext}.`,{rightScale:rate.cacheReadPerMTok/1e6});
  const sweep=[...new Set([0,1000,5000,10000,25000,50000,100000,250000,500000,o.initialContext])].sort((a,b)=>a-b);
  const difference=n=>{const q={...o,initialContext:n,initiallyCachedTokens:Math.min(n,o.initiallyCachedTokens),restartContext:n,sharedPrefixTokens:Math.min(n,o.sharedPrefixTokens),compactionThreshold:0};return forecast(q,model).total-forecast({...q,restartEveryTurns:p.turnsPerTask},model).total;};
  let contextCross=null;
  if(difference(0)*difference(500000)<0){let lo=0,hi=500000;const sign=Math.sign(difference(0));while(hi-lo>1){const mid=Math.floor((lo+hi)/2);if(Math.sign(difference(mid))===sign)lo=mid;else hi=mid;}contextCross=hi;}
  chart('context-break-even','07 / How much starting context changes the answer',['Long session','Fresh per task'].map((name,i)=>({name,points:sweep.map(n=>{const a={...o,initialContext:n,initiallyCachedTokens:Math.min(n,o.initiallyCachedTokens),restartContext:n,sharedPrefixTokens:Math.min(n,o.sharedPrefixTokens),compactionThreshold:0,restartEveryTurns:i?p.turnsPerTask:0};return {x:n,y:forecast(a,model).total};})})),assumption+` Sensitivity: initial and restart context varied together, shared prefix capped to context, compaction off. Context break-even: ${contextCross==null?'none between 0 and 500k tokens':contextCross+' tokens (whole-token boundary)'}.`,{xLabel:'Starting / reconstructed tokens',marker:contextCross,markerLabel:'Tokens'});
  comparisonRows=selectedModels.map(m=>{
   const mo=optionsFor(m,p),r=forecast(mo,m),f=forecast({...mo,initialContext:p.restartContext,initiallyCachedTokens:Math.min(mo.initiallyCachedTokens,p.restartContext),restartEveryTurns:p.turnsPerTask,compactionThreshold:0},m);
   const quota=quotaInputs[m.modelId];
   return {model:m.displayName,totalUsd:r.total,usdPerCompletedTask:r.total==null?null:r.total/p.tasks,freshUsd:f.total,
    firstMoreExpensiveTask:crossing(r,f,p.turnsPerTask),usdPerSuccessfulTask:r.total!=null&&p.successLong>0?r.total/p.tasks/p.successLong:null,
    freshUsdPerSuccessfulTask:f.total!=null&&p.successFresh>0?f.total/p.tasks/p.successFresh:null,quotaSharePercent:quota!=null&&r.total!=null?r.total*quota:null,
    effectiveTtlMinutes:mo.cacheTtlMinutes,priceDate:$('date').value,reasoning:mo.reasoningLevel};
  });
  $('comparison').innerHTML='<table><thead><tr>'+['Model','Long USD','USD / completed task¹','Fresh USD','First costlier task','USD / successful task²','Fresh USD / successful task²','Quota share³'].map(x=>`<th scope="col">${x}</th>`).join('')+'</tr></thead><tbody>'+comparisonRows.map(r=>`<tr><th scope="row">${esc(r.model)}</th><td>${money(r.totalUsd)}</td><td>${money(r.usdPerCompletedTask)}</td><td>${money(r.freshUsd)}</td><td>${r.firstMoreExpensiveTask??'None / unknown'}</td><td>${money(r.usdPerSuccessfulTask)}</td><td>${money(r.freshUsdPerSuccessfulTask)}</td><td>${r.quotaSharePercent==null?'Unknown':r.quotaSharePercent.toFixed(2)+'% (user conversion)'}</td></tr>`).join('')+'</tbody></table><p class="muted">¹ Assumes all configured tasks complete. ² Cost / tasks / supplied success probability; equal-cost independent attempts assumed, no empirical prediction. ³ Unknown without a model-specific measured conversion; API dollars are not subscription quota. Other models use provider TTL/minimum unless custom settings are selected. The comparison holds the billed token budget constant; an effort label does not imply that every model supports that effort.</p>';
  chart('model-totals','08 / Model cost comparison',comparisonRows.map(r=>({name:r.model,points:[{x:1,y:r.usdPerCompletedTask},{x:2,y:r.totalUsd}]})),`1 = USD per completed task; 2 = total for ${p.tasks} tasks. Identical token workload, selected cache policy, ${$('date').value}. Quota shares and quality assumptions are in the table.`,{xLabel:'1: per task · 2: entire session',bars:true});
  exportBundle.comparison=comparisonRows;exportBundle.successAssumptions={long:p.successLong,fresh:p.successFresh};exportBundle.quotaConversions={...quotaInputs};
  document.querySelectorAll('input[type="range"]').forEach(r=>r.value=$(r.dataset.for).value);
 } catch(e) {$('error').textContent=e.message;$('output').hidden=true;exportBundle=null;}
}
try {
 const responses=await Promise.all(['../data/context-cost.json','../data/price-history.json'].map(p=>fetch(p).then(r=>{if(!r.ok)throw new Error('Could not load '+p);return r.json();})));
 [data,prices]=responses;setupFields();
 $('model').innerHTML=data.models.map(m=>`<option value="${esc(m.modelId)}">${esc(prices.models.find(p=>p.modelId===m.modelId).displayName)}</option>`).join('');$('model').value='claude-opus-5';
 $('presets').innerHTML=data.presets.map(p=>`<button type="button" data-id="${p.id}" aria-pressed="false">${esc(p.title)}</button>`).join('');
 $('presets').addEventListener('click',e=>{const b=e.target.closest('button');if(b)setPreset(b.dataset.id);});
 $('evidence').innerHTML=data.evidence.map(e=>`<details id="evidence-${e.id}"><summary>${esc(e.title)} · ${esc(e.kind)} · ${esc(e.confidence)} confidence</summary><p><a href="${esc(e.sourceUrl)}">Primary source</a> · published ${esc(e.publishedOn??'not dated / living documentation')} · retrieved ${e.retrievedOn}</p><blockquote>${esc(e.excerpt)}</blockquote><p>${esc(e.conditions)}</p></details>`).join('');
 document.addEventListener('click',e=>{const a=e.target.closest('a[href^="#evidence-"]');if(a){const target=document.getElementById(a.hash.slice(1));if(target?.tagName==='DETAILS')target.open=true;}});
 $('controls').addEventListener('submit',e=>e.preventDefault());
 $('controls').addEventListener('input',e=>{
  if(e.target.id==='model')configureModel();
  if(e.target.id==='reasoning')configureReasoning();
  if(e.target.id==='cacheMode')configureCache();
  if(['reasoningTokensPerTurn','retainedReasoningTokensPerTurn'].includes(e.target.id))levelInputs[$('model').value+'/'+$('reasoning').value]=[Number($('reasoningTokensPerTurn').value),Number($('retainedReasoningTokensPerTurn').value)];
  if(e.target.id==='quotaConversion')quotaInputs[$('model').value]=$('quotaConversion').value===''?null:Number($('quotaConversion').value);
  render();
 });
 $('export-all').onclick=()=>download('context-cost-forecast.json',JSON.stringify(exportBundle,null,2));
 $('export-models').onclick=()=>download('context-cost-models.csv',csv(comparisonRows),'text/csv');
 configureModel();setPreset('coding');$('app').hidden=false;$('loading').hidden=true;
 document.dispatchEvent(new CustomEvent('site:content-ready'));
} catch(e) {$('loading').textContent=e.message+'. Serve this page over HTTP to load the data.';}
