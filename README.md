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

## Status

Extraction from `CodingAgentRunner.Pricing` (v0.5.0) into this standalone
package is in progress. Package id: **`TokenEconomy`** (nuget.org).

## License

Apache-2.0 — see [LICENSE](LICENSE).
