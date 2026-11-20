---
tags: [token-economy, execution-and-runner, decision, evidence]
taggingStatus: tagged
---

# Model catalogue refresh, 17 November 2026 (TE-82)

Implemented the catalogue-evidence option: add dated inventory, retain explicit provisional fleet pins, and leave default routes, priors and fallbacks unchanged. New inventory entries remain restricted in routing until fleet compatibility is observed. Mythos 5.2 and GPT-6 Cyber also require vendor approval. Terra's coding capability label is now **balanced**, between Luna and Sol; this label does not change the score-band route. No website page is changed; generated JSON projections follow their source catalogues so existing consistency guards remain valid.

All sources below were retrieved on **2026-11-17**. [M] means a third-party measurement whose raw data was pulled; [V] vendor-reported; [L] derived from list price; [P] press, unverified. Benchmark rows have a separate `evidenceClass`; API tariffs label their source [V], and subscription evidence has its own class and source fields. A confidence value such as `publisherReported` describes provenance and does not imply vendor authorship. No external benchmark becomes a local correctness cohort.

## Prices and inventory

USD per million tokens, Standard direct API, input / cache read / output:

| Added model | Released | Rates | Evidence and source |
| --- | --- | --- | --- |
| Claude Fable 5.2 | 2026-11-09 | 8 / 0.20 / 40 | [V] [Claude pricing](https://platform.claude.com/docs/en/about-claude/pricing), [release notes](https://platform.claude.com/docs/en/release-notes/overview), [model overview](https://platform.claude.com/docs/en/models/fable-5-2/overview) |
| Claude Mythos 5.2 | 2026-11-09 | 8 / 0.20 / 40 | [V] Same sources; organization verification required |
| GPT-6.2 Sol | 2026-11-17 | 2 / 0.10 / 10 | [V] [API pricing](https://developers.openai.com/api/docs/pricing), [Sol model](https://developers.openai.com/api/docs/models/gpt-6.2-sol), [API changelog](https://developers.openai.com/api/docs/changelog) |
| GPT-6.2 Astra | 2026-11-17 | 8 / 0.40 / 40 | [V] [API pricing](https://developers.openai.com/api/docs/pricing), [Astra model](https://developers.openai.com/api/docs/models/gpt-6.2-astra) |
| Gemini 4 Hydrogen | 2026-11-12 | 1.50 / 0.075 / 7.50 | [V] [Google pricing](https://ai.google.dev/gemini-api/docs/pricing), [changelog](https://ai.google.dev/gemini-api/docs/changelog); supersedes the press-only lead |
| GPT-6 Cyber | 2026-11-05 | no executable tariff | [V] [API changelog](https://developers.openai.com/api/docs/changelog), [specialized pricing](https://developers.openai.com/api/docs/pricing#cyber-models). Restricted inventory; price claims retain short-context 20 / 2 / 100, 30m writes 25, 12h writes 50. Long context is 40 / 4 / 150 with no published write tariff. The runtime cannot prohibit writes only in a long-context tier, so this entry stays unpriced instead of exposing incorrect costing. |

Fable/Mythos have 1M context, 128K maximum output and always-on adaptive thinking. Five-minute writes are $10 and one-hour writes $16. Fast mode is $16 input/$80 output, kept separate from Standard. Both are active with retirement no sooner than 2027-11-09. The November 9 release enforces prefix integrity for every account: changing system instructions, tools or earlier messages and resending a later thinking block returns HTTP 400. This is a migration constraint, not a quota condition. Sources: [model overview](https://platform.claude.com/docs/en/models/fable-5-2/overview), [pricing](https://platform.claude.com/docs/en/about-claude/pricing), [release notes](https://platform.claude.com/docs/en/release-notes/overview).

Both 6.2 API models reject `none` effort. Their API context is 1,050,000 tokens and maximum output 128,000. Above 272K input, the full request uses twice the input/cache rates and 1.5 times output. Standard 30m/12h writes are Sol $2.50/$5 and Astra $10/$20. Astra's $0.80 cached input is long-context Standard or short-context Fast; the Standard short-context rate is $0.40. The official API rate card is present, so press prices are not used as tariffs. Sources: [Sol](https://developers.openai.com/api/docs/models/gpt-6.2-sol), [Astra](https://developers.openai.com/api/docs/models/gpt-6.2-astra), [pricing](https://developers.openai.com/api/docs/pricing), [Codex models](https://learn.chatgpt.com/docs/models).

Hydrogen supports implicit caching only; explicit writes are marked unsupported. Its vendor release date is November 12, even though some press coverage added a November 13 confirmation. Google currently claims Carbon API GA on November 10 and publishes prices, contradicting the card's no-API lead and the November 1 catalogue. **Implemented the requested restricted, unpriced Carbon option**; this is a fleet policy constraint, not a claim that today's Google pages lack a tariff. A later access card can reconcile that policy. Sources: [Google changelog](https://ai.google.dev/gemini-api/docs/changelog), [pricing](https://ai.google.dev/gemini-api/docs/pricing).

## Benchmark evidence

[M] Raw Terminal-Bench leaderboard records and ARC evaluation rows are retained in [the dated evidence extract](analyses/model-catalogue-refresh-2026-11-17.json). Full source downloads and retrieval manifest are also in the task result bundle. Older results and price snapshots are preserved. Current prices are byte-identical to `model-prices.2026-11-17.json`, with its SHA-256 pinned in the snapshot index. Older prices not rechecked in this pass retain their actual verification dates; the snapshot date does not pretend every legacy tariff was reverified.

Terminal-Bench **4.1**, [official leaderboard](https://www.tbench.ai/leaderboard), 330 trials each:

| Model | Effort | Accuracy % | Reported total USD | USD/trial | Harness |
| --- | --- | ---: | ---: | ---: | --- |
| Fable 5.2 | max | 65.76 | 16492.76 | 49.98 | Claude Code |
| Opus 5.5 | max | 65.76 | 4718.65 | 14.30 | Claude Code |
| Sonnet 5.5 | max | 63.94 | 7333.64 | 22.22 | Claude Code |
| Haiku 5.5 | max | 40.00 | 952.47 | 2.89 | Claude Code |
| 6.1 Sol | max | 60.91 | 634.24 | 1.92 | Codex |
| 6.1 Terra | max | 53.33 | 874.44 | 2.65 | Codex |
| 6 Sol | max | 50.30 | 1451.32 | 4.40 | Codex |
| 6 Luna | max | 15.15 | 72.14 | 0.22 | Codex |
| 6 Astra | max | 60.30 | 3267.18 | 9.90 | Codex |
| Hydrogen | high | 56.36 | 784.24 | 2.38 | Antigravity CLI |
| Carbon | high | 64.55 | 1358.25 | 4.12 | Antigravity CLI |
| 6.2 Sol | max | 61.52 | 349.16 | 1.06 | Codex (observed 2026-11-20) |
| 6.2 Astra | max | 62.12 | 1409.14 | 4.27 | Codex (observed 2026-11-20) |

Costs per trial are the measured run total divided by trial count, with full precision retained to nine decimals in catalogue rows and rounded here. They are not recomputed from API list prices and do not establish subscription burn. The source `metadata.date` is the model release date, not execution date. Rows preserve source creation/update timestamps; actual run dates remain unknown. Their November 17 availability dates explicitly mean first-observed snapshots.

Sonnet 5.5's November 1 **4.0** score of 61.82 is retained; 63.94 belongs to **4.1**. On 4.1, Sonnet generated 186,577,885 output tokens versus Opus's 88,814,607 on the same 330 trials. This volume signal helps explain why the lower token tariff can yield a higher trial cost; it does not establish a universal model multiplier. Source: [Terminal-Bench raw leaderboard](https://www.tbench.ai/leaderboard).

[M] Added all 46 matching `v2_Semi_Private` rows from [ARC Prize evaluations.json](https://arcprize.org/media/data/evaluations.json): Opus/Sonnet/Haiku 5.5, Fable 5.2, 6.1 Sol, 6 Sol, Luna, 6.2 Sol/Astra, and Carbon. Preserve the exact cost per task and scale fractional scores to percent. These are **reasoning effort-ladder shapes only**, not coding-quality evidence. Missing effort rows are not filled in. The [Carbon result page](https://arcprize.org/results/google-gemini-4-carbon) records release date November 10; the raw evaluation rows carry no run/publication date, so November 10 is not falsely attached as their execution date.

[P] [Implicator's November 17 report](https://www.implicator.ai/openai-gpt-6-2-astra-sol-cost-per-task/) attributes a three-point Coding Agent Index decline to 6.2 Sol against 6.1 Sol, including a 14-point SWE-Atlas-QnA-AA decline. It also attributes blocker-deception omission rates of 22.28% for 6.2 Astra and 11.09% for 6 Astra to OpenAI's system card (22.3/11.1 rounded). The raw evaluator results and system card were not independently pulled in this pass; these stay press observations in listing notes, not measured scores or routing inputs.

## Subscription accounting and lifecycle

The [subscription catalogue](system/domains/subscription-catalogue.md) quotes the dated vendor statements. The machine observation catalogue stores seven Standard Codex credit rate cards, seven Plus message estimates and six vendor statements. These have units and provenance separate from USD API prices. Their arithmetic equality to API list prices times 25 does not reveal included-allowance weights. Pro has no five-hour limit. Eligible 6.2 Ultrafast overages cost 3x through February 28, 2027 and 6x from March 1; included Ultrafast remains 6x throughout. Source: [Codex pricing](https://learn.chatgpt.com/docs/pricing).

[V] [Claude lifecycle](https://platform.claude.com/docs/en/about-claude/model-deprecations): Sonnet 4.5 retires November 30; Opus 4.5 remains active with retirement no sooner than November 24; Haiku 4.5 remains active despite its October 15 minimum having passed. [Release notes](https://platform.claude.com/docs/en/release-notes/overview) remove Opus 4.8 **fast mode**, not the model, on November 30.

[V] [Codex changelog](https://developers.openai.com/codex/changelog) lists 0.168.0 and 0.169.0 on November 13. [Codex models](https://learn.chatgpt.com/docs/models) explicitly documents Terra minimum 0.159.0. **Implemented the fleet floor option:** retain 0.166.0 as the proven working resolver minimum, distinct from the vendor minimum; the operator reports fleet hosts on 0.166.0 since November 17. No upgrade or earliest-version execution probe is implied.

[V] Promotion conflict: [API pricing](https://developers.openai.com/api/docs/pricing) says GPT-5.6 Sol promotion until November 30, while [Codex pricing](https://learn.chatgpt.com/docs/pricing) promises at least through November 21. Both are recorded with their surfaces and retrieval date. No unpublished subsequent tariff or artificial price-history end date is invented.

## Re-verification, 20 November 2026

Follow-up round. Rows that relied on press leads on 17 November were checked against official sources again on **2026-11-20**:

- [V] GPT-6.2 Sol and Astra on [OpenAI API pricing](https://developers.openai.com/api/docs/pricing) are unchanged: Sol 2 / 0.10 / 10 (long context 4 / 0.20 / 15), Astra 8 / 0.40 / 40 (long context 16 / 0.80 / 60), with 30m writes 2.50 / 10 and 12h writes 5 / 20. Ultrafast is still 3x through 2027-02-28 and 6x from 2027-03-01. The same page still gives 2026-11-30 as the end of the GPT-5.6 Sol promotion.
- [V] Gemini 4 Hydrogen is unchanged at 1.50 / 0.075 / 7.50 Standard paid tier, implicit caching only, with no context-length tier. `ai.google.dev/gemini-api/docs/pricing` now redirects (301) to [aistudio.google.com/docs/pricing](https://aistudio.google.com/docs/pricing).
- [M] Terminal-Bench 4.1 now lists GPT-6.2 Sol and Astra (Codex harness, max effort, 330 trials; entries created 2026-11-18, absent on 17 November). They are added as `te82-tb41-gpt-6.2-sol-max` and `te82-tb41-gpt-6.2-astra-max`, dated by first observation on 20 November, with the raw record kept as the excerpt. On this benchmark 6.2 Sol scores 61.52 against 6.1 Sol's 60.91 (overlapping 95% intervals) at 1.06 against 1.92 USD per trial. That does not confirm or refute the press-reported Coding Agent Index decline, which is a different benchmark and stays [P].

Price evidence classes and validity dates did not change, so the price rows and the `model-prices.2026-11-17.json` snapshot are left as delivered. The embedded catalogue must stay byte-identical to that snapshot, and creating a new snapshot only to move `verifiedOn` would add no price information. This dated section is the record of the 20 November re-check.

## Verification

Release solution build passed with zero warnings and errors. Focused class filters passed 176 catalogue/provenance/routing/subscription tests and 32 benchmark/matrix/projection tests. The repository's only .NET test project also passed both guards with `FullyQualifiedName~.Architecture.|Category=Guard`. No unfiltered test or coverage run was used. `dotnet format style --verify-no-changes --severity error`, five JSON Schema validations, `python3 scripts/generate-website-data.py --check`, and `python3 scripts/check-website-links.py` passed. Exact class-filter expressions and logs are in the TE-82 task result bundle.
