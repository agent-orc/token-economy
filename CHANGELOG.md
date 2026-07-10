# Changelog

All notable changes to TokenEconomy are recorded here (Keep a Changelog,
SemVer; pre-1.0 the public API may still shift).

## [Unreleased]

## [0.2.0] - 2026-07-10

### Added

- **Token-efficiency matrix + `SuggestModel` API** — the Selection axis of
  token-budget load management ("was kriege ich für meine Tokens?"), beside the
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
    Lastverteilung view. Capability fit leads; budget pressure tips the balance
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

## [0.1.0] - 2026-07-10

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
