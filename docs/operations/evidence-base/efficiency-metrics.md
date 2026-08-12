# Efficiency metrics

The library should measure resources per accepted outcome, not tokens in
isolation. A cheap failed attempt can be more expensive than a costly first-pass
delivery once retries are included.

This specification builds on the pricing catalog shipped in v0.3.1 and v0.3.2:

- [`TokenUsage`](../../../src/TokenEconomy/ModelPrice.cs) retains fresh input,
  output, cache-read, and cache-write token categories;
- [`ModelPriceCatalog.ComputeCost`](../../../src/TokenEconomy/ModelPriceCatalog.cs)
  resolves a model alias and the price period in effect at the attempt's UTC
  timestamp;
- [`CostBreakdown`](../../../src/TokenEconomy/CostBreakdown.cs) returns component
  costs, currency, list-price caveat, and explicit price status; and
- the [catalog](../../../src/TokenEconomy/catalog/model-prices.json) is
  append-only and includes the GPT-5.6/GPT-5.4 mini histories introduced in
  v0.3.1 and the GPT-5.5 histories added in v0.3.2.

The new functions should compose these types. They must not duplicate rates or
change `ComputeCost` semantics.

## Unit of analysis

A **delivery** is one task or requested artifact. A delivery has one or more
**attempts**. Exactly one terminal acceptance may be credited to a delivery.

An attempt is accepted only when it passes the declared external success gate
or receives an explicit human acceptance outcome. A response completed by the
model is not enough. Semantic failures, substantive C/D reviews,
infrastructure failures, cancellations, quota truncations, and missing delivery
paths remain distinct classifications.

Every attempt that consumed measurable tokens contributes to spend, regardless
of why it failed. Infrastructure failures do not count against model quality,
but their actual token and dollar cost still count against operational
efficiency.

## Required input

```csharp
public sealed record DeliveryAttemptEfficiencyInput
{
    public required string DeliveryId { get; init; }
    public required string AttemptId { get; init; }
    public required int AttemptOrdinal { get; init; }
    public required ModelId Model { get; init; }
    public required DateTime ExecutedAtUtc { get; init; }
    public TokenUsage? Usage { get; init; }
    public required DeliveryOutcome Outcome { get; init; }
    public required AttemptFailureClass FailureClass { get; init; }
}
```

The names can align with existing routing-evidence types during implementation.
The following semantics are required:

- `DeliveryId` groups retries of the same requested outcome.
- `AttemptId` is immutable and unique; exact replays deduplicate by this value.
- `AttemptOrdinal` starts at one and cannot skip or repeat within a delivery.
- `Model` records the actual route, not the card's configured route.
- `ExecutedAtUtc` is mandatory because price history is date-sensitive.
- `Usage = null` means unavailable. It is not four zero counts.
- Outcome and failure class preserve the existing difference between semantic
  quality and substrate failures.

If a model-based verifier or review is part of the delivery's cost boundary, it
must be recorded as its own attributed attempt/step and included explicitly.
The API must expose which roles were included; it must not silently mix core and
support calls.

## Token basis

For a measured attempt `i`, define billed tokens as:

```text
T_i = max(0, Input_i)
    + max(0, Output_i)
    + max(0, CacheRead_i)
    + max(0, CacheWrite_i)
```

This scalar is useful for throughput comparisons because the repository's
`TokenUsage` categories are mutually costed components. The result should also
retain the four component sums; two runs with the same scalar can have very
different prices.

Do not create “input-equivalent tokens” by applying price weights. That is a
cost metric and would become stale when prices change.

## Cost basis

For each measured attempt:

```csharp
var cost = catalog.ComputeCost(
    attempt.Model,
    attempt.Usage.Value,
    attempt.ExecutedAtUtc);
```

Define `C_i = cost.Total` only when `cost.Status == PriceStatus.Resolved`.
Aggregate cost is nullable if any included attempt has unavailable usage,
unknown model, no price for its date, or a different currency. Return known
partial cost and coverage as diagnostic fields, but never publish that partial
sum as the total.

An `Unconfirmed` component makes the aggregate unconfirmed. Every resolved
aggregate retains the “estimated - list prices” caveat. Subscription fees,
negotiated discounts, free credits, quota opportunity cost, human fallback, and
verification labor are outside this catalog estimate unless the caller supplies
them through a separately named cost source.

## Computable metrics

Let:

- `D` be the number of distinct started deliveries;
- `A` be the number of distinct accepted deliveries;
- `N` be the number of retained attempts;
- `R` be attempts with ordinal greater than one;
- `sum(T)` be tokens across all attempts, including failed retries; and
- `sum(C)` be dated list-price cost across all attempts.

### 1. Tokens per accepted outcome

```text
TokensPerAcceptedOutcome = sum(T) / A
```

Return null when `A == 0` or token coverage is incomplete. Also return component
tokens per accepted outcome and `TokenCoverage = measured attempts / N`.

This is the normative meaning of “tokens per outcome.” A companion
`TokensPerStartedDelivery = sum(T) / D` is useful for capacity planning when the
observation window contains unresolved deliveries.

### 2. Estimated list-price cost per accepted delivery

```text
CostPerAcceptedDelivery = sum(C) / A
```

Return null when `A == 0` or cost coverage is incomplete. This metric includes
the first attempt and every paid retry for both accepted and ultimately failed
deliveries in the cohort. It answers how much model spend the system incurred
for each accepted result it actually produced.

The result must carry currency, price coverage, unconfirmed status, and the
catalog list-price caveat.

### 3. Observed retry-adjusted cost

```text
InitialCostPerStartedDelivery = sum(C where AttemptOrdinal == 1) / D

RetryAdjustedCostPerStartedDelivery = sum(C) / D

RetryCostUplift =
    RetryAdjustedCostPerStartedDelivery / InitialCostPerStartedDelivery - 1
```

Return uplift as null if either aggregate is unavailable or the initial value is
zero. This uses `D`, not `A`, so it answers the operational question: how much
did retries add to the average task we admitted, including tasks that never
reached acceptance?

Also return:

```text
AttemptsPerStartedDelivery = N / D
RetriesPerStartedDelivery = count(R) / D
AttemptsPerAcceptedDelivery = N / A      // null when A == 0
SemanticRetryRate = deliveries with a semantic retry / D
```

Substrate retries contribute to spend and attempt counts but not to
`SemanticRetryRate`.

### 4. Expected retry-adjusted cost to acceptance

This forecast is separate from observed aggregates and must be evidence-gated.
For a planned sequence of at most `K` routes, let `p_k` be the qualified
acceptance probability and `c_k` the expected dated cost of attempt `k`.

```text
ExpectedAttemptCost =
    sum from k=1..K of [c_k * product from j=1..k-1 of (1 - p_j)]

ProbabilityAcceptedByK = 1 - product from k=1..K of (1 - p_k)
```

If a guaranteed fallback has separately supplied expected cost `F`, then:

```text
ExpectedCostToDeliveredOutcome =
    ExpectedAttemptCost
    + F * product from k=1..K of (1 - p_k)
```

For an unbounded stationary route with constant cost `c` and independent
acceptance probability `p > 0`, the familiar special case is `c / p`. The
library should not use that shortcut for escalating routes, correlated retries,
or capped attempts.

Return an unknown forecast when probability evidence is below the comparable-
cohort gate, when route/scaffold versions differ materially, or when any needed
cost is unresolved. Do not substitute a global leaderboard pass rate.

### 5. Outcome efficiency comparison

To compare two routes or recommendation policies, return absolute and relative
deltas only when both sides have compatible acceptance definitions and complete
coverage:

```text
TokenDeltaPerAcceptedOutcome = candidate - baseline
CostDeltaPerAcceptedDelivery = candidate - baseline
RetryAdjustedCostRatio = candidate / baseline
AcceptanceRateDelta = candidate acceptance rate - baseline acceptance rate
```

Do not choose a winner from cost alone. Present acceptance, severe false
positives, semantic reissues, tokens, cost, and duration together. A route below
the correctness floor is not on the efficiency frontier.

## Proposed result shape

```csharp
public sealed record OutcomeEfficiencyReport
{
    public required int DeliveriesStarted { get; init; }
    public required int DeliveriesAccepted { get; init; }
    public required int Attempts { get; init; }
    public required int RetryAttempts { get; init; }

    public long? TokensPerAcceptedOutcome { get; init; }
    public long? TokensPerStartedDelivery { get; init; }
    public decimal? CostPerAcceptedDelivery { get; init; }
    public decimal? InitialCostPerStartedDelivery { get; init; }
    public decimal? RetryAdjustedCostPerStartedDelivery { get; init; }
    public decimal? RetryCostUplift { get; init; }

    public required MetricCoverage TokenCoverage { get; init; }
    public required MetricCoverage CostCoverage { get; init; }
    public string? Currency { get; init; }
    public bool Unconfirmed { get; init; }
    public string? Caveat { get; init; }
    public required IReadOnlyList<string> UnknownReasons { get; init; }
}
```

Use `decimal` for cost and ratios derived from cost. Token component totals use
`long`; per-outcome values may use `decimal` if the API must retain fractional
averages. The final implementation should choose one rounding policy and return
unrounded values from the core.

Suggested pure functions:

```csharp
OutcomeEfficiencyReport ComputeObserved(
    IEnumerable<DeliveryAttemptEfficiencyInput> attempts,
    ModelPriceCatalog catalog,
    EfficiencyScope scope);

ExpectedOutcomeEfficiency Forecast(
    IReadOnlyList<PlannedAttempt> attempts,
    OptionalFallbackCost fallback);

OutcomeEfficiencyComparison Compare(
    OutcomeEfficiencyReport baseline,
    OutcomeEfficiencyReport candidate);
```

These functions perform no probes, launches, filesystem writes, or quota
mutation.

## Edge cases that tests must pin

- empty input, zero deliveries, and zero accepted deliveries;
- duplicate attempt IDs and non-contiguous ordinals;
- one delivery with several retries and only one credited acceptance;
- missing usage versus measured zero-token usage;
- negative token fields following the current clamp-to-zero cost behavior;
- alias resolution and typed `ModelId` overloads;
- price changes between attempts of the same delivery;
- unknown model, no price for date, and unconfirmed price;
- cache rate present versus documented input-rate fallback;
- mixed currency and partial cost coverage;
- infrastructure retry spend without a semantic quality penalty;
- model escalation across attempts;
- unresolved delivery at the end of the observation window;
- expected-cost forecast with `p = 0`, `p = 1`, capped attempts, and fallback;
  and
- deterministic replay with reordered input attempts.

## Publication rule

Every published efficiency number must identify the task/capability cohort,
acceptance definition, observation dates, attempt budget, included workflow
roles, sample size, token coverage, price coverage, catalog version, and whether
the figure is observed or forecast. Without those fields, show “insufficient
evidence,” not a number that looks comparable.
