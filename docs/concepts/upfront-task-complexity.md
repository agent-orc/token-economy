# Upfront task complexity for routing

## Decision

`TaskComplexityEstimator` produces a versioned per-card estimate before a run:
`trivial`, `standard`, `demanding`, or `critical`, plus a 0–1 confidence,
predicted tokens/duration/reissues, dimension evidence, and the historical neighbours
used. `ITaskComplexityEstimateStore.Upsert` is the integration contract for
Agent Studio routing policy.

| Dimension | Weight | Measurable inputs |
|---|---:|---|
| Touched surface | 20% | referenced files/subsystems, dependency fan-out |
| Novelty | 18% | measured override; otherwise new-design/research language |
| Constraint density | 18% | acceptance criteria, security/concurrency/correctness constraints |
| Specification ambiguity | 15% | measured override, exploration/decision language |
| Required reading | 15% | prompt/context size, subsystems, capped repository retrieval term |
| Verification cost | 14% | acceptance gates, integration/E2E/backtest/benchmark requirements |

The input catalogue accepts prompt, task type, project/area, epic context,
acceptance criteria, touched files/subsystems, dependency fan-out, repository
file count, and optional measured dimension overrides. Signals that cannot be
observed before launch—actual changed files, tool calls, or review feedback—are
outcomes for later calibration, not estimator inputs.

Total repository size is not treated as work. It has a small capped effect only
inside required reading; touched surface and fan-out dominate. The production
backtest should compare a model with and without that term and retain it only if
out-of-sample token/reissue error improves.

## Calibration and 30-card backtest

`AgentStudioTaskStorageImporter` retains prompt/card features.
`ComplexityHistory.FromRunRecords` aggregates all attempts into one measured
sample per card (total tokens/duration plus reissue count), and
`ComplexityBacktester.Run` performs leave-one-out evaluation. It reports band
accuracy, token median absolute percentage error, reissue mean absolute error,
and token-cost Spearman rank correlation. Duplicate card keys are rejected so
another attempt of the held-out card cannot leak into its historical neighbours.

The automated suite applies this pipeline to exactly 30 controlled fixture
cards. These validate the method; they are **not production evidence**. The
requested historical report must be generated from 30 real, prompt-enriched
Agent Studio cards after ingestion. Invented production metrics are not reported.

## Optional mini-model assessment and break-even

The library accepts an `LlmComplexityAssessment` but does not call a provider.
The host can version, log, cache, or disable its rubric call. Influence is capped
at 20% and scaled by supplied confidence; agreement raises overall confidence.

Use the assessment when:

`P(mis-route without LLM) - P(mis-route with LLM) > estimator cost / avoidable mis-route cost`

For an illustrative 2,000-token assessment and a 200,000-token avoidable
mis-route, break-even is a 1% absolute reduction in mis-routing probability. At
3,000 versus 300,000 tokens it is also 1%. Use money instead when model prices
differ materially, and include reissue and gate-time costs.

## Routing research fit and learning loop

[RouteLLM](https://arxiv.org/abs/2406.18665) learns a router from preference
data between stronger and weaker models. [FrugalGPT](https://arxiv.org/abs/2305.05176)
learns model cascades under cost/quality trade-offs. Token Economy starts with
auditable features plus measured neighbours and an optional semantic judge:

`task → extract features → select model + prompt variant → observe run → ingest tokens/duration/reissues/outcome → recalibrate`

Model-selection research is worth the time when:

`task volume × avoidable cost per task × achievable error reduction > estimator + maintenance + evaluation cost`

For one-off cheap tasks, use deterministic policy. For repeated expensive task
families, collect outcomes and use temporal or project-held-out evaluation
before enabling learning-based selection.
