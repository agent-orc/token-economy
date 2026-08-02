# Upfront task complexity for routing

## Decision

`TaskComplexityEstimator` converts a card into a versioned, auditable routing
worksheet before implementation starts. The worksheet follows the weighted
decision and hard floors in
[`model-routing-policy.md`](../system/domains/model-routing-policy.md); that policy
remains authoritative if this implementation note drifts.

The schema-version 2 result exposes:

- correctness risk, expected scope, context demand, task uncertainty, empirical
  confidence, and quota/cost headroom as separate scored criteria;
- the score, maximum score, and English evidence for every criterion, plus
  evidence for the total score and confidence calculation;
- the final complexity band, overall confidence (capped when history is absent),
  and every applied hard-floor trigger;
- the existing token, duration, and reissue point forecasts plus
  lower/expected/upper ranges and range evidence;
- comparable neighbour keys and measurement availability, plus aggregate
  historical coverage.

`ITaskComplexityEstimateStore.Upsert` remains the Agent Studio integration
contract. The point-forecast properties remain available for existing callers;
each equals the `Expected` value in its corresponding forecast range.

## Canonical weighted worksheet

The maximum is 100 points. Scores are policy points rather than normalized
percentages.

| Criterion | Maximum | Deterministic intake anchors |
|---|---:|---|
| Correctness risk | 35 | `0` prose/non-behavioral; `12` reversible local behavior with verification; `24` persistent state, public contract/protocol, unclear bug, or consequential migration; `35` critical authority, security, concurrency, or data-loss risk |
| Expected scope | 20 | `0` up to about 50 expected lines in one subsystem; `8` 51–200 lines or two related components; `14` 201–500 lines or three runtime subsystems; `20` more than 500 lines, four or more runtime subsystems, or repository-wide migration |
| Context demand | 20 | `0` exact file/behavior known; `8` adjacent component or contract; `14` several layers or historical behavior; `20` broad codebase/history and cross-repository or distributed invariants |
| Task uncertainty | 10 | `0` mechanical/copy; `3` clear refactor/content/docs; `6` well-specified bug or feature; `10` unknown root cause, architecture decision, or derived requirements |
| Empirical confidence | 10 | `0` qualified cohort of at least 20; `3` at least five favorable comparable runs; `6` sparse or mixed history; `10` no cohort, repeated semantic reissues, or unfavorable history |
| Quota and cost headroom | 5 | `5` comfortable headroom; `3` nearing a cap; `0` unavailable. It never lowers a hard floor. |

Totals map to the policy ladder exactly: `0–20` is `trivial` (Luna/medium),
`21–50` is `standard` (Terra/medium), `51–69` is `demanding`
(Sol/medium), and `70–100` is `critical` (Sol/xhigh). The optional LLM
assessment can improve confidence when it agrees with the worksheet, but it
does not silently rewrite any canonical score.

Callers may supply explicit pre-launch policy scores through
`ComplexityRoutingSignals`. Each override is clamped to its policy maximum and
its evidence says that it was supplied explicitly. Empirical confidence cannot
be overridden: it is calculated from the leakage-safe historical cohort.

## Intake-only scope and importer mappings

Estimator inputs must exist before implementation: authored prompt, task type,
project/area, epic context, acceptance criteria, referenced or expected files,
expected runtime subsystems, expected changed-line range, dependency fan-out,
repository file count, explicit intake routing scores, and explicit hard-floor
facts.

Expected scope is not eventual changed scope. The importer maps
`referencedFiles`/`expectedFiles`,
`referencedSubsystems`/`expectedSubsystems`, and `expectedChangedLines`. It
deliberately does not map `changedFiles`, `diffStats`, review feedback, tool
calls, completion lanes, or other post-launch outcomes into `ComplexityCard`.
Routing scores and `hardFloorTriggers` may be direct card fields or live under
`routingFeatures`, `upfrontComplexity`, or `complexityRoutingSignals`.

Repository size is retained only for backward-compatible forecast similarity;
it is not a routing-scope score. Generated files do not count toward expected
scope.

## Hard floors

Hard floors are applied after the weighted total and never alter that total.
The result lists the trigger, minimum band, source evidence, and whether it was
an explicit intake fact or a deterministic card-text match.

- `P0`, `Fencing`, `LeaseOwnership`, `StaleWriteRejection`,
  `DistributedAuthority`, `SecurityBoundary`, and `CredibleDataLoss` require
  `critical` / Sol-xhigh.
- `PublicProtocol`, `PersistentStateMigration`,
  `ThreeOrMoreRuntimeSubsystems`, and
  `DestructiveOrSecurityCriticalBoundedDecision` require at least `demanding`
  / Sol-medium.
- `UnclearBug` requires at least `standard` / Terra-medium.

Quota is run-scoped and cannot reduce these floors. Model/provider selection
still belongs to the orchestrator; the estimator supplies its auditable routing
facts and floor.

## History, confidence, and forecast ranges

Historical outcomes calibrate only empirical confidence and the existing cost
forecasts. They never change correctness risk, expected scope, context demand,
or task uncertainty.

`ComplexityHistory.FromRunRecords` deduplicates task key plus attempt, then
aggregates one sample per card. Missing token, duration, grade, or semantic
reissue telemetry remains marked incomplete. It is excluded from the relevant
forecast or metric instead of becoming a measured zero. `HistoricalEvidence`
and each neighbour expose this coverage, so no-history and low-confidence
routes remain visible.

Before similarity or cohort calculations, `Estimate` removes every historical
sample whose task key equals the evaluated card. `ComplexityBacktester.Run`
leaves out the entire card. `RunHeldOut` accepts explicit training and evaluation
sets and rejects any overlapping card key, preventing an attempt of an evaluated
card from entering its neighbours. Backtest metrics are nullable and include
evaluation counts; unavailable outcome coverage therefore cannot appear as a
zero error.

The lower/expected/upper forecasts envelope both confidence and the observed
span of complete comparable history. Without complete neighbours,
deterministic point forecasts are retained and the range is widened according
to confidence. Range evidence says which path was used.

## Calibration and learning loop

The deterministic 30-card fixtures validate mechanics and boundaries; they are
not production evidence. Real-card calibration artifacts live under
[`results/complexity-backtest`](../../results/complexity-backtest/). Refresh
them with `dotnet run --project tools/ComplexityBacktestReport` while the Agent
Studio API is available. Prefer temporal, project, or scenario holdouts through
`RunHeldOut` for claims about generalization.

The loop remains:

`task → extract auditable intake features → select route → observe run → ingest complete/unknown outcomes → recalibrate on held-out cards`

Use the optional model assessment only when its expected reduction in expensive
misroutes exceeds its own token, latency, and maintenance cost. Controlled
comparisons—not observational routing history—are required before changing the
canonical policy thresholds or correctness floors.
