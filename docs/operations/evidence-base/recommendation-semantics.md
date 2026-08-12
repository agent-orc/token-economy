# Recommendation semantics

This is a specification for TE-42 and a future library API. It is not an
executable-policy change. The current
[model-routing policy](../../system/domains/model-routing-policy.md) remains
authoritative until its versioned projection changes.

## Separate three questions

TokenEconomy currently risks collapsing three different questions:

1. **Capability:** what minimum capability does the task require?
2. **Recommendation:** which routes are evidence-qualified and effectively
   equivalent for that task, independent of current capacity?
3. **Selection:** which one of those routes is usable now under a trustworthy
   quota snapshot?

The library should answer them in that order. Price and quota may choose among
routes that already clear the floor. They may not redefine the floor.

## Task-class semantics

Add two first-class values:

```csharp
public enum TaskClass
{
    HeavyDesign,
    Planning,
    DecisionMaking,
    Feature,
    MechanicalChore,
    DocEdit,
    Research,
    Review,
}
```

`Planning` produces or revises a multi-step course of action under dependencies,
uncertainty, constraints, or trade-offs. `DecisionMaking` selects among
meaningfully different alternatives whose downstream consequences matter. A
request can carry both capabilities; the highest floor wins.

A parseable label over compact structured evidence is not automatically
`DecisionMaking`. It remains the existing `boundedPipelineDecision` workflow
role only when all of the following hold:

- evidence is compact and bounded;
- output choices and schema are fixed;
- a deterministic or directly reviewable contract exists;
- the result cannot independently authorize a destructive, security,
  lane-affecting, or otherwise high-consequence action; and
- ambiguity promotes the call to a strong route.

This preserves the current Mini/high role exception without allowing a caller
to relabel consequential decisions as classification.

## Ideal suitability by class

| Task class or capability | `Ideal` band | Smaller band treatment | Evidence state |
| --- | --- | --- | --- |
| `Planning` | **Strong** | `Underpowered` by default | Operator floor; external E02/E10/E13; no local cohort. |
| `DecisionMaking` | **Strong** | `Underpowered` by default | Operator floor; external E02; no local cohort. |
| `HeavyDesign` | **Strong** | At most `Capable` when scope is tightly bounded; never ideal by cost alone | External E01/E10–E12; local evidence observational. |
| Open-ended `Research` | **Strong** | Smaller only after the task is narrowed to bounded retrieval or transformation | External E10–E12; no local cohort. |
| Real pull-request `Review` | **Strong, provisional** | No smaller route may be called ideal until review-specific qualification | External E05/E06; local O05 has no real runs. |
| Visual or interactive frontend/HTML | **Strong** | Smaller is eligible only for a separately qualified static/template capability with rendered gates | External E02/E07–E09; no local cohort. |
| Demanding `Feature` or unclear bug | **Strong** when current score/floors require it | A smaller band cannot cross the existing correctness or scope floor | External E01–E03; local O02. |
| Clear, one-subsystem `Feature` | **Balanced** | Light only for a separately qualified local-repair capability | Local O01 is favorable but sparse; O02 shows the boundary matters. |
| `MechanicalChore` | **Light or balanced** | Eligible and preferred when verification is deterministic | Provisional; indirect E14/E15, no local comparison. |
| `DocEdit` | **Light or balanced** | Eligible and preferred for bounded prose changes | Provisional intuition; no aligned benchmark. |
| Bounded structured pipeline decision | **Light role route** | Eligible under the five conditions above | Existing policy exception; very sparse local observations. |

“GPT-5.5 tier and below” is therefore a candidate band for mechanical chores,
bounded documentation changes, simple local repairs, and strictly bounded
transforms. It is not automatically selectable: the concrete model/effort pair
must be supported, unrestricted, task-qualified, and above every correctness
floor. `gpt-5.5` being present in the price catalog does not change its current
`unsupported` routing status.

## Recommendation returns a set

The quota-independent result should be semantically unordered even if it uses a
stable display order.

```csharp
public sealed record ModelRecommendationSet
{
    public required TaskClass TaskClass { get; init; }
    public required CapabilityTier MinimumCapability { get; init; }
    public required Suitability TargetSuitability { get; init; }
    public required IReadOnlyList<EquivalentRoute> Candidates { get; init; }
    public required EquivalenceStatus Equivalence { get; init; }
    public required IReadOnlyList<string> EvidenceReferences { get; init; }
    public required IReadOnlyList<string> UncertaintyReasons { get; init; }
    public required string PolicyVersion { get; init; }
}

public sealed record EquivalentRoute
{
    public required ModelId Model { get; init; }
    public required EffortLevel Thinking { get; init; }
    public required Cli Cli { get; init; }
    public required EvidenceStrength Evidence { get; init; }
    public required ModelSuggestion Suggestion { get; init; }
}

public enum EquivalenceStatus
{
    Demonstrated,
    Provisional,
    Singleton,
    InsufficientEvidence,
}
```

Names are illustrative, but the semantics are normative:

- `Candidates` contains model-and-thinking routes, not bare model families.
- A result may be a singleton. The API should expose this as an evidence or
  coverage limitation rather than pad the set with an underpowered model.
- Empty means no safe recommendation, not “pick the cheapest.”
- Stable ordering is for serialization only and must not imply that candidate
  zero is the selected route.
- The set retains policy, benchmark, trust, and price uncertainty for every
  candidate.
- Recommendation does not consume a quota snapshot and never contains a hidden
  quota-derived winner.

## What counts as equivalent

Equivalence is task-class and capability specific. Two routes are
`Demonstrated` equivalents only when:

1. both independently clear the task's capability, workflow, trust, and
   correctness gates;
2. outcomes came from comparable tasks, prompts, scaffolds, verification, and
   attempt budgets;
3. the cohort passes the existing coverage gate and a pre-registered
   equivalence/non-inferiority test;
4. the confidence interval for the paired quality difference stays within the
   declared practical margin; and
5. no material secondary outcome such as false-positive severity, unsafe
   behavior, or semantic reissue rate crosses its own guardrail.

For binary accepted-delivery outcomes, let `p_best - p_candidate` be the paired
quality loss and `delta` the class-specific maximum acceptable loss. A candidate
is non-inferior only when the upper confidence bound of that loss is at most
`delta`. The dossier does not prescribe one global `delta`: five percentage
points may be reasonable for reversible chores and unacceptable for security or
data-loss work. The benchmark definition must declare it before results exist.

Close point estimates, overlapping confidence intervals, a shared capability
tier, or “no observed failures” in a tiny cohort do not prove equivalence.

When controlled evidence is sparse, policy may return `Provisional` peers that
all clear the same conservative floor. The uncertainty must be visible, and a
provisional set must not be described as “equally good” in user-facing copy.

## Quota-aware selection

Selection is a pure function over a recommendation set and a run-scoped quota
snapshot:

```csharp
public static ModelSelectionResult Select(
    ModelRecommendationSet recommendation,
    ProviderAvailabilitySnapshot quota,
    DateTime atUtc,
    SelectionPolicy policy);
```

The evaluation order is:

1. Validate that the snapshot is current and internally trustworthy at
   `atUtc`. Stale, missing, suspicious, or contradictory data is unknown, not
   healthy.
2. Preserve the original recommendation set and capability floor in the result.
3. Remove routes whose CLI or model is unavailable, restricted, deprecated, or
   workflow-incompatible.
4. Remove routes whose known quota state cannot admit the projected attempt.
5. Among the remaining equivalent routes, prefer sufficient headroom, then the
   lowest evidence-qualified expected retry-adjusted cost, then tokens per
   accepted outcome, then dated list-price estimate. Use canonical model and
   thinking identifiers only as the final deterministic tie-break.
6. Return `Selected` with source and reason, or `Wait` when no candidate remains.

An unknown quota snapshot does **not** authorize step 5. The result is
`RecommendationOnly` with no selected route. A caller that cannot represent an
unselected recommendation must not call this overload.

If all equivalent candidates are constrained, `Select` returns `Wait`. The
existing policy's narrowly allowed one-tier downgrade is a new policy
evaluation, not selection from the original equivalent set. That reevaluation
must retain the original recommendation, satisfy its five-point window and
deterministic-verification requirements, and never cross a hard or semantic-
reissue floor.

## Result audit fields

Every concrete selection should retain:

- the full recommendation set and its equivalence status;
- the selected route, or null for recommendation-only/wait/override;
- task class, capability, score worksheet, and effective correctness floor;
- policy, knowledge, benchmark, trust, and equivalence-test versions;
- quota snapshot identity, observation time, freshness, and per-window state;
- selection source and eliminated-candidate reasons;
- dated cost status and retry-efficiency evidence used to choose; and
- operator pin plus the existing below-policy warning.

Quota state remains attempt-scoped. Neither the recommendation set nor the
selected route rewrites card configuration.

## Compatibility path for TE-42

1. Add the two task classes and suitability rows without changing existing
   string values.
2. Introduce `RecommendSet(...)` beside the existing `SuggestModel(...)` API.
   Keep the old method as a compatibility projection only if it can avoid
   inventing a winner; otherwise deprecate it with a clear migration path.
3. Add the pure `Select(...)` operation and a result state for
   `RecommendationOnly`.
4. Project existing explicitly declared provider fallbacks into provisional
   sets; do not infer peers from catalog adjacency.
5. Add controlled equivalence evidence before changing a set from provisional
   to demonstrated.
6. Version the canonical Markdown policy, JSON projection, generated knowledge,
   and tests together when executable defaults change.

## Acceptance invariants

- `Planning` and `DecisionMaking` never return a smaller route as `Ideal`.
- Unknown quota never produces a concrete selection.
- Known cost cannot compensate for an unqualified capability.
- A recommendation set never contains a restricted, deprecated, or unsupported
  model/effort pair.
- A singleton is valid; an empty result is explicit.
- No selection crosses a hard correctness floor.
- Equivalent routes are traceable to task-local evidence or labeled
  provisional.
- Replaying identical recommendation, quota, catalog, and policy inputs returns
  the same result.
