# Deterministic model-routing API

`ModelRouter.Route` is the public admission decision that composes the existing
routing components. It does not replace them:

- `TaskComplexityEstimate` supplies the upfront six-axis worksheet and its
  evidence.
- `ModelRoutingPolicy` establishes the scored recommendation, correctness
  floors, bounded-decision exception, and semantic-reissue rules.
- `ModelRoutingKnowledgeBase` resolves canonical models, thinking levels,
  workflow roles, policy evidence, and explicitly declared provider fallbacks.
- `ModelEfficiencyMatrix`, `EfficiencyPolicy`, and `ModelSuggestion` retain the
  compatibility and date-aware cost view for every recommended or selected
  route.
- `RoutingEvidenceReport` supplies versioned benchmark qualification, while
  `ModelTrustAssessment` supplies independently derived trust restrictions and
  uncertainty. A caller can set `RequiredBenchmarkCapability` when more than
  one capability cohort exists for the same model, thinking level, and task
  class; otherwise that ambiguity cannot qualify a provider fallback.
- `ProviderAvailabilitySnapshot`, the explicit available-CLI set, and
  `ModelRoutingCapacity` supply run-scoped quota, budget, and deterministic
  verification state.

The authoritative behavior remains
[`docs/system/domains/model-routing-policy.md`](system/domains/model-routing-policy.md).

## Astra support and evidence

`KnownModels.Gpt6Astra` is supported by `EvaluateModel`, `SuggestModel`, and
explicit `ModelRouter` operator pins for core tasks. Its reasoning ladder is
`low`, `medium`, `high`, `xhigh`, and `max`; `Resolve` rejects unsupported levels
such as `minimal` and `ultra`. `EvaluateModel` can instead clamp a desired effort
to the supported ladder, as it does for other models.

Its `Selectable` routing status and `Provisional` evidence status answer
different questions: the former permits use, while the latter reports that
local completion evidence has not validated the routing fit. Published
external benchmark scores are retained separately from local task completion,
review suitability, and trust. Those local metrics remain unknown when no
qualifying cohort exists; support does not create a success rate.

The four automatic core tiers and task-class sets remain unchanged. A pin can
select Astra while retaining the recommended core route, missing-evidence
reasons, and the below-policy flag. An unpinned task still follows the score
ladder and correctness floors. See the
[support policy](system/domains/model-routing-policy.md#additional-supported-models).

`KnownModels.ClaudeFable51` follows the same support boundary through Claude
Code. All five efforts from `Low` through `Max` (excluding `Ultra`) are
supported. The provider defaults to `high`; Token Economy derives its own
suggested effort from the task and pressure unless `desiredEffort` is supplied.
A Claude-only `SuggestModel` call can therefore return Fable 5.1, while Sonnet 5
remains an explicitly declared fallback. Neither listing nor comparison invents
local completion evidence or makes Fable 5.1 an automatic provider fallback.

Publication dates are available as `ModelPriceCatalog.Default.Find(model)?.ReleaseDate`
with a `ReleaseDateSource` link. They are separate from `ModelPrice.ValidFrom`;
a price revision or snapshot suffix is not a new model release. Unknown dates
remain null. See the [dated release register](model-release-dates.md).

## Task-class card-creation prior

`TaskClassRecommendationCatalog.Default.Recommend(taskClass)` exposes the
versioned taxonomy recommendation set used when Agent Studio creates a card.
`Candidates` is a stable ranked set of model/thinking pairs, not a hidden
quota winner; it retains equivalence status, rationale/evidence versions,
uncertainty, measured cost per outcome, and explicit downgrade/no-downgrade
conditions. A singleton is valid and means that no task-qualified provider peer
is known. HTML/UI implementation and source-code review use controlled pilot
results; rows without a comparable study say `PolicyBaseline` rather than
implying a measured winner. Planning and Decision-Making always have a strong
minimum capability and no smaller cost downgrade.

`TaskClassRecommendationCatalog.Select(set, quotaState, atUtc)` is the separate
pure selection step. It chooses only from `set.Candidates`, first by fresh quota
headroom and then by retained retry/cost/token evidence and stable rank. Missing,
stale, suspicious, or incomplete quota produces `RecommendationOnly` with no
selected route; fully known exhausted capacity produces `Wait`. Selection never
introduces the catalog's separately documented downgrade route. A downgrade is
a new concrete-card policy evaluation through `ModelRouter`, so score windows
and correctness floors are applied again.

`OutcomeEfficiency.ComputeObserved(...)` supplies tokens per accepted outcome,
dated list-price cost per accepted delivery, initial cost, retry-adjusted cost,
retry uplift, coverage, and explicit unknown reasons. Failed semantic and
substrate attempts both contribute measured spend. Missing usage or price data
keeps complete-cohort metrics null rather than turning them into zero.

This prior does not replace `ModelRouter.Route` at attempt admission. The
concrete score, uncertainty, hard floors, semantic promotion, capacity, and
operator pin still decide the launch route. See
[`task-class-routing-studies.md`](task-class-routing-studies.md).

## Evaluation order

The router is pure and deterministic. It performs no probes, launches, writes,
or logging. For the same input graph it returns the same result.

1. Validate that the task and schema-version-2 upfront estimate agree.
2. Reconstruct and retain the six score criteria.
3. Ask `ModelRoutingPolicy` for the scored route and apply every correctness or
   semantic-reissue floor.
4. Enforce the requested workflow role. Mini/high requires compact structured
   evidence, a deterministic output contract, and bounded context. An
   ambiguous or unbounded authorizing decision uses at least Sol/medium.
5. Resolve an operator pin without rewriting it. A valid pin wins and is
   visibly flagged when it is below the policy recommendation. Unknown,
   unsupported, restricted, deprecated, or workflow-incompatible pins require
   an explicit override decision.
6. Only now consult available CLIs and run-scoped quota/budget evidence.
7. If the preferred route is constrained, try an explicitly declared,
   task-qualified equivalent-provider fallback first.
8. If none qualifies, permit one lower core tier only when the effective score
   is within five points of that tier's lower threshold, deterministic
   verification exists, and no correctness or semantic-reissue floor applies.
9. Return `Wait` or `OverrideRequired` when no safe route remains.

The downgrade windows are therefore exactly `21–26`, `51–56`, and `70–75`.
No downgrade exists below Luna. A critical, stale, missing, suspicious, or
unknown quota state is never converted into healthy capacity.

## Result contract

Every `ModelRoutingResult` contains:

- the recommended route and nullable selected route;
- the complete upfront score worksheet, effective post-reissue policy score,
  effective empirical-uncertainty points, policy reason, and explicit
  correctness floor; the original upfront scorecard is retained unchanged;
- policy version, knowledge schema/evidence versions, and benchmark/gate
  versions;
- the selection source and a fallback, wait, or override reason;
- the original operator pin and its below-policy flag;
- matrix-produced `ModelSuggestion`, benchmark qualification, and trust
  assessment on each resolved route; and
- explicit uncertainty reasons, including provisional policy evidence,
  missing or below-gate benchmark evidence, unverified trust, and unknown or
  unconfirmed cost.

`SelectedRoute` is null only for `Wait` and `OverrideRequired`. The recommended
route and all other audit fields remain populated in those outcomes so a host
can explain the decision without reconstructing it.

## Agent Studio admission adapter

`AgentStudioTaskAdmission.PrepareAttempt` wires this pure result into the host
boundary before each attempt. It obtains or stores the intake estimate through
`ITaskComplexityEstimateStore`, selects the newest classified prior evidence
from `IAgentStudioRunLedger`, routes against the supplied run-scoped quota
snapshot, and records an immutable schema-version-2
`AgentStudioRoutingDecisionRecord`. Only a `Selected` result produces an
attempt-local `LaunchRoute`; wait and override results cannot accidentally be
launched.

The card's configured route and an explicit operator pin are separate request
fields. Neither is overwritten by the selected attempt route. Full host order,
persistence fields, import mappings, and operator rendering are documented in
[`agent-studio-routing-integration.md`](agent-studio-routing-integration.md).

## Human-friendly language capability

`LanguageCapabilityCatalog.Default` embeds the dated German/English language
catalogue. `Find(model, language, thinkingLevel, atUtc)` returns the latest row
of the strongest evidence status for that configuration at the UTC cutoff.
`Records` retains all observations; `Studies` exposes publication kind, primary
URL, takeaway, status, and verification date. An omitted effort is useful for
inspection; routing always uses the actual selected effort.

```csharp
var at = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
var record = LanguageCapabilityCatalog.Default.Find(
    KnownModels.Gpt6Luna, "de", EffortLevel.Medium, at);

var requirement = new HumanFriendlyLanguageRequirement
{
    Language = "de",
    MinimumOverall = 0.75m,
    MinimumScores = new()
    {
        Readability = 0.75m,
        AbsenceOfAiIsms = 0.75m,
        FactualRestraint = 0.80m,
    },
    ThinkingLevel = EffortLevel.Medium,
    AllowProvisional = false,
};

var candidates = ModelEfficiencyMatrix.Default.SuggestModel(
    TaskClass.DocEdit, BudgetPressure.Tight,
    [Cli.Codex, Cli.ClaudeCode], at, requirement);

// Apply to an already policy-qualified route at its actual effort.
var evaluated = ModelEfficiencyMatrix.Default.EvaluateModel(
    KnownModels.Gpt56Sol, TaskClass.DocEdit, BudgetPressure.Tight, at,
    desiredEffort: EffortLevel.Medium, requirement: requirement);

var prior = TaskClassRecommendationCatalog.Default.RecommendLanguage(
    TextWorkKind.OperatorMessages, "de");
// Also: Copy, Documentation, Replies. Each prior retains its requirement.
```

The optional `requirement` is the fifth `SuggestModel` parameter and follows
`desiredEffort` in both string and typed `EvaluateModel` overloads. Existing
calls retain their behavior. `Cli.ClaudeCode` names the same runtime as the
existing `Cli.Claude` enum value.

All supplied thresholds are conjunctive and in `[0,1]`; invalid or empty
requirements throw. A missing score cannot satisfy a threshold of zero.
Unverified claims never pass, and provisional measured scores require explicit
opt-in. A different language or effort cannot substitute for missing evidence.
A requested effort that would be clamped to another level does not pass. The
cutoff excludes future observations, and a new failure at the same evidence
status supersedes an older passing score.

For tight or critical budgets, candidates passing the constraint sort by the
recorded USD cost per sample (known costs first), then the established
compatibility preference for ties. Comfortable budgets retain compatibility
ranking among passing models. `Score` remains the legacy compatibility score;
it is not the primary sort key for constrained tight/critical results. A
passing `ModelSuggestion.LanguageCapability` identifies the exact scores,
status, measured cost, and dated evidence used. Empty lists or null evaluations
mean no qualifying evidence; they do not authorize a fallback.

Hosts can inject a validated `LanguageCapabilityCatalog` as the third
`ModelEfficiencyMatrix` constructor argument. This supports evidence snapshots
without services or filesystem access in the selection path. The core library
has no new package dependency. Lookup resolves canonical identities through
the repository price catalogue; imported rows require canonical ids.

The initial seed contains research-informed gaps with null scores and costs,
so the default constrained call currently returns an empty set. Import a real
Voice Lint result to populate measured candidates. The
[concept and intake guide](human-friendly-language.md) documents the shared
schema, equal-weight mean, date precision, provenance, cost limitations, and
all primary sources.

Always establish correctness floors through the existing routing policy before
using this compatibility menu. Language evidence adds a constraint; it does
not alter model tiers, default routes, supported fallback equivalences, or
`policyVersion`. An explicit `EvaluateModel` desired effort must be the effort
that the policy-qualified route will actually execute.
