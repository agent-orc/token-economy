# Token Economy

> **Token economics for LLM coding agents** — one tested source for model
> pricing (with history), run-cost computation, and token-efficiency model
> selection. .NET, dependency-free core.

[![NuGet](https://img.shields.io/nuget/v/TokenEconomy.svg?label=NuGet)](https://www.nuget.org/packages/TokenEconomy)
[![NuGet downloads](https://img.shields.io/nuget/dt/TokenEconomy.svg?label=downloads)](https://www.nuget.org/packages/TokenEconomy)
[![CI](https://github.com/agent-orc/token-economy/actions/workflows/ci.yml/badge.svg)](https://github.com/agent-orc/token-economy/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

```bash
dotnet add package TokenEconomy
```

Package page: [TokenEconomy](https://www.nuget.org/packages/TokenEconomy) ·
[GitHub releases](https://github.com/agent-orc/token-economy/releases)

An agent run bills for input, output, and cache tokens at a rate that changes
over time, per model. Getting that wrong is not a rounding error: a hard-coded
price silently costs the wrong amount for every historic run, and a missing
price that defaults to `0` reports a budget as healthy while it drains.
TokenEconomy keeps the prices — with their validity dates — and answers two
questions from that one source: *what did this run cost?* and *which model buys
the most for the tokens I have left?* Unknown is always returned as unknown.

**Docs & website:** <https://agent-orchestrator.dev/token-economy/> — a static
site built from [`website/`](website/) and deployed by
[`deploy-website.yml`](.github/workflows/deploy-website.yml) (see
[`website/DEPLOY.md`](website/DEPLOY.md)).

**Research plan:** [Forecast each task as a percentage of a five-hour cap](docs/concepts/cap-forecast-per-task.md)
defines the proposed measurement, uncertainty, repository boundaries, and
GO-blocked delivery slices. Its [visual explainers](website/cap-forecast/index.html)
are part of the static site. No forecast implementation is authorised yet.

**Delegation economy:** [the pattern guide](docs/concepts/delegation-economy.md)
explains how orchestrators assign bounded work to cheaper model tiers and when
to escalate. Prompts and task cards can include the
[standardized context block](contexts/delegation-economy.md) verbatim.
**Task-cutting guide:**
[Dynamic workflows as a task-cutting strategy](docs/analyses/dynamic-workflows-task-cutting.md)
compares one Claude workflow-sized card with small `dependsOn` cards using
Agent Studio token, review, retry, and gate evidence. It includes the current
Claude-only workflow constraint, Codex integration gap, TE-8 routing hook, and
a reusable AI-pattern candidate.

**Prompt-enrichment analysis:**
[Prompt enrichment before an agent run](docs/analyses/prompt-enrichment-preprocessing.md)
quantifies rule, embedding, classifier, and hybrid preprocessing against the
Agent Studio retry baseline. It specifies the auditable
`enrichment-report.json` contract, a Task Server integration boundary, a
selection rubric, and a standalone `ai-patterns` handoff.

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
- **Agent Studio ingest** — reads each card's durable `task.json` directly and
  upserts model-run metrics by task key + run. It maps model/provider, usage,
  price-at-run-time, final-lane outcome signal, timestamps, project, task type,
  CLI and thinking level; `ModelRunViews` provides daily per-model and project
  consumption/outcome views. The filesystem contract is intentionally used over
  the task-server API so reporting jobs do not require a running server.

- **Provider quota dashboard** — derives trailing tokens/hour, capability-tier
  share, quota-mark projection, and ok/warning/critical state from imported
  runs. See the [rendered dashboard view](website/provider-quota-dashboard/index.html).

- **Controlled A/B benchmarks** — versioned repository definitions execute the
  same task against model/effort variants in isolated workspaces, retain raw
  append-only measurements, and derive deterministic comparison reports. See
  [the benchmark guide](docs/benchmarks.md).
- **Upfront task complexity** — card/repository signals plus measured historical
  neighbours produce a versioned routing score, confidence, token/reissue
  forecast, and audit evidence. A host-supplied mini-model rubric is optional.
  See the [design and backtest contract](docs/concepts/upfront-task-complexity.md).

- **Document-to-text benchmark** — a curated PDF/Word/spreadsheet/presentation
  hard-case corpus runs across every catalog model and derives evidence-linked,
  per-document-type capability records without turning failures into
  unsupported claims. See [the benchmark guide](docs/benchmarks.md#document-to-text-capability-benchmark).

- **Native media capability catalog** — evidence-dated image, video, music,
  speech, and dictation rows for Codex, Antigravity, and Claude Code are pulled
  from the same embedded catalog convention as pricing. Includes the retained
  N=4 Codex image benchmark and explicit unknown/unverified cost factors. See
  [the capability matrix](docs/media-capabilities.md).

- **Model trust ledger** — records model capability assertions separately from
  durable observed-run, benchmark, or audit evidence. Trust is derived from
  independently verifiable successful artifacts; self-reported claims never
  raise it, and open high-severity incidents restrict the model.

The trust ledger also keeps an explicit observed-run denominator, violations,
and source references, so a per-model/CLI violation rate is `null` rather than
a misleading `0%` when no denominator is retained. See [historical evidence
and rate limits](docs/model-trust-evidence.md).

### Future orchestrator sampling (concept only)

An orchestrator may later use the derived trust level to choose *sampling
frequency*: unverified or provisional model/CLI pairs receive denser audit
sampling, while verified pairs may be sampled less often; any open material
incident restores dense sampling. This is only a measurement concept. It must
not change model selection or override the routing-policy correctness floors,
and a small or missing denominator must never be treated as evidence of safety.

## Install

```bash
dotnet add package TokenEconomy --version 0.2.0
```

Dependency-free, targets `net10.0`. The API is pre-1.0 and may still shift —
pin a version and watch releases.

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
// e.g. claude-sonnet-5 @ Medium — claude-sonnet-5: balanced tier, an ideal
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
token-efficiency matrix + `SuggestModel`. **`TokenEconomy` 0.2.0 is published
on [nuget.org](https://www.nuget.org/packages/TokenEconomy/0.2.0)**; release
operations and the one-time setup fallback are documented in
[docs/PUBLISHING.md](docs/PUBLISHING.md), with the verified TE-1 operator
handoff retained in
[results/TE-1-nuget-first-publish.md](results/TE-1-nuget-first-publish.md).

## Repository layout

| Path | What it holds |
| --- | --- |
| `src/TokenEconomy/` | The published library. `catalog/` holds the embedded price and media-capability JSON. |
| `src/TokenEconomy.Benchmarks/` | CLI that executes the A/B and document-to-text benchmark runs. |
| `tests/TokenEconomy.Tests/` | xUnit suite; also the guard that the website data cannot drift from the library. |
| `benchmarks/` | Benchmark setups, fixtures and corpora, plus append-only raw results under `benchmarks/results/`. |
| `docs/` | Concepts, benchmark guide, publishing and repository metadata. |
| `contexts/` | Short, reusable policy blocks for agent and task-card prompts. |
| `results/` | Retained operator and backtest records. |
| `scripts/` | Release, pack, and website-data generation. |
| `tools/` | The complexity-backtest report generator. |
| `website/` | The repository's own static site. |

**`website/` is a real part of this repository, not a mirror.** It is the
public documentation surface at
<https://agent-orchestrator.dev/token-economy/> — plain static HTML with a
checked data step: `scripts/generate-website-data.py` regenerates
`website/data/*.json` from the committed evidence, and CI rejects stale data,
so the published charts and benchmark tables cannot drift from the library.
Preview it locally on <http://localhost:4340> with:

```bash
python -m http.server 4340 --directory website
```

See [`website/README.md`](website/README.md) for the content rules and
[`website/DEPLOY.md`](website/DEPLOY.md) for deployment.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for build and test steps and the
project's invariants. Issues and pull requests are welcome. Report security
issues through the private process in [SECURITY.md](SECURITY.md).

## Agent Orchestrator ecosystem

TokenEconomy is the token-economics layer of the Agent Orchestrator family. It
answers what a run costs and which model to spend the next tokens on;
[Agent Studio](https://github.com/agent-orc/agent-studio) is the orchestrator
that turns tasks into agent runs and is the source of the run metrics imported
here; [CodingAgentRunner](https://github.com/agent-orc/runner) is the process
and protocol layer that actually launches the coding-agent CLIs and reports the
token usage this library prices; and
[Agent Chat](https://github.com/agent-orc/chat) is the conversation UI for
those runs. See the other projects on the
[Agent Orchestrator website](https://agent-orchestrator.dev/) and in the
[agent-orc GitHub organization](https://github.com/agent-orc).

## License

[Apache-2.0](LICENSE) © 2026 Robert Mischke. See [NOTICE](NOTICE).
