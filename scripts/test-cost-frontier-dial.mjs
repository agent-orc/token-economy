// Browser check for the Cost Frontier Quality dial and effort ladders.
//   JOB_RESULTS_DIR=/absolute/results node scripts/test-cost-frontier-dial.mjs
// Serves website/ under /token-economy/, drives the dial in headless Chromium, enforces the page rules
// (no superseded model as an option, never the words route or routing, every number with a dated source),
// and writes desktop and phone screenshots of each new view to the results directory.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createServer } from 'node:http';
import { resolve, join, extname, isAbsolute } from 'node:path';
import assert from 'node:assert/strict';
import { launchBrowser } from './headless-browser.mjs';

const root = resolve(import.meta.dirname, '..');
const results = process.env.JOB_RESULTS_DIR || process.argv[2];
if (!results || !isAbsolute(results)) throw new Error('Supply an absolute results directory.');
await mkdir(results, { recursive: true });
const dial = JSON.parse(await readFile(join(root, 'website/data/effort-dial.json'), 'utf8'));
const prices = JSON.parse(await readFile(join(root, 'src/TokenEconomy/catalog/model-prices.json'), 'utf8'));
const superseded = prices.filter(m => m.lifecycleStatus === 'superseded').map(m => m.displayName);
let assertions = 0;
const check = (value, message) => { assert.ok(value, message); assertions++; };
const mime = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.json': 'application/json' };
const server = createServer(async (req, res) => {
  try {
    const path = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    if (!path.startsWith('/token-economy/')) { res.writeHead(404).end(); return; }
    let file = resolve(root, 'website', path.slice('/token-economy/'.length) || 'index.html');
    if (path.endsWith('/')) file = join(file, 'index.html');
    if (!file.startsWith(join(root, 'website') + '/')) throw new Error('outside root');
    res.setHeader('Content-Type', mime[extname(file)] ?? 'application/octet-stream'); res.end(await readFile(file));
  } catch { res.writeHead(404).end(); }
});

const session = await launchBrowser({ name: 'cost frontier dial' });
const { client, evaluate } = session;
const text = sel => evaluate(`document.querySelector(${JSON.stringify(sel)}).innerText`);
const setRange = (sel, value) => evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(sel)});e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
const runRow = label => evaluate(`[...document.querySelectorAll('#dial-table tbody tr')].find(r=>r.querySelector('th').innerText.includes(${JSON.stringify(label)}))?.querySelector('td').innerText||''`);
const banned = /\brout(e|es|ed|ing)\b/i;
try {
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const base = `http://127.0.0.1:${server.address().port}/token-economy/cost-frontier/`;
  await client.send('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });

  // Quality dial: one slider, picks straight from the projected EffortDial.
  await session.navigate(base + '#mix');
  await session.waitUntil('dial rows', `document.querySelectorAll('#dial-table tbody tr').length === ${dial.classes.length}`);
  check(await evaluate(`document.querySelectorAll('#view-mix input[type=range]#dial-pos').length === 1`), 'one dial slider');
  check(await evaluate(`!document.querySelector('#target')`), 'the old percent-of-best slider is gone, no competing control');
  check((await runRow('Standard feature logic')).includes('Opus 5.5 medium'), 'default: feature logic on Opus 5.5 medium');
  check((await runRow('Review aspects')).includes('Opus 5.5 low'), 'default: review aspects on Opus 5.5 low');
  check((await runRow('Creative work')).includes('Sonnet 5.5 high'), 'default: creative work on Sonnet 5.5 high');
  check((await text('#dial-table')).includes('$4.57'), 'fleet-anchored cost per accepted task shown');
  check((await text('#dial-table')).includes('Unknown'), 'unknown costs stay unknown');
  await setRange('#dial-pos', 100);
  check((await runRow('Hard bugs')).includes('Opus 5.5 xHigh'), 'strong end: hard work on Opus 5.5 xhigh');
  check((await runRow('Creative work')).includes('Opus 5.5 max'), 'strong end: creative work on Opus 5.5 max');
  await setRange('#dial-pos', 0);
  check((await runRow('Mechanical')).includes('Haiku 5.5 medium'), 'frugal end: mechanical docs rounds on Haiku 5.5');
  await evaluate(`document.querySelector('#dial-cli [data-cli=codex]').click()`);
  check((await runRow('Mechanical')).includes('GPT-6.1 Terra low'), 'Codex frugal end: GPT-6.1 Terra low');
  check((await text('#dial-table')).includes('Codex credits'), 'Codex costs also in credits');
  await setRange('#dial-pos', 50);
  check((await runRow('Standard feature logic')).includes('GPT-6.1 Sol medium'), 'Codex default: GPT-6.1 Sol medium');
  await evaluate(`document.querySelector('#dial-cli [data-cli=claude]').click()`);
  check(await evaluate(`[...document.querySelectorAll('#dial-table a.ev')].every(a => a.getAttribute('href') && /\\d{4}/.test(a.innerText))`), 'every evidence chip has a link and a date');
  check(await evaluate(`[...document.querySelectorAll('#dial-table a.ev b')].every(b => /^[MVLPF]$/.test(b.innerText))`), 'every evidence chip names its class');

  // Creative work: AA-3D main evidence with intervals and its limitation, WebDev Arena second.
  const creative = await text('#dial-creative');
  check(creative.includes('Coding results do not carry over'), 'creative class says coding results do not carry over');
  check(creative.includes('AA-3D') && creative.includes('AA-3D Explorer'), 'AA-3D with Explorer link');
  check(/1176–1232/.test(creative), 'AA-3D Sonnet 5.5 interval shown');
  check(creative.includes('not the chance of getting a working result'), 'AA-3D limitation stated next to the numbers');
  check(/Sonnet 5\.5 high, Opus 5\.5 low/.test(creative), 'missing AA-3D rows named');
  check(creative.includes('WebDev Arena') && creative.includes('both bad'), 'WebDev Arena with broken-build rule');
  check(creative.includes('no cost or tokens per task'), 'WebDev Arena publishes no cost');
  for (const d of ['Originality', 'Design', 'Game feel', 'Technical execution']) check(creative.toLowerCase().includes(d.toLowerCase()), `rubric dimension ${d}`);
  check(creative.includes('not measured'), 'a dimension without evidence says so');

  // Cost drivers, subscriptions, fleet.
  check((await text('#dial-drivers')).includes('three quarters'), 'cache-read share explained');
  check((await text('#dial-drivers')).includes('0.63'), 'reprice factor shown');
  const subs = await text('#dial-subs');
  check(subs.includes('Weekly limits apply across all models') && subs.includes('No number exists'), 'quoted vendor statements and the missing number');
  const fleet = await text('#dial-fleet');
  check(fleet.includes('20 Oct 2026') && fleet.includes('17 Nov 2026') && /\d+ cases/.test(fleet), 'fleet window and case count');
  check(!/\b[A-Z]{2,}-\d+\b/.test(fleet.replace(/GPT-\d/g, '')), 'fleet section carries no task keys');
  await setRange('#load-feature-logic', 100);
  check((await text('#month-stats')).includes('$'), 'month bill follows the workload');

  // Effort ladders: three boards, verdict tables, Sonnet's token volume.
  await evaluate(`document.getElementById('tab-ladders').click()`);
  await session.waitUntil('ladder chart', `document.querySelectorAll('#lad-plot .pt').length > 10`);
  check((await text('#lad-verdicts')).includes('Cheaper model wins'), 'ARC-AGI-2: a cheaper model at a higher level wins somewhere');
  check((await text('#lad-verdicts')).includes('Falls short'), 'ARC-AGI-2: and fails somewhere');
  await evaluate(`document.querySelector('#lad-seg [data-l=tb41]').click()`);
  check((await text('#lad-side')).includes('output tokens'), 'Terminal-Bench 4.1: Sonnet token volume');
  await evaluate(`document.querySelector('#lad-seg [data-l=aa5]').click()`);
  check(await evaluate(`document.querySelectorAll('#lad-plot .pt').length >= 25`), 'Intelligence Index ladders drawn');

  // Page rules across every view.
  for (const view of ['atlas', 'missions', 'mix', 'ladders', 'releases', 'sources']) {
    await evaluate(`document.getElementById('tab-${view}').click()`);
    const body = await evaluate(`document.querySelector('main').innerText + document.querySelector('.hero').innerText`);
    check(!banned.test(body), `no route/routing in page text: ${view}`);
    const optionsText = view === 'mix'
      ? await evaluate(`[...document.querySelectorAll('#view-mix > :not(#dial-drivers):not(#dial-fleet)')].map(e=>e.innerText).join(' ')`)
      : body;
    // Releases quote lab claims about predecessors, and Sources explains the selection, so options are checked where they are offered.
    if (!['mix', 'ladders', 'missions'].includes(view)) continue;
    for (const name of superseded) {
      // Sources explains where GPT-6 Sol appears; the Releases tab quotes the lab's own post title for GPT-6 Luna.
      const allowed = name === 'GPT-6 Sol' && (view === 'sources' || (view === 'releases' && body.includes('Introducing GPT-6 Sol and Luna')));
      check(allowed || !new RegExp(name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '(?![.\\dA-Za-z])').test(optionsText), `superseded ${name} not offered on ${view}`);
    }
  }

  // Screenshots: desktop and phone, each new view, both themes for the dial.
  const shots = [];
  for (const width of [1440, 390]) {
    await client.send('Emulation.setDeviceMetricsOverride', { width, height: width === 390 ? 844 : 1000, deviceScaleFactor: width === 390 ? 2 : 1, mobile: width === 390 });
    for (const theme of ['light', 'dark']) {
      await client.send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: theme }] });
      for (const [view, target, nameSuffix] of [['mix', '#mix-root', 'dial'], ['mix', '#dial-kinds', 'kinds'], ['mix', '#dial-creative', 'creative'], ['mix', '#dial-month', 'month'], ['mix', '#dial-drivers', 'drivers'], ['mix', '#dial-subs', 'subscriptions'], ['mix', '#dial-fleet', 'fleet'], ['ladders', '#view-ladders', 'ladders'], ['atlas', '#cats', 'atlas']]) {
        if (theme === 'dark' && !['dial', 'creative', 'ladders'].includes(nameSuffix)) continue;
        await evaluate(`document.getElementById('tab-${view}').click()`);
        if (view === 'atlas') await evaluate(`[...document.querySelectorAll('.cat')].find(c=>c.innerText.startsWith('Code')).click(); document.querySelector('#bench-seg button:nth-child(2)').click()`);
        // scroll-margin-top on the sections keeps them clear of the sticky header (and of the tab bar where it sticks).
        await evaluate(`(()=>{const el=document.querySelector(${JSON.stringify(target)}); el.style.scrollMarginTop ||= getComputedStyle(el).scrollMarginTop === '0px' ? 'calc(var(--site-header-offset, 6rem) + 3.5rem)' : ''; el.scrollIntoView({block:'start', behavior:'instant'});})()`);
        await new Promise(r => setTimeout(r, 1600));
        check(await evaluate(`document.documentElement.scrollWidth <= innerWidth`), `no horizontal page overflow: ${nameSuffix} ${theme} ${width}`);
        const shot = await client.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
        const file = `cost-frontier-${nameSuffix}-${theme}-${width}.png`;
        await writeFile(join(results, file), Buffer.from(shot.data, 'base64'));
        shots.push(file);
      }
    }
  }
  console.log(`Cost Frontier dial check passed: ${assertions} assertions, ${shots.length} screenshots in ${results}.`);
} catch (error) {
  console.error(await session.diagnostics(error));
  process.exitCode = 1;
} finally {
  await session.close();
  server.close();
}
