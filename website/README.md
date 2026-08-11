# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML with a checked data step.** `index.html` has inline CSS,
  inline JavaScript, and a data-URI favicon. `scripts/generate-website-data.py` writes three
  artifacts — `website/data/benchmarks.json` (published studies) and
  `website/data/token-usage.json` (the chart aggregates), plus
  `website/data/model-efficiency-matrix.json` (the checked
  `ModelEfficiencyMatrix.Describe` projection) — and CI rejects stale data.
- **English**, light/dark theme-aware, responsive.
- Content: what/why, a rendered token-efficiency matrix and `SuggestModel`
  example, install, the dated cost API, a complexity-estimation explainer,
  token-usage charts, published benchmark summaries, and family links. The
  generated sections describe the real `TokenEconomy` API and checked-in
  evidence.

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
website`; the generated-data sections intentionally show a visible fallback
under `file://`.

The provider availability page is a deterministic contract example rather
than live telemetry. It mirrors `ProviderAvailabilitySnapshot`: provider/CLI
probe state, independently named observed quota windows, explicitly inferred
projections, freshness, warning state, and decision-time cost coverage. It
must not imply that the library selects a route.
