# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML with a checked data step.** `index.html` has inline CSS
  and a data-URI favicon. `scripts/generate-website-data.py` writes three
  artifacts: `website/data/benchmarks.json` (published studies),
  `website/data/token-usage.json` (the chart aggregates), and
  `website/data/model-efficiency-matrix.json` (the checked public projection of
  `ModelEfficiencyMatrix.Describe()`). CI rejects stale data for all three.
- **English**, light/dark theme-aware, responsive.
- Content: what/why, model selection and its full efficiency matrix, install and
  cost APIs, a complexity-estimation and closed learning-loop explainer,
  token-usage charts and published benchmark summaries rendered from the append-only JSON evidence in
  `benchmarks/results/`, and family links — all describing the real
  `TokenEconomy` API.

The token-usage charts (per model, per task class, per measured reissue count,
and one measured session over time) read `token-usage.json` only. That file is
derived from the capability run under `benchmarks/results/`, the card backtest
snapshot in `results/complexity-backtest/`, the session table in
`docs/analyses/long-vs-short-session-cost.md`, and list prices resolved from
`src/TokenEconomy/catalog/model-prices.json` at each run's own timestamp.
`WebsiteTokenUsageDataTests` re-costs the committed file through
`ModelPriceCatalog.ComputeCost`, so the generator's arithmetic cannot drift from
the library, and a model without a published rate stays explicitly unpriced —
never a silent $0.

When adding a benchmark result, add its setup or corpus, fixture, raw JSON, and
derived report or capability record first, then run `python
scripts/generate-website-data.py`. The browser renders the resulting table; do
not add result rows to `index.html` by hand. Follow the
[end-to-end benchmark methodology](../benchmarks/README.md) for fixture and
oracle requirements, execution, immutable artifacts, statistics, and the full
publication checklist.
Editing: change the relevant HTML page and push to `main`; CI deploys the whole
directory recursively (see [`DEPLOY.md`](DEPLOY.md)). Every page remains
self-contained. Preview locally with `python -m http.server --directory
website`; generated data cannot be fetched through `file://`.

The provider availability page is a deterministic contract example rather
than live telemetry. It mirrors `ProviderAvailabilitySnapshot`: provider/CLI
probe state, independently named observed quota windows, explicitly inferred
projections, freshness, warning state, and decision-time cost coverage. It
must not imply that the library selects a route.
