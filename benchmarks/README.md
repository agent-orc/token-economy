# Benchmark methodology

This directory contains the versioned inputs and append-only evidence used to
measure model quality and resource use. Benchmarking runs locally from this
repository; it does not require an Agent Studio runtime. This guide is the
end-to-end operating method. See the [benchmark protocol background](../docs/benchmarks.md)
for the established-suite influences and the detailed rationale behind the
first controlled setup.

There are two benchmark shapes. Choose the shape from the question being asked,
not from the desired presentation:

| Shape | Use it when | Definition | Unit of execution | Derived sidecar |
| --- | --- | --- | --- | --- |
| Controlled A/B setup | The same task and executable success gate can compare at least two model/thinking variants. | [`setups/<id>.json`](setups/) against [`schema/setup.schema.json`](schema/setup.schema.json) | One fresh workspace per variant and repetition | `<run-id>.report.json` |
| Document-to-text capability corpus | A collection of document fixtures and text oracles should be attempted by every canonical catalog model. | [`document-to-text/<corpus>.json`](document-to-text/curated-hard-cases.json) against [`document-to-text/corpus.schema.json`](document-to-text/corpus.schema.json) | One extraction per model and corpus case | `<run-id>.capabilities.json` |

Do not use the capability corpus for a two-model task comparison: it has no
variant or repetition fields. Do not force a document matrix into an A/B setup:
the corpus runner supplies document-type oracles and conservative capability
levels that the general runner does not.

## Anatomy of a controlled A/B setup

[`setups/palindrome-repair.json`](setups/palindrome-repair.json) is the complete
checked-in example. Schema version 1 defines these parts:

- `id` is the stable experiment id. It also names the result directory under
  `benchmarks/results/` and should match the fixture directory where practical.
- `formatAttribution.sources` records each source suite, an absolute reference,
  and the exact format or metric elements borrowed from it.
  `formatAttribution.deviations` records every deliberate difference and its
  reason. Attribution belongs in the executable definition, not only in prose.
- `task.prompt` is identical for every variant. `task.seedWorkspace` is a
  repository-relative directory copied for every case. Optional
  `task.responseFile` tells the included invoker to write the model's final text
  to that safe path below the copied workspace; omit it for an agentic editing
  task. `taskClass` and `capability` retain routing dimensions in the raw run.
- `variants` has at least two entries. Each has a stable result `id`, a model id,
  and an optional `thinkingLevel`. Change only the dimensions the benchmark is
  intended to compare.
- `repetitions` is the number of fresh-workspace invocations of every variant
  and defaults to one. All variants receive the same count.
- `invocationTimeoutSeconds` limits each model invocation and defaults to 300.
  `successCriteria` supplies a direct executable, argument array, expected exit
  code (default `0`), and evaluation timeout (default 120 seconds).
- Optional `costCaps.maxTotalTokensPerInvocation` and
  `costCaps.maxUsdPerInvocation` are per invocation. Token totals include fresh
  input, output, cache-read, and cache-write tokens.

The runner copies the seed, invokes the model, applies the response artifact
when configured, and only then executes `successCriteria` in that copy. The
success command is therefore the quality oracle; a plausible response is not a
pass unless the command returns `expectedExitCode`.

## Anatomy of the document-to-text corpus

[`document-to-text/curated-hard-cases.json`](document-to-text/curated-hard-cases.json)
is the schema-version-1 example. A corpus has a stable `id`, one invocation
timeout for all attempts, and one or more cases. Each case declares:

- a unique `id` and one of `Pdf`, `Word`, `Spreadsheet`, or `Presentation`;
- a repository-relative `documentPath`;
- non-empty `requiredFragments`, which must appear in the declared order;
- optional `forbiddenFragments`, none of which may appear; and
- an optional `note` explaining the hard case.

Matching uses Unicode Form KC normalization and ignores case and whitespace
differences. The corpus does not list models: `DocumentTextBenchmarkRunner`
uses every listing in `ModelPriceCatalog.Default` once per case. Add a second
case of a document type when the desired capability rate needs more than a
single example; there is no corpus-level repetitions setting.

## Fixtures and deterministic success

The relevant layout is:

```text
benchmarks/
  fixtures/<setup-id>/                 controlled A/B seed workspaces
  setups/<setup-id>.json               controlled A/B definitions
  document-to-text/
    fixtures/<document-file>           capability-corpus source documents
    <corpus>.json                       document cases and text oracles
  results/                              append-only run artifacts
```

For a controlled setup, keep the complete minimal starting state under
`benchmarks/fixtures/<setup-id>/`. The runner recursively copies it to a
temporary directory for every variant/repetition pair, so cases cannot observe
another case's edits. `palindrome-repair` contains the broken source, its small
.NET project, and an executable set of examples. Other benchmark-related test
fixtures may coexist under `benchmarks/fixtures/`; only `task.seedWorkspace` is
copied by the controlled runner.

A fixture and its oracle must be reproducible from a clean checkout:

- Check in every required input and keep it as small as the task permits.
- Keep the evaluation independent of wall-clock time, randomness, mutable
  external services, network state, and the order in which variants run.
- Invoke one executable with an argument array. The harness does not interpret
  shell pipelines, redirection, variable expansion, or compound commands.
- Make pass/fail machine-observable through the expected exit code. Before
  publishing, prove that the untouched fixture exposes the intended defect and
  that a known-correct change passes without unrelated failures.
- Use the same prompt, seed, oracle, timeouts, and caps for every variant. Put no
  variant-specific hint in the shared prompt.
- Treat documents as canonical bytes. Required fragments should be specific
  enough to test visible content and order; forbidden fragments should catch
  metadata, hidden content, revisions, worksheets, or notes that must not leak.

The harness executes `successCriteria.command` directly on the host. Its
temporary workspace is isolation between cases, not a security boundary for
untrusted generated code. Provide an external sandbox before evaluating
untrusted fixtures or commands.

## Running benchmarks

Install the .NET 10 SDK and run commands from the repository root. The CLIs used
by the chosen invoker must also be installed, authenticated, and able to access
the model ids being measured.

The harness entry point,
[`src/TokenEconomy.Benchmarks/Program.cs`](../src/TokenEconomy.Benchmarks/Program.cs),
accepts exactly one verb and one path:

| Verb | Command shape | Behavior |
| --- | --- | --- |
| `run` | `dotnet run --project src/TokenEconomy.Benchmarks -- run benchmarks/setups/<setup>.json` | Runs a controlled definition through `BenchmarkRunner`. |
| `document-to-text` | `dotnet run --project src/TokenEconomy.Benchmarks -- document-to-text benchmarks/document-to-text/<corpus>.json` | Runs every catalog model over every corpus case. |
| `aggregate` | `dotnet run --project src/TokenEconomy.Benchmarks -- aggregate <agent-studio-task-storage>` | Regenerates routing evidence from controlled raw runs and observational task storage; it does not invoke a benchmark model. See [`docs/routing-evidence.md`](../docs/routing-evidence.md). |

To re-execute the checked-in A/B example:

```bash
dotnet run --project src/TokenEconomy.Benchmarks -- run benchmarks/setups/palindrome-repair.json
```

This creates a new timestamped run; it does not overwrite the
[checked-in raw run](results/palindrome-repair/20260722T233105307Z.json).
Re-execution reproduces the checked-in definition, fixture isolation, and
scoring method, but model output and timing are observations and need not be
byte-identical to the historical run.

To re-execute the capability corpus:

```bash
dotnet run --project src/TokenEconomy.Benchmarks -- document-to-text benchmarks/document-to-text/curated-hard-cases.json
```

The invoker dispatch is intentionally explicit:

- `run` constructs `CodexCliBenchmarkInvoker`, so every controlled variant is
  sent through the authenticated `codex` CLI. A model family is not dispatched
  to another CLI merely because of its name.
- `document-to-text` constructs `DocumentTextCliExtractor`. Models whose ids
  start with `claude-` are sent through the `claude` CLI; all other catalog
  models are sent through `codex`. Both CLIs are required for a complete
  all-model run.
- `IBenchmarkInvoker` and `IDocumentTextExtractor` are the transport seams for a
  future host-specific adapter; the runners retain validation, scoring, and
  persistence semantics.

The controlled invocation timeout returns exit `-1` and a timeout failure. An
evaluation timeout also returns `-1`. Exceeding a token cap does not truncate an
invocation: the completed measurement is retained and marked unsuccessful
before evaluation. A USD cap is enforced only when the invoker supplies
`CostUsd`; unknown cost stays `null`, never zero. The included Codex invoker
records CLI token usage but does not calculate USD cost. The document corpus has
an invocation timeout but no token or USD cap in its schema.

The command exits non-zero when a controlled report has a variant with no
successful repetition, or when any document capability record is not
`Demonstrated`. A non-zero process exit does not prevent the completed raw and
sidecar artifacts from being written.

## Result artifacts and immutability

Harness-generated run ids are UTC timestamps in `yyyyMMddTHHmmssfffZ` form, for
example `20260722T233105307Z`. They are used as file names and are also stored in
the raw and derived JSON:

| Run shape | Raw evidence | Derived sidecar |
| --- | --- | --- |
| Controlled A/B | `benchmarks/results/<setup-id>/<run-id>.json` | `benchmarks/results/<setup-id>/<run-id>.report.json` |
| Document corpus | `benchmarks/results/document-to-text/<corpus-id>/<run-id>.json` | `benchmarks/results/document-to-text/<corpus-id>/<run-id>.capabilities.json` |

Controlled raw cases retain the variant/model/thinking level, repetition,
invocation and evaluation exit codes, pass/fail, four token components,
duration, optional cost, and failure reason. The report derives success rate,
total and average tokens, optional total cost, average duration, winner, and the
first two ranked variants' quality/cost deltas. Ranking is highest success rate,
then lowest average tokens, lowest average duration, and stable variant id.

Corpus raw cases retain extracted text, missing and unexpected oracle fragments,
usage, cost, duration, exit, and failure. The capability sidecar groups by model
and document type: all cases passing is `Demonstrated`, some is `Partial`, and
none is `NotDemonstrated`. `NotDemonstrated` is deliberately not a universal
claim of unsupported capability.

Both runners reject a run id if either destination path already exists and use
create-new writes. After a run is published, do not edit its raw JSON or its
adjacent sidecar. Treat its setup/corpus and fixtures as versioned inputs too;
if their semantics change, give the revised definition a new id. If an
environment or derived-logic correction is needed, create a new run id. Retain
the superseded run for provenance, and explain why the later run replaces it in
any public interpretation.

## From raw evidence to the website

[`scripts/generate-website-data.py`](../scripts/generate-website-data.py) is the
only bridge from repository evidence to the static site's generated data:

```text
benchmarks/results/**/*.json
  -> create_payload()
  -> website/data/benchmarks.json
  -> published A/B and capability tables

pinned evidence + dated price catalog
  -> create_usage_payload()
  -> website/data/token-usage.json
  -> published token and cost charts
```

`create_payload()` recursively globs every `*.json` under
`benchmarks/results/`. It skips `.report.json` and `.capabilities.json` as
primary inputs, and ignores other evidence that has neither `setupId` nor
`corpusId`. A controlled raw run must have an adjacent report; a corpus raw run
must have an adjacent capabilities file. Identity mismatches or a missing
sidecar fail generation. Only the explicitly projected fields are copied to
`website/data/benchmarks.json`.

`create_usage_payload()` is a separate, deliberately shaped projection. It
currently reads the pinned `curated-hard-cases-v1` raw run, the complexity
backtest snapshot under `results/`, the measured session table under
`docs/analyses/`, and the dated model-price catalog. It creates the per-model,
document-type, card-task, reissue, session, and cost data in
`website/data/token-usage.json`. Adding an arbitrary benchmark result does not
automatically add it to a token chart.

After adding evidence, regenerate and check both website artifacts:

```bash
python3 scripts/generate-website-data.py
python3 scripts/generate-website-data.py --check
dotnet test tests/TokenEconomy.Tests/TokenEconomy.Tests.csproj --filter FullyQualifiedName~WebsiteTokenUsageDataTests
```

`--check` rebuilds both payloads in memory, ignores only their volatile
`generatedAtUtc` values, and fails when the committed evidence-derived bodies
differ. It is the pre-deploy gate in
[`.github/workflows/deploy-website.yml`](../.github/workflows/deploy-website.yml).
[`WebsiteTokenUsageDataTests`](../tests/TokenEconomy.Tests/WebsiteTokenUsageDataTests.cs)
provides a second drift guard: it re-aggregates the published usage from the
referenced raw evidence and re-costs it through `ModelPriceCatalog.ComputeCost`,
including explicit unpriced states.

The website reads generated JSON; do not hand-author a study row or chart value
in `website/index.html`.

## Add a new benchmark

Use this checklist for a controlled setup from definition to publication:

1. State the question and choose controlled A/B or capability corpus using the
   distinction above. Predeclare the compared dimension and repetition plan.
2. Choose a stable, filesystem-safe id. For A/B, add the minimal checked-in seed
   under `benchmarks/fixtures/<id>/`. For document capability, add canonical
   files under `benchmarks/document-to-text/fixtures/`.
3. Copy [`setups/palindrome-repair.json`](setups/palindrome-repair.json) to
   `benchmarks/setups/<id>.json`, keep `schemaVersion: 1`, and complete
   `formatAttribution`, `task`, at least two `variants`, `repetitions`, timeouts,
   `successCriteria`, and appropriate caps. Review every field against
   [`schema/setup.schema.json`](schema/setup.schema.json). For a corpus, instead
   copy the corpus shape and review it against
   [`document-to-text/corpus.schema.json`](document-to-text/corpus.schema.json).
4. Exercise the oracle locally. Confirm the untouched A/B seed fails for the
   intended reason and a known-correct result passes, or confirm every corpus
   fixture's required/forbidden fragments represent visible/hidden content
   unambiguously.
5. Run the concrete `run` or `document-to-text` command from the repository root.
   The harness creates the timestamp id and both result files.
6. Inspect every raw case before reading the sidecar. Separate invocation,
   timeout, authentication, quota, and evaluator-host failures from actual
   executable-gate or extraction-oracle misses. Confirm usage is present where
   resource comparisons are intended.
7. If the run is invalid, preserve it and run again after correcting or
   versioning the input/environment. Never repair published evidence in place.
   If it is valid, keep the definition, fixture/corpus, raw file, and matching
   sidecar together.
8. Run the website generator, its `--check` mode, and
   `WebsiteTokenUsageDataTests`. A valid A/B or corpus run appears automatically
   in the published study tables through `create_payload()`.
9. If the new evidence should drive a token chart, explicitly extend
   `create_usage_payload()`, the renderer in `website/index.html`, and
   `WebsiteTokenUsageDataTests`; do not repurpose an existing chart or hand-copy
   numbers. Preview the result with
   `python3 -m http.server 4340 --directory website` and open
   `http://localhost:4340`.

## Statistical principles

`repetitions: N` means exactly `N` fresh-workspace invocations of each controlled
variant. It is not a retry-until-success policy: failed attempts remain in the
denominator, and the runner never gives one variant extra tries. Fresh
workspaces prevent edit leakage, but do not guarantee statistical independence
from shared CLI caches or provider state. Run order is variant by variant, with
repetitions `1..N`, so an environment that changes over time can bias the
comparison; hold it stable and record any known incident in the interpretation.

One repetition is useful as a smoke test and as a single piece of raw evidence,
but it measures no within-variant variance. Its success rate can only be `0` or
`1`, and token/duration differences may be run noise. A one-repeat winner is the
result of the deterministic report tie-breaker, not evidence of a general model
advantage. Use a predeclared multi-repeat design when making comparative claims,
and report counts and rates rather than hiding the denominator.

The current sidecars count every recorded unsuccessful attempt in their rate,
including infrastructure failures. Interpret the raw failure fields before
attributing a miss to model capability:

- An invoker exit or timeout caused by a missing/gated model, authentication,
  quota, or provider state is an infrastructure failure. An evaluation-command
  failure shown independently to come from a broken host is infrastructure too.
  Keep captured attempts in raw evidence, disclose them, and do not use them as
  negative capability evidence. A missing controlled-run CLI executable or an
  evaluator process-start exception can abort before artifacts are persisted;
  fix the environment and start a new run.
- An invocation that completed normally but failed the deterministic
  `successCriteria` command, or a successful extraction with missing required or
  present forbidden fragments, is a capability miss for that exact fixture and
  oracle.
- Missing usage or price is unknown, not zero. Do not rank a failed launch as
  token-efficient, and do not infer cheapness from `CostUsd: null`.

Keep both categories visible. Removing failed rows after observing them changes
the experiment; combining infrastructure failure with semantic failure changes
the claim.
