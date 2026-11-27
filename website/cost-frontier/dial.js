'use strict';
/* Quality dial (frugal to strong) and effort ladders for the Cost Frontier page.
   Reads data/effort-dial.json, which tools/EffortDialSiteData projects from the library's EffortDial,
   so every pick and cost here is the library's own answer for that slider position. */
window.CostFrontierDial = (() => {
  let D = null, X = null;
  const st = { pos: 50, cli: 'claude', loads: {}, ladder: 'arc2', lab: 'all', built: false, ladderBuilt: false };
  // Example month only: card counts a visitor can change. They are not measured volumes.
  const EXAMPLE_MONTH = { mechanical: 40, 'feature-logic': 60, 'ui-visual': 15, hard: 15, 'review-aspect': 150, 'review-decision': 75, research: 10, summary: 200, 'orchestrator-chat': 0, creative: 5 };
  const CLI = { claude: 'Claude Code', codex: 'Codex' };
  const LAB_CLI = { anthropic: 'claude', openai: 'codex' };
  const RANK = { minimal: 0, low: 1, medium: 2, high: 3, xhigh: 4, max: 5, ultra: 6 };
  const DIM_LABEL = { measured: 'measured', partial: 'partly', none: 'not measured', excluded: 'excluded before the vote', evidence: 'evidence' };
  const BASIS = { coding: 'Coding evidence', uiVisual: 'Visual-quality evidence', review: 'Review work', textAgentic: 'Text work', creative: 'Creative evidence only' };

  const $ = (s, r = document) => r.querySelector(s);
  const esc = v => X.esc(v);
  const money = v => X.money(v);
  const range = (lo, hi) => lo == null ? 'unknown' : Math.abs(hi - lo) < 0.005 ? money(lo) : `${money(lo)}–${money(hi)}`;
  const num = v => v == null ? 'n/a' : Number(v).toLocaleString('en-US');
  const big = v => v >= 1e9 ? (v / 1e9).toFixed(1) + ' B' : v >= 1e6 ? (v / 1e6).toFixed(1) + ' M' : num(v);
  const pct = v => Math.round(v * 100) + '%';
  const date = d => d ? X.fmtDate(d) : 'undated';
  const eff = e => e === 'xhigh' ? 'xHigh' : e;
  const short = id => D.models[id].name.replace(/^Claude /, '');
  const label = (id, e) => `${short(id)} ${eff(e)}`;
  const cls = id => D.classes.find(c => c.id === id);
  const rungOf = (c, cli, id) => c.ladders[cli].rungs.find(r => r.id === id);
  const pickAt = (c, cli, pos) => rungOf(c, cli, c.ladders[cli].picks.find(s => pos >= s.from && pos <= s.to).rung);
  const linkOf = ev => ev.cls === 'F' ? '#dial-fleet' : ev.links[0].url;
  const a = (url, text, extra = '') => url.startsWith('#')
    ? `<a href="${esc(url)}" data-jump="${esc(url.slice(1))}"${extra}>${text}</a>`
    : `<a href="${esc(url)}" target="_blank" rel="noopener"${extra}>${text}</a>`;
  const chip = (c, d, url, title) => a(url, `<b>${esc(c)}</b> ${esc(date(d))}`, ` class="ev ev-${esc(c)}" title="${esc(title)}"`);
  const evChip = id => { const ev = D.evidence[id]; return chip(ev.cls, ev.date, linkOf(ev), `${classLabel(ev.cls)}: ${ev.title}`); };
  const rowChip = id => { const r = D.rows[id]; return chip(r.cls, r.retrieved, r.url, `${classLabel(r.cls)}: ${D.benchmarks[r.bench].name}, published ${date(r.published)}`); };
  const classLabel = c => (D.evidenceClasses.find(x => x.id === c) || { label: c }).label;
  const priceChip = id => { const m = D.models[id]; return m.priceUrl ? chip('V', m.priceDate, m.priceUrl, `Vendor list price for ${m.name}`) : ''; };
  const restrictedNote = r => r.selectable ? '' : '<span class="tag gold">Not yet cleared for our fleet</span>';

  async function ensure(ctx) {
    X = ctx;
    if (D) return;
    const response = await fetch('/token-economy/data/effort-dial.json');
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    D = await response.json();
    st.pos = D.positions.default;
    D.classes.forEach(c => { st.loads[c.id] = EXAMPLE_MONTH[c.id] ?? 0; });
  }

  /* ---------- Quality dial ---------- */
  async function renderDial(host, ctx) {
    try { await ensure(ctx); } catch (error) { host.innerHTML = '<p class="card">The dial data could not be loaded. Please try again.</p>'; console.error(error); return; }
    if (!st.built) {
      host.innerHTML = shell();
      st.built = true;
      $('#dial-pos', host).oninput = e => { st.pos = +e.target.value; update(); };
      host.querySelectorAll('#dial-cli button').forEach(b => b.onclick = () => { st.cli = b.dataset.cli; host.querySelectorAll('#dial-cli button').forEach(x => x.setAttribute('aria-pressed', x === b)); update(); renderKinds(); renderCreative(); });
      host.addEventListener('click', jump);
      renderStatic();
    }
    update(); renderKinds(); renderCreative();
  }

  function jump(e) {
    const t = e.target.closest('[data-jump]'); if (!t) return;
    const el = document.getElementById(t.dataset.jump); if (!el) return;
    e.preventDefault();
    if (el.tagName === 'DETAILS') el.open = true;
    el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function shell() {
    const p = D.positions;
    return `<div class="section-head" id="mix-root"><h2>Quality dial: from frugal to strong</h2><p>One slider. For each kind of work it shows which model and reasoning level would run, what one accepted task is expected to cost, and the evidence behind it.</p></div>
    <nav class="jumps" aria-label="On this tab">${[['dial-runs', 'What runs'], ['dial-kinds', 'Kinds of work'], ['dial-creative', 'Creative work'], ['dial-month', 'Your month'], ['dial-drivers', 'Cost drivers'], ['dial-subs', 'Subscriptions'], ['dial-fleet', 'Measured in our fleet']].map(([id, t]) => a('#' + id, esc(t))).join('')}<a href="#ladders" class="to-ladders">Effort ladders →</a></nav>
    <div class="stage dial-stage" id="dial-runs">
      <div class="dial-top">
        <div class="ctrl dial-ctrl"><label for="dial-pos">Dial position <output id="dial-out"></output></label>
          <input id="dial-pos" type="range" min="${p.min}" max="${p.max}" step="1" value="${st.pos}" list="dial-ticks" aria-describedby="dial-scale">
          <datalist id="dial-ticks">${[0, 25, 50, 75, 100].map(v => `<option value="${v}"></option>`).join('')}</datalist>
          <div class="dial-scale" id="dial-scale" aria-hidden="true"><span>Frugal</span><span>Default (${p.default})</span><span>Strong</span></div></div>
        <div class="ctrl"><span>CLI</span><div class="seg" id="dial-cli" role="group" aria-label="CLI">${Object.entries(CLI).map(([k, v]) => `<button data-cli="${k}" aria-pressed="${st.cli === k}">${v}</button>`).join('')}</div></div>
      </div>
      <div class="bigstats" id="dial-stats"></div>
      <div class="tbl-scroll"><table class="runs" id="dial-table"></table></div>
      <p class="stage-note">Costs are list-price ranges in USD. A range comes from two independent bounds, never from a guess in between. Where nothing measures a kind of work, the cost says unknown, not zero. Evidence chips name the class (M third-party measurement, V vendor, L list-price derivation, F measured in our fleet) and the date; each links its source. At the default position every kind of work runs at its standard level: medium for normal work, high for hard work.</p>
    </div>
    <section class="dial-sec" id="dial-kinds"><div class="section-head"><h2>Kinds of work</h2><p>Cheapest sufficient and strongest setting per CLI, with what the evidence does and does not show.</p></div><div class="kinds" id="kinds"></div></section>
    <section class="dial-sec" id="dial-creative"></section>
    <section class="dial-sec" id="dial-month"></section>
    <section class="dial-sec" id="dial-drivers"></section>
    <section class="dial-sec" id="dial-subs"></section>
    <section class="dial-sec" id="dial-fleet"></section>`;
  }

  function costCell(c) {
    if (c.status === 'unknown') return `<span class="unknown">Unknown</span><small>${esc(c.unknownReason)}</small>`;
    const unit = c.unit === 'acceptedCard' ? 'per accepted task' : 'per AA-3D build, a proxy';
    const credits = c.creditsLow != null ? `<small class="num">${num(Math.round(c.creditsLow))}${Math.round(c.creditsHigh) !== Math.round(c.creditsLow) ? '–' + num(Math.round(c.creditsHigh)) : ''} Codex credits</small>` : '';
    return `<b class="num">${range(c.low, c.high)}</b><small>${unit}</small>${credits}`;
  }

  function strip(c, cli) {
    const picks = c.ladders[cli].picks;
    return `<div class="strip" aria-hidden="true">${picks.map(s => { const r = rungOf(c, cli, s.rung); const on = st.pos >= s.from && st.pos <= s.to;
      return `<i class="${on ? 'on' : ''}" style="flex:${s.to - s.from + 1}" title="${esc(label(r.model, r.effort))}: ${s.from}–${s.to}"></i>`; }).join('')}<b style="left:${st.pos}%"></b></div>`;
  }

  function update() {
    const pos = st.pos, cli = st.cli, def = D.positions.default;
    $('#dial-out').textContent = pos === def ? `${pos}, default` : pos < def ? `${pos}, more frugal` : `${pos}, stronger`;
    const rows = D.classes.map(c => {
      const r = pickAt(c, cli, pos), d = pickAt(c, cli, def), ri = c.ladders[cli].rungs.indexOf(r), di = c.ladders[cli].rungs.indexOf(d);
      const move = ri > di ? '<span class="tag up">stronger than default</span>' : ri < di ? '<span class="tag down">lighter than default</span>' : '';
      const restriction = r.restriction ? `<small>${esc(r.restriction)}</small>` : '';
      return `<tr><th scope="row"><span class="letter">${esc(c.letter)}</span> ${a('#kind-' + c.id, esc(c.label))}</th>
        <td><b>${esc(short(r.model))}</b> <span class="lvl">${esc(eff(r.effort))}</span> ${move}${restriction}${strip(c, cli)}</td>
        <td class="cost">${costCell(r.cost)}</td>
        <td class="evs">${r.evidence.map(evChip).join('')}<small>${esc(r.confidence)} confidence</small></td></tr>`;
    }).join('');
    $('#dial-table').innerHTML = `<thead><tr><th>Kind of work</th><th>What would run</th><th>Expected cost</th><th>Evidence</th></tr></thead><tbody>${rows}</tbody>`;
    const known = D.classes.map(c => pickAt(c, cli, pos)).filter(r => r.cost.status !== 'unknown' && r.cost.unit === 'acceptedCard');
    const measured = D.classes.filter(c => pickAt(c, cli, pos).evidence.some(id => D.evidence[id].cls === 'F')).length;
    const cheapest = known.slice().sort((x, y) => x.cost.low - y.cost.low)[0];
    $('#dial-stats').innerHTML = `<div><b>${measured} of ${D.classes.length}</b><span>kinds of work whose pick has a fleet measurement (F) behind it</span></div>
      <div><b class="num">${cheapest ? range(cheapest.cost.low, cheapest.cost.high) : 'n/a'}</b><span>cheapest accepted task here: ${cheapest ? esc(label(cheapest.model, cheapest.effort)) : ''}</span></div>
      <div><b class="num">${monthRange(pos, cli, 'acceptedCard')}</b><span>example month, ${CLI[cli]} (set it in <a href="#dial-month" data-jump="dial-month">Your month</a>)</span></div>`;
    renderMonth();
  }

  function renderKinds() {
    const host = $('#kinds');
    host.innerHTML = D.classes.map(c => {
      const ladder = cli => {
        const L = c.ladders[cli], lo = pickAt(c, cli, 0), hi = pickAt(c, cli, 100), esc8 = L.rungs.filter(r => r.escalationOnly);
        const floors = c.floors.filter(f => f.cli === cli);
        const line = (labelText, r) => `<div class="rung"><span class="lab">${labelText}</span><div><b>${esc(label(r.model, r.effort))}</b> ${restrictedNote(r)}${r.restriction ? `<small>${esc(r.restriction)}</small>` : ''}</div><div class="cost">${costCell(r.cost)}</div><div class="evs">${r.evidence.map(evChip).join('')}</div>${r.note ? `<p class="detail-quote">${esc(r.note)}</p>` : ''}</div>`;
        return `<div class="cli-col"><h4>${CLI[cli]}</h4>${line('Cheapest sufficient (frugal end)', lo)}${hi.id !== lo.id ? line('Strongest on the dial', hi) : '<p class="detail-quote">The same setting runs across the whole dial.</p>'}
          ${esc8.map(r => line('Only after a failed attempt', r)).join('')}
          ${floors.length ? `<p class="detail-quote"><b>Floor:</b> ${floors.map(f => `${esc(label(rungOf(c, cli, f.rung).model, rungOf(c, cli, f.rung).effort))} ${f.condition ? 'when the task is ' + esc(f.condition.replace(/-/g, ' ')) : 'always'}. ${esc(f.reason)}`).join(' ')}</p>` : ''}
          ${L.candidates.length ? `<details><summary>Recorded, not on this ladder (${L.candidates.length})</summary><ul class="notes">${L.candidates.filter(k => D.models[k.model].current).map(k => `<li><b>${esc(label(k.model, k.effort))}</b>: ${esc(k.reason)} ${k.evidence.map(evChip).join('')}</li>`).join('')}</ul></details>` : ''}</div>`;
      };
      const applies = D.conclusions.filter(k => k.appliesTo.includes(c.id)), prov = D.conclusions.filter(k => k.provisionalFor.includes(c.id)), not = D.conclusions.filter(k => k.doesNotApplyTo.includes(c.id));
      return `<details class="card kind" id="kind-${esc(c.id)}"${c.id === 'creative' ? ' open' : ''}><summary><span class="letter">${esc(c.letter)}</span> ${esc(c.label)} <span class="chip">${esc(BASIS[c.basis] || c.basis)}</span><span class="chip">${esc(c.confidence)} confidence</span></summary>
        <p>${esc(c.description)}</p>
        <div class="cli-cols">${ladder('claude')}${ladder('codex')}</div>
        ${applies.length || prov.length || not.length ? `<div class="concl">${applies.map(k => `<p class="yes"><b>Holds here.</b> ${esc(k.statement)} ${k.evidence.map(evChip).join('')}</p>`).join('')}${prov.map(k => `<p class="maybe"><b>Provisional here.</b> ${esc(k.statement)} ${k.evidence.map(evChip).join('')}</p>`).join('')}${not.map(k => `<p class="no"><b>Does not carry over.</b> ${esc(k.statement)}</p>`).join('')}</div>` : ''}
        ${c.gaps.length ? `<div><span class="lab">Evidence gaps</span><ul class="notes">${c.gaps.map(g => `<li>${esc(g)}</li>`).join('')}</ul></div>` : ''}</details>`;
    }).join('');
  }

  /* ---------- Creative work ---------- */
  function intervals(ids, unit) {
    const rows = ids.map(id => D.rows[id]).filter(r => r.ci != null);
    if (!rows.length) return '';
    const lo = Math.min(...rows.map(r => r.score - r.ci)) - 10, hi = Math.max(...rows.map(r => r.score + r.ci)) + 10;
    const W = 100, x = v => ((v - lo) / (hi - lo)) * W;
    return `<div class="ivals" role="img" aria-label="${esc(unit)} with 95% intervals">${rows.map(r => { const m = D.models[r.model];
      return `<div class="ival"><span>${esc(label(r.model, r.effort))}</span><svg viewBox="0 0 100 10" preserveAspectRatio="none" aria-hidden="true"><line x1="${x(r.score - r.ci).toFixed(2)}" x2="${x(r.score + r.ci).toFixed(2)}" y1="5" y2="5" class="ival-line" style="stroke:var(--${m.lab === 'anthropic' || m.lab === 'openai' || m.lab === 'google' ? m.lab : 'other'})"/><circle cx="${x(r.score).toFixed(2)}" cy="5" r="2.4" style="fill:var(--${m.lab === 'anthropic' || m.lab === 'openai' || m.lab === 'google' ? m.lab : 'other'})"/></svg><span class="num">${Math.round(r.score)} ± ${Math.round(r.ci)}</span></div>`; }).join('')}
      <div class="ival-axis num"><span>${Math.round(lo)}</span><span>${Math.round(hi)}</span></div></div>`;
  }

  function renderCreative() {
    const host = $('#dial-creative'), c = cls('creative'), boards = D.creativeBoards, aa = boards[0], wd = boards[1];
    const aaB = D.benchmarks[aa.bench], wdB = D.benchmarks[wd.bench];
    const has = (b, model, effort) => b.rows.some(id => D.rows[id].model === model && D.rows[id].effort === effort);
    const missingAa = [['claude-sonnet-5-5', 'high'], ['claude-opus-5-5', 'low'], ['claude-opus-5-5', 'medium'], ['gpt-6.1-sol', 'medium']].filter(([m, e]) => !has(aa, m, e));
    const missingWd = [['claude-opus-5-5', 'high'], ['claude-opus-5-5', 'medium'], ['claude-sonnet-5-5', 'medium'], ['gpt-6.1-sol', 'high'], ['gpt-6.1-terra', 'high']].filter(([m, e]) => !has(wd, m, e));
    const aaRow = id => { const r = D.rows[id];
      return `<tr><td>${esc(label(r.model, r.effort))} ${D.models[r.model].selectable ? '' : '<span class="tag gold">not yet cleared</span>'}</td><td class="r num">${Math.round(r.score)}</td><td class="r num">${r.ci == null ? 'anchor, fixed' : `${Math.round(r.score - r.ci)}–${Math.round(r.score + r.ci)}`}</td><td class="r num">${r.cost != null ? money(r.cost) : 'n/a'}</td><td class="r num">${esc(r.scenes || 'n/a')}</td><td>${rowChip(id)}</td></tr>`; };
    const wdRow = id => { const r = D.rows[id];
      return `<tr><td>${esc(label(r.model, r.effort))} ${D.models[r.model].selectable ? '' : '<span class="tag gold">not yet cleared</span>'}</td><td class="r num">${r.score.toFixed(0)}</td><td class="r num">± ${r.ci.toFixed(0)}</td><td class="r num">${num(r.votes)}</td><td>${rowChip(id)}</td></tr>`; };
    const dimCell = v => `<td class="dim dim-${esc(v)}">${esc(DIM_LABEL[v] || v)}</td>`;
    const ladder = c.ladders[st.cli];
    host.innerHTML = `<div class="section-head"><h2>Creative work: design, UI aesthetics, 3D scenes and games</h2><p>Kind of work (${esc(c.letter)}) has its own evidence. Coding benchmark results do not carry over to it.</p></div>
      <div class="callout"><b>Coding results do not carry over.</b> Terminal-Bench, ARC-AGI-2 and the coding indexes say nothing here about quality. The finding that Sonnet 5.5 is not a cheap Opus 5.5 holds for coding only. On interactive 3D scenes at max, the two are level. Creative picks rest on human-preference arenas and judged builds, rated on four dimensions.</div>
      <div class="rubric">${D.rubric.map(d => `<div class="card"><span class="lab">${esc(d.label)}</span><b>${esc(d.question)}</b><p class="detail-quote">For a 3D game: ${esc(d.example)}</p></div>`).join('')}</div>
      <div class="cr-grid">
        <div class="card main-ev"><span class="lab">Main evidence · ${chip('M', D.rows[aa.rows[0]].retrieved, D.rows[aa.rows[0]].url, 'Third-party measurement')}</span>
          <h3>${a(D.rows[aa.rows[0]].url, esc(aaB.name))}</h3>
          <p>${esc(aa.judged)} A recruited panel votes blind between two scenes after viewing each for at least 30 seconds; ratings are Bradley-Terry Elo with 95% intervals. ${a(aaB.methodology, 'Methodology')} · ${a(aa.explorer, 'AA-3D Explorer')}</p>
          <div class="limit"><b>Limitation:</b> scenes that do not run are removed before the vote. The score is preference among working results, not the chance of getting a working result. Artificial Analysis publishes no share of excluded scenes per model; "scenes in voting" below is our count of tasks that still have a scene in the vote, not a playable rate.</div>
          ${intervals(aa.rows, 'AA-3D Elo')}
          <div class="tbl-scroll"><table><thead><tr><th>Model, level</th><th class="r">Elo</th><th class="r">95% interval</th><th class="r">Cost per build [L]</th><th class="r">Scenes in voting</th><th>Source</th></tr></thead><tbody>${aa.rows.map(aaRow).join('')}</tbody></table></div>
          <p class="detail-quote">Cost per build is our derivation from the token counts AA publishes, at list price [L]. Cache writes are not published, so it is a lower bound.</p>
          <p class="missing"><b>Rows that do not exist:</b> ${missingAa.map(([m, e]) => esc(label(m, e))).join(', ')}. Claude and OpenAI models appear at max only, so AA-3D cannot rank Sonnet 5.5 high against Opus 5.5 low.</p>
        </div>
        <div class="card"><span class="lab">Second source, main source for UI work (b2) · ${chip('M', D.rows[wd.rows[0]].retrieved, D.rows[wd.rows[0]].url, 'Third-party measurement')}</span>
          <h3>${a('https://arena.ai/leaderboard/code/webdev', 'WebDev Arena')} <small>(${esc(wdB.version)})</small></h3>
          <p>${esc(wd.judged)} ${a(wdB.methodology, 'Method')} · ${a(D.rows[wd.rows[0]].url, 'Arena dataset')}</p>
          <div class="limit"><b>Broken builds stay in the vote</b>: voters can pick "both bad", so a failing build loses. That makes it a partial check of technical execution, unlike AA-3D. Arena publishes no cost or tokens per task.</div>
          ${intervals(wd.rows, 'WebDev Arena score')}
          <div class="tbl-scroll"><table><thead><tr><th>Model, level</th><th class="r">Score</th><th class="r">95%</th><th class="r">Votes</th><th>Source</th></tr></thead><tbody>${wd.rows.map(wdRow).join('')}</tbody></table></div>
          <p class="detail-quote"><b>Where it differs from AA-3D:</b> here Sonnet 5.5 xhigh is about 48 points below Opus 5.5 max, outside both intervals, and Sonnet 5.5 high about 105 below. On AA-3D the two are level at max. Arena published no 13 Nov snapshot; figures quoted for that day lie between its 11 and 18 Nov snapshots. We show the 23 Nov snapshot.</p>
          <p class="missing"><b>Rows that do not exist:</b> ${missingWd.map(([m, e]) => esc(label(m, e))).join(', ')}.</p>
        </div>
      </div>
      <div class="card"><span class="lab">What each source measures</span>
        <div class="tbl-scroll"><table class="dims"><thead><tr><th>Source</th><th>Originality</th><th>Design</th><th>Game feel</th><th>Technical execution</th><th>Broken builds before the vote</th><th>Cost published</th><th>Rows</th></tr></thead><tbody>${boards.map(b => { const B = D.benchmarks[b.bench];
          return `<tr><td>${a(D.rows[b.rows[0]]?.url || B.methodology, esc(B.name))}</td>${dimCell(b.dimensions.originality)}${dimCell(b.dimensions.design)}${dimCell(b.dimensions.gameFeel)}${dimCell(b.dimensions.technicalExecution)}<td>${b.excludesBrokenBuilds === true ? 'excluded' : b.excludesBrokenBuilds === false ? 'kept' : 'not stated'}</td><td>${esc({ tokensPerTask: 'tokens per task', none: 'none', perGeneration: 'per generation', perBuild: 'per build' }[b.publishesCost] || b.publishesCost)}</td><td>${b.rows.length ? rowChip(b.rows[0]) : ''}</td></tr>`; }).join('')}</tbody></table></div>
        <p class="detail-quote">No source publishes a playable rate. Technical execution has to come from our own creative tasks.</p></div>
      <div class="card"><span class="lab">Rung by rung, ${CLI[st.cli]}: what the evidence says per dimension</span>
        <div class="tbl-scroll"><table class="dims"><thead><tr><th>Rung</th><th>Dial band</th>${D.rubric.map(d => `<th>${esc(d.label)}</th>`).join('')}<th>Cost</th></tr></thead><tbody>${ladder.rungs.map(r => `<tr><td><b>${esc(label(r.model, r.effort))}</b> ${restrictedNote(r)}</td><td class="num">${r.from == null ? 'after a failure' : r.from + '–' + r.to}</td>${['originality', 'design', 'gameFeel', 'technicalExecution'].map(k => { const d = r.dimensions[k]; return `<td class="dim dim-${esc(d.status)}"><b>${esc(DIM_LABEL[d.status])}</b><br>${esc(d.text)} ${d.evidence.map(evChip).join('')}</td>`; }).join('')}<td class="cost">${costCell(r.cost)}</td></tr>`).join('')}</tbody></table></div>
        <p class="detail-quote">Where a dimension says "not measured", no source covers it at that level. Sonnet 5.5 high against Opus 5.5 low stays unranked: no creative source has an Opus 5.5 row below max apart from one Design Arena row at high. Switch the CLI above to see the Codex ladder.</p></div>
      <details class="card"><summary>Design Arena and Arena 3D rows</summary>${boards.slice(2).map(b => { const B = D.benchmarks[b.bench];
        return `<h4>${esc(B.name)}</h4><p class="detail-quote">${esc(b.judged)} ${esc(b.coverage)}</p><div class="tbl-scroll"><table><thead><tr><th>Model, level</th><th class="r">Score</th><th class="r">±</th><th class="r">Cost per task</th><th>Source</th></tr></thead><tbody>${b.rows.map(id => { const r = D.rows[id]; return `<tr><td>${esc(short(r.model))} ${r.effort === 'unspecified' ? '<small>default level, not published</small>' : esc(eff(r.effort))}</td><td class="r num">${Math.round(r.score)}</td><td class="r num">${r.ci != null ? Math.round(r.ci) : 'n/a'}</td><td class="r num">${r.cost != null ? money(r.cost) : 'n/a'}</td><td>${rowChip(id)}</td></tr>`; }).join('')}</tbody></table></div>`; }).join('')}</details>`;
  }

  /* ---------- Your month ---------- */
  function monthAt(pos, cli, unit) {
    let lo = 0, hi = 0;
    for (const c of D.classes) { const r = pickAt(c, cli, pos), n = st.loads[c.id] || 0;
      if (!n || r.cost.status === 'unknown' || r.cost.unit !== unit) continue; lo += n * r.cost.low; hi += n * r.cost.high; }
    return [lo, hi];
  }
  const moneyRange = (lo, hi) => Math.abs(hi - lo) < 0.5 ? X.bigMoney(lo) : `${X.bigMoney(lo)}–${X.bigMoney(hi)}`;
  const monthRange = (pos, cli, unit) => { const [lo, hi] = monthAt(pos, cli, unit); return moneyRange(lo, hi); };

  function renderMonth() {
    const host = $('#dial-month'); if (!host) return;
    if (!host.dataset.built) {
      host.dataset.built = '1';
      host.innerHTML = `<div class="section-head"><h2>Your month</h2><p>Set how many accepted tasks of each kind you run per month. The bill follows the dial.</p></div>
        <div class="mix-grid"><div class="card"><span class="lab">Accepted tasks per month (example values)</span><div class="loads" id="dial-loads">${D.classes.map(c => `<div class="load"><label for="load-${c.id}"><span><span class="letter">${esc(c.letter)}</span> ${esc(c.label.split(':')[0])}</span><output id="loadout-${c.id}"></output></label><input id="load-${c.id}" data-c="${c.id}" type="range" min="0" max="500" step="5" value="${st.loads[c.id]}"></div>`).join('')}</div></div>
        <div class="stage"><div class="bigstats" id="month-stats"></div><div class="plot" id="month-curve"></div><p class="stage-note" id="month-note"></p></div></div>`;
      host.querySelectorAll('#dial-loads input').forEach(el => el.oninput = e => { st.loads[e.target.dataset.c] = +e.target.value; update(); });
    }
    D.classes.forEach(c => { $('#loadout-' + c.id).textContent = num(st.loads[c.id]); });
    const [lo, hi] = monthAt(st.pos, st.cli, 'acceptedCard'), [clo, chi] = monthAt(st.pos, st.cli, 'benchmarkTask');
    const [flo, fhi] = monthAt(0, st.cli, 'acceptedCard'), [slo, shi] = monthAt(100, st.cli, 'acceptedCard');
    $('#month-stats').innerHTML = `<div><b>${moneyRange(lo, hi)}</b><span>at position ${st.pos}, ${CLI[st.cli]}, list price</span></div><div><b>${moneyRange(flo, fhi)}</b><span>all the way frugal (0)</span></div><div><b>${moneyRange(slo, shi)}</b><span>all the way strong (100)</span></div>`;
    const unknown = D.classes.filter(c => st.loads[c.id] && pickAt(c, st.cli, st.pos).cost.status === 'unknown').map(c => c.letter);
    $('#month-note').innerHTML = `${chi > 0 ? `Creative work adds ${moneyRange(clo, chi)} on the AA-3D build proxy, kept apart because it is not a cost per accepted task. ` : ''}${unknown.length ? `${unknown.length === 1 ? 'Kind' : 'Kinds'} ${unknown.map(esc).join(', ')} ${unknown.length === 1 ? 'is' : 'are'} left out: the cost is unknown, not zero. ` : ''}${st.cli === 'claude' ? 'On a Claude subscription, list price is only a proxy for the weekly limit: no vendor publishes a per-model weight.' : 'Codex credits are list price times 25 on the official rate card.'}`;
    drawMonth();
  }

  function drawMonth() {
    const host = $('#month-curve'); if (!host) return;
    const W = Math.max(280, host.clientWidth || 600), H = 200, m = { l: 58, r: 14, t: 12, b: 32 };
    const data = []; for (let p = 0; p <= 100; p++) data.push([p, ...monthAt(p, st.cli, 'acceptedCard')]);
    const mx = Math.max(...data.map(d => d[2]), 1), x = p => m.l + p / 100 * (W - m.l - m.r), y = v => H - m.b - v / mx * (H - m.t - m.b);
    const ticks = []; const raw = mx / 4, mag = 10 ** Math.floor(Math.log10(raw)), step = [1, 2, 5, 10].map(s => s * mag).find(s => s >= raw); for (let v = 0; v <= mx + 1e-9; v += step) ticks.push(v);
    const stepPath = i => { let d = `M${x(0)},${y(data[0][i])}`; data.forEach((p, k) => { if (k) d += `H${x(p[0])}V${y(p[i])}`; }); return d; };
    const hiPath = stepPath(2), loPath = stepPath(1);
    const area = `${hiPath}H${x(100)}` + data.slice().reverse().map(p => `V${y(p[1])}H${x(p[0])}`).join('') + 'Z';
    const cur = data[st.pos];
    host.innerHTML = `<svg viewBox="0 0 ${W} ${H}" height="${H}" role="img" aria-label="Monthly list-price bill against dial position"><g class="grid">${ticks.map(v => `<line x1="${m.l}" x2="${W - m.r}" y1="${y(v)}" y2="${y(v)}"/>`).join('')}</g><g class="axis">${ticks.map(v => `<text x="${m.l - 7}" y="${y(v) + 4}" text-anchor="end">${X.bigMoney(v)}</text>`).join('')}${[0, 25, 50, 75, 100].map(t => `<text x="${x(t)}" y="${H - m.b + 16}" text-anchor="middle">${t}</text>`).join('')}</g><path class="curve-area" d="${area}"/><path class="curve-line" d="${hiPath}"/><path class="curve-line lo" d="${loPath}"/><line class="budget-line" x1="${x(st.pos)}" x2="${x(st.pos)}" y1="${m.t}" y2="${H - m.b}"/><circle cx="${x(st.pos)}" cy="${y(cur[2])}" r="5" fill="var(--gold)" stroke="var(--stage)" stroke-width="2"/></svg>`;
  }

  /* ---------- Static sections: cost drivers, subscriptions, fleet ---------- */
  function renderStatic() {
    const cd = D.costDrivers, from = D.models[cd.from], to = D.models[cd.to], F = D.fleet;
    const fleetLink = a('#dial-fleet', `<b>F</b> ${esc(date(F.windowEnd))}`, ` class="ev ev-F" title="Measured in our fleet, ${esc(date(F.windowStart))} to ${esc(date(F.windowEnd))}"`);
    const tb = D.ladders.find(l => l.id === 'tb41'), tbRows = tb.rows.map(id => D.rows[id]);
    const son = tbRows.find(r => r.model === 'claude-sonnet-5-5'), opus = tbRows.find(r => r.model === 'claude-opus-5-5');
    const credits = cd.credits.filter(c => D.models[c.model].current || c.model === cd.from);
    const shares = cd.aa3dShares.map(s => s.share), lever = (cd.cacheShare * 100).toFixed(0);
    $('#dial-drivers').innerHTML = `<div class="section-head"><h2>Cost drivers: the cached-input price</h2><p>Why the dial prefers GPT-6.1 Sol, and why a list price alone misleads.</p></div>
      <div class="cr-grid">
        <div class="card"><span class="lab">Agent runs re-read their context</span>
          <div class="share"><div style="width:${lever}%"><b>${pct(cd.cacheShare)}</b> cached-input re-reads</div><div>${pct(1 - cd.cacheShare)} everything else</div></div>
          <p>In our fleet, cached-input re-reads made up about <b>three quarters</b> of the list-price cost of ${esc(from.name)} at ${esc(cd.effort)} effort: ${big(cd.cachedTokens)} cached tokens over ${F.days} days (${esc(date(F.windowStart))} to ${esc(date(F.windowEnd))}). ${fleetLink}</p>
          <p class="detail-quote">So the price of one cached token moves an agent's bill more than the input or output price. The Terminal-Bench 4.1 runs show the same shape: for Sonnet 5.5, ${big(son.cachedInputTokens)} of ${big(son.totalTokens)} tokens were cached input. ${rowChip(tb.rows.find(id => D.rows[id].model === 'claude-sonnet-5-5'))}</p></div>
        <div class="card"><span class="lab">Same list price, different bill</span>
          <div class="tbl-scroll"><table><thead><tr><th>Per million tokens</th><th class="r">Input</th><th class="r">Cached input</th><th class="r">Output</th><th>Source</th></tr></thead><tbody>
            ${[from, to].map((m, i) => `<tr><td>${esc(m.name)}${i === 0 ? ' <small>(previous Sol, measured in our fleet)</small>' : ''}</td><td class="r num">${money(m.input)}</td><td class="r num"><b>${money(m.cacheRead)}</b></td><td class="r num">${money(m.output)}</td><td>${priceChip(i === 0 ? cd.from : cd.to)}</td></tr>`).join('')}</tbody></table></div>
          <p>Repricing the measured ${esc(from.name)} ${esc(cd.effort)} profile at ${esc(to.name)}'s card: ${cd.cacheShare} × ${to.cacheRead / from.cacheRead} (cached-price ratio) + ${(1 - cd.cacheShare).toFixed(2)} × ${to.input / from.input} (input-price ratio) = <b class="num">${cd.factor}</b>. The median accepted task drops from <b class="num">${money(cd.anchorUsd)}</b> to <b class="num">${money(cd.repricedUsd)}</b>. ${chip('L', D.asOf, D.docs.dial + '#cost-model', 'List-price derivation')}</p>
          <p class="detail-quote">The same lever shows in Codex credits: ${credits.filter(c => c.model === cd.from || c.model === cd.to).map(c => `${esc(D.models[c.model].name)} charges ${c.cacheRead} credits per million cached tokens`).join(', ')}. ${chip('V', credits[0].retrieved, credits[0].url, 'Vendor rate card')}</p></div>
        <div class="card"><span class="lab">Token volume can beat a token discount</span>
          <p>Sonnet 5.5 lists at half of Opus 5.5, yet costs <b class="num">${money(son.cost)}</b> per Terminal-Bench 4.1 trial against <b class="num">${money(opus.cost)}</b>. Over the same ${son.trials} trials it wrote ${big(son.outputTokens)} output tokens to Opus's ${big(opus.outputTokens)}. ${rowChip(tb.rows.find(id => D.rows[id].model === 'claude-sonnet-5-5'))}</p>
          <p class="detail-quote">That is coding evidence. It is why the dial runs Opus 5.5 rather than Sonnet 5.5 on coding work, and why it does not apply to creative work.</p></div>
        <div class="card"><span class="lab">Where the lever is weaker</span>
          <p>On AA-3D creative builds, cache reads are only ${pct(Math.min(...shares))} to ${pct(Math.max(...shares))} of the derived cost per build, so the cached-input price is not the main lever for kind of work (i). ${rowChip(cd.aa3dShares[0].row)}</p></div>
      </div>`;

    const S = D.subscriptions, byLab = lab => S.statements.filter(s => s.lab === lab);
    const quote = s => `<blockquote><p>“${esc(s.quote)}”</p><footer>${a(s.url, esc(new URL(s.url).hostname + new URL(s.url).pathname))} · ${chip(s.cls, s.retrieved, s.url, 'Vendor statement')}</footer><small>${esc(s.meaning)}</small></blockquote>`;
    const plus = S.messages.filter(m => m.plan === 'openai-plus');
    $('#dial-subs').innerHTML = `<div class="section-head"><h2>Subscriptions: how model choice counts against limits</h2><p>What the vendors say, quoted and dated. Where they publish no number, we say so.</p></div>
      <div class="cr-grid">
        <div class="card"><span class="lab">Claude plans (Claude Code)</span>${byLab('anthropic').map(quote).join('')}
          <p class="nonum"><b>No number exists.</b> Anthropic publishes no per-model weight for the weekly limit: how much of it one Opus 5.5 task uses compared with a Sonnet 5.5 task is not stated anywhere. The dial therefore shows Claude costs at list price and labels them as a proxy until our fleet measures limit points per accepted task.</p></div>
        <div class="card"><span class="lab">ChatGPT plans (Codex)</span>${byLab('openai').map(quote).join('')}
          <div class="tbl-scroll"><table><thead><tr><th>Model</th><th class="r">Input</th><th class="r">Cached</th><th class="r">Output</th><th class="r">Plus messages / 5 h</th><th>Source</th></tr></thead><tbody>${credits.filter(c => D.models[c.model].current).map(c => { const msg = plus.find(m => m.model === c.model);
            return `<tr><td>${esc(D.models[c.model].name)}</td><td class="r num">${c.input}</td><td class="r num">${c.cacheRead}</td><td class="r num">${c.output}</td><td class="r num">${msg ? msg.min + '–' + msg.max : 'n/a'}</td><td>${chip(c.cls, c.retrieved, c.url, 'Vendor rate card')}</td></tr>`; }).join('')}</tbody></table></div>
          <p class="detail-quote">Credits per million tokens on the Standard rate card. They equal the API list price times 25 [L], which prices purchased credits; it does not tell how included usage weighs each model. The message ranges are OpenAI's estimates, not entitlements.</p>
          <p class="nonum"><b>No number exists</b> for how much of a plan's included weekly usage one task of each model consumes. OpenAI states that usage depends on the model, the task and the context.</p></div>
      </div>`;

    const rows = F.rows, cases = rows.reduce((s, r) => s + r.cards, 0);
    $('#dial-fleet').innerHTML = `<div class="section-head"><h2>Measured in our fleet</h2><p>Its own evidence class, F. Anonymised: no task or project names.</p></div>
      <div class="card fleet"><div class="chips"><span class="chip ev-F">F · measured in our fleet</span><span class="chip">Window ${esc(date(F.windowStart))} to ${esc(date(F.windowEnd))} (${F.days} days)</span><span class="chip">${num(cases)} cases</span><span class="chip">List price at execution time</span></div>
        <p>${esc(F.population)} About ${X.bigMoney(F.totalListUsd)} of list-price spend over the window, ${pct(F.codingShare)} of it coding runs.</p>
        <div class="tbl-scroll"><table><thead><tr><th>Model, level</th><th class="r">Tasks</th><th class="r">Accepted</th><th class="r">First review passed</th><th class="r">Median USD per accepted task</th><th class="r">Median tokens per accepted task</th></tr></thead><tbody>${rows.map(r => `<tr><td>${esc(D.models[r.model].name)} ${esc(eff(r.effort))}${D.models[r.model].current ? '' : ' <small>(previous Sol)</small>'}</td><td class="r num">${r.cards}</td><td class="r num">${r.accepted}</td><td class="r num">${r.firstReviewPassed}/${r.firstReviewed}</td><td class="r num">${money(r.usdPerAccepted)}</td><td class="r num">${big(r.tokensPerAccepted)}</td></tr>`).join('')}</tbody></table></div>
        <div><span class="lab">Read with care</span><ul class="notes">${F.caveats.map(c => `<li>${esc(c)}</li>`).join('')}</ul></div>
        <p class="detail-quote">Cache reads over the same window: ${F.cacheReads.map(o => `${esc(D.models[o.model].name)} ${esc(eff(o.effort))} ${big(o.tokens)} cached tokens${o.share != null ? ', ' + pct(o.share) + ' of its list-price cost' : ', cost share not reported'}`).join('; ')}. GPT-6 Sol is the previous Sol model; its rows are the measured anchor from which the dial reprices GPT-6.1 Sol. ${a(F.record, 'Cohort record')}</p></div>`;
  }

  /* ---------- Effort ladders ---------- */
  async function renderLadders(host, ctx) {
    try { await ensure(ctx); } catch (error) { host.innerHTML = '<p class="card">The ladder data could not be loaded. Please try again.</p>'; console.error(error); return; }
    if (!st.ladderBuilt) {
      st.ladderBuilt = true;
      host.innerHTML = `<div class="section-head"><h2>Effort ladders: does a cheaper model at a higher level beat a stronger one?</h2><p>Accuracy against cost per task by reasoning level for Opus, Sonnet and Haiku 5.5, GPT-6.1 Sol, GPT-6.1 Terra and GPT-6 Luna.</p></div>
        <div class="layout"><div class="stage"><div class="stage-head"><div class="seg" id="lad-seg" role="group" aria-label="Benchmark">${D.ladders.map(l => `<button data-l="${l.id}" aria-pressed="${st.ladder === l.id}">${esc(D.benchmarks[l.bench].name.split('/')[0].replace(' Semi-Private', '').trim())}</button>`).join('')}</div>
          <div class="seg" id="lad-lab" role="group" aria-label="Lab">${[['all', 'Both labs'], ['anthropic', 'Claude'], ['openai', 'OpenAI']].map(([k, v]) => `<button data-k="${k}" aria-pressed="${st.lab === k}">${v}</button>`).join('')}</div></div>
          <div class="plot" id="lad-plot"></div><p class="stage-note" id="lad-note"></p></div>
          <aside class="side" id="lad-side"></aside></div>
        <div class="card" id="lad-verdicts"></div>`;
      host.querySelectorAll('#lad-seg button').forEach(b => b.onclick = () => { st.ladder = b.dataset.l; host.querySelectorAll('#lad-seg button').forEach(x => x.setAttribute('aria-pressed', x === b)); drawLadder(); });
      host.querySelectorAll('#lad-lab button').forEach(b => b.onclick = () => { st.lab = b.dataset.k; host.querySelectorAll('#lad-lab button').forEach(x => x.setAttribute('aria-pressed', x === b)); drawLadder(); });
    }
    drawLadder();
  }

  function ladderPoints(l) {
    return l.rows.map(id => ({ id, ...D.rows[id] })).filter(r => r.cost != null && (st.lab === 'all' || D.models[r.model].lab === st.lab))
      .map(r => ({ key: r.model + '|' + r.effort, model: r.model, name: D.models[r.model].name, v: D.models[r.model].lab, effort: r.effort, score: r.score, cost: r.cost, id: r.id, restricted: false }));
  }

  function verdicts(l) {
    const rows = l.rows.map(id => ({ id, ...D.rows[id] })).filter(r => r.cost != null);
    const out = [];
    for (const lab of ['anthropic', 'openai']) {
      if (st.lab !== 'all' && st.lab !== lab) continue;
      const ms = [...new Set(rows.filter(r => D.models[r.model].lab === lab).map(r => r.model))].sort((p, q) => D.models[p].output - D.models[q].output);
      for (let i = 0; i < ms.length; i++) for (let j = i + 1; j < ms.length; j++) {
        const cheap = rows.filter(r => r.model === ms[i]), strong = rows.filter(r => r.model === ms[j]);
        for (const s of strong) {
          const higher = cheap.filter(c => RANK[c.effort] > RANK[s.effort]);
          if (!higher.length) continue;
          const reach = higher.filter(c => c.score >= s.score).sort((p, q) => p.cost - q.cost)[0];
          if (reach) out.push({ s, c: reach, kind: reach.cost <= s.cost ? 'wins' : 'costlier' });
          else out.push({ s, c: higher.sort((p, q) => q.score - p.score)[0], kind: 'short' });
        }
      }
    }
    return out;
  }

  function drawLadder() {
    const l = D.ladders.find(x => x.id === st.ladder), B = D.benchmarks[l.bench], pts = ladderPoints(l);
    const unit = B.unit.startsWith('%') ? B.unit : B.unit;
    X.drawChart($('#lad-plot'), pts, { ladder: true, effort: 'all', interactive: false, labelAll: true, yLabel: B.unit, xLabel: `Cost per ${l.per}`, sig: 'lad' + st.ladder + st.lab, aria: `${B.name}: cost against score by reasoning level`, height: 420 });
    const levels = new Set(pts.map(p => p.effort));
    const tbNote = levels.size === 1 ? `Every Claude and OpenAI row on this board runs at ${[...levels][0]}, so it compares models, not levels.` : 'Lines join the levels of one model, from its cheapest to its most expensive row. The white line is the frontier: no row is both cheaper and better.';
    $('#lad-note').innerHTML = `${tbNote} Cost axis is log scale. ${esc(B.name)}, ${esc(B.publisher)}: ${l.rows.length ? rowChip(l.rows[0]) : ''}`;
    const v = verdicts(l), count = k => v.filter(x => x.kind === k).length;
    const side = [];
    if (l.id === 'tb41') {
      const son = D.rows[l.rows.find(id => D.rows[id].model === 'claude-sonnet-5-5')], op = D.rows[l.rows.find(id => D.rows[id].model === 'claude-opus-5-5')];
      side.push(`<div class="card"><span class="lab">Sonnet's token volume</span><p>Sonnet 5.5 lists at half of Opus 5.5 and still costs <b class="num">${money(son.cost)}</b> per trial against <b class="num">${money(op.cost)}</b>. Over ${son.trials} trials it wrote ${big(son.outputTokens)} output tokens; Opus wrote ${big(op.outputTokens)}.</p><p class="detail-quote">Scores: Opus ${op.score}%, Sonnet ${son.score}%, margin about ±3 points. ${rowChip(l.rows.find(id => D.rows[id].model === 'claude-sonnet-5-5'))}</p></div>`);
    } else {
      side.push(`<div class="card"><span class="lab">Cheaper model at a higher level</span><div class="bigtally"><div><b>${count('wins')}</b><span>beats the stronger model: at least the score, for no more money</span></div><div><b>${count('costlier')}</b><span>reaches the score, but costs more</span></div><div><b>${count('short')}</b><span>falls short at every higher level</span></div></div></div>`);
    }
    side.push(`<div class="card"><span class="lab">How to read it</span><ul class="notes"><li>${l.id === 'arc2' ? 'ARC-AGI-2 is an abstract-reasoning test. Use it for the shape of an effort ladder and its cost, not as coding or creative quality.' : l.id === 'aa5' ? 'The Intelligence Index is a composite of nine evaluations. A composite hides strengths, so check the kind of work too.' : 'Terminal-Bench 4.1 is coding evidence: command-line tasks checked by test suites, in each vendor’s native agent.'}</li><li>Light models (Haiku 5.5, Luna) collapse at low and medium on hard reasoning. The dial uses them only for pure docs or config rounds and for summaries.</li><li>None of these boards says anything about creative work; see the Quality dial for that.</li></ul></div>`);
    $('#lad-side').innerHTML = side.join('');
    const row = x => `<tr class="v-${x.kind}"><td>${esc(label(x.s.model, x.s.effort))}</td><td class="r num">${fmtScore(x.s.score)}</td><td class="r num">${money(x.s.cost)}</td><td>${esc(label(x.c.model, x.c.effort))}</td><td class="r num">${fmtScore(x.c.score)}</td><td class="r num">${money(x.c.cost)}</td><td>${x.kind === 'wins' ? '<b>Cheaper model wins</b>' : x.kind === 'costlier' ? 'Same score, costs more' : 'Falls short'}</td><td>${rowChip(x.c.id)}</td></tr>`;
    $('#lad-verdicts').innerHTML = v.length ? `<span class="lab">Every pair: stronger model at a lower level against the cheaper model of the same lab at a higher level</span><p class="detail-quote">For each stronger row, the cheapest higher-level row of the cheaper model that reaches its score; if none does, that model's best higher-level row.</p><div class="tbl-scroll"><table><thead><tr><th>Stronger model, level</th><th class="r">Score</th><th class="r">Cost</th><th>Cheaper model, higher level</th><th class="r">Score</th><th class="r">Cost</th><th>Verdict</th><th>Source</th></tr></thead><tbody>${v.map(row).join('')}</tbody></table></div>`
      : `<span class="lab">No level pairs on this board</span><p>All rows run at the same level, so no cheaper model at a higher level can be compared with a stronger model at a lower level here. Use ARC-AGI-2 or the Intelligence Index for ladders.</p>`;
  }
  const fmtScore = s => Number.isInteger(s) ? String(s) : s.toFixed(1);

  function resize() { if (st.built && $('#month-curve')?.offsetParent) drawMonth(); if (st.ladderBuilt && $('#lad-plot')?.offsetParent) drawLadder(); }
  return { renderDial, renderLadders, resize };
})();
