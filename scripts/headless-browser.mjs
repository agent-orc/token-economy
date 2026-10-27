// Shared headless-Chrome harness for the website browser checks.
//
// The checks run on GitHub-hosted runners, where the first Chrome start of a job
// can stay silent for longer than 15 seconds (cold disk and font cache). The
// harness therefore never sleeps for a fixed time and never parses stderr for
// readiness. Chrome writes DevToolsActivePort into the profile once its DevTools
// endpoint listens; the harness waits for that file, then for a page target, and
// then for a CDP round trip. Only this start sequence is retried, a bounded
// number of times with a fresh profile. Every later wait polls an explicit page
// condition with a deadline, and a failure prints the step, the elapsed time,
// the page URL, the last console messages and the tail of Chrome's stderr.
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

const START_ATTEMPTS = 3;
const START_TIMEOUT_MS = 60_000;
const COMMAND_TIMEOUT_MS = 30_000;
const CONDITION_TIMEOUT_MS = 30_000;
const CONSOLE_HISTORY = 25;
const STDERR_TAIL = 4_000;

const delay = ms => new Promise(r => setTimeout(r, ms));

export function findBrowser() {
  const path = process.env.CHROME_PATH || ['/usr/bin/chromium', '/usr/bin/google-chrome',
    ...['1243', '1217'].map(v => `/home/agent/.cache/ms-playwright/chromium_headless_shell-${v}/chrome-headless-shell-linux64/chrome-headless-shell`)].find(existsSync);
  if (!path) throw new Error('Set CHROME_PATH to Chromium.');
  return path;
}

function createCdpClient(webSocketUrl) {
  const socket = new WebSocket(webSocketUrl);
  const pending = new Map();
  const listeners = new Map();
  let nextId = 1;
  let closedError = null;

  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) {
      const request = pending.get(message.id);
      if (!request) return;
      pending.delete(message.id);
      if (message.error) request.reject(new Error(`${request.method}: ${message.error.message}`));
      else request.resolve(message.result);
      return;
    }
    for (const listener of [...(listeners.get(message.method) ?? [])]) listener(message.params);
  });
  socket.addEventListener('close', () => {
    closedError = new Error('The page DevTools socket closed.');
    for (const request of pending.values()) request.reject(closedError);
    pending.clear();
  });

  const opened = new Promise((resolveOpen, reject) => {
    socket.addEventListener('open', resolveOpen, { once: true });
    socket.addEventListener('error', () => reject(new Error('Could not connect to the page DevTools socket.')), { once: true });
  });

  const on = (method, listener) => {
    listeners.set(method, [...(listeners.get(method) ?? []), listener]);
    return () => listeners.set(method, (listeners.get(method) ?? []).filter(l => l !== listener));
  };

  return {
    on,
    async send(method, params = {}, timeoutMs = COMMAND_TIMEOUT_MS) {
      await opened;
      if (closedError) throw closedError;
      const id = nextId++;
      const response = new Promise((resolveResponse, rejectResponse) => {
        const timeout = setTimeout(() => {
          pending.delete(id);
          rejectResponse(new Error(`Timed out after ${timeoutMs} ms waiting for ${method}.`));
        }, timeoutMs);
        pending.set(id, {
          method,
          resolve(result) { clearTimeout(timeout); resolveResponse(result); },
          reject(error) { clearTimeout(timeout); rejectResponse(error); },
        });
      });
      socket.send(JSON.stringify({ id, method, params }));
      return response;
    },
    // Registers before the caller triggers the event, so a fast event is not missed.
    waitFor(method, timeoutMs = COMMAND_TIMEOUT_MS) {
      return new Promise((resolveEvent, rejectEvent) => {
        const timeout = setTimeout(() => {
          off();
          rejectEvent(new Error(`Timed out after ${timeoutMs} ms waiting for ${method}.`));
        }, timeoutMs);
        const off = on(method, params => { clearTimeout(timeout); off(); resolveEvent(params); });
      });
    },
    close() { socket.close(); },
  };
}

async function stopProcess(child) {
  if (child.exitCode !== null || child.signalCode !== null) return;
  const exited = new Promise(r => child.once('exit', r));
  child.kill('SIGKILL');
  await Promise.race([exited, delay(5_000)]);
}

async function removeProfile(profile) {
  try {
    await rm(profile, { recursive: true, force: true, maxRetries: 3 });
  } catch (error) {
    console.warn(`Could not remove browser profile ${profile}: ${error.message}`);
  }
}

// Resolves with the DevTools port once Chrome has written DevToolsActivePort.
async function waitForDevToolsPort(child, profile, deadline, stderr) {
  const portFile = join(profile, 'DevToolsActivePort');
  while (Date.now() < deadline) {
    if (child.exitCode !== null || child.signalCode !== null)
      throw new Error(`Browser exited before DevTools started (code ${child.exitCode}, signal ${child.signalCode}). stderr: ${stderr() || '(empty)'}`);
    if (existsSync(portFile)) {
      const [port] = (await readFile(portFile, 'utf8')).split('\n');
      if (/^\d+$/.test(port.trim())) return Number(port.trim());
    }
    await delay(100);
  }
  throw new Error(`DevToolsActivePort did not appear within ${START_TIMEOUT_MS} ms. stderr so far: ${stderr() || '(empty)'}`);
}

async function waitForPageTarget(port, deadline) {
  let lastError = 'no page target yet';
  while (Date.now() < deadline) {
    try {
      const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(5_000) })).json();
      const page = targets.find(t => t.type === 'page');
      if (page) return page.webSocketDebuggerUrl;
    } catch (error) {
      lastError = error.message;
    }
    await delay(100);
  }
  throw new Error(`No page target on DevTools port ${port}: ${lastError}`);
}

async function startOnce(browserPath, attempt, stderrLog) {
  const profile = await mkdtemp(join(tmpdir(), 'te-browser-'));
  const child = spawn(browserPath, ['--headless=new', '--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage',
    '--no-first-run', '--no-default-browser-check', '--disable-extensions', '--disable-component-update',
    '--disable-background-networking', '--disable-sync', '--password-store=basic', '--use-mock-keychain',
    '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'] });
  child.on('error', error => stderrLog.push(`[attempt ${attempt}] spawn error: ${error.message}\n`));
  child.stderr.setEncoding('utf8');
  child.stderr.on('data', chunk => stderrLog.push(`[attempt ${attempt}] ${chunk}`));
  const stderr = () => stderrLog.text().slice(-STDERR_TAIL);
  const deadline = Date.now() + START_TIMEOUT_MS;
  let client;
  try {
    const port = await waitForDevToolsPort(child, profile, deadline, stderr);
    client = createCdpClient(await waitForPageTarget(port, deadline));
    const remaining = () => Math.max(1_000, deadline - Date.now());
    await client.send('Page.enable', {}, remaining());
    await client.send('Runtime.enable', {}, remaining());
    await client.send('Log.enable', {}, remaining());
    return { child, profile, client };
  } catch (error) {
    client?.close();
    await stopProcess(child);
    await removeProfile(profile);
    throw error;
  }
}

// Starts Chrome and returns a session for one page. Call session.close() in finally.
export async function launchBrowser({ name }) {
  const startedAt = Date.now();
  const elapsed = () => `${((Date.now() - startedAt) / 1000).toFixed(1)} s`;
  const chunks = [];
  const stderrLog = { push: c => { chunks.push(c); if (chunks.length > 200) chunks.shift(); }, text: () => chunks.join('') };
  const browserPath = findBrowser();
  let started;
  const failures = [];
  for (let attempt = 1; attempt <= START_ATTEMPTS && !started; attempt++) {
    const attemptStart = Date.now();
    try {
      started = await startOnce(browserPath, attempt, stderrLog);
      console.log(`${name}: ${browserPath} ready after ${Date.now() - attemptStart} ms (attempt ${attempt}/${START_ATTEMPTS}).`);
    } catch (error) {
      failures.push(`attempt ${attempt} after ${Date.now() - attemptStart} ms: ${error.message}`);
      console.warn(`${name}: browser start attempt ${attempt}/${START_ATTEMPTS} failed after ${Date.now() - attemptStart} ms: ${error.message}`);
    }
  }
  if (!started) throw new Error(`${name}: headless Chrome did not start (${browserPath}, ${elapsed()}).\n  ${failures.join('\n  ')}`);

  const { child, profile, client } = started;
  const consoleLines = [];
  const remember = line => { consoleLines.push(line); if (consoleLines.length > CONSOLE_HISTORY) consoleLines.shift(); };
  const describe = arg => arg.value !== undefined ? (typeof arg.value === 'string' ? arg.value : JSON.stringify(arg.value)) : (arg.description ?? arg.type);
  client.on('Runtime.consoleAPICalled', p => remember(`console.${p.type}: ${p.args.map(describe).join(' ')}`));
  client.on('Runtime.exceptionThrown', p => remember(`exception: ${p.exceptionDetails.exception?.description ?? p.exceptionDetails.text}`));
  client.on('Log.entryAdded', p => remember(`${p.entry.source}.${p.entry.level}: ${p.entry.text}${p.entry.url ? ` (${p.entry.url})` : ''}`));
  let pageUrl = 'about:blank';
  client.on('Page.frameNavigated', p => { if (!p.frame.parentId) pageUrl = p.frame.url; });

  let step = 'browser started';
  const evaluate = async expression => {
    const r = await client.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? JSON.stringify(r.exceptionDetails));
    return r.result.value;
  };

  return {
    client,
    evaluate,
    elapsed,
    set step(value) { step = value; },
    get step() { return step; },
    async navigate(url) {
      step = `navigate ${url}`;
      const loaded = client.waitFor('Page.loadEventFired');
      const result = await client.send('Page.navigate', { url });
      if (result.errorText) throw new Error(`Navigation to ${url} failed: ${result.errorText}`);
      await loaded;
    },
    // Polls `expression` until it returns a truthy value. `failure` is an optional
    // expression that returns a message as soon as the page reports an error.
    async waitUntil(label, expression, { failure, timeoutMs = CONDITION_TIMEOUT_MS } = {}) {
      step = `wait for ${label}`;
      const deadline = Date.now() + timeoutMs;
      for (;;) {
        const value = await evaluate(expression);
        if (value) return value;
        const message = failure ? await evaluate(failure) : '';
        if (message) throw new Error(`${label}: page reported "${message}".`);
        if (Date.now() >= deadline) throw new Error(`${label}: condition still false after ${timeoutMs} ms: ${expression}`);
        await delay(50);
      }
    },
    async diagnostics(error) {
      let liveUrl = pageUrl;
      try {
        liveUrl = await Promise.race([evaluate('location.href'), delay(2_000).then(() => `${pageUrl} (page did not answer)`)]);
      } catch (urlError) {
        liveUrl = `${pageUrl} (page unavailable: ${urlError.message})`;
      }
      return [
        `${name} failed in step "${step}" after ${elapsed()}: ${error?.stack ?? error}`,
        `  page URL: ${liveUrl}`,
        `  last ${consoleLines.length} console messages:`,
        ...(consoleLines.length ? consoleLines.map(l => `    ${l}`) : ['    (none)']),
        `  Chrome stderr tail:`,
        ...(stderrLog.text().slice(-STDERR_TAIL).trim() || '(empty)').split('\n').map(l => `    ${l}`),
      ].join('\n');
    },
    async close() {
      client.close();
      await stopProcess(child);
      await removeProfile(profile);
    },
  };
}
