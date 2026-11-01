'use strict';
(async () => {
  try {
    const response = await fetch('/token-economy/data/cost-frontier.json');
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const {AS_OF, M, V, EFFORTS, RUNGS, AA, CARD, REP, B, RESTRICTED, REFS, NAME_LINKS, EXTRA_LINKS, RELEASES, CATS, EVIDENCE, INSIGHTS, MISSIONS, LOADS, CURRENT: currentIds, MAJOR, PRICE_CLAIMS} = await response.json();
    const CURRENT = new Set(currentIds);
const $ = (s, r=document) => r.querySelector(s);
const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c]);
const money = v => v == null ? 'n/a' : v >= 100 ? '$' + Math.round(v).toLocaleString('en-US') : v >= 1 ? '$' + v.toFixed(2) : v >= .1 ? '$' + v.toFixed(2) : v >= .01 ? '$' + v.toFixed(3) : '$' + v.toFixed(4);
const bigMoney = v => v >= 1000 ? '$' + Math.round(v).toLocaleString('en-US') : v >= 10 ? '$' + v.toFixed(0) : '$' + v.toFixed(2);
const eff = e => e === 'xhigh' ? 'xHigh' : e;
const ANCHOR = (u, t) => `<a href="${esc(u)}" target="_blank" rel="noopener">${esc(t)}</a>`;
const benchHome = id => REFS[id][0][1];
const LINK_MAP = Object.assign(Object.fromEntries(NAME_LINKS.map(([n, id]) => [n, benchHome(id)])), EXTRA_LINKS);
const LINK_RE = new RegExp('\\b(' + Object.keys(LINK_MAP).sort((a, b) => b.length - a.length).map(n => n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|') + ')\\b', 'g');
const linked = t => esc(t).replace(LINK_RE, m => ANCHOR(LINK_MAP[m], m));
const benchLink = id => ANCHOR(benchHome(id), B[id].short);
const fmtDate = d => new Date(d + 'T00:00:00Z').toLocaleDateString('en-GB', { day:'numeric', month:'short', year:'numeric', timeZone:'UTC' });
const releaseOf = m => RELEASES.find(r => r.ms.includes(m));
const store = { get(k, d) { try { const v = localStorage.getItem('cf-' + k); return v == null ? d : JSON.parse(v); } catch { return d; } }, set(k, v) { try { localStorage.setItem('cf-' + k, JSON.stringify(v)); } catch {} } };
function shape(v, r=6.5) {
  if (v === 'a') return `M${-r},0a${r},${r} 0 1,0 ${2*r},0a${r},${r} 0 1,0 ${-2*r},0Z`;
  if (v === 'o') { const s=r*.9; return `M${-s},${-s}H${s}V${s}H${-s}Z`; }
  if (v === 'g') { const s=r*1.28; return `M0,${-s}L${s},0L0,${s}L${-s},0Z`; }
  const s=r*1.2; return `M0,${-s}L${s},${s*.75}L${-s},${s*.75}Z`;
}
const glyph = (v, hollow) => `<svg viewBox="-9 -9 18 18" aria-hidden="true"><path d="${shape(v)}" fill="${hollow?'none':V[v][1]}" stroke="${V[v][1]}" stroke-width="${hollow?2:0}"/></svg>`;

const PROFILES = [ { id:'short', label:'Short call', i:2000, o:200 }, { id:'std', label:'Standard', i:100000, o:10000 }, { id:'agent', label:'Long agent run', i:2000000, o:60000 } ];
const S = { view:'atlas', cat:'reasoning', bench:'aa5', effort:'all', budgetT:null, off:new Set(), profile:'std', sel:null, ins:new Set(store.get('ins', [])), best:store.get('best', null), playing:null };

function points(bid, profileId=S.profile) {
  const b = B[bid], p = PROFILES.find(x => x.id === profileId);
  return b.rows.map(r => {
    const m = M[r[0]]; let cost = r[3];
    if (b.cost === 'modelled') cost = m[2] == null ? null : (m[2]*p.i + m[3]*p.o) / 1e6;
    return { key:r[0]+'|'+r[1], model:r[0], name:m[0], v:m[1], effort:r[1], score:r[2], cost, modelled:b.cost==='modelled', bench:bid, restricted:!!RESTRICTED[r[0]] };
  }).filter(x => x.cost != null);
}
function unplaced(bid) {
  const b = B[bid], extra = b.cost === 'modelled' ? b.rows.filter(r => M[r[0]][2] == null).map(r => [M[r[0]][0], r[2]]) : [];
  return (b.noCost || []).concat(extra);
}
function frontier(pts) {
  const sorted = pts.filter(p => !p.restricted).sort((a,b) => a.cost - b.cost || b.score - a.score), out = [];
  let best = -Infinity;
  for (const p of sorted) if (p.score > best) { out.push(p); best = p.score; }
  return out;
}
function premium(bid) {
  const pts = points(bid).filter(p => !p.restricted); if (pts.length < 2) return null;
  const top = Math.max(...pts.map(p => p.score));
  const best = pts.filter(p => p.score === top).sort((a,b) => a.cost-b.cost)[0];
  const floor = B[bid].elo ? top-100 : top*.9;
  const cheap = pts.filter(p => p.score >= floor).sort((a,b) => a.cost-b.cost)[0];
  return { ratio:best.cost/cheap.cost, best, cheap };
}
function niceTicks(lo, hi, n=5) {
  const raw=(hi-lo)/n, mag=10**Math.floor(Math.log10(raw)), step=[1,2,5,10].map(s=>s*mag).find(s=>s>=raw);
  const out=[]; for(let v=Math.ceil(lo/step)*step; v<=hi+1e-9; v+=step) out.push(+v.toFixed(6)); return out;
}
function logTicks(lo, hi, wide) {
  const out=[]; for(let e=Math.floor(Math.log10(lo)); e<=Math.ceil(Math.log10(hi)); e++) for(const m of (wide?[1,2,5]:[1])) { const v=m*10**e; if(v>=lo*.999&&v<=hi*1.001) out.push(v); }
  if (out.length<3) for(let e=Math.floor(Math.log10(lo)); e<=Math.ceil(Math.log10(hi)); e++) for(const m of [1,2,3,5,7]) { const v=m*10**e; if(v>=lo*.999&&v<=hi*1.001&&!out.includes(v)) out.push(v); }
  return out.sort((a,b)=>a-b);
}
const tickMoney = v => v>=1 ? '$'+(+v.toFixed(2)) : v>=.01 ? (+(v*100).toFixed(1))+'¢' : (+(v*100).toFixed(2))+'¢';

/* ---------- The chart. Dots keep their DOM node across renders so they fly between positions. ---------- */
function drawChart(host, pts, o={}) {
  const W = Math.max(280, host.clientWidth || 700), narrow = W < 560;
  const H = o.height || (narrow ? 360 : 470);
  const rugW = o.rug && o.rug.length ? (narrow ? 26 : 150) : 0;
  const m = { l:narrow?38:48, r:12+rugW, t:16, b:44 };
  const all = o.domain || pts;
  const costs = all.map(p=>p.cost), scores = all.map(p=>p.score).concat(o.ref?[o.ref.y]:[]).concat((o.rug||[]).map(r=>r[1]));
  let xLo=Math.min(...costs)/1.5, xHi=Math.max(...costs)*1.5;
  if (xHi/xLo<4) { const g=Math.sqrt(xHi*xLo); xLo=g/2.2; xHi=g*2.2; }
  const sLo=Math.min(...scores), sHi=Math.max(...scores), pad=Math.max((sHi-sLo)*.1, o.elo?20:2);
  const yLo=Math.max(0, sLo-pad), yHi=o.elo?sHi+pad:(sHi+pad>100?Math.max(100,sHi)+1:sHi+pad);
  const x=v=>m.l+(Math.log10(v)-Math.log10(xLo))/(Math.log10(xHi)-Math.log10(xLo))*(W-m.l-m.r);
  const y=v=>H-m.b-(v-yLo)/(yHi-yLo)*(H-m.t-m.b);
  let svg=host.querySelector('svg.main');
  if(!svg){ svg=document.createElementNS('http://www.w3.org/2000/svg','svg'); svg.setAttribute('class','main'); host.appendChild(svg);
    svg.innerHTML='<g class="axis-layer"></g><g class="lines-layer"></g><g class="pts-layer"></g><g class="top-layer"></g>'; svg._nodes=new Map(); }
  svg.setAttribute('viewBox',`0 0 ${W} ${H}`); svg.setAttribute('height',H);
  svg.setAttribute('role','img'); svg.setAttribute('aria-label',o.aria||'Cost against benchmark score');
  const axis=svg.querySelector('.axis-layer'), lines=svg.querySelector('.lines-layer'), layer=svg.querySelector('.pts-layer'), top=svg.querySelector('.top-layer');
  const xt=logTicks(xLo,xHi,!narrow||xHi/xLo<100), yt=niceTicks(yLo,yHi,narrow?4:6);
  axis.innerHTML=`<g class="grid">${yt.map(v=>`<line x1="${m.l}" x2="${W-m.r}" y1="${y(v)}" y2="${y(v)}"/>`).join('')}</g>
    <g class="axis">${yt.map(v=>`<text x="${m.l-7}" y="${y(v)+4}" text-anchor="end">${+v.toFixed(2)}</text>`).join('')}
    ${xt.map(v=>`<text x="${x(v)}" y="${H-m.b+17}" text-anchor="middle">${tickMoney(v)}</text>`).join('')}</g>
    <text class="axis-title" x="${m.l}" y="${H-7}">${esc(o.xLabel||'Cost per task')}, log scale</text>
    <text class="axis-title" x="${m.l}" y="11">${esc(o.yLabel||'Score')}</text>`;

  // Lines: effort ladders, then the frontier.
  const visible=pts.filter(p=>!(o.off&&o.off.has(p.v)));
  const showLadders=o.ladder&&o.effort==='all';
  const ladderSvg=showLadders?[...new Set(visible.map(p=>p.model))].map(id=>{
    const r=visible.filter(p=>p.model===id).sort((a,b)=>a.cost-b.cost); if(r.length<2) return '';
    return `<path class="ladder" stroke="${V[r[0].v][1]}" d="M${r.map(p=>x(p.cost).toFixed(1)+','+y(p.score).toFixed(1)).join('L')}"/>`; }).join(''):'';
  const fr=frontier(visible), frKeys=new Set(fr.map(p=>p.key));
  const frD=fr.length>1?`M${fr.map(p=>x(p.cost).toFixed(1)+','+y(p.score).toFixed(1)).join('L')}`:'';
  const frSig=(o.sig||'')+frD;
  if(lines.dataset.sig!==frSig){ lines.dataset.sig=frSig;
    lines.innerHTML=ladderSvg+(frD?`<path class="frontier" pathLength="1" d="${frD}"/>`:''); }

  // Dots with node reuse.
  const nodes=svg._nodes, used=new Set(); const next=new Map();
  for(const p of pts){
    let n=nodes.get(p.key);
    if(!n||used.has(n)) n=[...nodes.entries()].find(([k,v])=>k.split('|')[0]===p.model&&!used.has(v)&&!pts.some(q=>q.key===k))?.[1];
    let fresh=false;
    if(!n){ n=document.createElementNS('http://www.w3.org/2000/svg','g'); n.innerHTML='<circle class="hit" r="14"/><path class="mark"/>'; layer.appendChild(n); fresh=true; n.style.transform=`translate(${x(p.cost)}px,${H-m.b}px)`; n.style.opacity='0'; n.setAttribute('tabindex',o.interactive?'0':'-1');
      if(o.onHover){ n.addEventListener('pointerenter',e=>o.onHover(n._p,n,e)); n.addEventListener('pointerleave',()=>o.onHover(null)); n.addEventListener('focus',e=>o.onHover(n._p,n,e)); n.addEventListener('blur',()=>o.onHover(null)); }
      if(o.onPick){ n.addEventListener('click',()=>o.onPick(n._p)); n.addEventListener('keydown',e=>{ if(e.key==='Enter'||e.key===' '){ e.preventDefault(); o.onPick(n._p); } }); } }
    used.add(n); n._p=p; next.set(p.key,n);
    n.querySelector('.mark').setAttribute('d',shape(p.v, narrow?5.8:6.5)); n.querySelector('.mark').setAttribute('fill',V[p.v][1]); n.querySelector('.mark').style.stroke=p.restricted?'var(--gold)':p.modelled?V[p.v][1]:'';
    const overB=o.budget!=null&&p.cost>o.budget, offV=o.off&&o.off.has(p.v);
    let cls='pt'+(p.modelled?' hollow':'')+(p.restricted?' restricted':'')+(overB?' over':'')+(o.sel===p.key?' sel':'');
    if(o.effort&&o.effort!=='all'&&p.effort!==o.effort&&p.effort!=='default') cls+=' hide';
    if(offV) cls+=' hide';
    n.setAttribute('class',cls);
    n.setAttribute('aria-label',`${p.name} ${eff(p.effort)}: ${p.score} at ${money(p.cost)}${p.restricted?', not generally available':''}`);
    n.style.transform=`translate(${x(p.cost).toFixed(1)}px,${y(p.score).toFixed(1)}px)`;
    if(fresh){ requestAnimationFrame(()=>{ n.style.opacity=''; }); }
  }
  for(const [k,n] of nodes) if(![...next.values()].includes(n)){ n.remove(); }
  svg._nodes=next;

  // Top layer: reference, budget, rug, ring, labels.
  let t='';
  if(o.ref) t+=`<line class="ref-line" x1="${m.l}" x2="${W-m.r}" y1="${y(o.ref.y)}" y2="${y(o.ref.y)}"/><text class="ref-label" x="${W-m.r-4}" y="${y(o.ref.y)+15}" text-anchor="end">${esc(o.ref.label)}</text>`;
  if(o.budget!=null){ const bx=Math.min(x(o.budget),W-m.r); t=`<rect class="budget-shade" x="${bx}" y="${m.t}" width="${Math.max(0,W-m.r-bx)}" height="${H-m.t-m.b}"/><line class="budget-line" x1="${bx}" x2="${bx}" y1="${m.t}" y2="${H-m.b}"/><text class="budget-tag" x="${bx<W*.75?bx+6:bx-6}" y="${m.t+13}" text-anchor="${bx<W*.75?'start':'end'}">${money(o.budget)}</text>`+t; }
  if(rugW){ const rx=W-rugW+4;
    t+=`<text class="rug-title" x="${rx}" y="11">${narrow?'':'Score only, no cost'}</text>`;
    const placed=[]; [...o.rug].sort((a,b)=>b[1]-a[1]).forEach(r=>{ const ry=y(r[1]); let ly=ry+4; while(placed.some(v=>Math.abs(v-ly)<13)) ly+=13; placed.push(ly);
      t+=`<line class="rug" x1="${rx}" x2="${rx+10}" y1="${ry}" y2="${ry}"><title>${esc(r[0])}: ${r[1]}</title></line>`; if(!narrow) t+=`<text class="axis-title" style="font-size:10.5px" x="${rx+15}" y="${ly}">${esc(r[0].length>21?r[0].slice(0,20)+'…':r[0])} ${r[1]}</text>`; }); }
  if(o.ring) t+=`<circle class="ring" cx="${x(o.ring.cost)}" cy="${y(o.ring.score)}" r="12"/>`;
  if(o.ring2) t+=`<circle class="ring" style="stroke:var(--stage-hi)" cx="${x(o.ring2.cost)}" cy="${y(o.ring2.score)}" r="12"/>`;
  // Greedy labels: selected first, then frontier, then explicit extras.
  const placedBoxes=[]; const candidates=[];
  const seen=new Set(); const push=p=>{ if(p&&!seen.has(p.key)&&!(o.off&&o.off.has(p.v))&&!(o.effort&&o.effort!=='all'&&p.effort!==o.effort&&p.effort!=='default')){ seen.add(p.key); candidates.push(p); } };
  (o.labelFirst||[]).forEach(push); [...fr].reverse().forEach(push); if(o.labelAll) pts.forEach(push);
  const maxLabels=narrow?6:11;
  candidates.slice(0,maxLabels).forEach(p=>{
    const showEffort=p.effort!=='default'&&(o.effort==='all'||!o.effort);
    const txt=shortName(p.name)+(p.restricted?' (restricted)':showEffort?' '+eff(p.effort):''); const tw=txt.length*5.9+4, px=x(p.cost), py=y(p.score);
    const tries=[[10,16,'start',px+10,py+4],[11,4,'start',px+11,py-9],[-11,4,'end',px-11-tw,py-9],[0,-12,'middle',px-tw/2,py-24],[0,20,'middle',px-tw/2,py+8]];
    for(const [dx,dy,anc,bx,by] of tries){ const box=[bx,by,bx+tw,by+14];
      if(box[0]<m.l||box[2]>W-m.r||box[1]<m.t-2||box[3]>H-m.b) continue;
      if(placedBoxes.some(b=>box[0]<b[2]&&box[2]>b[0]&&box[1]<b[3]&&box[3]>b[1])) continue;
      placedBoxes.push(box); t+=`<text class="pt-label" x="${px+dx}" y="${py+dy}" text-anchor="${anc}">${esc(txt)}</text>`; break; }
  });
  top.innerHTML=t;
  svg._scale={x,y,xLo,xHi,m,W,H};
  return svg._scale;
}
const shortName=n=>n.replace('Claude ','').replace('Gemini ','Gem ').replace('DeepSeek ','DS ');

/* ---------- Atlas ---------- */
function benchXLabel(b) { return (b.cost==='modelled'?'Estimated cost per ':b.cost==='chartread'?'Cost per ':'Cost per ')+b.per; }
const costChip={ measured:['measured','Cost measured'], chartread:['chartread','Cost read from chart'], modelled:['modelled','Cost estimated from list price'] };
function sparkFor(bid) {
  const pts=points(bid); const fr=frontier(pts); if(fr.length<1) return '';
  const lo=Math.log10(Math.min(...pts.map(p=>p.cost))), hi=Math.log10(Math.max(...pts.map(p=>p.cost))), sLo=Math.min(...pts.map(p=>p.score)), sHi=Math.max(...pts.map(p=>p.score));
  const px=c=>4+(hi===lo?.5:(Math.log10(c)-lo)/(hi-lo))*132, py=s=>26-(sHi===sLo?.5:(s-sLo)/(sHi-sLo))*22;
  const d='M'+fr.map(p=>px(p.cost).toFixed(1)+','+py(p.score).toFixed(1)).join('L');
  const dots=pts.map(p=>`<circle class="spark-dot" r="1.4" cx="${px(p.cost).toFixed(1)}" cy="${py(p.score).toFixed(1)}"/>`).join('');
  return `<svg viewBox="0 0 140 30" aria-hidden="true">${dots}${fr.length>1?`<path class="spark${B[bid].cost==='modelled'?' dashed':''}" d="${d}"/>`:''}</svg>`;
}
function renderAtlasShell() {
  $('#view-atlas').innerHTML=`
    <div class="cats" id="cats" role="group" aria-label="Categories"></div>
    <div class="layout">
      <div class="stage" id="stage">
        <div class="stage-head">
          <div class="seg" id="bench-seg" role="group" aria-label="Benchmark"></div>
          <div class="legend" id="legend"></div>
        </div>
        <div class="plot" id="plot"><div class="tip" id="tip" hidden></div></div>
        <div class="controls" id="controls"></div>
      </div>
      <aside class="side" id="side"></aside>
    </div>
    <div id="below" style="display:grid;gap:1rem"></div>
    <section style="display:grid;gap:.6rem">
      <div class="section-head"><h2>Insights</h2><p id="ins-count"></p></div>
      <div class="insights" id="insights"></div>
    </section>`;
}
function renderCats() {
  $('#cats').innerHTML=CATS.map(c=>{ const bid=c.benches[0], pr=premium(bid), b=B[bid];
    const tag=b.cost==='modelled'?'estimated':b.cost==='chartread'?'chart-read':'measured';
    return `<button class="cat" data-cat="${c.id}" aria-pressed="${S.cat===c.id}"><b>${esc(c.name)}</b>${sparkFor(bid)}<small><span>${!pr?'few rows':pr.ratio<1.05?'No premium':'Last 10%: ×'+(pr.ratio>=10?pr.ratio.toFixed(0):pr.ratio.toFixed(1))}</span><span>${tag}</span></small></button>`; }).join('');
  $('#cats').querySelectorAll('.cat').forEach(el=>el.onclick=()=>{ stopPlay(); S.cat=el.dataset.cat; S.bench=CATS.find(c=>c.id===S.cat).benches[0]; resetBench(); renderAtlas(); });
}
function resetBench(){ S.effort='all'; S.budgetT=null; S.sel=null; }
function bounds(pts){ const c=pts.map(p=>p.cost); return [Math.min(...c)*.8, Math.max(...c)*1.2]; }
function budgetValue(pts){ const [lo,hi]=bounds(pts); if(S.budgetT==null) return 10**(Math.log10(lo)+.8*(Math.log10(hi)-Math.log10(lo))); return 10**(Math.log10(lo)+S.budgetT*(Math.log10(hi)-Math.log10(lo))); }
function currentPts() { return points(S.bench).filter(p=>!S.off.has(p.v)&&(S.effort==='all'||p.effort===S.effort||p.effort==='default')); }
function bestUnder(pts,budget){ const ok=pts.filter(p=>p.cost<=budget&&!p.restricted); if(!ok.length) return null; const top=Math.max(...ok.map(p=>p.score)); return ok.filter(p=>p.score===top).sort((a,b)=>a.cost-b.cost)[0]; }

function renderChart() {
  const b=B[S.bench], all=points(S.bench), pts=currentPts(), budget=budgetValue(all), under=bestUnder(pts,budget);
  const drawn=S.effort==='all'?all:all.filter(p=>p.effort===S.effort||p.effort==='default');
  const sel=S.sel&&all.find(p=>p.key===S.sel);
  const scale=drawChart($('#plot'),drawn,{ domain:all, interactive:true, budget, off:S.off, effort:S.effort, ladder:!!b.ladder, sel:S.sel, ring:under, ref:b.ref, rug:unplaced(S.bench), elo:b.elo, yLabel:b.yLabel, xLabel:benchXLabel(b), sig:S.bench+S.effort+S.profile,
    labelFirst:[sel,under,...all.filter(p=>p.restricted)].filter(Boolean), aria:`${b.name}: cost against score`, labelAll:all.length<=8,
    onHover:showTip, onPick:p=>{ S.sel=p.key; checkInsights({b:S.bench,m:p.model,e:p.effort}); renderChart(); renderSide(); } });
  return {pts,budget,under,scale};
}
function showTip(p,node) {
  const tip=$('#tip'); if(!p){ tip.hidden=true; return; }
  const b=B[p.bench], ratio=b.elo?'':`<div>${money(p.cost/Math.max(p.score,.01))} per point</div>`;
  tip.innerHTML=`<b>${esc(p.name)}${p.effort!=='default'?' · '+esc(eff(p.effort)):''}</b><div class="num">${p.score} ${esc(b.unit)}</div><div class="num">${money(p.cost)} per ${esc(b.per)}${p.modelled?' (estimate)':b.cost==='chartread'?' (read from chart)':''}</div>${ratio}${p.restricted?'<div>Not generally available</div>':''}`;
  tip.hidden=false;
  const r=$('#plot').getBoundingClientRect(), n=node.querySelector('.mark').getBoundingClientRect();
  const tw=tip.offsetWidth, th=tip.offsetHeight; let left=n.left-r.left+16, top=n.top-r.top-th-6;
  if(left+tw>r.width) left=n.left-r.left-tw-10; if(left<0) left=4; if(top<0) top=n.bottom-r.top+8;
  tip.style.left=left+'px'; tip.style.top=top+'px';
}
function renderControls() {
  const b=B[S.bench], all=points(S.bench), [lo,hi]=bounds(all), budget=budgetValue(all);
  const t=(Math.log10(budget)-Math.log10(lo))/(Math.log10(hi)-Math.log10(lo));
  let html=`<div class="ctrl"><label for="budget">Budget per ${esc(b.per)}: <output id="budget-out">${money(budget)}</output></label><input id="budget" type="range" min="0" max="1000" value="${Math.round(t*1000)}"></div>`;
  if(b.ladder) html+=`<div class="ctrl"><span>${b.ladder===RUNGS?'Effort rung (levels differ by model)':'Effort level'}</span><div class="ctrl-row"><div class="seg" id="effort-seg" role="group" aria-label="Effort">${['all',...b.ladder].map(e=>`<button data-e="${e}" aria-pressed="${S.effort===e}">${e==='all'?'All':esc(eff(e)).replace('rung ','R')}</button>`).join('')}</div><button class="play" id="play">${S.playing?'Stop':'Play ladder'}</button></div></div>`;
  else if(b.cost==='modelled') html+=`<div class="ctrl"><span>Estimate assumes this token volume per task</span><div class="seg" id="profile-seg" role="group" aria-label="Token profile">${PROFILES.map(p=>`<button data-p="${p.id}" aria-pressed="${S.profile===p.id}">${p.label}: ${p.i>=1e6?p.i/1e6+'M':p.i/1000+'k'} in, ${p.o/1000}k out</button>`).join('')}</div></div>`;
  $('#controls').innerHTML=html;
  $('#budget').oninput=e=>{ stopPlay(); S.budgetT=e.target.value/1000; const v=budgetValue(all); $('#budget-out').textContent=money(v); renderChart(); renderPicks(); };
  $('#effort-seg')?.querySelectorAll('button').forEach(el=>el.onclick=()=>{ stopPlay(); S.effort=el.dataset.e; syncEffort(); });
  if($('#play')) $('#play').onclick=()=>togglePlay();
  $('#profile-seg')?.querySelectorAll('button').forEach(el=>el.onclick=()=>{ S.profile=el.dataset.p; S.budgetT=null; renderAtlas(); });
}
function syncEffort(){ $('#effort-seg')?.querySelectorAll('button').forEach(el=>el.setAttribute('aria-pressed',S.effort===el.dataset.e)); renderChart(); renderPicks(); }
function togglePlay(){ if(S.playing){ stopPlay(); return; }
  const lad=B[S.bench].ladder; let i=-1; const step=()=>{ i=(i+1)%lad.length; S.effort=lad[i]; syncEffort(); };
  step(); S.playing=setInterval(step,1300); $('#play').textContent='Stop'; }
function stopPlay(){ if(S.playing){ clearInterval(S.playing); S.playing=null; if($('#play')) $('#play').textContent='Play ladder'; } }
function renderLegend(){ const used=[...new Set(points(S.bench).map(p=>p.v))]; const b=B[S.bench];
  $('#legend').innerHTML=used.map(v=>`<button data-v="${v}" aria-pressed="${!S.off.has(v)}" title="Show or hide ${V[v][0]}">${glyph(v,b.cost==='modelled')}${V[v][0]}</button>`).join('')+`<span>${b.cost==='modelled'?'Hollow: cost is an estimate':b.cost==='chartread'?'Filled: cost read from chart':'Filled: cost measured'}</span><span>White line: frontier</span>${points(S.bench).some(p=>p.restricted)?'<span>Gold dashes: not generally available</span>':''}`;
  $('#legend').querySelectorAll('button').forEach(el=>el.onclick=()=>{ const v=el.dataset.v; S.off.has(v)?S.off.delete(v):S.off.add(v); S.budgetT=null; renderLegend(); renderChart(); renderPicks(); renderControls(); }); }
function renderPicks(){ const host=$('#picks'); if(!host) return; const b=B[S.bench], pts=currentPts(), budget=budgetValue(points(S.bench)), under=bestUnder(pts,budget), fr=frontier(pts);
  if(!pts.length){ host.innerHTML='<p class="detail-quote">No rows match this filter.</p>'; return; }
  const buy=pts.filter(p=>!p.restricted); if(!buy.length){ host.innerHTML='<p class="detail-quote">No generally available rows match this filter.</p>'; return; }
  const top=[...buy].sort((a,c)=>c.score-a.score||a.cost-c.cost)[0], floor=b.elo?top.score-100:top.score*.9, cheap=buy.filter(p=>p.score>=floor).sort((a,c)=>a.cost-c.cost)[0], locked=pts.filter(p=>p.restricted);
  const row=(lab,p,sub)=>p?`<div class="pick"><span class="lab">${lab}</span><b>${esc(shortName(p.name))}${p.effort!=='default'?' '+esc(eff(p.effort)):''}</b><span class="num">${p.score} · ${money(p.cost)}</span>${sub?`<small>${sub}</small>`:''}</div>`:`<div class="pick"><span class="lab">${lab}</span><small>Nothing fits this budget.</small></div>`;
  host.innerHTML=row('Best under your budget',under)+row('Highest score you can buy',top)+row(b.elo?'Cheapest within 100 Elo of the top':'Cheapest within 10% of the top',cheap,cheap&&top.cost>cheap.cost?`The top score costs ×${(top.cost/cheap.cost).toFixed(1)} as much.`:'')+(locked.length?`<p class="detail-quote">Excluded here: ${locked.map(p=>esc(p.name)+' ('+p.score+')').join(', ')}. ${esc(RESTRICTED[locked[0].model])}</p>`:'');
  const bars=$('#bars'); if(bars){ if(b.elo){ bars.innerHTML='<p class="detail-quote">Ratings do not divide into a meaningful cost per point. Use the chart.</p>'; return; }
    const list=pts.filter(p=>p.score>0).map(p=>({p,v:p.cost/p.score})).sort((a,c)=>a.v-c.v), keep=frontier(pts).map(p=>p.key), show=list.filter(x=>keep.includes(x.p.key)).slice(0,10), mx=Math.max(...show.map(x=>x.v));
    bars.innerHTML=show.map(x=>`<div class="barrow"><span title="${esc(x.p.name)} ${esc(x.p.effort)}">${esc(shortName(x.p.name))}${x.p.effort!=='default'?' '+esc(eff(x.p.effort)):''}</span><div class="bartrack"><div class="barfill" style="width:${Math.max(1.5,x.v/mx*100)}%;background:${V[x.p.v][1]}"></div></div><span class="num">${money(x.v)}</span></div>`).join(''); }
}
function ageDays(d){ return Math.round((new Date(AS_OF)-new Date(d))/864e5); }
function renderSide() {
  const b=B[S.bench], c=costChip[b.cost], sel=S.sel&&points(S.bench).find(p=>p.key===S.sel);
  $('#side').innerHTML=`
    ${sel?`<div class="card"><span class="lab">Selected</span><h3>${esc(sel.name)}${sel.effort!=='default'?' · '+esc(eff(sel.effort)):''}</h3><div class="num">${sel.score} ${esc(b.unit)} · ${money(sel.cost)} per ${esc(b.per)}</div><p class="detail-quote">List price ${M[sel.model][2]!=null?`$${M[sel.model][2]} in / $${M[sel.model][3]} out per million tokens`:'not in our price set'}.${sel.restricted?' '+esc(RESTRICTED[sel.model]):frontier(points(S.bench)).some(p=>p.key===sel.key)?' On the frontier: no row is both cheaper and better.':dominator(sel)}</p>${releaseOf(sel.model)?`<p style="font-size:.82rem"><span class="lab">Release</span><br>${fmtDate(releaseOf(sel.model).d)}: ${ANCHOR(releaseOf(sel.model).u,releaseOf(sel.model).t)}</p>`:''}</div>`:''}
    <div class="card"><span class="lab">Your options</span><div class="picks" id="picks"></div></div>
    <div class="card"><span class="lab">Cost per point, frontier rows</span><div class="bars" id="bars"></div></div>
    <div class="card"><h3>${ANCHOR(benchHome(S.bench),b.name)}</h3>
      <div class="chips"><span class="chip ${c[0]}">${c[1]}</span><span class="chip">${REP[b.rep]}</span><span class="chip">${esc(b.date)}</span>${ageDays(b.date)>90?'<span class="chip stale">Older than 90 days</span>':''}</div>
      <p style="font-size:.88rem">${linked(b.what)}</p>${b.costMethod?`<p class="detail-quote">${esc(b.costMethod)}</p>`:''}
      <ul class="notes">${b.notes.map(n=>`<li>${linked(n)}</li>`).join('')}</ul>
      <div class="refs"><span class="lab">The benchmark</span><div>${REFS[S.bench].map(r=>ANCHOR(r[1],r[0])).join(' · ')}</div><span class="lab" style="margin-top:.3rem">Numbers read from</span><div>${[[b.url,b.rep==='own'?'Study results':'Results'],...(b.urls||[])].map(u=>ANCHOR(u[0],u[1])).join(' · ')}</div></div>
      ${newerThan(S.bench)}
    </div>`;
  renderPicks();
}
function newerThan(id){ const b=B[id], on=new Set(b.rows.map(r=>r[0])), labels=(b.noCost||[]).map(r=>r[0]);
  const missing=[...CURRENT].filter(m=>!on.has(m)&&!labels.some(l=>l.includes(M[m][0].replace('Claude ','')))).map(m=>{ const r=releaseOf(m); return r?ANCHOR(r.u,M[m][0]):esc(M[m][0]); });
  return missing.length?`<p class="detail-quote"><b>Current models not on this benchmark yet:</b> ${missing.join(', ')}.</p>`:''; }
function dominator(sel){ const d=points(S.bench).filter(p=>p.cost<=sel.cost&&p.score>=sel.score&&p.key!==sel.key).sort((a,c)=>a.cost-c.cost)[0]; return d?` Beaten by ${esc(shortName(d.name))}${d.effort!=='default'?' '+esc(eff(d.effort)):''}: ${d.score} at ${money(d.cost)}.`:''; }
function renderBelow(){ const c=CATS.find(x=>x.id===S.cat), b=B[S.bench], rows=points(S.bench).sort((a,z)=>z.score-a.score||a.cost-z.cost);
  const ev=c.evidence?EVIDENCE[c.evidence]:null;
  $('#below').innerHTML=(ev?`<details class="card" id="evidence-box"><summary>${esc(ev.title)}</summary><div style="display:grid;gap:.5rem;font-size:.88rem;margin-top:.5rem">${ev.body}</div></details>`:'')+
   `<details class="card"><summary>Data table for ${esc(b.short)}</summary><div class="tbl-scroll" style="margin-top:.5rem"><table><thead><tr><th>Model</th><th>Setting</th><th class="r">Score</th><th class="r">Cost per ${esc(b.per)}</th></tr></thead><tbody>${rows.map(p=>`<tr><td>${esc(p.name)}</td><td>${esc(eff(p.effort))}</td><td class="r num">${p.score}</td><td class="r num">${money(p.cost)}</td></tr>`).join('')}${unplaced(S.bench).map(r=>`<tr><td>${esc(r[0])}</td><td></td><td class="r num">${r[1]}</td><td class="r">no cost</td></tr>`).join('')}</tbody></table></div></details>`;
  const box=$('#evidence-box'); if(box) box.addEventListener('toggle',()=>{ if(box.open) checkInsights({evidence:c.evidence}); });
}
function renderAtlas() {
  renderCats();
  const c=CATS.find(x=>x.id===S.cat);
  $('#bench-seg').innerHTML=c.benches.map(id=>`<button data-b="${id}" aria-pressed="${S.bench===id}">${esc(B[id].short)}</button>`).join('');
  $('#bench-seg').querySelectorAll('button').forEach(el=>el.onclick=()=>{ stopPlay(); S.bench=el.dataset.b; resetBench(); renderAtlas(); });
  renderLegend(); renderChart(); renderControls(); renderSide(); renderBelow(); renderInsights();
  checkInsights({b:S.bench});
}
function checkInsights(ev){
  for(const i of INSIGHTS){ if(S.ins.has(i.id)) continue; const o=i.on;
    if(ev.evidence){ if(o.evidence===ev.evidence) unlock(i); continue; }
    if(o.evidence) continue;
    if(o.b!==ev.b) continue;
    if(o.m&&o.m!==ev.m) continue;
    if(o.e&&o.e!==ev.e) continue;
    if(!ev.m&&o.m) continue;
    unlock(i); }
}
function unlock(i){ S.ins.add(i.id); store.set('ins',[...S.ins]); updateHud(); renderInsights();
  const host=$('#toast-host'); host.innerHTML=`<div class="toast pop"><small>Insight ${S.ins.size} of ${INSIGHTS.length}</small><b>${esc(i.t)}</b><p>${linked(i.x)}</p></div>`;
  clearTimeout(unlock._t); unlock._t=setTimeout(()=>{ host.innerHTML=''; },6500); }
function renderInsights(){ const host=$('#insights'); if(!host) return;
  $('#ins-count').textContent=`${S.ins.size} of ${INSIGHTS.length} found. Click dots and open benchmarks to discover the rest.`;
  host.innerHTML=INSIGHTS.map(i=>S.ins.has(i.id)?`<div class="ins"><b>${esc(i.t)}</b><p>${linked(i.x)}</p></div>`:`<div class="ins locked"><span class="lab">Locked</span><p>${linked(i.hint)}</p><button data-go="${i.id}">Go to this chart</button></div>`).join('');
  host.querySelectorAll('[data-go]').forEach(el=>el.onclick=()=>{ const i=INSIGHTS.find(x=>x.id===el.dataset.go); stopPlay(); S.cat=i.cat; S.bench=i.b; resetBench(); renderAtlas(); $('#cats').scrollIntoView({behavior:'smooth',block:'start'}); }); }
function updateHud(){ $('#hud-ins').textContent=`${S.ins.size}/${INSIGHTS.length}`; $('#hud-best').textContent=S.best==null?'none':S.best+'/1000'; }

/* ---------- Missions ---------- */
const G={ i:0, total:0, log:[], picked:null };
function rowOf(bid,m,e){ return points(bid).find(p=>p.model===m&&p.effort===e); }
function scoreMission(ms,p){ const opts=ms.opts.map(o=>rowOf(ms.bench,o[0],o[1])); const ok=opts.filter(o=>o.cost<=ms.budget);
  if(p.cost>ms.budget) return {pts:0,stars:0,over:true,opts,best:bestUnder(ok,ms.budget)};
  const top=Math.max(...ok.map(o=>o.score)), lo=Math.min(...opts.map(o=>o.score)), best=bestUnder(ok,ms.budget);
  let pts=top===lo?100:Math.round(100*(p.score-lo)/(top-lo));
  if(p.score===top&&p.key!==best.key) pts=80; pts=Math.max(pts,p.score===lo?10:pts);
  return {pts,stars:pts>=100?3:pts>=60?2:pts>=25?1:0,over:false,opts,best}; }
function renderMissions(){ const v=$('#view-missions');
  if(G.i>=MISSIONS.length) return renderEnd(v);
  const ms=MISSIONS[G.i], b=B[ms.bench];
  const prog=MISSIONS.map((_,k)=>`<i class="${k<G.i?'done':k===G.i?'now':''}"></i>`).join('');
  v.innerHTML=`<div class="progress" aria-label="Mission ${G.i+1} of ${MISSIONS.length}">${prog}</div>
    <div class="mission-grid"><div class="mission-card" id="mission-left">
      <span class="lab">Mission ${G.i+1} of ${MISSIONS.length} · ${G.total} points · ${benchLink(ms.bench)}</span>
      <h2>${esc(ms.t)}</h2><p>${linked(ms.brief)}</p>
      ${ms.check?'<span class="chip" style="justify-self:start">Evidence check</span>':`<span class="budget-chip">Budget ${money(ms.budget)} per ${esc(b.per)}</span>`}
      <div class="opts" id="opts"></div><div class="result" id="result"></div></div>
      <div class="stage" id="mission-stage"><div class="plot" id="mplot" style="min-height:22rem;display:grid;place-items:center"><p style="color:var(--stage-muted);text-align:center;max-width:30ch" id="mhold">${ms.check?'The chart appears after your answer.':'Scores and real costs stay hidden until you commit. List prices are all you get, as on a vendor page.'}</p></div></div></div>`;
  const host=$('#opts');
  if(ms.check){ host.innerHTML=ms.choices.map((c,k)=>`<button class="opt" data-k="${k}" style="grid-template-columns:minmax(0,1fr)"><span>${esc(c[0])}</span></button>`).join('');
    host.querySelectorAll('.opt').forEach(el=>el.onclick=()=>answerCheck(ms,+el.dataset.k)); return; }
  host.innerHTML=ms.opts.map((o,k)=>{ const m=M[o[0]]; return `<button class="opt" data-k="${k}">${glyph(m[1])}<span><b>${esc(m[0])}</b>${o[1]!=='default'?' · '+esc(eff(o[1])):''}<small>${m[2]!=null?`List $${m[2]} / $${m[3]} per million`:'List price not in our set'}</small></span><span class="val"></span></button>`; }).join('');
  host.querySelectorAll('.opt').forEach(el=>el.onclick=()=>answer(ms,+el.dataset.k));
}
function revealChart(ms,o={}){ $('#mplot').removeAttribute('style'); $('#mplot').innerHTML=''; const b=B[ms.bench], all=points(ms.bench);
  drawChart($('#mplot'),all,{ budget:ms.budget, ladder:!!b.ladder, effort:'all', elo:b.elo, yLabel:b.yLabel, xLabel:benchXLabel(b), ring:o.ring, ring2:o.ring2, labelFirst:o.labels||[], ref:b.ref, height:400, sig:'m'+G.i, aria:b.name }); }
function answer(ms,k){ const o=ms.opts[k], p=rowOf(ms.bench,o[0],o[1]), r=scoreMission(ms,p), b=B[ms.bench]; G.total+=r.pts; G.log.push(r.pts);
  $('#opts').querySelectorAll('.opt').forEach((el,i)=>{ el.disabled=true; const q=r.opts[i]; el.querySelector('.val').innerHTML=`${q.score}<br>${money(q.cost)}`;
    if(i===k) el.classList.add('picked'); if(q.cost>ms.budget) el.classList.add('bust'); if(r.best&&q.key===r.best.key) el.classList.add('best'); });
  $('#result').innerHTML=`<div class="result-head pop"><span class="result-pts">+${r.pts}</span><span class="stars" aria-label="${r.stars} of 3 stars">${'★'.repeat(r.stars)}${'☆'.repeat(3-r.stars)}</span><b>${r.over?'Over budget':r.pts>=100?'Best available pick':r.pts>=60?'Close to the best':'Better options existed'}</b></div><p style="font-size:.92rem">${linked(ms.lesson)}</p><p class="detail-quote">Measured on ${benchLink(ms.bench)} (${esc(REP[b.rep].toLowerCase())}, ${esc(b.date)}, ${ANCHOR(b.url,'results')}). Green marks the best in-budget option, red marks options over budget.</p><button class="primary" id="next">${G.i+1<MISSIONS.length?'Next mission':'See results'}</button>`;
  revealChart(ms,{ring:r.best,ring2:r.best&&p.key===r.best.key?null:p,labels:[p,r.best].filter(Boolean)});
  $('#next').onclick=()=>{ G.i++; renderMissions(); window.scrollTo({top:$('.bar').offsetTop,behavior:'smooth'}); }; $('#next').focus({preventScroll:true}); }
function answerCheck(ms,k){ const b=B[ms.bench], ok=ms.choices[k][1], pts=ok?100:0; G.total+=pts; G.log.push(pts);
  $('#opts').querySelectorAll('.opt').forEach((el,i)=>{ el.disabled=true; if(i===k) el.classList.add('picked'); el.classList.add(ms.choices[i][1]?'best':'x'); });
  $('#result').innerHTML=`<div class="result-head pop"><span class="result-pts">+${pts}</span><span class="stars" aria-label="${ok?3:0} of 3 stars">${ok?'★★★':'☆☆☆'}</span><b>${ok?'Correct':'Not supported by the evidence'}</b></div><p style="font-size:.92rem">${linked(ms.lesson)}</p><p class="detail-quote">About ${benchLink(ms.bench)}: ${ANCHOR(b.url,'results')}.</p><button class="primary" id="next">${G.i+1<MISSIONS.length?'Next mission':'See results'}</button>`;
  const all=points(ms.bench); $('#mplot').removeAttribute('style'); $('#mplot').innerHTML=''; drawChart($('#mplot'),all,{ elo:b.elo, yLabel:b.yLabel, xLabel:benchXLabel(b), height:400, sig:'m'+G.i, labelAll:true, rug:unplaced(ms.bench), aria:b.name });
  $('#next').onclick=()=>{ G.i++; renderMissions(); window.scrollTo({top:$('.bar').offsetTop,behavior:'smooth'}); }; $('#next').focus({preventScroll:true}); }
function rankOf(t){ return t>=900?'Cost auditor':t>=720?'Frontier reader':t>=480?'Budget keeper':'List-price reader'; }
function renderEnd(v){ if(S.best==null||G.total>S.best){ S.best=G.total; store.set('best',G.total); updateHud(); }
  const blurb={ 'Cost auditor':'You checked what the numbers measure and picked by per-task cost.', 'Frontier reader':'You read the cost curves well. Your remaining losses are in effort settings or evidence checks.', 'Budget keeper':'You stayed mostly on budget. The Atlas shows which models you could have paid less for.', 'List-price reader':'List prices misled you often, as they do most buyers. Try the Atlas, then run again.' };
  const r=rankOf(G.total);
  v.innerHTML=`<div class="mission-card"><span class="lab">Run complete</span><div class="endstat">${G.total}<span style="font-size:.4em;color:var(--muted)"> / 1000</span></div><h2>${r}</h2><p>${blurb[r]}</p>
    <div class="tbl-scroll"><table><thead><tr><th>Mission</th><th>Benchmark</th><th class="r">Points</th></tr></thead><tbody>${MISSIONS.map((ms,k)=>`<tr><td>${esc(ms.t)}</td><td>${benchLink(ms.bench)}</td><td class="r num">${G.log[k]}</td></tr>`).join('')}</tbody></table></div>
    <div class="ctrl-row"><button class="primary" id="again">Play again</button><button class="secondary" id="to-atlas">Open the Atlas</button></div></div>`;
  $('#again').onclick=()=>{ G.i=0; G.total=0; G.log=[]; renderMissions(); };
  $('#to-atlas').onclick=()=>setView('atlas');
}

/* ---------- Quality dial ---------- */
const MX={ target:90, loads:LOADS.map(l=>l.v) };
function mixFor(target){ return LOADS.map((l,i)=>{ const pts=points(l.b).filter(p=>!p.restricted), top=Math.max(...pts.map(p=>p.score)), need=top*target/100, pick=pts.filter(p=>p.score>=need).sort((a,c)=>a.cost-c.cost)[0]; return {l,pick,top,tasks:MX.loads[i],bill:pick.cost*MX.loads[i]}; }); }
const sum=a=>a.reduce((x,y)=>x+y,0);
function renderMix(){ const v=$('#view-mix');
  if(!$('#mix-root',v)){ v.innerHTML=`<div class="section-head" id="mix-root"><h2>How much does the last bit of quality cost?</h2><p>Set your monthly workload and choose how close to the top score each task must be.</p></div>
    <div class="mix-grid"><div class="card"><span class="lab">Monthly workload</span><div style="display:grid;gap:.7rem" id="loads">${LOADS.map((l,i)=>`<div class="load"><label for="load-${i}">${esc(l.label)}<output id="loadout-${i}"></output></label><input id="load-${i}" data-i="${i}" type="range" min="0" max="100" value="${loadToSlider(l.v)}"></div>`).join('')}</div></div>
    <div class="stage"><div class="ctrl"><label for="target">Quality target: at least <output id="target-out"></output> of the best score in each benchmark</label><input id="target" type="range" min="60" max="100" step="1" value="${MX.target}"></div><div class="bigstats" id="stats"></div><div class="plot" id="curve"></div></div></div>
    <div class="card"><span class="lab">Cheapest model that meets the target</span><div class="tbl-scroll"><table id="mix-table"></table></div>
    <p class="detail-quote">A pick is the cheapest published row that reaches the target on that benchmark. "Percent of best" compares within one benchmark and does not mean the same thing across benchmarks. Computer-use and chart costs are read from a vendor chart, and only Claude models have costs there. Models that are not generally available are left out. These picks are a starting point for your own test. They do not replace one.</p></div>`;
    $('#target').oninput=e=>{ MX.target=+e.target.value; updateMix(); };
    v.querySelectorAll('#loads input').forEach(el=>el.oninput=e=>{ MX.loads[+e.target.dataset.i]=sliderToLoad(+e.target.value); updateMix(); }); }
  updateMix(); }
const loadToSlider=v=>v<=0?0:Math.round(Math.log10(v)/5*100); const sliderToLoad=s=>{ if(s===0) return 0; const v=10**(s/100*5), m=10**Math.max(0,Math.floor(Math.log10(v))-1); return Math.round(v/m)*m; };
function updateMix(){ const rows=mixFor(MX.target), bill=sum(rows.map(r=>r.bill)), topBill=sum(mixFor(100).map(r=>r.bill)), saved=topBill>0?1-bill/topBill:0;
  $('#target-out').textContent=MX.target+'%';
  LOADS.forEach((l,i)=>{ $('#loadout-'+i).textContent=MX.loads[i].toLocaleString('en-US'); });
  $('#stats').innerHTML=`<div><b>${bigMoney(bill)}</b><span>Monthly bill at ${MX.target}%</span></div><div><b>${bigMoney(topBill)}</b><span>Monthly bill for the top score everywhere</span></div><div><b>${Math.round(saved*100)}%</b><span>Saved by accepting ${100-MX.target}% below the top score</span></div>`;
  $('#mix-table').innerHTML=`<thead><tr><th>Workload</th><th>Benchmark</th><th>Model</th><th class="r">Score</th><th class="r">% of best</th><th class="r">Unit cost</th><th class="r">Per month</th></tr></thead><tbody>${rows.map(r=>`<tr><td>${esc(r.l.label)}</td><td>${benchLink(r.l.b)}</td><td>${esc(r.pick.name)}${r.pick.effort!=='default'?' · '+esc(eff(r.pick.effort)):''}</td><td class="r num">${r.pick.score}</td><td class="r num">${Math.round(r.pick.score/r.top*100)}%</td><td class="r num">${money(r.pick.cost)}</td><td class="r num">${bigMoney(r.bill)}</td></tr>`).join('')}</tbody>`;
  drawCurve(); }
function drawCurve(){ const host=$('#curve'), W=Math.max(280,host.clientWidth||600), H=210, m={l:58,r:14,t:12,b:32};
  const data=[]; for(let t=60;t<=100;t++) data.push([t,sum(mixFor(t).map(r=>r.bill))]);
  const mx=Math.max(...data.map(d=>d[1]),1), x=t=>m.l+(t-60)/40*(W-m.l-m.r), y=v=>H-m.b-v/mx*(H-m.t-m.b), yt=niceTicks(0,mx,4);
  // The bill jumps when a cheaper row stops qualifying, so the curve is drawn as steps.
  let d=`M${x(60)},${y(data[0][1])}`; data.forEach((p,i)=>{ if(i){ d+=`H${x(p[0])}V${y(p[1])}`; } });
  const cur=data.find(p=>p[0]===MX.target);
  host.innerHTML=`<svg viewBox="0 0 ${W} ${H}" height="${H}" role="img" aria-label="Monthly bill against quality target"><g class="grid">${yt.map(v=>`<line x1="${m.l}" x2="${W-m.r}" y1="${y(v)}" y2="${y(v)}"/>`).join('')}</g><g class="axis">${yt.map(v=>`<text x="${m.l-7}" y="${y(v)+4}" text-anchor="end">${bigMoney(v)}</text>`).join('')}${[60,70,80,90,100].map(t=>`<text x="${x(t)}" y="${H-m.b+16}" text-anchor="middle">${t}%</text>`).join('')}</g><path class="curve-area" d="${d}V${y(0)}H${x(60)}Z"/><path class="curve-line" d="${d}"/><line class="budget-line" x1="${x(cur[0])}" x2="${x(cur[0])}" y1="${m.t}" y2="${H-m.b}"/><circle cx="${x(cur[0])}" cy="${y(cur[1])}" r="6" fill="var(--gold)" stroke="var(--stage)" stroke-width="2"/></svg>`; }

/* ---------- Releases ---------- */
const RL={ lab:'all', sel:null };
function jumpTo(id,m){ S.cat=CATS.find(c=>c.benches.includes(id)).id; S.bench=id; resetBench(); const r=points(id).filter(p=>p.model===m).sort((a,c)=>c.score-a.score)[0]; S.sel=r?r.key:null; setView('atlas'); if(r) checkInsights({b:id,m:r.model,e:r.effort}); $('#cats').scrollIntoView({behavior:'smooth',block:'start'}); }
function renderReleases(){ const v=$('#view-releases');
  if(!$('#rel-list',v)){ v.innerHTML=`<div class="section-head"><h2>What the labs announced</h2><p>Every entry links to the lab's own post. Figures in the text are the lab's claims, not measurements of ours.</p></div>
    <div class="stage"><div class="seg" id="rel-seg" role="group" aria-label="Lab">${[['all','All labs'],...Object.entries(V).map(([k,x])=>[k,x[0]])].map(([k,n])=>`<button data-l="${k}" aria-pressed="${RL.lab===k}">${n}</button>`).join('')}</div><div class="plot" id="rel-tl"></div></div>
    <div class="rel-list" id="rel-list"></div>`;
    v.querySelectorAll('#rel-seg button').forEach(el=>el.onclick=()=>{ RL.lab=el.dataset.l; RL.sel=null; v.querySelectorAll('#rel-seg button').forEach(x=>x.setAttribute('aria-pressed',x===el)); drawTimeline(); drawReleaseList(); }); }
  drawTimeline(); drawReleaseList(); }
function drawTimeline(){ const host=$('#rel-tl'), W=Math.max(280,host.clientWidth||800), narrow=W<560, lanes=Object.keys(V).filter(k=>RL.lab==='all'||RL.lab===k), laneH=narrow?54:64, m={l:narrow?8:88,r:14,t:narrow?20:14,b:28}, H=m.t+m.b+laneH*lanes.length;
  const lo=+new Date('2026-07-20'), hi=+new Date('2026-11-08'), x=d=>m.l+(+new Date(d)-lo)/(hi-lo)*(W-m.l-m.r), months=['2026-08-01','2026-09-01','2026-10-01','2026-11-01'];
  let t=`<g class="grid">${months.map(d=>`<line x1="${x(d)}" x2="${x(d)}" y1="${m.t}" y2="${H-m.b}"/>`).join('')}</g><g class="axis">${months.map(d=>`<text x="${x(d)}" y="${H-m.b+17}" text-anchor="middle">${new Date(d).toLocaleDateString('en-GB',{month:'short',timeZone:'UTC'})}</text>`).join('')}</g><line class="rel-today" x1="${x(AS_OF)}" x2="${x(AS_OF)}" y1="${m.t}" y2="${H-m.b}"/>`;
  lanes.forEach((k,li)=>{ const cy=m.t+laneH*li+laneH/2, rows=RELEASES.map((r,i)=>({r,i})).filter(o=>o.r.l===k).sort((a,c)=>a.r.d<c.r.d?-1:1);
    t+=`<text class="lane-label" x="${narrow?m.l:m.l-12}" y="${narrow?cy-laneH/2+13:cy+4}" text-anchor="${narrow?'start':'end'}">${V[k][0]}</text>`;
    let last=-99,flip=1; rows.forEach(o=>{ const px=x(o.r.d); let off=0; if(px-last<15){ off=11*flip; flip=-flip; } else flip=1; last=px; const py=cy+off+(narrow?6:0);
      t+=`<g class="rel-dot${RL.sel===o.i?' sel':''}" data-i="${o.i}" tabindex="0" role="button" aria-label="${esc(o.r.t)}, ${fmtDate(o.r.d)}" transform="translate(${px.toFixed(1)},${py})"><title>${esc(o.r.t)} (${fmtDate(o.r.d)})</title><circle class="hit" r="11"/><path d="${shape(k,narrow?5:6)}" fill="${V[k][1]}" stroke="var(--stage)" stroke-width="1.5"/></g>`; });
    const latest=rows[rows.length-1]; if(latest&&!narrow){ const lx=x(latest.r.d); t+=`<text class="pt-label" x="${lx-10}" y="${cy-13}" text-anchor="end">${esc(M[latest.r.ms[0]][0])}</text>`; } });
  host.innerHTML=`<svg viewBox="0 0 ${W} ${H}" height="${H}" role="group" aria-label="Release timeline by lab">${t}</svg>`;
  host.querySelectorAll('.rel-dot').forEach(el=>{ const go=()=>{ RL.sel=+el.dataset.i; drawTimeline(); drawReleaseList(); document.getElementById('rel-'+RL.sel)?.scrollIntoView({behavior:'smooth',block:'nearest'}); }; el.addEventListener('click',go); el.addEventListener('keydown',e=>{ if(e.key==='Enter'||e.key===' '){ e.preventDefault(); go(); } }); }); }
function drawReleaseList(){ const host=$('#rel-list'), rows=RELEASES.map((r,i)=>({r,i})).filter(o=>RL.lab==='all'||o.r.l===RL.lab);
  host.innerHTML=rows.map(({r,i})=>{ const bench=Object.keys(B).filter(id=>B[id].rows.some(x=>r.ms.includes(x[0]))), prices=r.ms.slice(0,1).map(m=>M[m]).filter(m=>m[2]!=null).map(m=>`$${m[2]} / $${m[3]} per million tokens`);
    return `<article class="rel${RL.sel===i?' sel':''}" id="rel-${i}"><time datetime="${r.d}">${r.approx?'about ':''}${fmtDate(r.d)}</time><div class="rel-body"><div class="rel-title">${glyph(r.l)}${ANCHOR(r.u,r.t)}</div>${r.c?`<p>${linked(r.c)}</p>`:''}<div class="rel-jump">${prices.length?`<span>${prices[0]}</span>`:''}${bench.length?bench.map(id=>`<button data-b="${id}" data-m="${r.ms.find(m=>B[id].rows.some(x=>x[0]===m))}">${esc(B[id].short)}</button>`).join(''):'<span>Not on a chart yet</span>'}</div></div></article>`; }).join('');
  host.querySelectorAll('.rel-jump button').forEach(el=>el.onclick=()=>jumpTo(el.dataset.b,el.dataset.m)); }

/* ---------- Sources ---------- */
function renderSources(){ const rows=CATS.flatMap(c=>c.benches.map(id=>({c,id,b:B[id]})));
  $('#view-sources').innerHTML=`<div class="section-head"><h2>Where the numbers come from</h2><p>Read on ${AS_OF}. Live boards change, so every row keeps its date.</p></div>
  <div class="card"><div class="tbl-scroll"><table><thead><tr><th>Category</th><th>Benchmark</th><th>References</th><th>Reported by</th><th>Cost</th><th>Date</th><th class="r">Rows</th></tr></thead><tbody>${rows.map(r=>`<tr><td>${esc(r.c.name)}</td><td>${ANCHOR(benchHome(r.id),r.b.name)}</td><td>${REFS[r.id].slice(1).map(x=>ANCHOR(x[1],x[0])).concat(ANCHOR(r.b.url,'Results')).join(' · ')}</td><td>${REP[r.b.rep]}</td><td><span class="chip ${r.b.cost}">${costChip[r.b.cost][1]}</span></td><td class="num">${r.b.date}</td><td class="r num">${r.b.rows.length+(r.b.noCost||[]).length}</td></tr>`).join('')}</tbody></table></div></div>
  <div class="layout" style="grid-template-columns:repeat(auto-fit,minmax(min(22rem,100%),1fr))">
    <div class="card"><h3>Only current models</h3><ul class="notes"><li>A model appears only if it is its lab's newest in that tier, checked against each lab's own model list on the read date: Claude Fable 5.1, Opus 5.5, Sonnet 5.5 and Haiku 5.5; GPT-6 Astra, GPT-6.1 Sol, GPT-6.1 Terra and GPT-6 Luna; Gemini 4 Carbon, 4 Argon and 3.8 Flash; and the newest from other labs.</li><li>Google lists no Gemini 4 Flash, so 3.8 Flash is its current Flash model.</li><li><b>Benchmarks left out, because too few current models have rows:</b> ${ANCHOR(benchHome('oswv'),'OSWorld-Verified')}, ${ANCHOR(benchHome('cyscen'),'CyScenarioBench')}, ${ANCHOR(benchHome('rabbit'),'CodeRabbit')}, and our own ${ANCHOR(benchHome('ownrev'),'code-review')} and ${ANCHOR(benchHome('ownui'),'HTML UI')} pilots, which still run on GPT-5.6.</li></ul></div>
    <div class="card"><h3>Three kinds of cost</h3><ul class="notes"><li><b>Cost measured:</b> the benchmark owner or study publishes the spend per task, page or outcome.</li><li><b>Cost read from chart:</b> a vendor plots the spend without printing values. We read it off the figure, accurate to about ±5%.</li><li><b>Cost estimated from list price:</b> nobody publishes spend, so we apply each model's list price to one token volume. The default is 100k input and 10k output, as on the Token Economy benchmark page. It ignores how many tokens each model really uses, and measured rows show that gap can reach seven times. Those dots are hollow.</li></ul></div>
    <div class="card"><h3>How to read a score</h3><ul class="notes"><li>A frontier row has no other row that is both cheaper and at least as good. It is the shortlist, not a verdict.</li><li>Scores compare only within one benchmark version. "Last 10%" on a category card is the one figure meant to compare across categories: the cost of the top score divided by the cheapest row within 10% of it.</li><li>External benchmarks suggest candidates for your own test. The correctness floors in Token Economy's model recommendation are separate and stay in force.</li><li>Rows older than 90 days at the read date are flagged.</li></ul></div>
    <div class="card"><h3>Known conflicts and gaps</h3><ul class="notes"><li><b>Gemini 4 Carbon price:</b> Artificial Analysis states $4 / $20. Arena shows $2 / $10, and Google's pricing page did not list Carbon when read. We use $4 / $20.</li><li><b>Dated price changes:</b> Gemini 4 Argon moves from $2 / $10 to $4 / $20 on 1 Dec 2026, and Gemini 3.8 Flash from $0.75 / $3.75 to $1.50 / $7.50 on 1 Jan 2027. We show today's prices.</li><li><b>Gemini 4 Carbon access:</b> Google limits it to a subset of cyber defenders and trusted partners, so it stays out of every pick. <a href="https://blog.google/innovation-and-ai/models-and-research/gemini-models/introducing-gemini-4-carbon/" target="_blank" rel="noopener">Google, 28 Oct</a></li><li><b>Release dates:</b> Kimi K3.1 Pro and MiMo-V2.6 posts show no date, so we use press dates. Anthropic's newsroom lists Fable 5.1 on 15 Sept, while its release notes and system card say 1 Sept. The Qwen post did not load in our browser check, so its title and date come from search results.</li><li><b>OSWorld version:</b> Haiku 5.5's 72.4% claim uses an OSWorld 2.1 offline subset, so it is omitted from the OSWorld 2.0 score strip.</li><li><b>HLE-Diamond lead:</b> the v5 article names Opus 5.5, while the later Carbon article names Carbon.</li><li><b>Unconfirmed prices:</b> GPT-6.1 Terra and Gemini 4 Carbon are in the price catalog. Grok 4.7, Qwen3.8 Max and DeepSeek V4.1 Flash use separately attributed third-party price claims for this explorer.</li><li><b>Missing rows:</b> no Claude 5.5, GPT-6.1 or Gemini 4 rows on DeepSWE. The official OSWorld 2.0 board has no current model yet. No per-task cost for security or language. Artificial Analysis charts GDPval cost as stacked parts without totals, so knowledge work stays an estimate. No independent classification benchmark, and no version-level German translation test.</li><li><b>Lab claims:</b> figures on the Releases tab are the labs' own. Each lab picks its benchmarks and setups, so read them as claims until an independent row exists.</li><li><b>Secondary sources:</b> the Astra ExploitBench score and the feedback-labelling study come from secondary reports.</li></ul></div>
  </div>`; }

/* ---------- Boot ---------- */
function setView(vw){ stopPlay(); S.view=vw; document.querySelectorAll('.tab').forEach(t=>t.setAttribute('aria-selected',t.dataset.view===vw));
  document.querySelectorAll('.view').forEach(s=>s.hidden=s.id!=='view-'+vw);
  if(vw==='atlas') renderAtlas(); if(vw==='missions') renderMissions(); if(vw==='mix') renderMix(); if(vw==='releases') renderReleases(); if(vw==='sources') renderSources();
  try{ if(location.hash.slice(1)!==vw) history.replaceState(null,'','#'+vw); }catch{} }
document.querySelectorAll('.tab').forEach(t=>t.onclick=()=>setView(t.dataset.view));
renderAtlasShell(); updateHud();
let resizeT; window.addEventListener('resize',()=>{ clearTimeout(resizeT); resizeT=setTimeout(()=>{ if(S.view==='atlas') renderChart(); if(S.view==='mix') drawCurve(); if(S.view==='releases') drawTimeline(); },150); });
window.addEventListener('hashchange',()=>{ const h=location.hash.slice(1); if(['atlas','missions','mix','releases','sources'].includes(h)&&h!==S.view) setView(h); });
const startHash=location.hash.slice(1);
setView(['atlas','missions','mix','releases','sources'].includes(startHash)?startHash:'atlas');

  } catch (error) {
    console.error('Cost Frontier could not load', error);
    document.querySelector('#view-atlas').innerHTML = '<p class="card">The Cost Frontier data could not be loaded. Please try again.</p>';
  }
})();
