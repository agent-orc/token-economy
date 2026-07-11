# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML, no build step.** `index.html` is self-contained: inline
  CSS, a data-URI favicon, and no external requests, so it works offline and
  under the `/token-economy/` subpath without any asset-path juggling.
- **English**, light/dark theme-aware, responsive.
- Content: what/why, the pricing-history explainer, the cost API, a
  `SuggestModel` preview, install, and family links — all describing the real
  `TokenEconomy` API — plus the clearly labelled, plan-only cap-forecast
  explainers under [`cap-forecast/`](cap-forecast/index.html).

Editing: change the relevant HTML page and push to `main`; CI deploys the whole
directory recursively (see [`DEPLOY.md`](DEPLOY.md)). Every page remains
self-contained. Preview locally with `python3 -m http.server --directory
website` or by opening a page directly.
