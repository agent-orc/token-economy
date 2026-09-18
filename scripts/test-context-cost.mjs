import { spawn, execFileSync } from 'node:child_process';
import { readFile, writeFile, mkdir, mkdtemp, rm, readdir } from 'node:fs/promises';
import { readFileSync, existsSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve, join, extname } from 'node:path';
import { tmpdir } from 'node:os';
import assert from 'node:assert/strict';
import { forecast } from '../website/context-cost/model.mjs';
const root=resolve(import.meta.dirname,'..');
const results=process.env.JOB_RESULTS_DIR || process.argv[2];
if(!results)throw new Error('Set JOB_RESULTS_DIR or supply an absolute results directory.');
await mkdir(results,{recursive:true});
const fixture=join(results,'library-forecasts.json');
execFileSync('dotnet',['run','--project',join(root,'tools/ContextCostReport'),'-c','Release','--',fixture],{cwd:root,stdio:'inherit'});
const report=JSON.parse(await readFile(fixture,'utf8'));
const models=JSON.parse(await readFile(join(root,'website/data/price-history.json'),'utf8')).models;
let assertions=0;
function check(value,message){assert.ok(value,message);assertions++;}
for(const c of report.cases){
 const f=forecast(c.options,models.find(m=>m.modelId===c.options.modelId));
 check(Math.abs(f.total-c.forecast.total)<1e-8,'C# / browser total parity');
 for(let i=0;i<f.turns.length;i++){
  const a=f.turns[i],b=c.forecast.turns[i];
  for(const k of ['contextTokens','contextBeforeCompaction','cumulativeCost'])check(Math.abs(a[k]-b[k])<1e-8,k+' parity');
  for(const k of ['input','output','cacheRead','cacheWrite'])check(a.usage[k]===b.usage[k],'usage '+k+' parity');
  for(const [key,property] of [['input','inputCost'],['output','outputCost'],['cacheRead','cacheReadCost'],['cacheWrite','cacheWriteCost']])
   check(Math.abs(a.components[key]-b.cost[property]-(b.compactionCost?.[property]??0))<1e-8,'component '+key+' parity');
 }
}
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


const mime={'.html':'text/html','.css':'text/css','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json'};
const server=createServer(async(req,res)=>{try{
 let path=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
 if(!path.startsWith('/token-economy/')){res.writeHead(404).end();return;}
 let file=resolve(root,'website',path.slice('/token-economy/'.length)||'index.html');
 if(path.endsWith('/'))file=join(file,'index.html');
 if(!file.startsWith(join(root,'website')+'/'))throw new Error('outside root');
 res.setHeader('Content-Type',mime[extname(file)]??'application/octet-stream');res.end(await readFile(file));
}catch{res.writeHead(404).end();}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const url=`http://127.0.0.1:${server.address().port}/token-economy/context-cost/`;
const browserPath=process.env.CHROME_PATH || ['/usr/bin/chromium','/usr/bin/google-chrome',...['1243','1217'].map(v=>`/home/agent/.cache/ms-playwright/chromium_headless_shell-${v}/chrome-headless-shell-linux64/chrome-headless-shell`)].find(existsSync);
if(!browserPath)throw new Error('Set CHROME_PATH to Chromium.');
const profile=await mkdtemp(join(tmpdir(),'te54-browser-'));
const browser=spawn(browserPath,['--headless=new','--no-sandbox','--disable-gpu','--remote-debugging-port=0',`--user-data-dir=${profile}`,'about:blank'],{stdio:['ignore','ignore','pipe']});
let client;
try{
 const socket=await waitForDevTools(browser);const http=new URL(socket);http.protocol='http:';http.pathname='/json/list';
 const pages=await(await fetch(http)).json();client=createCdpClient(pages.find(p=>p.type==='page').webSocketDebuggerUrl);
 await client.send('Page.enable');await client.send('Runtime.enable');
 await client.send('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:resolve(results)});
 const evaluate=async expression=>{const r=await client.send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
 const set=async(id,value)=>evaluate(`(()=>{const e=document.getElementById(${JSON.stringify(id)});e.value=${JSON.stringify(String(value))};e.dispatchEvent(new Event('input',{bubbles:true}));return document.getElementById('error').textContent;})()`);
 const loaded=client.waitFor('Page.loadEventFired');await client.send('Page.navigate',{url});await loaded;
 await evaluate(`new Promise((resolve,reject)=>{let n=0;const t=setInterval(()=>{if(!document.getElementById('app').hidden){clearInterval(t);resolve(true);}else if(++n>100){clearInterval(t);reject(new Error(document.getElementById('loading').textContent));}},100);})`);
 check(await evaluate(`document.querySelectorAll('.chart svg').length===8`),'eight charts render');
 check(await evaluate(`!document.getElementById('output').hidden && !document.getElementById('error').textContent`),'initial forecast renders');
 for(const id of ['coding','orchestrator','general']){
  await evaluate(`document.querySelector('[data-id="${id}"]').click()`);
  for(const model of ['claude-opus-5','claude-sonnet-5','gpt-5.6-sol','gpt-6-astra']){
   check((await set('model',model))==='','model switch valid');
   const expected=report.findings.find(f=>f.scenario===id && f.model===model);
   const text=await evaluate(`document.getElementById('summary').textContent`);
   check(text.includes(new Intl.NumberFormat('en-US',{style:'currency',currency:'USD',maximumFractionDigits:4}).format(expected.longUsd)),'preset total matches C#');
  }
 }
 await set('model','gpt-5.6-sol');await set('reasoning','xhigh');await set('reasoningTokensPerTurn',1000);
 check(await evaluate(`document.getElementById('policy-note').textContent.includes('xhigh')`),'reasoning effort label');
 await set('successLong',.5);check(await evaluate(`document.getElementById('comparison').textContent.includes('Unknown')`),'unknown quota stays unknown');
 await set('quotaConversion',.1);check(await evaluate(`document.getElementById('comparison').textContent.includes('(user conversion)')`),'explicit quota conversion');
 await set('cacheMode','unknown');check(await evaluate(`document.getElementById('output').hidden`),'unknown TTL blocks fabricated cost');
 await set('cacheMode','hour');check(await evaluate(`document.getElementById('output').hidden`),'unsupported hour policy stays unknown');
 await set('cacheMode','provider');await set('date','2020-01-01');check(await evaluate(`document.getElementById('output').hidden`),'unknown dated price');
 await set('date','2026-09-18');await set('quotaConversion','');await set('model','claude-opus-5');await set('cacheMode','hour');check(await evaluate(`!document.getElementById('output').hidden`),'Claude one-hour tariff');
 await set('cacheMode','provider');await set('reasoning','medium');
 await evaluate(`document.querySelector('[data-id="coding"]').click()`);
 await set('tasks',3);await set('turnsPerTask',1);await set('gaps','0, 577, 500');check(await evaluate(`!document.getElementById('output').hidden`),'custom measured gaps');
 await set('gaps','0, 1');check(await evaluate(`document.getElementById('output').hidden`),'reject malformed gaps');
 await evaluate(`document.querySelector('[data-id="coding"]').click()`);
 await set('compactionThreshold',30000);await set('compactedContext',10000);
 for(const theme of ['light','dark'])for(const width of [1440,390]){
  await client.send('Emulation.setEmulatedMedia',{features:[{name:'prefers-color-scheme',value:theme}]});
  await client.send('Emulation.setDeviceMetricsOverride',{width,height:900,deviceScaleFactor:1,mobile:width===390});
  check(await evaluate(`document.documentElement.scrollWidth <= window.innerWidth`),'no horizontal overflow '+theme+' '+width);
  await evaluate('window.scrollTo(0,0)');
  const {cssContentSize}=await client.send('Page.getLayoutMetrics');
  const shot=await client.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width,height:Math.ceil(cssContentSize.height),scale:1}});
  await writeFile(join(results,`context-cost-${theme}-${width}.png`),Buffer.from(shot.data,'base64'));
  const viewport=await client.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
  await writeFile(join(results,`context-cost-${theme}-${width}-viewport.png`),Buffer.from(viewport.data,'base64'));
  await evaluate(`window.scrollTo(0,document.getElementById('charts').getBoundingClientRect().top+window.scrollY)`);
  const chartShot=await client.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
  await writeFile(join(results,`context-cost-${theme}-${width}-charts.png`),Buffer.from(chartShot.data,'base64'));
  await evaluate('window.scrollTo(0,0)');
 }
 await evaluate(`document.getElementById('export-all').click();document.querySelector('.chart button').click();document.getElementById('export-models').click()`);
 await new Promise(r=>setTimeout(r,800));
 const files=await readdir(results);check(files.includes('context-cost-forecast.json'),'JSON download');check(files.includes('cumulative.csv'),'chart CSV download');check(files.includes('context-cost-models.csv'),'model CSV download');
 const exported=JSON.parse(await readFile(join(results,'context-cost-forecast.json'),'utf8'));check(exported.long.total>0 && exported.evidence.length>=10,'export includes data and sources');
 await writeFile(join(results,'browser-smoke.json'),JSON.stringify({passed:true,assertions,parityCases:report.cases.length,themes:['light','dark'],widths:[1440,390],charts:8},null,2));
 console.log(`${assertions} assertions passed; screenshots and exports in ${results}`);
}finally{client?.close();browser.kill();server.close();await rm(profile,{recursive:true,force:true}).catch(()=>{});}
