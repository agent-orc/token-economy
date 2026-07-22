# Built-in A/B benchmarks

Benchmarking is a Token Economy capability. It executes locally from this repository and has no Agent Studio runtime dependency. Agent Studio history imported by `AgentStudioTaskStorageImporter` is observational input; controlled benchmark results are a separate calibration source for complexity estimation and future routing policy.

## Run the real example

Prerequisites are .NET 10 and an authenticated Codex CLI that exposes the configured models. From the repository root:

```powershell
dotnet run --project src/TokenEconomy.Benchmarks -- run benchmarks/setups/palindrome-repair.json
```

The CLI invokes the exact same prompt once with `gpt-5.6-terra`/medium and once with `gpt-5.6-sol`/medium. Each invocation receives a fresh copy of `benchmarks/fixtures/palindrome-repair`; no case sees another case's output. For this small response-artifact task, the harness writes each model's final response to `task.responseFile`, then runs `dotnet run` against the repaired fixture to determine success. This keeps nested tool execution out of the measured variable.

Raw output is written once to `benchmarks/results/palindrome-repair/<UTC-run-id>.json`. The adjacent `<UTC-run-id>.report.json` is derived from it. Existing paths are rejected, making result files append-only. Temporary workspaces are removed after collection.

Each raw case records the selected model and effort, repetition, invocation and evaluation exits, success, token counts, duration, optional USD cost, and failure reason. A report aggregates success rate (quality), tokens, duration, and cost when the invoker can supply one. The winner has the highest success rate; ties use average tokens, duration, then stable variant id. `qualityDelta` and `costDeltaUsd` compare the first two ranked variants. Cost remains `null` when no authoritative price is available—it is never guessed.

## Define the next setup

1. Copy a setup under `benchmarks/setups/` and give it a stable `id`. Definitions use `benchmarks/schema/setup.schema.json` (schema version 1).
2. Put only the minimal starting files under `benchmarks/fixtures/<id>/`. The path in `task.seedWorkspace` must stay repository-relative.
3. Write one task prompt shared by every variant. Do not put variant-specific hints in it. For a response-artifact task, set `task.responseFile` to a safe path below the copied workspace and ask for only that file's content; omit it when the model should edit through its tools.
4. Add at least two variants. A variant combines a stable result label, model id, and optional thinking level.
5. Choose repetitions. Use more than one when variance matters; every model receives the same count.
6. Define a deterministic success command as an executable plus an argument array. Avoid shell syntax and subjective judging. Set evaluation and invocation timeouts.
7. Set token and/or USD caps. A known over-cap response is retained but marked unsuccessful. An unavailable USD amount does not masquerade as zero.
8. Run the setup, inspect both JSON files, and commit the definition, fixture, raw result, and report together. Never edit an existing raw run; repeat the setup to create a new run.

## Extend the harness

`IBenchmarkInvoker` is the transport seam. `BenchmarkRunner` owns setup validation, workspace copying, caps, deterministic evaluation, measurements, persistence, reporting, and structured events; an invoker owns only a model call and returns exit status, usage, optional cost, and diagnostics. The included console adapter uses the Codex CLI JSON event stream. A future API adapter can implement the same interface without changing experiment semantics.

Lifecycle events are `benchmark.run.started`, `benchmark.case.completed`, and `benchmark.run.completed`. Hosts can subscribe to `EventOccurred`; the CLI emits them as structured JSON on stderr.

To add a metric, add it to `BenchmarkCaseResult`, populate it in the runner or invoker, aggregate it in `Compare`, extend the setup schema if input changes, and cover raw and aggregate behavior in `BenchmarkRunnerTests`. Keep raw data sufficient to regenerate every report.
