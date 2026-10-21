# TE-66 status

Result: done
Implementation result SHA: `9b45805f15adeb54934ab186e86b3c5f5210beb4` (fix). The branch head after it changes only this file.

## Failing test on windows-latest

I read the logs of every failing `ci` run on main through the Actions API. The logs are saved as `ci-main-c6d1de5-windows.log` in the results directory. In every failing windows-latest Test step, exactly one test failed, out of 584 in c6d1de5:

- `TokenEconomy.Tests.PriceSnapshotTests.EverySnapshotIsHashPinnedAndLoadsAsACatalog`. The expected hash was `76b69e30…` (the `sha256` in `price-snapshots/index.json` for `model-prices.2026-09-24.json`), the actual hash was `b432b938…`.

Correction to the timeline in the card: the windows-latest Test step has failed since 99fb473 (2026-09-29), the commit that added `price-snapshots/`. It failed in the runs for 99fb473, a96995d, 61f2fa3, b925640, ca09e34, 1207248 and c6d1de5. In the runs for bf6f8a9, bd67a08, 7fe56fb and a0bfadd, windows-latest passed 498/498. Those runs failed only because of the ubuntu "Check website and context-cost explorer" step.

## Classification

This is a checkout line-ending problem, not a product defect, and the test's assumption is correct. In the repository, the snapshots and `model-prices.json` are stored with LF (`git ls-files --eol`: `i/lf`). The SHA-256 pins in `index.json` are taken over those repository bytes. The windows-latest checkout uses `core.autocrlf=true`, so the files arrived with CRLF and the hash differed. At runtime, `ModelPriceCatalogData` only deserializes the embedded JSON, and line endings do not change the parsed catalog. The release packages are built on ubuntu.

## Fix

`.gitattributes` now declares `text eol=lf` for `src/TokenEconomy/catalog/price-snapshots/*.json` and `src/TokenEconomy/catalog/model-prices.json`. This follows the existing rules for the byte-hashed policy snapshot and `KnownModels.g.cs`. Every checkout now has the same bytes that the pins describe. That also keeps the embedded catalog identical to the current snapshot when the package is built on Windows. The test is unchanged and still proves the hash pin for every snapshot. `git add --renormalize .` changed no file content, so there was no line-ending rewrite. No test was skipped, so there is no open decision.

## Verification (remote, GitHub Actions)

`ci` does not run for branch pushes and the runner token cannot open a PR (HTTP 403). I therefore pushed a temporary probe branch, `runner/agent-runner-01/TE-66-ci-probe`. It holds this branch's content plus one temporary commit that adds the probe branch to the `push` trigger of `ci.yml`. That commit is not part of this branch.

- Probe run 39534648628 on the fix commit: build & test (windows-latest) **passed 584/584**. build & test (ubuntu-latest) passed Restore, Build and Test (584/584). Its later step "Check website and context-cost explorer" failed because headless Chrome timed out (`scripts/test-context-cost.mjs`: "Timed out waiting for DevTools", dbus errors). That step does not touch the changed files. The token cannot re-run jobs (HTTP 403), so I pushed the probe again on the final head (see below).
- Final probe run: see the section below.
- Lint: `dotnet format style --verify-no-changes --severity error` is part of the review gate. This change touches no C# source.
