# Quality Studio review evidence

Quality Studio review runs are an observational operational evidence stream for
the `review` task class. They are not controlled A/B comparisons: model,
thinking level, CLI, reviewed change, review aspect, and later sighting behavior
can all be correlated. The evidence can support a routing signal after declared
coverage gates, but it cannot establish that one model caused a better result.

## Drop contract

Quality Studio writes one immutable JSON document per review run to a directory
chosen by the operator. The contract is
[`benchmarks/schema/quality-studio-review-run.schema.json`](../benchmarks/schema/quality-studio-review-run.schema.json).
Every run records:

- the source run ID and UTC completion timestamp;
- model, thinking level, and CLI as actually executed;
- the review aspect;
- reviewed file count and reported finding count;
- confirmed and dismissed finding counts when Quality Studio sighting outcomes
  are available; absent outcomes remain unknown rather than becoming zeros;
- policy `evidenceStatus: observational`;
- `isFixture`, which excludes contract fixtures from all model metrics.

The first contract fixture is
[`qs-review-fixture-001.json`](../benchmarks/fixtures/quality-studio-review-runs/qs-review-fixture-001.json).
It deliberately contains plausible sighting outcomes but is excluded from
aggregation. Replace or supplement this drop with Quality Studio's persisted
run artifacts when the counterpart integration is available; do not clear
`isFixture` on synthetic data.

## Import and aggregation

Run the importer from the repository root:

```powershell
dotnet run --project tools/QualityStudioReviewEvidence -- <quality-studio-drop-path>
```

An optional second argument changes the evidence output root. The default is
`results/routing-evidence/review/`. Schema version 1 writes:

- immutable normalized runs to `v1/runs/<sourceRunId>.json`;
- the deterministic aggregate to `v1/review-evidence.json`.

Re-importing identical input is a no-op. Different content for a retained
source run ID fails rather than overwriting history. The importer hashes the
source artifact, canonicalizes known model aliases and thinking levels, checks
the model/CLI pair, and retains explicit eligibility issues. If
`JOB_RESULTS_DIR` is set, the aggregate is also written there for run-level
collection.

The report groups comparable operational runs by model, thinking level, CLI,
and review aspect, then provides one `review` summary for every model in the
routing knowledge base. Each summary contains run, file, finding, confirmed,
dismissed, assessed-finding, outcome-coverage, date, dimension, and provenance
metrics.

## Evidence quality and routing use

Review evidence uses conservative version-1 gates: at least 20 operational
runs, at least 20 sighted findings, and at least 70% finding-outcome coverage.
A model below any gate is `insufficientEvidence`; a model with no operational
runs also retains policy evidence status `unknown`. A cohort that clears the
gates is `observationalSupport` with policy evidence status `observational`—it
is never called validated.

For gated observations, confirmed findings divided by confirmed plus dismissed
findings derives the compatibility signal used by the matrix. Version 1 retains
the declared thresholds in the report: at least 80% is `ideal`, at least 60% is
`capable`, and a lower gated result is `underpowered`. This is only a precision
signal over findings that the reviewer emitted. It cannot measure missed
findings or review recall, so the report retains that limitation and the
observational evidence label.

`ModelRoutingKnowledgeBase.Default` composes the committed evidence report,
and `ModelEfficiencyMatrix.Default` projects it into the review column. A host
can compose a newer report with `PolicyOnly.WithReviewEvidence(report)` and
`ModelEfficiencyMatrix.FromKnowledge(knowledge)`. Until a model clears the
gates, `SuitabilityOf(model, TaskClass.Review)` is null and
`SuggestModel(TaskClass.Review, ...)` omits it. If no model clears the gates,
the suggestion list is empty instead of asserting a fit.

Regenerate the public knowledge view only through its tool:

```powershell
dotnet run --project tools/ModelRoutingKnowledgeReport -- docs/model-routing-knowledge.md
```

The generator reads the committed review aggregate by default. Its drift test
ensures the per-model review-quality column, detailed metrics, and evidence
quality stay synchronized.
