// Runs the website checks of the GitHub workflows in one command, so the Agent
// Studio gate fails a delivery before it reaches main:
//   - ci.yml, step "Check website and context-cost explorer"
//   - deploy-website.yml, step "Build and sanity-check the site data"
// Keep the command list in step with both workflow steps.
//
// Like ci.yml, the checks run on Linux only. Python packages from
// scripts/requirements-language.txt are taken from the interpreter, or from the
// directory .agent-studio/prepare installs them into (.agent-studio/.python).
import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { delimiter, join, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');

if (process.platform !== 'linux') {
  console.log(`Website checks run on Linux only, as in ci.yml; skipped on ${process.platform}.`);
  process.exit(0);
}

const env = { ...process.env };
const packages = join(root, '.agent-studio', '.python');
if (existsSync(packages)) env.PYTHONPATH = [packages, env.PYTHONPATH].filter(Boolean).join(delimiter);
env.JOB_RESULTS_DIR ||= mkdtempSync(join(tmpdir(), 'context-cost-results-'));

if (!existsSync(join(root, 'website', 'index.html'))) {
  console.error('website/index.html is missing');
  process.exit(1);
}

const commands = [
  ['python3', '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_language_evidence.py'],
  ['python3', 'scripts/generate-website-data.py', '--check'],
  ['python3', 'scripts/check-website-links.py'],
  [process.execPath, 'scripts/test-context-cost.mjs'],
  [process.execPath, 'scripts/test-language-page.mjs'],
];
for (const [command, ...args] of commands) {
  console.log(`$ ${[command, ...args].join(' ')}`);
  const result = spawnSync(command, args, { cwd: root, env, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    console.error(`Website check failed (exit ${result.status ?? result.signal}): ${[command, ...args].join(' ')}`);
    process.exit(result.status || 1);
  }
}
console.log('All website checks passed.');
