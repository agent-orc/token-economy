import { spawn } from 'node:child_process';
import { readFile, writeFile, mkdir, mkdtemp, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve, join, extname, isAbsolute } from 'node:path';
import { tmpdir } from 'node:os';
import assert from 'node:assert/strict';
const root = resolve(import.meta.dirname, '..');
const results = process.env.JOB_RESULTS_DIR || process.argv[2];
if (!results || !isAbsolute(results)) throw new Error('Supply an absolute results directory.');
await mkdir(results, { recursive: true });
let assertions = 0;
const check = (value, message) => { assert.ok(value, message); assertions++; };
function waitForDevTools(browserProcess) {
  return new Promise((resolveUrl, reject) => {
    let stderr = "";
    const timeout = setTimeout(() => reject(new Error(`Timed out waiting for DevTools. ${stderr}`)), 15_000);

    browserProcess.stderr.setEncoding("utf8");
    browserProcess.stderr.on("data", (chunk) => {
      stderr += chunk;
      const match = stderr.match(/DevTools listening on (ws:\/\/[^\s]+)/);
      if (match) {
        clearTimeout(timeout);
        resolveUrl(match[1]);
      }
    });
    browserProcess.once("exit", (code) => {
      clearTimeout(timeout);
      reject(new Error(`Browser exited before DevTools started (code ${code}). ${stderr}`));
    });
  });
}

function createCdpClient(webSocketUrl) {
  const socket = new WebSocket(webSocketUrl);
  const pending = new Map();
  const eventWaiters = new Map();
  let nextId = 1;

  socket.addEventListener("message", ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) {
      const request = pending.get(message.id);
      if (!request) return;
      pending.delete(message.id);
      if (message.error) request.reject(new Error(message.error.message));
      else request.resolve(message.result);
      return;
    }

    const waiters = eventWaiters.get(message.method);
    if (!waiters?.length) return;
    eventWaiters.delete(message.method);
    for (const waiter of waiters) waiter(message.params);
  });

  const opened = new Promise((resolveOpen, reject) => {
    socket.addEventListener("open", resolveOpen, { once: true });
    socket.addEventListener("error", () => reject(new Error("Could not connect to the page DevTools socket.")), { once: true });
  });

  return {
    async send(method, params = {}) {
      await opened;
      const id = nextId++;
      const response = new Promise((resolveResponse, rejectResponse) => {
        const timeout = setTimeout(() => {
          pending.delete(id);
          rejectResponse(new Error(`Timed out waiting for ${method}.`));
        }, 15_000);
        pending.set(id, {
          resolve(result) {
            clearTimeout(timeout);
            resolveResponse(result);
          },
          reject(error) {
            clearTimeout(timeout);
            rejectResponse(error);
          },
        });
      });
      socket.send(JSON.stringify({ id, method, params }));
      return response;
    },
    waitFor(method) {
      return new Promise((resolveEvent, rejectEvent) => {
        const waiters = eventWaiters.get(method) ?? [];
        const timeout = setTimeout(() => {
          const activeWaiters = eventWaiters.get(method) ?? [];
          eventWaiters.set(method, activeWaiters.filter((waiter) => waiter !== complete));
          rejectEvent(new Error(`Timed out waiting for ${method}.`));
        }, 15_000);
        const complete = (params) => {
          clearTimeout(timeout);
          resolveEvent(params);
        };
        waiters.push(complete);
        eventWaiters.set(method, waiters);
      });
    },
    close() {
      socket.close();
    },
  };
}



const seed = JSON.parse(await readFile(join(root, 'website/data/language-capabilities.json'), 'utf8'));
let payload = seed;
let failData = false;
const mime = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.json': 'application/json' };
const server = createServer(async (req, res) => {
  try {
    let path = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    if (!path.startsWith('/token-economy/')) { res.writeHead(404).end(); return; }
    if (path.endsWith('/data/language-capabilities.json')) {
      if (failData) { res.writeHead(503).end(); return; }
      res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(payload)); return;
    }
    let file = resolve(root, 'website', path.slice('/token-economy/'.length) || 'index.html');
    if (path.endsWith('/')) file = join(file, 'index.html');
    if (!file.startsWith(join(root, 'website') + '/')) throw new Error('outside root');
    res.setHeader('Content-Type', mime[extname(file)] ?? 'application/octet-stream'); res.end(await readFile(file));
  } catch { res.writeHead(404).end(); }
});
const browserPath = process.env.CHROME_PATH || ['/usr/bin/chromium', '/usr/bin/google-chrome',
  ...['1243', '1217'].map(v => `/home/agent/.cache/ms-playwright/chromium_headless_shell-${v}/chrome-headless-shell-linux64/chrome-headless-shell`)].find(existsSync);
if (!browserPath) throw new Error('Set CHROME_PATH to Chromium.');
await new Promise(r => server.listen(0, '127.0.0.1', r));
const url = `http://127.0.0.1:${server.address().port}/token-economy/human-friendly-language/`;
const profile = await mkdtemp(join(tmpdir(), 'te56-browser-'));
const browser = spawn(browserPath, ['--headless=new', '--no-sandbox', '--disable-gpu', '--remote-debugging-port=0',
  `--user-data-dir=${profile}`, 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'] });
let client;
try {
  const socket = await waitForDevTools(browser); const http = new URL(socket); http.protocol = 'http:'; http.pathname = '/json/list';
  const pages = await (await fetch(http)).json(); client = createCdpClient(pages.find(p => p.type === 'page').webSocketDebuggerUrl);
  await client.send('Page.enable'); await client.send('Runtime.enable');
  const evaluate = async expression => {
    const result = await client.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails)); return result.result.value;
  };
  const ready = async () => evaluate(`new Promise((resolve,reject)=>{let n=0;const t=setInterval(()=>{
    const text=document.getElementById('load-status').textContent;
    if(!text.startsWith('Loading')){clearInterval(t);resolve(text);}else if(++n>100){clearInterval(t);reject(new Error(text));}
  },50);})`);
  const navigate = async () => {
    const loaded = client.waitFor('Page.loadEventFired'); await client.send('Page.navigate', { url }); await loaded; return ready();
  };
  const set = async (id, value) => evaluate(`(()=>{const e=document.getElementById(${JSON.stringify(id)});
    e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  check((await navigate()).includes(seed.asOfDate), 'seed data date renders');
  check(await evaluate(`document.querySelectorAll('#matrix-body tr').length === 22`), '22 language rows');
  check(await evaluate(`document.querySelectorAll('#study-list article').length === 5`), 'five primary studies');
  check(await evaluate(`document.querySelectorAll('#cost-body tr').length === 0`), 'unknown scores never become priced recommendations');
  check(await evaluate(`document.getElementById('quality-status').textContent.includes('No priced measurements')`), 'useful empty state');
  await set('language', 'de'); check(await evaluate(`document.querySelectorAll('#matrix-body tr').length === 11`), 'German filter');
  await set('thinking', 'medium'); check(await evaluate(`document.querySelectorAll('#matrix-body tr').length === 0`), 'no invented medium scores');
  await set('thinking', 'all'); await set('language', 'all');
  await evaluate(`document.querySelector('#matrix-body a').click()`);
  check(await evaluate(`document.querySelector('#evidence-list details').open`), 'evidence link expands dated sources');
  await evaluate(`history.replaceState(null,'',location.pathname);document.querySelector('#evidence-list details').open=false`);
  for (const theme of ['light', 'dark']) for (const width of [1440, 390]) {
    await client.send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: theme }] });
    await client.send('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: width === 390 });
    check(await evaluate(`document.documentElement.scrollWidth <= innerWidth`), `no page overflow: ${theme}/${width}`);
    await evaluate('scrollTo(0,0)');
    const viewport = await client.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    await writeFile(join(results, `language-${theme}-${width}.png`), Buffer.from(viewport.data, 'base64'));
    await evaluate(`document.getElementById('catalog').scrollIntoView()`);
    const matrixShot = await client.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    await writeFile(join(results, `language-matrix-${theme}-${width}.png`), Buffer.from(matrixShot.data, 'base64'));
  }
  // Real rendering of synthetic measurements, isolated to this server; no fixture is published.
  payload = structuredClone(seed);
  for (const [index, cost, score] of [[0, .0002, .8], [2, .0001, .9], [4, .00001, 0]]) {
    const row = payload.records[index]; row.thinkingLevel = 'medium'; row.status = 'observed';
    row.scores = Object.fromEntries(payload.dimensions.map(d => [d, score])); row.overall = score; row.costPerSampleUsd = cost;
    row.notes = '<img src=x onerror="window.injection=true"> Synthetic fixture';
    const quality = payload.costQuality.find(r => r.recordId === row.id);
    Object.assign(quality, { status: row.status, thinkingLevel: row.thinkingLevel, overall: score, costPerSampleUsd: cost,
      costPerQualityPointUsd: score > 0 ? cost / score : null });
  }
  check((await navigate()).includes(seed.asOfDate), 'measured fixture renders');
  check(await evaluate(`document.querySelectorAll('#cost-body tr').length === 2`), 'positive costs and qualifying quality');
  check(await evaluate(`document.querySelector('#cost-body tr').textContent.includes('gpt-6-sol')`), 'cheapest passing row first');
  check(await evaluate(`!window.injection && !document.querySelector('#evidence-list img')`), 'imported strings render as text');
  await set('threshold', '0'); check(await evaluate(`document.querySelectorAll('#cost-body tr').length === 3`), 'measured zero is retained');
  check(await evaluate(`document.querySelector('#cost-body tr').textContent.includes('—')`), 'zero quality has no ratio');
  await set('threshold', '1.2'); check(await evaluate(`document.getElementById('quality-status').textContent.includes('Enter a threshold')`), 'invalid threshold rejected');
  await set('threshold', ''); check(await evaluate(`document.querySelectorAll('#cost-body tr').length === 0`), 'blank threshold rejected');
  failData = true;
  check((await navigate()).includes('could not load'), 'network error renders fallback');
  const taskLoaded = client.waitFor('Page.loadEventFired');
  await client.send('Page.navigate', { url: url.replace('human-friendly-language/', 'task-class-routing/') });
  await taskLoaded;
  await evaluate(`new Promise((resolve,reject)=>{let n=0;const t=setInterval(()=>{
    if(document.querySelectorAll('#catalog tbody tr').length===14){clearInterval(t);resolve(true);}
    else if(++n>100){clearInterval(t);reject(new Error('Task studies did not render'));}
  },50);})`);
  check(await evaluate(`getComputedStyle(document.body).fontFamily.includes('BlinkMacSystemFont')`), 'task-study page loads extracted shared styles');
  check(await evaluate(`document.documentElement.scrollWidth <= innerWidth`), 'task-study page retains mobile layout');
  await writeFile(join(results, 'language-browser-smoke.json'), JSON.stringify({ passed: true, assertions,
    themes: ['light', 'dark'], widths: [1440, 390], syntheticDataIsTestOnly: true }, null, 2));
  console.log(`${assertions} language browser checks passed; screenshots in ${results}`);
} finally {
  client?.close(); browser.kill(); server.close(); await rm(profile, { recursive: true, force: true }).catch(() => {});
}
