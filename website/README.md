# website/

The static marketing + documentation site for **Token Economy**, served at
<https://agent-orchestrator.dev/token-economy/>.

- **Plain static HTML with a checked data step.** Content-specific CSS and
  JavaScript remain inline, the favicon is a data URI, and every page loads the
  local `navigation.css` and `navigation.js` assets. The homepage model table uses
  `matrix.js` for compact status controls and expandable evidence. `scripts/generate-website-data.py` writes five
  artifacts — `website/data/benchmarks.json` (published studies) and
  `website/data/token-usage.json` (the checked worked example and retained
  chart aggregates), plus
  `website/data/model-efficiency-matrix.json` (the checked
  `ModelEfficiencyMatrix.Describe` projection), plus
  `website/data/task-class-recommendations.json` (the checked public projection
  of the library's study-backed card-creation advice) — and CI rejects stale data.
  The fifth artifact, `website/data/model-benchmark-matrix.json`, joins the
  versioned external benchmark catalog to dated prices for the
  `model-benchmarks/` page and its candidate recommendations.
- **English**, light/dark theme-aware, responsive.
- Content: library overview, installation and cost quickstart, a dedicated
  [`api/`](api/) guide with input/return contracts and complete C# examples,
  compact model matrix, a complexity-estimation explainer, one
  real-run `ComputeCost` example, a controlled-benchmark teaser linking to
  `benchmarks/`, and family links. The controlled-benchmark subpage renders the
  full methods, limits, results, and provenance from `benchmarks.json`. The
  generated sections describe the real `TokenEconomy` API and checked-in
  evidence.

The library overview renders one raw case from `token-usage.json` as a dated
`ComputeCost` calculation. The full token-usage charts (per model, per task
class, per measured reissue count, and one measured session over time) live on
the Agent Studio usage-evidence page and read the same file.

The token-efficiency matrix carries the concrete input, output, and effective
cached-input rates resolved from the dated catalog, alongside its derived cost
class. Generation fails when a catalog model with a price valid at generation
time would be published with an unknown cost class.

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
not add result rows to `benchmarks/index.html` by hand. Follow the
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

## Model table and navigation

The five-column model table orders entries by verified publication date, newest
first. Unknown dates follow dated entries; deprecated models appear only inside
the collapsed archive. Release dates come from the price catalog's explicit
`releaseDate` / `releaseDateSource` metadata, never from price validity periods.

Status controls separate routing permission from provisional evidence. Their
expandable rows show supported reasoning levels, policy routes, task-study
links, dated external scores with benchmark versions, and qualification gaps.
External scores do not replace compatibility scores or establish local route
qualification. The generator only attaches a task's headline outcome to its
primary recommended model, not to its separately listed alternatives.

Navigation uses button disclosures with native first-click links. Escape,
focus leaving the menu, outside clicks, route changes, and viewport changes
close the relevant menu. Async landing-page data announces completion through
`site:content-ready`; anchor restoration stops after content settles or the
reader starts scrolling. New delayed-content pages should use the same event
and `data-pending-content` marker.

Public text follows the Voice writing-rules `public-docs` and
`technical-reference` profiles: lead with the reader's task, keep evidence and
qualification limits beside results, and move source-maintenance details out
of introductory copy. Lexical scans alone do not establish editorial quality.

## Price history

`price-history/` compares every catalog model at a selected date and shows all periods with sources. `scripts/generate-website-data.py` projects `model-prices.json` into `data/price-history.json` and fails on missing model coverage or provenance. Subscription amounts are labeled API-equivalent consumption; the reference index uses the same fixed token mix and dated rate snapshot. See `docs/price-history-research-2026-09-12.md` for the source audit.

## Complexity and model assessments

`task-complexity/` documents intake sources, policy anchors, AGT import, historical coverage and held-out evaluation. Its three C# downloads are compiled against the local source; the API reader requires a host-supplied AGT URL and task key. September audit evidence is anonymized in `docs/analyses/agt-*-2026-09-12.*`.

Model assessments are maintained in `docs/analyses/model-assessments-2026-09-12.json`; the website generator joins them to benchmark records with original scales, source type, harness and uncertainty. Research notes are in `docs/model-empirical-research-2026-09-12.md`.

## Code review

`code-review/` is the dedicated review-capability guide. The generator projects every
`BenchmarkCapabilityClass.CodeReview` measurement into `data/code-review.json` and
checks that its ID belongs to one documented study in
`docs/analyses/code-review-studies-2026-09-12.json`. Study summaries carry no duplicate
score fields. Precision, known-issue coverage and internal configurations retain
their original protocol and units; Kodus ratios remain on the 0–1 scale.

The feed also includes the committed Quality Studio operational report, with
fixtures excluded and unavailable qualification visible. `api/review-benchmarks.cs`
and `api/quality-studio-drop.cs` are compiled, read-only examples. Native Quality
Studio `quality-run-report.v1` exports remain a separate contract from Token
Economy's review-run drop. Research and integration limits are documented in
`docs/code-review-research-2026-09-12.md` and the public guide.


## Ecosystem link

The product header includes GitHub and an Agent Orchestrator link on the same
row. The separate family bar is removed. `family-navigation.css` follows
`family-link-v1` and is kept byte-identical to Runner's local copy. Its orange
dot and full/compact labels identify the ecosystem without changing this site's
product colors. The full accessible name remains present when only `AO` is shown.

Maintain the [Marketing Studio contract](https://github.com/RobertMischke/agent-studio-marketing/blob/main/02-produktname/dachmarke-und-produktseiten-header.md) when changing
the link, its shared styles or adoption status. Copy the canonical header into
all pages and run the existing website data/link checks. Check small and wide
viewports, both themes, keyboard menus and deep-link alignment after height changes.
