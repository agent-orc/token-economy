# Token Economy

**Token economics for LLM coding agents** — one tested source for model
pricing (with history), run-cost computation, and token-efficiency model
selection. .NET, dependency-free core.

Part of the coding-agent family
([coding-agent-runner](https://github.com/RobertMischke/coding-agent-runner),
[coding-agent-chat](https://github.com/RobertMischke/coding-agent-chat)).

**Docs & website:** <https://agent-orchestrator.dev/token-economy/> — a static
site built from [`website/`](website/) and deployed by
[`deploy-website.yml`](.github/workflows/deploy-website.yml) (see
[`website/DEPLOY.md`](website/DEPLOY.md)).

**Research plan:** [Forecast each task as a percentage of a five-hour cap](docs/concepts/cap-forecast-per-task.md)
defines the proposed measurement, uncertainty, repository boundaries, and
GO-blocked delivery slices. Its [visual explainers](website/cap-forecast/index.html)
are part of the static site. No forecast implementation is authorised yet.

## What it does

- **Pricing catalog with history** — per-model price entries keyed by
  `ValidFrom`; a run's cost is computed with the prices valid *at the run's
  timestamp*. Historic entries are kept, never overwritten.
- **Cost API** — `ComputeCost(model, usage, atUtc)` → deterministic
  breakdown + total; unknown models return an explicit unknown, never a
  silent zero.
- **Token-efficiency matrix** — `SuggestModel(taskClass, budgetPressure,
  availableClis, atUtc)`: which model buys the most for these tokens, ranked
  with a rationale string for audit trails. Cost class is *derived* from the
  pricing catalog, never restated.

## Install

```
dotnet add package TokenEconomy
```

Dependency-free, targets `net10.0`.

## Usage

```csharp
using TokenEconomy;

// The seeded catalog: known Claude 4.x/5 and OpenAI gpt-5.x models.
var breakdown = ModelPriceCatalog.Default.ComputeCost(
    "claude-opus-4-8",
    new TokenUsage(Input: 250_000, Output: 12_000, CacheRead: 40_000),
    DateTime.UtcNow);

if (breakdown.HasPrice)
    Console.WriteLine($"{breakdown.Total} {breakdown.Currency}");   // ≈ 1.57 USD
else
    Console.WriteLine(breakdown.Status);   // UnknownModel or NoPriceForDate — never a silent $0
```

`Total` is `null` for an unknown or unpriced model, never `0` — a missing price
is always explicit. Prices carry history, so a run at an earlier timestamp is
costed with the rate that was valid then.

### Picking a model for a task under budget pressure

```csharp
using TokenEconomy;

// Which model should run a plain feature when the budget is getting tight and
// only the Claude CLI has quota right now?
var ranked = ModelEfficiencyMatrix.Default.SuggestModel(
    TaskClass.Feature,
    BudgetPressure.Tight,
    availableClis: [Cli.Claude],
    atUtc: DateTime.UtcNow);

var best = ranked[0];   // empty list ⇒ nothing available; wait, don't launch
Console.WriteLine($"{best.ModelId} @ {best.SuggestedEffort} — {best.Rationale}");
// e.g. claude-sonnet-5 @ medium — claude-sonnet-5: balanced tier, an ideal
// match for feature work; standard cost — moderate spend under tight pressure.
// Suggested effort: medium.
```

Capability fit leads the ranking; budget pressure tips the balance toward
cheaper models (a downshift). Cost class is *derived* from the pricing catalog,
so it never restates a price and tracks price history over time. The matrix is
data + pure functions — the *policy* of when to downshift / throttle / wait
stays in the caller's admission algorithm.

## Status

The pricing catalog + cost API were extracted from `CodingAgentRunner.Pricing`
(v0.5.0) into this standalone package (0.1.0); **0.2.0** adds the
token-efficiency matrix + `SuggestModel`. Tag-ready; the first nuget.org publish
is pending — see [docs/PUBLISHING.md](docs/PUBLISHING.md). Package id:
**`TokenEconomy`** (nuget.org).

## License

Apache-2.0 — see [LICENSE](LICENSE).
