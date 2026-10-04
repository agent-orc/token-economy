# Model catalogue refresh, 4 October 2026 (TE-60)

Implemented the recommended option: add GPT-6.1 Sol as a priced, selectable, provisional model with a proposal-only GPT-6 Sol migration. No default tier, task-class prior, or provider fallback changed.

## Diff from the 29 September snapshot

| Field | 2026-09-29 | 2026-10-04 |
| --- | --- | --- |
| Price listings | 26 | 27: added `gpt-6.1-sol` |
| New model release date | absent | 2026-09-29 |
| New model Standard short-context USD/MTok: input / cached input / cache write / output | absent | 2 / 0.10 / 2.50 / 10 |
| Shared listings and rates | 26 | unchanged for all 26 |
| GPT-6 Sol → GPT-6.1 Sol migration | absent | proposal only; `safeAuto: false`, `evidence.kind: none` |
| Routing | absent | selectable, provisional, frontier, core task; explicit pins only |

The [OpenAI API changelog](https://developers.openai.com/api/docs/changelog) dates the release to September 29 and prints all four short-context rates for prompts up to 272K input tokens. The [pricing page](https://developers.openai.com/api/docs/pricing) and [model page](https://developers.openai.com/api/docs/models/gpt-6.1-sol) print the longer-context Standard tariff: **4 / 0.20 / 5 / 15 USD/MTok** above 272K input tokens. The dated catalogue entry represents the short-context Standard rate; its note records the separate long-context tariff. All four new catalogue numbers and the long-context numbers were checked on 2026-10-04 and are confirmed. Midnight UTC is a calendar-date convention; OpenAI does not print an intraday billing cutover.

Standard GPT-6 Sol input/output stay at 2 / 10 USD/MTok; GPT-6.1 Sol cached input halves from 0.20 to 0.10. The source Codex ladder has `max` and `ultra`; the target's production Agent Studio discovery on 2026-10-04 advertises `minimal`, `low`, `medium`, `high`, `xhigh` with default `xhigh`. Source `max` and `ultra` pins need an explicit downgrade to `xhigh`. The [API model page](https://developers.openai.com/api/docs/models/gpt-6.1-sol) says API `minimal` is unsupported; Codex CLI `minimal` was not tested successfully.

A host probe with **codex-cli 0.155.0** ran `codex exec -m gpt-6.1-sol 'Reply with exactly: ok'` on 2026-10-04 and received HTTP 400: `The 'gpt-6.1-sol' model is not supported when using Codex with a ChatGPT account.` The CLI also lacked bundled metadata for this model. This is an account/CLI availability gap, not evidence of model quality or a minimum working CLI version. No successful CLI version was established; execution availability and each advertised Codex level remain unconfirmed on this host.

The [OpenAI announcement](https://openai.com/index/introducing-gpt-6-1-sol/) reports a 7.7% factual-error rate for the exact model at low effort on deliberately difficult flagged-error conversations, but the page does not display a publication date. The API changelog dates the model release, not that evaluation. No GPT-6.1 Sol benchmark row was added because a publication date for the evaluation could not be established. There is no identical-case repository benchmark for the migration. Human-friendly language and media task performance for GPT-6.1 Sol are unmeasured, so no scores were copied into those capability catalogues.
