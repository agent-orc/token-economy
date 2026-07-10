# Changelog

All notable changes to TokenEconomy are recorded here (Keep a Changelog,
SemVer; pre-1.0 the public API may still shift).

## [Unreleased]

- Token-efficiency matrix + `SuggestModel(taskClass, budgetPressure, available)`
  follows in TE-2.

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
