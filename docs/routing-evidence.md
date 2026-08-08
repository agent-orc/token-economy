# Versioned routing evidence

`RoutingEvidencePipeline` regenerates one deterministic report from two evidence
classes that remain separate throughout the report:

- controlled results below `benchmarks/results/`, read from immutable raw
  `BenchmarkRunResult` and `DocumentTextBenchmarkResult` artifacts;
- observational Agent Studio task history, imported without changing the source
  `task.json` files and deduplicated by task key plus attempt number.

Quality Studio review outcomes form a separate observational stream because
their unit of evidence and quality signals are review-specific. Its drop
contract, append-only importer, confidence gates, and per-model `review`
projection are documented in
[`quality-studio-review-evidence.md`](quality-studio-review-evidence.md).

Run it from the repository root:

```powershell
dotnet run --project src/TokenEconomy.Benchmarks -- aggregate <agent-studio-task-storage>
```

The derived schema-version-2 report is written to
`results/routing-evidence/v2/routing-evidence.json`. Re-running with unchanged
inputs produces identical content and does not rewrite an identical file. Raw
artifacts are never modified. A host can supply another derived output path by
calling `RoutingEvidencePipeline.Run`. The command also writes
`routing-evidence.json` to `JOB_RESULTS_DIR` when that job evidence directory is
set.

## Cohorts and unknown values

Each evidence class is grouped by canonical catalog model, normalized thinking
level, task class, and capability. A catalog alias resolves to its canonical
model. An unrecognized model, thinking level, task class, or missing capability
stays `null`; the aggregator does not infer it from a filename or a neighboring
attempt.

Agent Studio attempt routes take precedence over card-level routes. When task
storage contains an attempt array, a route missing from one attempt remains
unknown rather than inheriting the card's final route. Re-importing a task is an
idempotent upsert. Conflicting records with the same task, attempt, and
observation timestamp lose their ambiguous route instead of choosing one.

Each imported attempt materializes an immutable routing-decision record, an
append-only raw outcome observation, and a versioned derived classification.
The decision ID joins policy version, recommendation and selection to the
attempt. The observation joins the actual model/thinking level, tokens,
duration, cost status, review result, and raw reissue reason. Exact replays are
idempotent; a changed source snapshot appends a new observation, and a retained
decision ID cannot be rewritten.

Outcome classification version 1 distinguishes success, semantic failure,
substantive C/D review, environmental failure, stale base, broken test host,
cancellation, quota truncation, and missing delivery path. Only semantic
failure and substantive review count as semantic reissues or negative model
quality. Substrate, cancellation, and quota outcomes remain visible in category
counts and resource totals without promoting or penalizing the model.

Every cohort reports:

- sample size and attempt/card/unknown route counts;
- decision-join coverage and the policy version where it was recorded;
- outcome and review-grade availability, success and favorable-grade rates;
- counts for every outcome category and the classification versions present;
- explicitly classified semantic-reissue availability and rate;
- duration, token, and cost availability, with nullable totals and averages;
- first and latest observation dates;
- sorted source references and optional source SHA-256 values.

Missing telemetry is not a measured zero. In particular, a task with no usage
object has `TokenUsageAvailable = false`, `PriceStatus.UsageUnavailable`, and no
cost estimate. A retry number alone is not a semantic-reissue classification.

## Confidence gates

The report retains gate version 1 beside every qualification. The defaults are
the conservative routing-policy thresholds: at least 20 comparable samples,
at least 70% grade, duration, token, and semantic-reissue coverage, at least 70%
favorable known grades, and no more than 10% semantic reissues.

Missing dimensions, provenance, or observation dates produce `Unknown`.
Complete evidence below a threshold produces `BelowConfidenceGate`. A
controlled cohort that clears every gate can be `Validated`. An observational
cohort can be `ObservationalSupport` but never claims validation.

`RoutingEvidenceTrust.FromReport` carries the same rule into `ModelTrustLedger`:
only a gated controlled qualification becomes supporting trust evidence. A
below-gate controlled cohort is inconclusive, and observational cohorts are not
converted to independent proof. Trust entries retain both the raw artifact and
the derived qualification reference. Multiple cohort qualifications backed by
one raw artifact count as one independent proof.
