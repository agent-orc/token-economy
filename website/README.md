# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML with a checked data step.** `index.html` has inline CSS
  and a data-URI favicon. `scripts/generate-website-data.py` copies published
  benchmark JSON into `website/data/benchmarks.json`; CI rejects stale data.
- **English**, light/dark theme-aware, responsive.
- Content: what/why, a complexity-estimation and closed learning-loop explainer,
  published benchmark summaries rendered from the append-only JSON evidence in
  `benchmarks/results/`, an honest implementation/plan status snapshot, the pricing-history explainer, the cost API, a
  `SuggestModel` preview, install, and family links — all describing the real
  `TokenEconomy` API — plus the clearly labelled, plan-only cap-forecast
explainers under [`cap-forecast/`](cap-forecast/index.html).

When adding a benchmark result, add its setup or corpus, fixture, raw JSON, and
derived report or capability record first, then run `python
scripts/generate-website-data.py`. The browser renders the resulting table; do
not add result rows to `index.html` by hand.
Update `website/data/site-status.json` for an honest status change.

Editing: change the relevant HTML page and push to `main`; CI deploys the whole
directory recursively (see [`DEPLOY.md`](DEPLOY.md)). Every page remains
self-contained. Preview locally with `python3 -m http.server --directory
website` or by opening a page directly.
