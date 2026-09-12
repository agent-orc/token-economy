# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML with a checked data step.** Content-specific CSS and
  JavaScript remain inline, the favicon is a data URI, and every page loads the
  local `navigation.css` and `navigation.js` assets. `scripts/generate-website-data.py` writes five
  artifacts — `website/data/benchmarks.json` (published studies) and
  `website/data/token-usage.json` (the chart aggregates), plus
  `website/data/model-efficiency-matrix.json` (the checked
  `ModelEfficiencyMatrix.Describe` projection), plus
  `website/data/task-class-recommendations.json` (the checked public projection
  of the library's study-backed card-creation advice) — and CI rejects stale data.
  The fifth artifact, `website/data/model-benchmark-matrix.json`, joins the
  versioned external benchmark catalog to dated prices for the
  `model-benchmarks/` page and its candidate recommendations.
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
The canonical copied header is
[`_includes/site-navigation.html`](_includes/site-navigation.html). Copy it
unchanged into every HTML page between its marker comments; the website-data
`--check` command rejects missing or drifting copies. Shared interaction and
theme styles live in the two local navigation assets. The separate
[`scripts/check-website-links.py`](../scripts/check-website-links.py) check
validates local files and fragments.

Editing: change the relevant HTML page and push to `main`; CI deploys the whole
directory recursively (see [`DEPLOY.md`](DEPLOY.md)). The deployed folder stays
self-contained and makes no third-party asset requests. Because production is
mounted at `/token-economy/`, preview the same path locally:

```bash
preview_root="$(mktemp -d)"
ln -s "$PWD/website" "$preview_root/token-economy"
python3 -m http.server 8080 --directory "$preview_root"
# → http://localhost:8080/token-economy/
```

Generated-data sections intentionally show a visible fallback under `file://`.

The provider availability page is a deterministic contract example rather
than live telemetry. It mirrors `ProviderAvailabilitySnapshot`: provider/CLI
probe state, independently named observed quota windows, explicitly inferred
projections, freshness, warning state, and decision-time cost coverage. It
must not imply that the library selects a route.
