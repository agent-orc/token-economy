# Contributing to TokenEconomy

TokenEconomy is pre-1.0, so its public surface can still change. Issues, ideas,
and pull requests are welcome.

## Build & test

Install the .NET 10 SDK, clone the repository, and run these commands from the
repository root:

```bash
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release
```

CI runs the same build and test steps on Windows and Linux
([`ci.yml`](.github/workflows/ci.yml)). The build treats warnings as errors
(`TreatWarningsAsErrors`), so a warning fails the build locally too.

The website data step needs Python and is only relevant when you touch
benchmark results, the backtest snapshot, or the catalog:

```bash
python scripts/generate-website-data.py
```

`WebsiteTokenUsageDataTests` re-costs the generated artifact through
`ModelPriceCatalog.ComputeCost`, so stale committed data fails the test suite
rather than silently drifting from the library.

## Conventions

- C# with nullable reference types enabled; `LangVersion` `latest`;
  target `net10.0`.
- **The core stays dependency-free.** Pricing and selection must not acquire a
  package reference. Reporting and import helpers may read the filesystem, but
  the cost and selection paths stay pure functions over data.
- **No silent zeros.** An unknown or unpriced model must surface an explicit
  status with a `null` total — never a fabricated `0`. Tests pin this; do not
  relax them.
- **Cost class is derived, never restated.** Anything that needs a price band
  derives it from the pricing catalog so it tracks price history. Do not
  hard-code a band next to a price.
- **Prices have history and are append-only.** Add a new `ModelPrice` entry with
  a new `ValidFrom` instead of editing an existing entry, so historic runs stay
  costed at the rate that was valid then.
- **Unconfirmed numbers are flagged, not invented.** A model with no published
  rate stays unpriced or `Unconfirmed`.
- **Evidence is append-only.** Benchmark and trust-ledger raw measurements are
  retained as written; derived reports are regenerated from them.
- Documentation and code are English, including commit messages.
- Conventional-commit style messages are appreciated.

Deeper background: the [benchmark guide](docs/benchmarks.md), the
[trust-evidence notes](docs/model-trust-evidence.md), and the concept documents
under [`docs/concepts/`](docs/concepts/). Release operations live in
[docs/PUBLISHING.md](docs/PUBLISHING.md).

## Scope

TokenEconomy is the *knowledge* layer for token economics: what a model costs,
what it costs at a given point in time, and which model buys the most for a
given token budget. It deliberately does **not** contain admission policy — the
decision of when to downshift, throttle, or wait belongs to the orchestrator
calling it. Keep data and pure functions here; keep policy in the caller.

## Agent-driven maintenance

The agent-orc organization uses agent-driven pipelines, so most changes in this
repository land without a conventional human-authored pull request — quality is
enforced at the pipeline level (build, tests, and review stages) rather than
through PR review. Human issues and pull requests are still welcome and are
reviewed against the same tests, scope, and project conventions. Do not be
surprised by commits attributed to a runner identity, or by a fast-moving
`main`; rebase before submitting.

## Pull requests

- Keep changes focused and explain the behavior or problem they address.
- Add or update tests when behavior changes.
- Update [CHANGELOG.md](CHANGELOG.md) under `## [Unreleased]` for user-visible
  changes.
- Update user-facing documentation when the public API or setup changes.
- Do not remove an explicit-unknown guard without reading its rationale and
  tests.

By participating, you agree to follow the
[Code of Conduct](CODE_OF_CONDUCT.md). Report security issues through the
private process in [SECURITY.md](SECURITY.md), not through a public issue.

## License

By contributing you agree that your contributions are licensed under the
[Apache-2.0](LICENSE) license.
