# Price history research, 12 September 2026

The review covers all 22 models already in Token Economy's catalog: direct-provider standard text-token prices in USD, cache categories, historical changes and subscription interpretation. Sources are primary provider documentation, dated announcements and an official historical rate sheet. It does not claim a complete history of every service tier, regional price or temporary offer.

The implementation removes Sonnet 5's cancelled price increase, moves Sol's reduction to its actual announced date, fills the three missing legacy model tariffs and replaces year-0001 placeholders with documented or explicitly inferred starts. Rate confirmation and date confidence are distinct fields. Full source URLs and review dates travel with each price entry.

Read the [interactive price history](../website/price-history/index.html) or inspect the [catalog](../src/TokenEconomy/catalog/model-prices.json). The research below retains evidence details and remaining uncertainty that the compact website summarizes.


---

# OpenAI price-history research

Verified on 2026-09-12 for the ten OpenAI entries already present in Token Economy. Research began with the OpenAI Docs search/fetch tools, then used rendered official model pages and dated OpenAI release announcements where Markdown omitted prices or dates. No third-party price aggregator is used.

## Confirmed current and historical prices

USD per million tokens, direct OpenAI API Standard processing, short context. "No extra" means cache writes have no surcharge; it is not a free-input rate. GPT-5-Codex is a historical entry because its public API has shut down.

| Model | Input | Output | Cache read | Cache write |
| --- | ---: | ---: | ---: | ---: |
| GPT-6 Astra | 10 | 50 | 1 | 12.5 |
| GPT-5.6 Sol | 4 | 20 | 0.4 | 5 |
| GPT-5.6 Terra | 2 | 12 | 0.2 | 2.5 |
| GPT-5.6 Luna | 0.2 | 1.2 | 0.02 | 0.25 |
| GPT-5.4 Mini | 0.75 | 4.5 | 0.075 | No extra |
| GPT-5.5 | 5 | 30 | 0.5 | No extra |
| GPT-5.5 Pro | 30 | 180 | No discount | No extra |
| GPT-5.5 Cyber Preview | 12.5 | 75 | 1.25 | No extra |
| GPT-5 | 1.25 | 10 | 0.125 | No extra |
| GPT-5-Codex (historical) | 1.25 | 10 | 0.125 | No extra |

[Current API pricing](https://developers.openai.com/api/docs/pricing), [GPT-5-Codex rendered price card](https://developers.openai.com/api/docs/models/gpt-5-codex). The companion JSON contains every published Standard/Batch/Flex/Fast rate for the requested models, including long-context columns and explicit missing values.

### Astra

The September 3 announcement supplies the 10/50 Standard rate and API access; the API changelog confirms the same release day. Current documentation adds cache-read 1 and cache-write 12.5. No later price change was found. Retain the existing September 3 interval, and retain the long-context and service-tier caveats. [Announcement](https://openai.com/index/gpt-6-astra/), [model contract](https://developers.openai.com/api/docs/models/gpt-6-astra), [September changelog](https://developers.openai.com/api/docs/changelog#september-2026).

### Sol, Terra and Luna

The June 26 limited preview explicitly included API and Codex access for approved partners. It published Sol 5/30, Terra 2.5/15 and Luna 1/6, with cache reads at 10% and writes at 125% of input. Therefore the existing June 26 price-history starts are supported, even though broad public API availability came July 9. The initial read/write rates were Sol 0.5/6.25, Terra 0.25/3.125, Luna 0.1/1.25. [Limited-preview announcement](https://openai.com/index/previewing-gpt-5-6-sol/).

Actual tariff changes:
- July 30: Terra input/output became 2/12 (20% reduction); Luna became 0.2/1.2 (80% reduction).
- August 21: Sol became 4/20, with the corresponding 0.4 read and 5 write rates.
- Sol's promotion is available at least through November 21. There is no confirmed future reversion date or price; do not add a November 22 price period.

The July 9 launch article retains its initial price paragraph but has explicit July 30 and August 21 update notices. Read those as historical context, not as conflicting current prices. The dated API changelog and current rate card agree. [General-release article and updates](https://openai.com/index/gpt-5-6/), [API changelog](https://developers.openai.com/api/docs/changelog#august-2026).

The local Sol catalog incorrectly starts the promotion September 3 and attributes it to Astra's launch. Correct both adjacent boundaries to August 21. This repairs a wrongly recorded date for one promotion; it does not represent a second discount.

### GPT-5.4 Mini

The March 17 release announcement states 0.75 input and 4.5 output, matching today's rates. Current cached-input rate is 0.075. There is no separately billed cache-write premium. The official snapshot is gpt-5.4-mini-2026-03-17 and can be added as a typed-catalog alias. No later tariff change was found. [Launch](https://openai.com/index/introducing-gpt-5-4-mini-and-nano/), [model](https://developers.openai.com/api/docs/models/gpt-5.4-mini).

### GPT-5.5 and GPT-5.5 Pro

The April 23 announcement specified future API prices of 5/30 and 30/180 respectively, while saying ChatGPT/Codex rollout was already starting and API access was coming shortly. The API changelog dates API release to April 24. Existing April 24 price-history starts are therefore correct; snapshot names ending April 23 are not API-availability dates. GPT-5.5 cached input is 0.5; Pro explicitly offers no cached-input discount. Neither has a cache-write surcharge. No subsequent Standard tariff change was found. [April 23 announcement](https://openai.com/index/introducing-gpt-5-5/), [April 24 API release](https://developers.openai.com/api/docs/changelog#april-2026), [Pro contract](https://developers.openai.com/api/docs/models/gpt-5.5-pro).

### GPT-5.5 Cyber Preview

Current API pricing explicitly lists gpt-5.5-cyber at 12.5 input, 1.25 cached input and 75 output. There is no cache-write surcharge. The support article confirms the callable legacy Responses model ID is gpt-5.5-cyber-preview, so the catalog's canonical ID and pricing alias are justified. Responses access and Codex ChatGPT-sign-in access are separately provisioned; Codex API-key authentication is currently unavailable for this legacy variant. [API rate card](https://developers.openai.com/api/docs/pricing#cyber-models), [access and exact ID](https://help.openai.com/en/articles/20001259).

The May 7 launch is confirmed, but the announcement contains no numerical tariff. Searches of the official API docs, release article and help documentation did not establish when the 12.5/75 tariff first took effect. The existing May 7 history boundary is a release-date inference, not independently verified historical pricing. Preserve that distinction in structured provenance if retaining the period; the current rate itself is confirmed. No new date or alternate rate should be invented. [May 7 announcement](https://openai.com/index/gpt-5-5-with-trusted-access-for-cyber/).

### GPT-5 and GPT-5-Codex

Both catalog entries currently omit documented prices. GPT-5's August 7 developer announcement states 1.25 input and 10 output. Its model page and current pricing table give the 0.125 cached-input rate. Backfill the API history from August 7 and add the official gpt-5-2025-08-07 alias. [Developer launch](https://openai.com/index/introducing-gpt-5-for-developers/), [model](https://developers.openai.com/api/docs/models/gpt-5).

GPT-5-Codex first appeared in Codex with ChatGPT sign-in on September 15. The September 23 changelog separately introduced Responses API availability and explicitly equated its API price with GPT-5. Its retained model card confirms 1.25/10/read0.125. Start API history September 23, not September 15. [Product/API changelog](https://learn.chatgpt.com/docs/changelog#codex-2025-09-23), [model](https://developers.openai.com/api/docs/models/gpt-5-codex).

Lifecycle is independent of price history. Official API deprecations list GPT-5-Codex shutdown July 23, 2026, and GPT-5 snapshot shutdown December 11, 2026. Retain historical prices for reproducible cost calculations; do not treat a surviving price card as proof that a model can run. A shutdown is not evidence of a changed tariff. [Deprecation register](https://developers.openai.com/api/docs/deprecations).

## Cache accounting

The suspected OpenAI 1.25x write rate is genuine for GPT-5.6 and later. It is the total rate for written tokens, not an additional fee on top of uncached input. For these models, use disjoint input categories:
- ordinary fresh input;
- cache reads;
- cache writes.

Earlier models, including GPT-5.5 and Pro, have no additional cache-write charge. The current library's null cache-write rate falling back to normal input is appropriate. A null cached-input rate for GPT-5.5 Pro similarly means no discount, not zero cost.

The new cache lifetime is at least 30 minutes since the latest write or reuse; its configured value is 30m. Do not describe these OpenAI rates using Anthropic's five-minute TTL vocabulary. The earlier 24h retention setting is a different API policy. [Prompt-caching contract](https://developers.openai.com/api/docs/guides/prompt-caching#summary-of-model-differences).

Reasoning tokens are billed as output. Higher reasoning can increase output volume without introducing a distinct reasoning-token tariff. [Reasoning accounting](https://developers.openai.com/api/docs/guides/reasoning#how-reasoning-works).

## Long context and processing tiers

For Astra and GPT-5.6, more than 272,000 input tokens raises the entire request to the long tier: 2x ordinary/cache input and 1.5x output. The GPT-5.5 documentation describes the premium for the full session. Exactly 272,000 is not "more than." Use total request context, not just fresh uncached input. The flat ModelPriceCatalog API cannot select these tiers automatically. [Astra model](https://developers.openai.com/api/docs/models/gpt-6-astra), [GPT-5.5 model](https://developers.openai.com/api/docs/models/gpt-5.5).

Batch and Flex must not be mixed into the default Standard history. Batch is asynchronous with a 24-hour completion window; Flex exchanges lower prices for slower responses and possible resource unavailability. The companion JSON preserves published combinations. GPT-5.5 Pro has blank long-context Batch/Flex columns, so do not synthesize those tariffs. [Batch](https://developers.openai.com/api/docs/guides/batch), [Flex](https://developers.openai.com/api/docs/guides/flex-processing).

Priority was renamed Fast on July 30, preserving the priority request value. API Fast is 2x for Astra/GPT-5.6 and 2.5x for GPT-5.5. GPT-5.6 Fast gained long-context support August 5. Astra Fast is unavailable with EU data residency and has no latency SLA. Eligible regional processing adds 10% for models released on/after March 5, 2026. Current published tier rates are not automatically proof of historical tier support. [Fast contract](https://developers.openai.com/api/docs/guides/fast-mode), [dated changelog](https://developers.openai.com/api/docs/changelog#july-2026).

## Integration recommendations

Add optional, backward-compatible ModelPrice provenance: SourceUrls (list), VerifiedOn (date), ValidFromBasis (string). Distinguish provider-announced-date from release-date-inference. Keep Unconfirmed about the numerical price, not permission to route or uncertainty about the start date.

Use calendar-day boundaries consistently, but disclose that provider pages do not establish the exact intraday UTC billing cutover. Midnight UTC and the preceding day's final tick are catalog conventions.

Keep source remarks, price history, retrieval date, and provenance together. Regenerate cost-derived examples and reports when a real tariff correction changes their calculations; do not rewrite original measured token usage or benchmark outcomes.

API-dollar cost can support a normalized resource-use comparison in a subscription context, but is not an invoice or a reliable calculation of subscription headroom. A successful task can consume different tokens, retries, reasoning, tools and parallel agents on different models. Current ChatGPT credit tariffs and their Fast multipliers are a separate schedule, researched by the parent agent.

## Explicit remaining uncertainties

No source established an exact historical tariff-start date for GPT-5.5 Cyber. No future Sol reversion is announced. No additional dated cache-price changes were found for older models, so current cached rates plus launch continuity are retained transparently. No intraday UTC cutover is claimed. Third-party, negotiated, cloud-hosted and subscription invoices remain outside the direct API rate history.



---

# Anthropic catalog pricing research

Research date: 2026-09-12. Scope: twelve Claude models already represented in the Token Economy catalog. Prices are USD per million tokens, standard direct Claude API, global inference unless explicitly stated. This report is a source review, not a provider invoice or a subscription conversion table.

## Findings that change the catalog

1. Sonnet 5 remains 2 input / 10 output. On August 10 Anthropic cancelled the previously scheduled September 1 increase to 3 / 15. Delete the scheduled higher-price row; preserve the cancellation as an event. [Dated August 10 release note](https://platform.claude.com/docs/en/release-notes/overview).
2. Opus 4.1 has a confirmed historical tariff of 15 / 75, cache read1.5, write5m18.75, write1h30. It retired on the direct Claude API on August5; pricing evidence and availability are separate. [Pricing](https://platform.claude.com/docs/en/about-claude/pricing), [Lifecycle](https://platform.claude.com/docs/en/about-claude/model-deprecations).
3. Opus 4.5 and Sonnet 4.5 prices are officially confirmed, not assumptions. Their public launches were November24 and September29,2025 respectively. [Opus4.5 launch](https://www.anthropic.com/news/claude-opus-4-5), [Sonnet4.5 launch](https://www.anthropic.com/news/claude-sonnet-4-5).
4. Year0001 is not a historical effective date. Public launch dates support a day-granular start for standard base tariffs; cache-category launch dates require their own evidence qualification. Do not present a midnight timestamp as a provider-published instant.

## Complete standard tariff table

The base input/output tariff at public launch equals the current listed standard base tariff for each model below. This establishes the endpoints; it does not prove that no unarchived temporary promotion occurred between them. Cache columns are confirmed current rates; the evidence qualification below matters when backdating them.

| Model | Public launch | Input | Output | Cache read | Write5m | Write1h | Launch proof |
|---|---|---:|---:|---:|---:|---:|---|
| Fable 5.1 | 2026-09-01 | 10 | 50 | 0.25 | 12.5 | 20 | [Announcement](https://www.anthropic.com/claude-fable-and-mythos-5-1) |
| Fable 5 | 2026-06-09 | 10 | 50 | 1 | 12.5 | 20 | [Announcement](https://www.anthropic.com/news/claude-fable-5-mythos-5) |
| Opus 5 | 2026-07-24 | 5 | 25 | 0.5 | 6.25 | 10 | [Announcement](https://www.anthropic.com/news/claude-opus-5) |
| Sonnet 5 | 2026-06-30 | 2 | 10 | 0.2 | 2.5 | 4 | [Announcement](https://www.anthropic.com/news/claude-sonnet-5) |
| Opus 4.8 | 2026-05-28 | 5 | 25 | 0.5 | 6.25 | 10 | [Announcement](https://www.anthropic.com/news/claude-opus-4-8) |
| Opus 4.7 | 2026-04-16 | 5 | 25 | 0.5 | 6.25 | 10 | [Announcement](https://www.anthropic.com/news/claude-opus-4-7) |
| Opus 4.6 | 2026-02-05 | 5 | 25 | 0.5 | 6.25 | 10 | [Announcement](https://www.anthropic.com/news/claude-opus-4-6) |
| Opus 4.5 | 2025-11-24 | 5 | 25 | 0.5 | 6.25 | 10 | [Announcement](https://www.anthropic.com/news/claude-opus-4-5) |
| Opus 4.1 (retired directAPI) | 2025-08-05 | 15 | 75 | 1.5 | 18.75 | 30 | [Announcement](https://www.anthropic.com/news/claude-opus-4-1) |
| Sonnet 4.6 | 2026-02-17 | 3 | 15 | 0.3 | 3.75 | 6 | [Announcement](https://www.anthropic.com/news/claude-sonnet-4-6) |
| Sonnet 4.5 | 2025-09-29 | 3 | 15 | 0.3 | 3.75 | 6 | [Announcement](https://www.anthropic.com/news/claude-sonnet-4-5) |
| Haiku 4.5 | 2025-10-15 | 1 | 5 | 0.1 | 1.25 | 2 | [Announcement](https://www.anthropic.com/news/claude-haiku-4-5) |

Current numeric rates: [official pricing table](https://platform.claude.com/docs/en/about-claude/pricing). Individual live model specifications are also linked in the JSON. Opus4.1 launch says pricing matches Opus4; the earlier [Claude4 announcement](https://www.anthropic.com/news/claude-4) explicitly supplies15/75.

The [official list-price PDF effective May27,2026](https://www-cdn.anthropic.com/files/4zrzovbb/website/3684c2faafb97418665782cea0001f439f74b1d2.pdf) confirms all five token categories for the eight older catalog models on pages1-2. Both pages were rendered and inspected. It provides a dated historical checkpoint, not proof of launch-day cache pricing. It lists Opus4.8 on May27 although the public release is May28; use the announcement date for public availability. The PDF also lists an Opus4.1 1M SKU; that unsupported capability inference is excluded from the recommended catalog changes.

## Chronology and event classification

| Date | Event | Consequence |
|---|---|---|
| 2026-02-05 | opus-4-6; launch-long-context-premium | Historical modifier only; end at2026-03-13. Original cache premium category prices were not separately verified. [Source](https://www.anthropic.com/news/claude-opus-4-6) |
| 2026-02-07 | opus-4-6; fast-mode-launched | Historical6x rate30/150 is corroborated by official older indexed Code docs, but exact launch promotional periods not fully proven. Do not backdate30/150 over an unverified introductory window. [Source](https://platform.claude.com/docs/en/release-notes/overview) |
| 2026-03-13 | opus-4-6, sonnet-4-6; long-context-premium-removed | Full1M standard rates from this date. Former Sonnet4.6 6/22.5 numeric tier not re-established from a dated primary document in this pass, so do not import by assumption. [Source](https://claude.com/blog/1m-context-ga) |
| 2026-04-30 | sonnet-4-5; long-context-retired | Requests beyond200k no longer available. Earlier1M beta existence confirmed; historical price not fully verified here. [Source](https://platform.claude.com/docs/en/release-notes/overview) |
| 2026-05-12 | opus-4-7; fast-mode-launched | Same historical rate as4.6 fast; survives only untilJuly24. [Source](https://platform.claude.com/docs/en/release-notes/overview) |
| 2026-05-28 | opus-4-8; fast-mode-launched | 2x standard. This is a new-model fast tariff, not a change of4.7 standard. [Source](https://www.anthropic.com/news/claude-opus-4-8) |
| 2026-06-29 | opus-4-6; fast-mode-retired | speed:fast requests now use standard speed and standard billing, without error. [Source](https://platform.claude.com/docs/en/release-notes/overview) |
| 2026-07-24 | opus-4-7; fast-mode-retired | speed:fast now errors, no standard fallback. [Source](https://platform.claude.com/docs/en/release-notes/overview) |
| 2026-07-24 | opus-5; fast-mode-launched | 2x standard. [Source](https://www.anthropic.com/news/claude-opus-5) |
| 2026-08-05 | opus-4-1; model-retired | Direct Claude API retirement; partner lifecycle independent. Preserve historical price lookup. [Source](https://platform.claude.com/docs/en/about-claude/model-deprecations) |
| 2026-08-10 | sonnet-5; scheduled-increase-cancelled | Record in event history. Do not apply cancelled rates to September usage. [Source](https://platform.claude.com/docs/en/release-notes/overview) |
| 2026-09-01 | fable-5-1; new-model-cache-price-difference | 75% lower cache-read tariff on new model; Fable5 tariff itself remains1. [Source](https://www.anthropic.com/claude-fable-and-mythos-5-1) |

## What changed and what did not

Sonnet5 is the important correction. Its June30 launch advertised2/10 as introductory pricing. The dated August10 release note cancelled the planned September1 increase before it took effect. Current model specifications and pricing agree at2/10, cache read0.2, write5m2.5, write1h4. Do not create a3/15 interval and do not invent a later reduction date. [Sonnet5 model specification](https://platform.claude.com/docs/en/models/sonnet-5/overview).

Fable5.1 is a new model with cache reads0.25 compared with Fable5 at1, a75% reduction in that category. Input10/output50 remain the same. Anthropic estimates roughly25% lower cost on typical workloads and up to45% on highly agentic work, based on its own workload mix; those percentages are not universal task-cost discounts or subscription multipliers. [Fable5.1 announcement](https://www.anthropic.com/claude-fable-and-mythos-5-1).

Opus4.1 at15/75 and Opus4.5 at5/25 represent different model generations. Do not rewrite Opus4.1 historical tokens at the newer model price. The dated4.5 launch directly confirms the lower new-model rate. [Opus4.5 launch](https://www.anthropic.com/news/claude-opus-4-5).

Opus4.6 had a documented long-context launch premium above200k input:10/37.50. The dated March13 announcement moved Opus4.6 and Sonnet4.6 to standard pricing over the entire1M window. This is a real context-tier change, even though the ordinary base rate did not change. [Opus4.6 launch details](https://www.anthropic.com/news/claude-opus-4-6), [March13 GA announcement](https://claude.com/blog/1m-context-ga).

The earlier numerical Sonnet long-context tariffs were not re-established from an accessible dated primary document in this pass. The common6/22.50 claim is therefore not recommended for production import yet. A historical beta and its retirement are supported; a missing historical numeric tier must remain missing.

Fast mode is also a separate billing dimension. Current Opus5 and4.8 Fast pricing is10/50, with caching and data-residency modifiers applied on top. The actual response speed matters when a request falls back. Fast is incompatible with Batch. [Fast-mode documentation](https://platform.claude.com/docs/en/build-with-claude/fast-mode).

For historical Fast, May12 release notes confirm4.7 added Fast at the same pricing as4.6; the official [May11-15 Code update](https://code.claude.com/docs/en/whats-new/2026-w20) and an older indexed [Code Fast documentation page](https://code.claude.com/docs/en/fast-mode) support30/150. Exact initial4.6 promotional boundaries were not reconstructed, so do not assume30/150 applied uniformly from February7. The current API release notes explicitly distinguish4.6 removal with standard billing from4.7 removal with an error.

## Cache, batch, context and token accounting

Five-minute cache creation is billed at1.25 times base input; one-hour creation at2 times. Reads normally cost0.1 times base input. Fable5.1 is the exception at0.025 times. A read refreshes cache lifetime without another creation charge. A write is its own billed token category, not ordinary input plus a second full write charge. [Prompt-caching guide](https://platform.claude.com/docs/en/build-with-claude/prompt-caching).

Do not collapse5m and1h writes into one price when both are present in a usage record. Show the requested duration and use the matching usage breakdown. Discounting a cache hit does not discount generated output tokens. [Prompt-caching guide](https://platform.claude.com/docs/en/build-with-claude/prompt-caching).

Batch halves base input/output rates. The May27 price sheet also explicitly lists half-rate cache categories for direct Claude API Batch rows. Cloud partner rows differ in supported combinations, so do not copy the direct API combination matrix to Bedrock or Google Cloud. [Dated platform price sheet](https://www-cdn.anthropic.com/files/4zrzovbb/website/3684c2faafb97418665782cea0001f439f74b1d2.pdf).

Current1M-capable models use1M by default and standard long-context rates. Earlier200k models remain200k; do not give Haiku4.5 or Opus4.5 a1M tariff just because later generations support it. [Context-window documentation](https://platform.claude.com/docs/en/build-with-claude/context-windows).

Prices per token do not directly predict cost for identical text across generations. Anthropic documents a newer tokenizer from Opus4.7 onwards, often producing roughly30% more tokens for the same text; the actual amount depends on the workload. Use usage records or the model-specific token counter. [Pricing tokenizer note](https://platform.claude.com/docs/en/about-claude/pricing).

## Recommended representation

Each model needs independent launch/retirement metadata and a sourced standard tariff. Each tariff should include input, output, cache-read, write5m, write1h, valid-from basis, verification date, source URLs, platform, speed, geography and context tier. The latter dimensions may be modeled as modifiers; they must not disappear from the scope label.

Use dates where sources give dates. If the implementation requires UTC instants, document start-of-day as an application convention and keep date precision in metadata. A verified-on date records the evidence check; it is not a new price effective date.

Price events need status: effective, scheduled, cancelled, retired, or cross-model comparison. A cancelled event can be displayed in the timeline without becoming a billable price interval. The accompanying JSON provides12 model recommendations and12 separate events.

Keep retired Opus4.1 for historical cost lookup. Its direct API status is retired; partner-operated platforms have separate lifecycle schedules. Do not show retirement as unknown price or advertise directAPI availability from a surviving pricing-table row. [Official lifecycle policy](https://platform.claude.com/docs/en/about-claude/model-deprecations).

## Evidence limitations

- No complete immutable day-by-day provider price archive; no claim every transient promotional or negotiated tariff was reconstructed.
- Cache rates are fully verified as of research date, with dated2026-05-27 snapshot for older models; exact original launch cache rates are not separately date-proven for every category.
- Historical Sonnet4.5/4.6 long-context numeric tiers and any Opus4.6 fast introductory promotional boundaries need additional dated official evidence before production import.
- The May27 list-price PDF lists Opus4.8 one day before its public May28 launch; use May28 for public catalog availability.
- PDF also contains an Opus4.1 1M SKU; do not import capability or that tier absent corroborating lifecycle/availability documentation.

This pass used public official documentation, announcements and a dated provider PDF. Search snippets with stale query-parameter variants were treated as historical leads, not current authority. It did not call an inference API, inspect a private bill, or modify library/website source.

## Source inventory

All sources below were retrieved on2026-09-12. Publication/effective dates are recorded in the model rows and event chronology; live-document retrieval dates must not be mistaken for original publication dates.

- [www.anthropic.com/claude-fable-and-mythos-5-1](https://www.anthropic.com/claude-fable-and-mythos-5-1) (live-official-documentation).
- [platform.claude.com/docs/en/about-claude/pricing](https://platform.claude.com/docs/en/about-claude/pricing) (live-official-documentation).
- [platform.claude.com/docs/en/models/fable-5-1/overview](https://platform.claude.com/docs/en/models/fable-5-1/overview) (live-official-documentation).
- [platform.claude.com/docs/en/release-notes/overview](https://platform.claude.com/docs/en/release-notes/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-fable-5-mythos-5](https://www.anthropic.com/news/claude-fable-5-mythos-5) (dated-announcement).
- [platform.claude.com/docs/en/models/fable-5/overview](https://platform.claude.com/docs/en/models/fable-5/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-opus-5](https://www.anthropic.com/news/claude-opus-5) (dated-announcement).
- [platform.claude.com/docs/en/models/opus-5/overview](https://platform.claude.com/docs/en/models/opus-5/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-sonnet-5](https://www.anthropic.com/news/claude-sonnet-5) (dated-announcement).
- [platform.claude.com/docs/en/models/sonnet-5/overview](https://platform.claude.com/docs/en/models/sonnet-5/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-opus-4-8](https://www.anthropic.com/news/claude-opus-4-8) (dated-announcement).
- [www-cdn.anthropic.com/files/4zrzovbb/website/3684c2faafb97418665782cea0001f439f74b1d2.pdf](https://www-cdn.anthropic.com/files/4zrzovbb/website/3684c2faafb97418665782cea0001f439f74b1d2.pdf) (dated-list-price-pdf).
- [www.anthropic.com/news/claude-opus-4-7](https://www.anthropic.com/news/claude-opus-4-7) (dated-announcement).
- [www.anthropic.com/news/claude-opus-4-6](https://www.anthropic.com/news/claude-opus-4-6) (dated-announcement).
- [platform.claude.com/docs/en/models/opus-4-6/overview](https://platform.claude.com/docs/en/models/opus-4-6/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-opus-4-5](https://www.anthropic.com/news/claude-opus-4-5) (dated-announcement).
- [platform.claude.com/docs/en/models/opus-4-5/overview](https://platform.claude.com/docs/en/models/opus-4-5/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-opus-4-1](https://www.anthropic.com/news/claude-opus-4-1) (dated-announcement).
- [www.anthropic.com/news/claude-4](https://www.anthropic.com/news/claude-4) (dated-announcement).
- [platform.claude.com/docs/en/about-claude/model-deprecations](https://platform.claude.com/docs/en/about-claude/model-deprecations) (live-official-documentation).
- [www.anthropic.com/news/claude-sonnet-4-6](https://www.anthropic.com/news/claude-sonnet-4-6) (dated-announcement).
- [platform.claude.com/docs/en/models/sonnet-4-6/overview](https://platform.claude.com/docs/en/models/sonnet-4-6/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-sonnet-4-5](https://www.anthropic.com/news/claude-sonnet-4-5) (dated-announcement).
- [platform.claude.com/docs/en/models/sonnet-4-5/overview](https://platform.claude.com/docs/en/models/sonnet-4-5/overview) (live-official-documentation).
- [www.anthropic.com/news/claude-haiku-4-5](https://www.anthropic.com/news/claude-haiku-4-5) (dated-announcement).
- [platform.claude.com/docs/en/models/haiku-4-5/overview](https://platform.claude.com/docs/en/models/haiku-4-5/overview) (live-official-documentation).
- [claude.com/blog/1m-context-ga](https://claude.com/blog/1m-context-ga) (dated-announcement).
- [code.claude.com/docs/en/fast-mode](https://code.claude.com/docs/en/fast-mode) (live-official-documentation).
- [code.claude.com/docs/en/whats-new/2026-w20](https://code.claude.com/docs/en/whats-new/2026-w20) (live-official-documentation).
- [platform.claude.com/docs/en/build-with-claude/fast-mode](https://platform.claude.com/docs/en/build-with-claude/fast-mode) (live-official-documentation).
- [platform.claude.com/docs/en/build-with-claude/prompt-caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching) (live-official-documentation).
- [platform.claude.com/docs/en/build-with-claude/context-windows](https://platform.claude.com/docs/en/build-with-claude/context-windows) (live-official-documentation).


---

# Subscription consumption and API list prices

Reviewed 2026-09-12. Scope: the 22 models already in Token Economy's catalog, direct-provider standard text-token pricing, and the interpretation of those rates when a coding agent uses a subscription.

## Findings

An API-equivalent amount is a useful common weighting for measured token use. Inside a subscription's included allowance it is neither a marginal invoice amount nor a reliable percentage of the remaining quota. For comparison over time, keep the price snapshot fixed; otherwise a tariff reduction can look like an efficiency gain even when token use is unchanged. This is our measurement recommendation, not a provider quota formula.

OpenAI distinguishes included usage, credit billing and API-key billing. Task complexity, context, reasoning, tools and caching affect included usage. The current Codex credit table is a separate rate card; Astra Fast consumes 2.5 times its standard credits. Credit purchase terms depend on the plan or agreement. Source: [Codex pricing](https://learn.chatgpt.com/docs/pricing), fetched 2026-09-12.

Claude Code's session dollar figure is computed locally from token counts. Pro and Max include usage, so this amount does not describe subscription billing. Provider usage dashboards and billing records are the authority for quota and charges. Source: [Claude Code costs](https://code.claude.com/docs/en/costs), reviewed 2026-09-12.

Claude usage allowances depend on the model, effort, conversation and features. Usage is shared across product surfaces. Source: [Usage and length limits](https://support.claude.com/en/articles/11647753-how-do-usage-and-length-limits-work), updated 2026-07-13.

Claude usage credits beyond the included allowance are charged separately at standard API rates. This is a billing context in which the list-price estimate can approximate variable spend. Source: [Manage usage credits](https://support.claude.com/en/articles/12429409-manage-usage-credits-for-paid-claude-plans), updated 2026-08-10.

The proposed switch of Claude Agent SDK and `claude -p` to a separate monthly credit did not take effect: the update at the top of the article pauses it. Subscription-authenticated SDK and noninteractive usage still draw from subscription limits. The old announcement below the update is historical, not current guidance. Source: [Agent SDK with a Claude plan](https://support.claude.com/en/articles/15036540-use-the-claude-agent-sdk-with-your-claude-plan), updated 2026-06-16.

## Measurement contract

Store measured input, output, cache-read and cache-write tokens in disjoint buckets, model ID, effort, task class, outcome, run timestamp, authentication/billing context, rate snapshot and source. Compute `sum(tokens * rate / 1,000,000)`. Show `API-equivalent USD` for included subscription usage and `estimated API list-price cost` for API-key runs. Show provider credits and provider-reported quota in their own units.

For a relative consumption index, divide the amount by an explicit reference workload's amount using the same dated rate snapshot. A smaller ratio only describes that token weighting. Qualification needs measured task success and actual tokens; identical-token simulations cannot establish model efficiency. Subscription fee divided by accepted tasks is a separate internal allocation metric and must not be added to an API-equivalent amount as if both were charges.

Do not infer dollar savings, credits, remaining messages or quota exhaustion from a list-price ratio. Exclude additional billed services, taxes, contract discounts and context/speed tiers unless they have been explicitly modeled.

