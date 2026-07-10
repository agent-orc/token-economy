# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML, no build step.** `index.html` is self-contained: inline
  CSS, a data-URI favicon, and no external requests, so it works offline and
  under the `/token-economy/` subpath without any asset-path juggling.
- **English**, light/dark theme-aware, responsive.
- Content: what/why, the pricing-history explainer, the cost API, a
  `SuggestModel` preview, install, and family links — all describing the real
  `TokenEconomy` API.

Editing: change `index.html` and push to `main`; CI deploys it (see
[`DEPLOY.md`](DEPLOY.md)). Preview locally with
`python3 -m http.server --directory website` or by opening `index.html`.
