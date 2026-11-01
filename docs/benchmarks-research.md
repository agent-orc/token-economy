# Benchmark evidence research and refresh

This recipe maintains the append-only benchmark definitions and measurements in
`src/TokenEconomy/catalog`. It is a collection workflow, not a model-routing
policy. External evidence may recommend a candidate for comparison, but it does
not change the correctness floors in `docs/system/domains/model-routing-policy.md`.

## Source register

| Benchmark type | Source | Extract | Cadence | Discovery |
| --- | --- | --- | --- | --- |
| Artificial Analysis Intelligence Index | [methodology](https://artificialanalysis.ai/methodology/intelligence-benchmarking), [Astra article](https://artificialanalysis.ai/articles/benchmarking-gpt-6-astra), and the [Astra low / Sol high comparison](https://artificialanalysis.ai/models/comparisons/gpt-6-astra-low-vs-gpt-5-6-sol-high) | Index version, model, effort, score, output tokens/task, cost/task, and latency. | Weekly while a new model is being evaluated; monthly otherwise. | Methodology and comparison URLs are stable, but the live comparison changes when the index changes. Capture a dated result row; do not overwrite the previous row. |
| Artificial Analysis Coding Agent Index | [methodology](https://artificialanalysis.ai/methodology/coding-agents-benchmarking/) and [Astra article](https://artificialanalysis.ai/articles/benchmarking-gpt-6-astra) | Index version or snapshot date, agent harness, model, effort, score, tokens/task, and cost/task. | Weekly after model/index releases; monthly otherwise. | Methodology is stable. Article URLs are stable; new models need search. |
| SWE-bench Verified | [definition and annotation method](https://openai.com/index/introducing-swe-bench-verified/), [current limitation notice](https://openai.com/index/why-we-no-longer-evaluate-swe-bench-verified/), and [official repository](https://github.com/SWE-bench/SWE-bench) | Dataset version, task count, agent/scaffold, model, effort when stated, resolved rate, date, and source. | Monthly; also after a leaderboard policy change. | Definition is stable. Leaderboard results require a search and must retain harness metadata in the excerpt. |
| DeepSWE v1 / v1.1 | [DataCurve leaderboard and method](https://deepswe.datacurve.ai/) and [OpenAI Astra launch table](https://openai.com/index/gpt-6-astra/) | Version, task count, model, effort, pass rate, confidence interval, average cost, output tokens, steps, and harness. | Weekly while the leaderboard is moving; monthly otherwise. | DataCurve is a live stable URL, so each refresh is a dated snapshot. Vendor launch tables are stable publisher-reported comparisons. |
| Terminal-Bench | [project announcement](https://www.tbench.ai/news/announcement), [repository](https://github.com/harbor-framework/terminal-bench), and [OpenAI Astra launch table](https://openai.com/index/gpt-6-astra/) | Benchmark version, harness, model, effort, pass rate, cost/task if explicitly published, and integrity treatment. | Monthly and after every major version. | Method/repository URLs are stable. Model result tables usually need a search. |
| Token Economy controlled setups | [`benchmarks/README.md`](../benchmarks/README.md), [`benchmarks/setups`](../benchmarks/setups), and append-only [`benchmarks/results`](../benchmarks/results) | Setup id, run id, model, thinking level, attempts, success rate, tokens, duration, and measured cost. | After every accepted local run. | Repository paths are stable and extracted locally; no web search is needed. |

Pricing is researched separately because prices stay only in
`model-prices.json`. Use the official [GPT-6 Astra model page](https://developers.openai.com/api/docs/models/gpt-6-astra)
and [GPT-5.6 Sol model page](https://developers.openai.com/api/docs/models/gpt-5.6-sol).
CloudZero, TechJack Solutions, MindStudio, and CometAPI can help discover a
claim, but an available publisher or benchmark-owner page takes precedence.

## Exact search queries used on 2026-09-11

Run these as quoted text searches; add `site:` only when a publisher is known.

```text
site:developers.openai.com/api/docs/models/gpt-6-astra GPT-6 Astra
site:developers.openai.com/api/docs/models/gpt-5.6-sol GPT-5.6 Sol pricing
"Benchmarking GPT-6 Astra" "Artificial Analysis"
"astra-reasoning-effort-cost" thenewstack
GPT-6 Astra effort low 49 Artificial Analysis Intelligence Index Sol high 48
site:mindstudio.ai "DeepSWE" "Astra" 68 72
site:cometapi.com "DeepSWE v1.1" Astra 74.1 Sol 72.7
site:openai.com/index "GPT-6 Astra" "DeepSWE v1.1"
Artificial Analysis Intelligence Index methodology official
Artificial Analysis Coding Agent Index methodology official
SWE-bench Verified official methodology benchmark
Terminal-Bench official benchmark methodology
"Astra low" "Sol high" "cost per task"
```

Record the final direct URL, not the search-result URL. If a stable source
cannot be fetched, record it in the refresh review's `gaps` and inspect it
manually.

## Extraction schema

Benchmark definitions go to `benchmark-types.json` with:

- stable `id` that includes a publisher version or an explicit snapshot date;
- `version`, `name`, `publisher`, and `capabilityClass`;
- score `unit`, `minimumScore`, `maximumScore`, and `direction`;
- `methodologyUrl`, `citationNote`, `validFrom`, `capturedAt`, and `retrievedAt`.

Measurements go to `benchmark-results.json` with:

- unique append-only `id`, `benchmarkTypeId`, canonical `modelId`, and
  `reasoningEffort`;
- primary `score`; optional input/output tokens per task, cost per task, and
  latency in `secondaryMetrics`;
- `publishedAt`, `retrievedAt`, direct `sourceUrl`, `retrievalMethod`, short
  verbatim `evidenceExcerpt`, and `confidence`.

Publisher effort labels map case-insensitively as follows:

| Publisher label | Catalog effort |
| --- | --- |
| minimal | `minimal` |
| low | `low` |
| medium / default | `medium` only when the source explicitly equates default with medium |
| high | `high` |
| xhigh / extra high | `xHigh` |
| ultra | `ultra` |
| max / maximum | `max` |
| absent, best, selected, lower-cost, or otherwise ambiguous | `unspecified` |

Do not infer effort from a model's supported ladder. A vendor table saying only
“best score” is `unspecified`, even when a separate leaderboard happens to show
a similarly rounded max-effort result.

## Acceptance and conflict rules

1. Benchmark-owner or model-publisher rows beat third-party summaries. Store
   the direct row and keep the discovery article only when it adds distinct,
   attributable evidence.
2. A number without an explicit effort level is stored with effort
   `unspecified`.
3. Conflicting numbers remain as separate append-only rows with their own
   source, benchmark version or snapshot, publication date, and excerpt. The
   deterministic matrix selects the newest published row for a cell and still
   returns every retained evidence row.
4. Do not turn an approximation into an exact published metric. For example,
   the 2026-09-09 Artificial Analysis article supports Astra/max Coding Agent
   cost of `$7.09` and says it is about 15% above Sol/max; it does not publish
   exact Sol cost `$6.17` in text, so that derived number is not stored as a
   published metric.
5. Likewise, the article directly supports 27k Astra/max Intelligence Index
   output tokens. The operator's “Sol about 81k” figure was not present as an
   explicit table value in the accessible source. The current v4.3 comparison
   instead publishes 29k for Sol/max, so both the date and index version matter.
6. Keep result confidence explicit: `publisherReported`, `thirdParty`, or
   `ownRun`. Confidence describes provenance, not truth.
7. Prices never enter benchmark result files. Append price changes to
   `model-prices.json`, close the previous validity period, regenerate
   `KnownModels.g.cs` when a model is new, and update the routing-policy catalog
   coverage in the same change. External benchmark evidence does not make a
   model policy-selectable.
8. Evidence older than 90 days at the matrix `asOfUtc` is `stale`. It remains
   visible and queryable, but candidate consumers must surface the flag.

## Refresh procedure

1. Run `python3 scripts/benchmark-refresh.py --output <review-file>`.
2. Review every `candidateRows`, `priceFacts`, and `gaps` entry against the
   rendered source. The script intentionally does not edit either catalog.
3. Add accepted definitions/results with a new id; never edit an old result to
   make a live leaderboard look current.
4. Run the KnownModels generator if a price-catalog model was added, then run
   `python3 scripts/generate-website-data.py`.
5. Run `python3 scripts/generate-website-data.py --check` and `dotnet test`.
6. Append one row to `docs/benchmarks-research-log.md` with wall time, sources
   touched, accepted rows, and material gaps.

The generated page uses a declared 100,000 input + 10,000 output token task only
when a result lacks published cost/task. That assumption is a comparison input,
not measured benchmark usage.

## Cost Frontier snapshot (2026-11-01)

The [Cost Frontier](/token-economy/cost-frontier/) uses append-only rows with
`costSource` set to `measured`, `chartRead`, or `estimated`. `chartRead` is reserved
for figures 8.13.1.B and 8.13.3.B in the [Claude Opus 5.5 system card](https://www-cdn.anthropic.com/fc1b44717c85dc068bc6ba5024219938094694bd/Claude%20Opus%205.5%20System%20Card.pdf),
with about ±5% cost reading uncertainty. Estimated rows carry no measured cost;
the generator joins them to dated list prices and the page defaults to 100k
input and 10k output tokens. Rating boards such as Arena and GDPval-AA have no
cost per point. Benchmarks are compared only within a version.

The additional register is in `src/TokenEconomy/catalog/cost-frontier-metadata.json`:
benchmark owner links, number sources, category copy, 16 insights, 10 missions,
and five Quality dial workloads. The result and type catalogs contain the score
rows; `model-prices.json` contains release posts, availability, and dated prices.
All category sources should be checked monthly, and within a week after a new
leaderboard or model release. Reopen both the benchmark's own page and the page
that supplies the numbers. Check release posts at each model announcement.
The browser check log is delivered with TE-72; a 200 response with no readable
body is a retrieval gap, not a verified numeric row. Arena returned a Cloudflare
403 in this Chromium run; a rendered search snapshot exposed its dated text
leaderboard rows. OpenAI announcements returned 403 to plain HTTP clients.

### Current-model refresh and live reading method (2026-11-01)

The Cost Frontier projects only models whose catalog `lifecycleStatus` is
`current`. Each current model must appear in a displayed chart or score-only
strip, or in an explicit gap list; `--check` rejects an omitted current model,
any selected superseded row, and generated-data drift. Old result records remain
append-only. The selected row IDs in `cost-frontier-metadata.json` pin the
published snapshot without deleting historical evidence. The page keeps eight
categories. OSWorld-Verified and CyScenarioBench no longer have enough current
model rows for a chart; CodeRabbit and our code-review and HTML UI pilots also
drop out. The pilots still run on GPT-5.6. CyberGym is the prototype's one
single-priced-model chart; its explicit `minimumPricedModels: 1` selection is
paired with score-only context. Other charts require two priced current models.

Refresh the boards monthly and within seven days of a relevant model or board
release. Reopen every selected row source before assigning an excerpt; preserve
a changed score or cost as a new result ID. The concrete reading methods are:

- **ARC-AGI-2:** fetch `https://arcprize.org/media/data/evaluations.json`
  and `https://arcprize.org/media/data/models.json`. Join `modelId` to `id`,
  retain `datasetId == v2_Semi_Private`, and read `score` (a fraction) and
  `costPerTask`. Effort labels are in model display names. The human panel is
  100% at $17 per task and is a reference line, not a model pick. Match model
  IDs exactly: the endpoint holds both the 2026-09-10 DeepSeek V4.1 Flash
  release (60.6% low, 67.5% high, 72.9% max) and a newer 2026-10-17
  `deepseek-v4-1-flash-1017-*` revision (73.6%, 68.6%, 78.1%). The page
  selects the newer revision used by the approved prototype. The older
  evaluation remains in the source feed and is not silently merged with it.
- **Terminal-Bench 4.0:** plain HTTP returns an empty visible table. The page
  embeds leaderboard JSON containing `metadata.model_display`,
  `metadata.reasoning_effort`, `metrics.accuracy`, `metrics.n_trials`, and
  `metrics.total_cost_usd`. Divide total cost by trial count for cost per task
  and label that value as derived. Do not treat the rounded display cost as a
  published per-task field. The 2026-11-01 spot checks are GPT-6.1 Sol/max
  58.18% and $634.24 / 330 = $1.92, Opus 5.5/max 64.85% and $14.30.
  Fable 5.1/medium derives to $8.58 ($2,832.90 / 330). The approved
  prototype rounded it to $8.59; that duplicate transcription was removed
  before publication because it refers to the same run and source date.
  The board does not expose the prototype's Kimi K3.1 Pro 67 or Carbon 64
  score-only claims. Moonshot's own K3.1 Pro post reports 65.7, while Google's
  Carbon benchmark graphic reports 65.2 for the AA harness. These vendor
  claims remain in the score-only strip with their own posts and are not
  presented as board measurements.
- **Artificial Analysis Intelligence Index v5:** read the rendered model rows
  at `https://artificialanalysis.ai/leaderboards/models`. Its article explains
  the version but does not list every effort rung. Capture each model, effort,
  score, and cost from the same leaderboard row; link that row's source rather
  than recycling the article introduction as evidence.
- **Artificial Analysis Coding Agent Index v2.0:** open
  `https://artificialanalysis.ai/agents/coding-agents` in a browser. The server
  payload and rendered chart carry index reward and mean `costUsd` per task;
  the visible text explains the three components and three attempts per task.
  Opus 5.5/max reads 62 and $11.42. Some score-only entries have no published
  cost and stay in the strip. The embedded payload exposes `indexScore` for
  those entries; retain that numeric field as the excerpt when the visible
  chart has no text row.
- **DeepSWE, Arena, GDPval-AA, and Vals AI MMMU:** read the rendered page or
  embedded browser data, not a plain HTTP text table. DeepSWE currently has no
  Claude 5.5, GPT-6.1, or Gemini 4 row; Gemini 3.8 Flash/high is 74% / $2.36.
  Arena is a rating board, so no cost per point is shown. GDPval-AA exposes
  stacked cost parts without totals, so knowledge-work spend remains a list-price
  estimate. The MMMU chart uses the Vals AI board directly. Record blocked or
  partial reads as gaps rather than copying an old result forward. Arena's
  `https://arena.ai/leaderboard/chat/text` has a dated ranking in the rendered
  search snapshot, but direct Chromium and plain HTTP access returned a
  Cloudflare block page in this environment.
  The DeepSWE board has no GPT-6 Luna row, so its 66.6% score-only entry is
  sourced to OpenAI's September 22 release post. GPT-6.1 Sol's OSWorld 2.0
  offline score-only claim is 71.4 in OpenAI's release chart, rather than the
  prototype's 70.5 tracker value. CyberGym's rendered model list includes
  MiMo-V2.6-Flash, MiMo-V2.6-Pro and DeepSeek V4 Pro; its unsupported Gemini
  3.8 Flash Cyber strip entry was removed. A duplicate GPT-6 Astra OSWorld
  score-only row that cited the Opus system card was removed; the independent
  tracker entry remains.

The approved prototype placed Haiku 5.5's 72.4% vendor claim in the OSWorld
2.0 score-only strip. [Anthropic's Haiku 5.5 post](https://www.anthropic.com/claude-haiku-5-5)
labels that result **OSWorld 2.1, offline subset**. The 2.0 page omits it so
scores from different benchmark versions cannot appear side by side.

Google's 2026-11-01 [Gemini model list](https://ai.google.dev/gemini-api/docs/models)
lists Gemini 4 Argon and Gemini 3.8 Flash, with no Gemini 4 Flash. Carbon's
limited access is recorded in the price catalog and excludes it from picks and
the Quality dial. Lab release statements are labelled claims, not our runs.

Carbon's $4/$20 Artificial Analysis claim conflicts with Arena's $2/$10;
Google's pricing page had no Carbon listing at the snapshot. The catalog uses
the former as an unconfirmed exploration price, and `availability=restricted`
excludes Carbon from all picks and Quality dial results. This has no effect on
the versioned recommendation policy or its correctness floors.
