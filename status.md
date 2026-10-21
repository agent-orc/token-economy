# TE-67 status

Result: done
Implementation result SHA: `be31b5bf3a2aa2bc0bfa16bb8fcc4c493adaffab`. Commits after it on the branch change only this file.

## Why the check timed out

The failing ubuntu job on main (run 39551990303, `9b1dc1d`, log `ci-main-9b1dc1d-ubuntu.log` in the results directory) failed in `scripts/test-context-cost.mjs` with `Timed out waiting for DevTools.` The stderr was empty after 15 s. The script used three timing-dependent waits:

- **Browser start:** it waited a fixed 15 s for Chrome to print `DevTools listening on …` to stderr. It had no retry. The first Chrome start in a fresh runner job is a cold start (disk, font cache, profile creation), and Chrome prints nothing until DevTools listens. The passing probe run measured this cold start at **12.6 s** on the runner. The second Chrome start in the same job took 0.5 s. So a normal cold start sits close to the 15 s limit, which explains the intermittent failures (c6d1de5 passed, TE-66 probe run 39534648628 and 9b1dc1d failed).
- **App readiness:** an in-page poll capped at 10 s. It did not report which step was waiting.
- **Downloads:** a fixed 800 ms sleep before it read the downloaded files.

`scripts/test-language-page.mjs` runs in the same CI step and had the same 15 s stderr wait, so I fixed it the same way.

## Change

New shared module `scripts/headless-browser.mjs`. Both website checks use it:

- **Explicit readiness signal for the start:** Chrome writes `DevToolsActivePort` into its profile once DevTools listens. The harness waits for that file, then for a page target on `/json/list`, then for `Page.enable`/`Runtime.enable`/`Log.enable` to answer. It does not parse stderr. Each attempt has a 60 s budget. It also adds the standard headless flags (`--no-first-run`, `--disable-dev-shm-usage`, `--disable-background-networking`, `--disable-component-update`, …).
- **Bounded retry only around the browser start:** up to 3 attempts. Each attempt uses a fresh profile, and a failed Chrome process is killed with SIGKILL before the next attempt. Nothing after the start is retried.
- **Explicit page conditions instead of sleeps:** `waitUntil` polls a page expression until a deadline (30 s). For the explorer it waits until `#app` is visible, which `explorer.mjs` does only after both data files loaded and the first forecast rendered. If `#loading` shows an error text, the wait stops at once with that text. Downloads are deleted first, then the harness polls until the three final file names exist (Chrome renames a download only when it is complete). The language page waits until `#load-status` no longer starts with `Loading`, and the task-study page waits until it shows 14 rows.
- **Diagnostics on failure:** every failure prints the current step, the elapsed time, the live page URL (or the last navigated URL if the page does not answer), the last 25 console, exception and log messages, and the tail of Chrome's stderr. A start failure lists every attempt with its duration and error.
- The CDP command timeout went from 15 s to 30 s. A closed socket now rejects pending commands at once instead of waiting for a timeout.
- The old `.catch(() => {})` around profile removal is gone. A failed removal now logs a warning, so there is no empty catch.

The check still proves the same things. All assertions are unchanged: C#/browser parity for all 108 cases, eight charts, 3 presets × 4 models matching the C# totals, reasoning, quota, TTL, date, gap and compaction cases, no overflow at 1440/390 in light and dark, screenshots, and the three downloads with source evidence. The language page checks are also unchanged. `docs/context-cost-explorer.md` describes the harness. No line endings were changed (the three scripts are LF, with no CR before or after).

## Verification (remote, GitHub Actions)

`ci` does not run for branch pushes, so, as in TE-66, I pushed the temporary probe branch `runner/agent-runner-01/TE-67-ci-probe`. It holds this branch plus one temporary commit that adds the probe branch to the `push` trigger of `ci.yml`. That commit is not part of this branch.

- Probe run 39553862059 on `c088d3e` (`be31b5b` plus the trigger commit): **success**. build & test (windows-latest) passed 584/584. build & test (ubuntu-latest) passed 584/584 and the step "Check website and context-cost explorer": `context-cost explorer: /usr/bin/chromium ready after 12566 ms (attempt 1/3)`, `34471 assertions passed`, `language page: … ready after 513 ms`, `23 language browser checks passed`. The logs are `ci-probe-c088d3e-ubuntu.log` and `ci-probe-c088d3e-windows.log` in the results directory.
- The TE-66 probe branch `runner/agent-runner-01/TE-66-ci-probe` no longer existed on origin (`git ls-remote`), so there was nothing to delete.
- Local checks: `node --check` on the three scripts. I did not build or test locally, because builds and tests run remotely. This change touches no C# source, so `dotnet format style --verify-no-changes --severity error` and the guard filter are unaffected. CI ran the full build and tests on both operating systems.
- main itself turns green for both jobs once this branch is merged. The probe shows the merged content passing on both jobs.
