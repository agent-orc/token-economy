# Provider availability snapshot

`ProviderQuotaDashboardBuilder.BuildSnapshot` produces immutable routing
evidence for one decision instant. It does not select, rank, downgrade, or
launch a model. A caller applies the authoritative
[model routing policy](../system/domains/model-routing-policy.md) after it has
established the correctness floor; quota and cost never lower that floor.

## Contract

A `ProviderAvailabilitySnapshot` records `DecisionAtUtc`, the rate lookback,
the maximum acceptable observation age, and one row for each explicitly
configured provider/CLI pair. Every row contains:

- the observed CLI availability and its observation timestamp;
- a conservative aggregate freshness and warning state;
- catalog status for every named model, resolved at `DecisionAtUtc`;
- zero or more named quota windows, each retained as an independent limit;
- trailing measured run tokens and tokens/hour; and
- capability-tier shares for measured trailing runs.

Each `ProviderQuotaWindowSnapshot` keeps the provider's `WindowId`, optional
start, reset, freshness, warning state, and two deliberately separate value
types:

| Value | Origin | Meaning |
| --- | --- | --- |
| `ObservedQuotaUsage` | Always `Observed` | Provider-reported used tokens, limit, headroom, percentage, and observation time. Null means missing, never zero. |
| `InferredQuotaProjection` | Always `Inferred` | Exhaustion time calculated from observed headroom and the imported-run rate over the declared trailing window. It says whether exhaustion precedes reset. |

Imported task runs never manufacture observed quota. They can produce a rate
and projection only when token telemetry is measured, the matching provider and
CLI are retained, the run execution time falls inside the lookback, and the
quota observation is fresh and internally usable. A later import/update time
does not make an old execution look recent.
An absent or zero recent rate yields no projection.

## Conservative states

`Healthy` is possible only when the provider/CLI probe is explicitly
`Available` and fresh, at least one quota window is present and fresh, every
window is below the warning threshold, and all named models have confirmed
catalog prices at the decision time.

- An exhausted or critically used window is `Critical`.
- An explicitly unavailable CLI is `Critical` even when quota headroom exists.
- A near-cap fresh window is `Warning`.
- Stale, missing, future-dated or otherwise suspicious observations are
  `Unknown`, never `Healthy`.
- Unknown models, models without a price for the decision date, unconfirmed
  prices, missing model ids, and missing quota windows make the provider state
  `Unknown`. Their cost is nullable/labelled, never displayed as zero.

Individual quota windows are not merged. A five-hour cap and a weekly cap for
the same provider/CLI remain two `ProviderQuotaWindowSnapshot` values with
their own usage, limit, reset, freshness, warning, and projection.

## Example

```csharp
var decisionAt = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
var options = new ProviderAvailabilitySnapshotOptions(
    decisionAt,
    TrailingWindow: TimeSpan.FromHours(1),
    MaximumObservationAge: TimeSpan.FromMinutes(15),
    Providers:
    [
        new("anthropic", "claude", ProviderCliAvailability.Available,
            decisionAt.AddMinutes(-2), ["claude-sonnet-5"]),
    ],
    QuotaWindows:
    [
        new("anthropic", "claude", "five-hour", 800, 1_000,
            decisionAt.AddMinutes(-2), decisionAt.AddHours(3)),
        new("anthropic", "claude", "weekly", 2_000, 10_000,
            decisionAt.AddMinutes(-2), decisionAt.AddDays(5)),
    ]);

ProviderAvailabilitySnapshot snapshot =
    new ProviderQuotaDashboardBuilder().BuildSnapshot(importedRuns, options);
string html = ProviderQuotaDashboardHtmlRenderer.RenderSnapshot(snapshot);
```

`ProviderQuotaDashboardBuilder.Build` and
`ProviderQuotaDashboardHtmlRenderer.Render` remain available for the legacy
historical utilization view. That older quota-mark view is descriptive and
must not be used as routing-grade availability evidence.

## Imported run views

`ModelRunViews` groups by day, provider, CLI, model, and project. Its
`RunCostStatusSummary` retains resolved, unconfirmed, unknown-model,
no-price-for-date, and usage-unavailable counts. `CostEstimate` is null if any
run in the group is unresolved, so aggregate reports cannot silently turn
partial cost coverage into a complete total.
