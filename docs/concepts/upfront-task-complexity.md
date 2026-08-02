# Upfront task complexity for routing

## Decision

`TaskComplexityEstimator` turns a task card into a versioned, auditable routing
worksheet before implementation starts. The worksheet follows the weighted
criteria, score bands, and hard floors in the canonical
[model-routing policy](../system/domains/model-routing-policy.md). It also keeps
the existing token, duration, and reissue point forecasts and adds a confidence
range around each forecast.

The estimate exposes:

- correctness risk (`0..35`);
- expected scope (`0..20`);
- context demand (`0..20`);
- task type and uncertainty (`0..10`);
- empirical-confidence uncertainty (`0..10`);
- run-scoped quota/cost headroom (`0..5`);
- total score, complexity band, confidence, route, and applied hard floors;
- point and lower/upper forecasts for tokens, duration, and reissues; and
- evidence for every criterion, empirical evidence status, and the exact
  historical neighbours used by the forecasts.

`ITaskComplexityEstimateStore.Upsert` remains the integration contract. Schema
version 2 adds the routing worksheet and forecast ranges without removing the
version 1 point forecasts (`PredictedTokens`, `PredictedDuration`, and
`PredictedReissues`).

## Intake-only inputs

Only facts available before implementation are valid inputs. The importer
whitelists `upfrontComplexity`/`upfrontRouting`, `expectedChangedLines`,
`expectedChangedFiles`, `expectedRuntimeSubsystems`, `hardFloorTriggers`, and
`routingFeatures`. It deliberately does not map eventual `changedLines`,
`changedFiles`, tool calls, attempt output, review feedback, or the final diff.

Expected scope uses the policy anchors:

| Points | Intake expectation |
|---:|---|
| 0 | Up to about 50 changed lines in one runtime subsystem |
| 8 | About 51–200 lines or two tightly related runtime components |
| 14 | About 201–500 lines or three runtime subsystems |
| 20 | More than 500 lines, four or more runtime subsystems, or a repository-wide migration |

Generated files do not count. `ReferencedFiles` and `ReferencedSubsystems` help
similarity and context retrieval, but they are not silently reinterpreted as an
observed diff. `ExpectedChangedFiles` and `ExpectedRuntimeSubsystems` are named
expectations captured at intake.

A host may provide a `ComplexityCriterionOverride` when a structured intake
already assigned policy points. Each override must include non-empty evidence
and stay within the criterion's canonical maximum. Otherwise the estimator uses
deterministic anchors from task type, expected scope, referenced context,
measured pre-launch signals, and explicit triggers. The optional
`LlmComplexityAssessment` may increase confidence when it agrees with the
worksheet, but it does not rewrite the authoritative weighted score.

Missing quota evidence remains null at intake and contributes zero points with
an explicit explanation. Quota can affect a borderline score only as allowed by
policy; it never lowers a hard floor.

## Score bands and hard floors

The raw total maps to these complexity bands and core routes:

| Score | Complexity band | Route |
|---:|---|---|
| 0–20 | `trivial` | Luna / medium |
| 21–50 | `standard` | Terra / medium |
| 51–69 | `demanding` | Sol / medium |
| 70–100 | `critical` | Sol / xhigh |

Hard floors are applied after scoring. `HardFloorTriggers` retains the exact
trigger ids, `AppliedHardFloors` retains the matching floor ids, and
`RecommendedRouteId` contains the post-floor route. The estimator recognizes
all canonical triggers:

- `p0`, `fencing`, `leaseOwnership`, `staleWriteRejection`,
  `distributedAuthority`, `securityBoundary`, and `credibleDataLoss` require
  Sol/xhigh;
- `publicProtocol`, `persistentStateMigration`, and
  `threeOrMoreRuntimeSubsystems` require at least Sol/medium; and
- `unclearBug` requires at least Terra/medium.

Three or more explicitly expected runtime subsystems automatically emits the
corresponding trigger. Other correctness triggers must be supplied as explicit
pre-launch facts. Unknown trigger ids fail validation instead of being ignored.

## Historical evidence and forecast ranges

`AgentStudioTaskStorageImporter` retains the pre-launch card snapshot and
attempt telemetry. `ComplexityHistory.FromRunRecords` aggregates attempts into
one sample per task key and carries token, duration, semantic-reissue, and grade
availability separately. Absent telemetry is not presented as a measured zero.

The empirical-confidence criterion follows the policy gates:

- no comparable cohort, repeated reissues, or an unfavorable cohort: 10;
- sparse, mixed, or incompletely observed evidence: 6;
- at least five favorable comparable runs: 3; and
- at least 20 comparable runs with useful grade/reissue coverage, at least 70%
  A/B among known grades, and under 10% reissue: 0.

`HistoryEvidenceStatus` is `Missing`, `LowConfidence`,
`FavorableSmallCohort`, `Sufficient`, or `Unfavorable`; `HistoryEvidence`
reports cohort size and coverage. Missing history therefore remains visible
even when a host supplies an explicit empirical score from another versioned
source.

The five most similar qualifying tasks calibrate the existing point forecasts.
Only neighbours with available token or duration telemetry calibrate that
metric. Range width grows as overall confidence falls. The range is an
uncertainty envelope for routing and planning, not a statistical confidence
interval.

## Held-out backtesting and leakage control

`ComplexityBacktester.Run` performs leave-one-task-out evaluation. Before
similarity, cohort scoring, or forecast aggregation, the estimator removes
every history sample whose task key matches the evaluated card. Supplying two
aggregated samples for one key is rejected, and `ComplexityHistory` collapses
raw attempts by task key first. Thus no attempt from the held-out card can
appear as its own neighbour.

Each `ComplexityBacktestRow` records its held-out task key, score, estimated and
actual band, neighbour keys, triggers, applied floors, and history-evidence
status. `HeldOutNeighbourLeakageCount` must remain zero. Deterministic tests
exercise totals `0`, `20`, `21`, `50`, `51`, `69`, `70`, and `100`, plus every
hard-floor trigger, through this held-out path.

The backtester still reports band accuracy, token median absolute percentage
error, reissue mean absolute error, and token-cost Spearman rank correlation.
These observational metrics calibrate forecasts; they are not causal model
comparisons. Use temporal or project-held-out cohorts before enabling a learned
router.
