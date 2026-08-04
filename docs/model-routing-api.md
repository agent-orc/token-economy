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
