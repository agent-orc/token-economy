# Token Economy

**Token economics for LLM coding agents** — one tested source for model
pricing (with history), run-cost computation, and token-efficiency model
selection. .NET, dependency-free core.

Part of the coding-agent family
([coding-agent-runner](https://github.com/RobertMischke/coding-agent-runner),
[coding-agent-chat](https://github.com/RobertMischke/coding-agent-chat)).

## What it does

- **Pricing catalog with history** — per-model price entries keyed by
  `ValidFrom`; a run's cost is computed with the prices valid *at the run's
  timestamp*. Historic entries are kept, never overwritten.
- **Cost API** — `ComputeCost(model, usage, atUtc)` → deterministic
  breakdown + total; unknown models return an explicit unknown, never a
  silent zero.
- **Token-efficiency matrix** *(in progress)* — `SuggestModel(taskClass,
  budgetPressure, available)`: which model buys the most for these tokens,
  with a rationale string for audit trails.

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

## Status

The pricing catalog + cost API have been extracted from
`CodingAgentRunner.Pricing` (v0.5.0) into this standalone package as **0.1.0**
(tag-ready; the first nuget.org publish is pending — see
[docs/PUBLISHING.md](docs/PUBLISHING.md)). Package id: **`TokenEconomy`**
(nuget.org). The token-efficiency matrix + `SuggestModel` follow in TE-2.

## License

Apache-2.0 — see [LICENSE](LICENSE).
