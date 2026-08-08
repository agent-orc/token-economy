# Changelog

All notable changes to TokenEconomy are recorded here (Keep a Changelog,
SemVer; pre-1.0 the public API may still shift).

## [Unreleased]

### Added

- A versioned Quality Studio review-evidence stream for the `review` task
  class: strict drop schema, append-only per-run importer, model/thinking/CLI/
  aspect cohorts, confirmed-versus-dismissed finding metrics, provenance, and
  conservative observational confidence gates. The generated routing knowledge
  and efficiency matrix expose per-model review quality and evidence strength;
  fixtures and sparse data remain `insufficientEvidence` and cannot produce a
  `SuggestModel` candidate.

- An Agent Studio task-admission adapter that persists intake estimates, routes
  before every attempt with run-scoped quota evidence, consumes the newest
  classified reissue outcome, and records a complete immutable launch/wait
  decision without rewriting card configuration. The expanded task-storage
  import contract and accessible operator fragment retain recommendation,
  selection, score, floor, source, policy/provisional state, quota fallback,
  pin warning, and wait reason. An end-to-end fixture covers initial routing,
  equivalent-provider quota fallback, semantic promotion, terminal ingestion,
  and deterministic replay.

- Durable Agent Studio routing decisions, append-only attempt observations, and
  versioned outcome classifications joined by stable IDs. Imports and model-run
  views now retain actual model/thinking level, policy version, tokens,
  duration, cost status, review result, and reissue reason. Semantic and
  substantive C/D outcomes update evidence and promote the next core attempt;
  infrastructure, stale-base, broken-host, cancellation, quota, and delivery
  failures do not. Replay tests protect historical decisions while regenerated
  schema-version-2 evidence incorporates later outcomes.

- A public deterministic `ModelRouter.Route` API that composes the existing
  complexity worksheet, versioned routing policy and knowledge, efficiency
  matrix suggestions, benchmark qualification, trust evidence, workflow
  capabilities, available CLIs, provider quota/budget state, and operator pins.
  Results retain recommended and selected routes, the score worksheet and
  correctness floor, all evidence versions, source/reason, pin warnings, and
  uncertainty. Equivalent-provider fallback precedes the tightly bounded
  one-tier quota downgrade; no capacity or cost state can cross a hard or
  semantic-reissue floor.

- An auditable upfront routing worksheet in `TaskComplexityEstimator` aligned
  with the canonical weighted criteria, score boundaries, and hard floors. It
  preserves token/duration/reissue point forecasts, adds evidence-backed ranges
  and missing-history coverage, imports expected (never eventual) scope, and
  supports leakage-safe explicit held-out backtests.
- A routing-grade provider availability snapshot alongside the retained
  historical quota dashboard. It reports provider/CLI availability, separate
  named quota windows with observed usage/headroom and reset, freshness and
  conservative warning states, decision-time price coverage, and explicitly
  inferred rate projections. Imported run views now retain CLI identity and
  unresolved cost counts; stale, missing, suspicious, unavailable, unknown,
  and unpriced evidence cannot appear healthy or as zero cost. The contract
  intentionally does not select a model.

- A deterministic routing-evidence pipeline that canonicalizes model aliases,
  keeps controlled benchmarks separate from Agent Studio observations, retains
  attempt-level mixed routes and explicit unknowns, and emits versioned cohorts
  with quality/reissue/resource coverage, provenance, observation dates, and
  confidence-gated qualifications. Observational cohorts never claim controlled
  validation, and source task files plus benchmark raw results stay append-only.

- **`claude-opus-5` onboarded** into the seeded pricing catalog and the
  efficiency matrix (TE-19). Priced at the confirmed Opus rate card
  ($5.00 / $25.00 per MTok, cache-read $0.50, 5-minute cache-write $6.25),
  profiled `Frontier` with the same `Low`/`Medium`/`High` effort ladder as the
  rest of the Opus family, and declared first in that tier so it is now the
  strongest coding default for heavy design. Consumers that read
  `ModelPriceCatalog.Default` / `ModelEfficiencyMatrix.Default` pick it up with
  no change of their own; cost class stays derived from the catalog.
- Token-usage charts on the website, rendered from a new generated
  `website/data/token-usage.json`: tokens per model, per document class, per card
  task class, per measured reissue count, and one measured session over time.
  Dollar figures are list prices resolved from the dated catalog at each run's own
  timestamp, and `WebsiteTokenUsageDataTests` re-costs the committed artifact
  through `ModelPriceCatalog.ComputeCost` so the site cannot drift from the
  library (TE-20).
- The embedded, schema-versioned native media capability catalog for Codex,
  Antigravity, and Claude Code, with exact-model then CLI-host pull lookup.
- The English media capability matrix, dated official evidence, and retained
  Codex image-generation benchmark (N=4). The reported 3–5× Codex cost factor is
  preserved as an unverified claim because the image tool exposed no comparable
  token or credit meter.
- An all-model document-to-text benchmark vertical with a versioned PDF,
  Word/RTF, SpreadsheetML and flat-ODF presentation hard-case corpus,
  deterministic visible/hidden-content oracles, append-only raw extraction
  evidence, and conservative per-model/per-document-type capability records.

## [0.2.0] - 2026-07-21

First release published to nuget.org.

### Added

- **Token-efficiency matrix + `SuggestModel` API** — the Selection axis of
  token-budget load management ("what do I get for my tokens?"), beside the
  pricing catalog and built on it (TE-2).
  - `ModelEfficiencyMatrix.Default` profiles **every** model in
    `ModelPriceCatalog.Default`: capability tier (`Frontier` / `Balanced` /
    `Light`), supported reasoning-effort levels, and a routing `CapabilityTier`.
    Ids, aliases and vendor all come from the catalog, so nothing is duplicated.
  - **Cost class is derived, not restated.** `CostClassOf(model, atUtc)` costs a
    fixed reference workload through the pricing catalog and buckets the result
    into `Economy` / `Standard` / `Premium`; an unpriced model is `Unknown`,
    never a guessed band. Because the derivation goes through price *history*,
    cost class tracks price changes over time.
  - **`SuggestModel(taskClass, budgetPressure, availableClis, atUtc)`** →
    candidates ranked best-first, each with a `Score`, a suggested effort, and a
    one-line English `Rationale` for the orchestrator's decision event and the
    load-distribution view. Capability fit leads; budget pressure tips the balance
    toward cheaper models (a downshift) without ever letting an underpowered
    model outrank a capable one. Only models whose CLI is in `availableClis` are
    ranked — a dry/absent CLI drops out with no launch attempt — and restricted
    (Glasswing-only) and deprecated models are never suggested. An empty result
    means "wait", never a bad launch.
  - `Describe(atUtc)` renders the matrix as inspectable `ModelEfficiencyRow`s
    (tier, cost class, effort levels, suitability for every task class).
  - Pure functions in `EfficiencyPolicy` (suitability grid, cost buckets,
    suggested effort) — the *knowledge*; the *policy* of when to downshift /
    throttle / wait stays in the admission algorithm, by design.

## 0.1.0 - 2026-07-10

The extraction milestone. Never published to nuget.org — 0.2.0 was the first
public release.

### Added

- **Pricing catalog + cost API**, extracted from `CodingAgentRunner.Pricing`
  (coding-agent-runner 0.5.0) into this standalone package under the
  `TokenEconomy` namespace (TE-1).
  - `ModelPriceCatalog` with `ResolvePrice(model, atUtc)`,
    `ComputeCost(model, usage, atUtc)`, `Find(model)`, and a `Listings` "list
    endpoint". `ModelPriceCatalog.Default` is the seeded catalog; you can also
    build one from your own `ModelListing` set.
  - **Prices have history.** Each model carries a list of `ModelPrice` entries
    keyed by `ValidFrom` (inclusive, UTC); a run's cost is computed with the
    price valid *at the run's timestamp*, so historic entries are kept, not
    overwritten — e.g. Claude Sonnet 5 seeds its introductory rate now and its
    standard rate from 2026-09-01.
  - **Unknown and unpriced models are explicit, never a silent `$0`.** An
    unknown id resolves to `PriceStatus.UnknownModel`; a known-but-unpriced
    model to `PriceStatus.NoPriceForDate`. In both cases `CostBreakdown.Total`
    is `null`.
  - `TokenUsage(Input, Output, CacheRead, CacheWrite)` input and a
    per-component `CostBreakdown` (`InputCost` / `OutputCost` / `CacheReadCost`
    / `CacheWriteCost` + nullable `Total`). Model ids and aliases resolve case-
    and dot/dash-insensitively (`claude-opus-4.8`, `gpt-5-6`, dated snapshots).
- **Seed data** for the Claude 4.x/5 families (confirmed input/output rates,
  with Anthropic's documented cache multipliers: cache-read 0.1x input,
  5-minute-TTL cache-write 1.25x input) and the OpenAI gpt-5.x families (known
  models with no published rate yet). Unconfirmed numbers are flagged
  `Unconfirmed` or left unpriced rather than invented.
- Dependency-free core targeting `net10.0`; ships XML docs and a symbol package.

[Unreleased]: https://github.com/agent-orc/token-economy/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/agent-orc/token-economy/releases/tag/v0.2.0
