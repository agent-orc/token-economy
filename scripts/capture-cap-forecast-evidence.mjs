import { spawn } from "node:child_process";
import { access, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "..");
const sourcePage = resolve(repositoryRoot, "website", "cap-forecast", "index.html");
const resultsDirectory = resolve(repositoryRoot, "results");

if (typeof WebSocket === "undefined") {
  throw new Error("This capture helper requires Node.js 22 or later (global WebSocket support).");
}

const chromeCandidates = [
  process.env.CHROME_PATH,
  process.env.PROGRAMFILES && join(process.env.PROGRAMFILES, "Google", "Chrome", "Application", "chrome.exe"),
  process.env["PROGRAMFILES(X86)"] && join(process.env["PROGRAMFILES(X86)"], "Microsoft", "Edge", "Application", "msedge.exe"),
  process.env.LOCALAPPDATA && join(process.env.LOCALAPPDATA, "Google", "Chrome", "Application", "chrome.exe"),
  "/usr/bin/google-chrome",
  "/usr/bin/chromium",
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
].filter(Boolean);

async function findBrowser() {
  for (const candidate of chromeCandidates) {
    try {
      await access(candidate);
      return candidate;
    } catch {
      // Try the next known installation path.
    }
  }

  throw new Error("Chrome or Edge was not found. Set CHROME_PATH to a Chromium browser executable.");
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

async function capture(client, { name, width, height, mobile }) {
  await client.send("Emulation.setDeviceMetricsOverride", {
    width,
    height,
    deviceScaleFactor: 1,
    mobile,
    screenWidth: width,
    screenHeight: height,
  });

  const loaded = client.waitFor("Page.loadEventFired");
  const navigation = await client.send("Page.navigate", { url: pathToFileURL(sourcePage).href });
  if (navigation.errorText) {
    loaded.catch(() => {});
    throw new Error(`Could not load ${sourcePage}: ${navigation.errorText}`);
  }
  await loaded;
  const fontReady = await client.send("Runtime.evaluate", {
    expression: "document.fonts.ready",
    awaitPromise: true,
  });
  if (fontReady.exceptionDetails) throw new Error("The page failed while waiting for document fonts.");

  const { cssContentSize } = await client.send("Page.getLayoutMetrics");
  const screenshot = await client.send("Page.captureScreenshot", {
    format: "png",
    fromSurface: true,
    captureBeyondViewport: true,
    clip: {
      x: 0,
      y: 0,
      width,
      height: Math.ceil(cssContentSize.height),
      scale: 1,
    },
  });

  const output = resolve(resultsDirectory, name);
  await writeFile(output, Buffer.from(screenshot.data, "base64"));
  process.stdout.write(`${name}: ${width}x${Math.ceil(cssContentSize.height)}\n`);
}

async function stopBrowser(browserProcess) {
  if (browserProcess.exitCode !== null || browserProcess.signalCode !== null) return;

  let timeout;
  const exited = new Promise((resolveExit) => browserProcess.once("exit", resolveExit));
  const timedOut = new Promise((resolveTimeout) => {
    timeout = setTimeout(resolveTimeout, 5_000);
  });
  browserProcess.kill();
  await Promise.race([exited, timedOut]);
  clearTimeout(timeout);
}

const browserExecutable = await findBrowser();
await access(sourcePage);
const profileDirectory = await mkdtemp(join(tmpdir(), "token-economy-cap-forecast-"));
const browserProcess = spawn(browserExecutable, [
  "--headless=new",
  "--disable-gpu",
  "--hide-scrollbars",
  "--remote-debugging-port=0",
  `--user-data-dir=${profileDirectory}`,
  "about:blank",
], {
  stdio: ["ignore", "ignore", "pipe"],
  windowsHide: true,
});

try {
  const browserSocketUrl = await waitForDevTools(browserProcess);
  const browserHttpUrl = new URL(browserSocketUrl);
  browserHttpUrl.protocol = "http:";
  browserHttpUrl.pathname = "/json/list";
  browserHttpUrl.search = "";

  const targets = await (await fetch(browserHttpUrl)).json();
  const pageTarget = targets.find((target) => target.type === "page");
  if (!pageTarget) throw new Error("Chromium did not expose a page target.");

  const client = createCdpClient(pageTarget.webSocketDebuggerUrl);
  await client.send("Page.enable");
  await capture(client, {
    name: "cap-forecast-overview-desktop--real.png",
    width: 1440,
    height: 900,
    mobile: false,
  });
  await capture(client, {
    name: "cap-forecast-overview-mobile--real.png",
    width: 390,
    height: 844,
    mobile: true,
  });
  client.close();
} finally {
  await stopBrowser(browserProcess);
  const temporaryRoot = resolve(tmpdir());
  if (resolve(profileDirectory).startsWith(`${temporaryRoot}\\`) || resolve(profileDirectory).startsWith(`${temporaryRoot}/`)) {
    await rm(profileDirectory, { recursive: true, force: true }).catch(() => {});
  }
}
