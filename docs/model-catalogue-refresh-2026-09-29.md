# Model catalogue refresh, 29 September 2026 (TE-59)

This refresh covers Claude Fable 5.1, Opus 5.5, Sonnet 5 (and the new Sonnet 5.5), Haiku 4.5, GPT-6 Sol, Luna and Astra, and the Codex ladder (GPT-5.6 Sol/Terra/Luna, GPT-5.5, GPT-5.4 Mini). It compares the vendors' price pages, model documentation and CLI documentation, retrieved 2026-09-29, with price snapshot 2026-09-24 (shipped in 0.3.5) and routing policy 2026-09-25.

**Result:** every listed price is unchanged. The only new price is Claude Sonnet 5.5. The effort ladders were wrong for the GPT-6 and GPT-5.6 Codex models and for four Claude models. Two availability facts matter for the fallback ladder:

- the runner's Codex login cannot run GPT-5.4 Mini;
- the runner's Claude Code cannot run Sonnet 5.5.

The per-model fact table, with catalogue before/after values and the source of every value, is in [`analyses/model-catalogue-refresh-2026-09-29.json`](analyses/model-catalogue-refresh-2026-09-29.json).

## Method

- Prices, context windows, max output, API effort levels, release dates and lifecycle came from the vendors' Markdown documentation (the `.md` form of each page), retrieved 2026-09-29.
- Claude Code effort levels came from the Claude Code model configuration page.
- Codex effort levels came from two sources:
  - the Codex CLI model catalogue (`codex-rs/models-manager/models.json`), traced across releases rust-v0.152.0 to rust-v0.159.0;
  - the catalogue the server sent to the runner's codex-cli 0.155.0 on 2026-09-29.
- One-line runner probes checked each disputed level (`Reply with the single word OK`). Only CLI rejections and completions are recorded. A probe is not quality evidence.

## Differences applied

| Model | Field | Before | After | Evidence |
| --- | --- | --- | --- | --- |
| Claude Sonnet 5.5 | listing | absent | $2 / $0.20 / $2.50 / $10, released 2026-09-28, display name "Claude Sonnet 5.5"; policy `unsupported` | Pricing page, model page, release notes. Claude Code needs v2.1.284; the runner's 2.1.281 returned `unrecognized_model`. |
| GPT-6 Sol | Codex ladder | minimal–xhigh | low, medium, high, xhigh, max, ultra | CLI catalogue (default `medium`); runner probes completed `max` and `ultra` |
| GPT-6 Luna | Codex ladder | minimal–xhigh | low–max | CLI catalogue; OpenAI: "up to Max, but not Ultra"; probe completed `max`; API rejected `minimal` |
| GPT-5.6 Sol, Terra | Codex ladder | minimal–xhigh, ultra | low–max, ultra | CLI catalogue; API rejected `minimal` for GPT-5.6 Sol |
| GPT-5.6 Luna | Codex ladder | minimal–xhigh, ultra | low–max | CLI catalogue advertises neither `minimal` nor `ultra` |
| GPT-5.5 | Codex ladder | minimal–xhigh | low–xhigh | CLI catalogue; `minimal` failed with Codex's web_search tool |
| Claude Opus 5, 4.8, 4.7 | Claude Code ladder | low, medium, high, max | + xhigh | Claude Code and the API effort page both list xhigh |
| Claude Sonnet 4.6 | Claude Code ladder | + xhigh | low, medium, high, max | Claude Code runs xhigh as high on Sonnet 4.6 |
| GPT-5.6 Sol → GPT-6 Sol, GPT-5.6 Luna → GPT-6 Luna | migration `ladderCompatible` | false | true (still proposal-only, `safeAuto` false) | The ladders now match |
| All current rates except GPT-5 Codex | `verifiedOn` | 2026-09-12 or 2026-09-24 | 2026-09-29, with the re-verification URLs added | Pricing pages; GPT-5 Codex is absent from today's OpenAI rate card, so it keeps 2026-09-12 |
| Claude Sonnet 5, Haiku 4.5, GPT-5.5, GPT-5.4 Mini | lifecycle notes | — | added (see below) | Deprecation and Codex model pages |

No price, cache rate, release date or display name of an existing listing changed. The test `PriceSnapshotTests.September29RefreshAddsSonnet55AndChangesNoSharedRate` enforces this.

## Facts per model (retrieved 2026-09-29)

Prices are standard USD per million tokens, in the order input / cached input / cache write / output. The Anthropic cache write is the 5-minute tariff. OpenAI prices are for short context (at most 272K input tokens); above that, input and cache rates double and output is 1.5x.

| Model | Prices | Context / max output | API effort (default) | CLI effort (default) | Released | Lifecycle |
| --- | --- | --- | --- | --- | --- | --- |
| Claude Fable 5.1 | 10 / 0.25 / 12.50 / 50 | 1M / 128K | low–max (high) | `claude`: low, medium, high, xhigh, max (high) | 2026-09-01 | active, retirement ≥ 2027-09-01 |
| Claude Opus 5.5 | 4 / 0.20 / 5 / 20 | 1M / 128K | low–max (medium) | `claude`: low–max (medium); needs v2.1.280 | 2026-09-22 | active, retirement ≥ 2027-09-22 |
| Claude Sonnet 5.5 | 2 / 0.20 / 2.50 / 10 | 1M / 128K | low–max (high) | `claude`: low–max (medium); needs v2.1.284 | 2026-09-28 | active, retirement ≥ 2027-09-28 |
| Claude Sonnet 5 | 2 / 0.20 / 2.50 / 10 | 1M / 128K | low–max (high) | `claude`: low–max (high) | 2026-06-30 | legacy, retirement ≥ 2027-06-30 |
| Claude Haiku 4.5 | 1 / 0.10 / 1.25 / 5 | 200K / 64K | not supported (extended thinking) | `claude`: none | 2025-10-15 | active, retirement ≥ 2026-10-15 |
| GPT-6 Astra | 10 / 1 / 12.50 / 50 | 1.05M (922K input) / 128K | low–max | `codex`: low, medium, high, xhigh, max, ultra (low) | 2026-09-03 | active |
| GPT-6 Sol | 2 / 0.20 / 2.50 / 10 | 1.05M (922K) / 128K | none–max (medium) | `codex`: low–max, ultra (medium) | 2026-09-22 | active |
| GPT-6 Luna | 0.10 / 0.01 / 0.125 / 0.50 | 1.05M (922K) / 128K | none–max (medium) | `codex`: low–max (medium) | 2026-09-22 | active |
| GPT-5.6 Sol | 4 / 0.40 / 5 / 20 | 1.05M (922K) / 128K | none–max (medium) | `codex`: low–max, ultra (low) | 2026-06-26 | promo price ≥ 2026-11-21 |
| GPT-5.6 Terra | 2 / 0.20 / 2.50 / 12 | 1.05M (922K) / 128K | none–max (medium) | `codex`: low–max, ultra (medium) | 2026-06-26 | during rollout |
| GPT-5.6 Luna | 0.20 / 0.02 / 0.25 / 1.20 | 1.05M (922K) / 128K | none–max (medium) | `codex`: low–max (medium) | 2026-06-26 | during rollout |
| GPT-5.5 | 5 / 0.50 / – / 30 | 1.05M / 128K | none–xhigh (medium) | `codex`: low–xhigh (medium) | 2026-04-23 | leaves Codex/ChatGPT 2026-10-14 |
| GPT-5.4 Mini | 0.75 / 0.075 / – / 4.50 | 400K (272K) / 128K | none–xhigh (none) | not offered to ChatGPT sign-in | 2026-03-17 | left Codex ChatGPT sign-in 2026-08-31 |

In the Codex CLI and in `model_reasoning_effort`, the level names are `low`, `medium`, `high`, `xhigh`, `max` and `ultra`. The ChatGPT app and IDE show them as Light, Medium, High, Extra High, Max and Ultra. `ultra` is Codex's subagent mode, not an API effort. The API's `none` level is not exposed in Codex.

In Claude Code, `--effort` and `/effort` accept `low`, `medium`, `high`, `xhigh` and `max`. `ultracode` is a separate workflow toggle; `--effort ultracode` also sets `xhigh`. `ultrathink` is a prompt keyword and does not change the effort level.

## Open decisions (not applied)

1. **Bounded-decision route `mini-high` (GPT-5.4 Mini).** OpenAI retired GPT-5.4 Mini from Codex with ChatGPT sign-in on 2026-08-31, and the runner probe returned HTTP 400. The route still works for API-key Codex. Replacing it changes routing policy; OpenAI names GPT-6 Luna as the replacement. The policy and price notes now carry the warning.
2. **Claude Sonnet 5.5 as the provider fallback.** It costs the same as Sonnet 5, but the runner needs `claude update` to v2.1.284 or later and a probe before the model can leave `unsupported`. Its effort scale is recalibrated, so Sonnet 5/high evidence does not transfer.
3. **Haiku 4.5 effort.** Claude Code sends no effort to Haiku 4.5. The policy keeps `low`/`medium` as nominal labels, because the schema requires at least one level, and says so in the note.

The Claude API and Claude Code sometimes disagree about models outside this card's scope; those were not changed. For example, Claude Code lists no effort for Opus 4.5 or Sonnet 4.5.

## Sources (all retrieved 2026-09-29)

- Anthropic: [pricing](https://platform.claude.com/docs/en/about-claude/pricing), [models overview](https://platform.claude.com/docs/en/about-claude/models/overview), [model deprecations](https://platform.claude.com/docs/en/about-claude/model-deprecations), [release notes](https://platform.claude.com/docs/en/release-notes/overview), [effort](https://platform.claude.com/docs/en/build-with-claude/effort), [context windows](https://platform.claude.com/docs/en/build-with-claude/context-windows), and the model pages for [Fable 5.1](https://platform.claude.com/docs/en/models/fable-5-1/overview), [Opus 5.5](https://platform.claude.com/docs/en/models/opus-5-5/overview), [Sonnet 5.5](https://platform.claude.com/docs/en/models/sonnet-5-5/overview), [Sonnet 5](https://platform.claude.com/docs/en/models/sonnet-5/overview) and [Haiku 4.5](https://platform.claude.com/docs/en/models/haiku-4-5/overview).
- Claude Code: [model configuration](https://code.claude.com/docs/en/model-config): effort levels, aliases, minimum versions and alias history.
- OpenAI: [pricing](https://developers.openai.com/api/docs/pricing), [changelog](https://developers.openai.com/api/docs/changelog), [deprecations](https://developers.openai.com/api/docs/deprecations), and the model pages for [GPT-6 Sol](https://developers.openai.com/api/docs/models/gpt-6-sol), [GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna), [GPT-6 Astra](https://developers.openai.com/api/docs/models/gpt-6-astra), [GPT-5.6 Sol](https://developers.openai.com/api/docs/models/gpt-5.6-sol), [Terra](https://developers.openai.com/api/docs/models/gpt-5.6-terra), [Luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna), [GPT-5.5](https://developers.openai.com/api/docs/models/gpt-5.5) and [GPT-5.4 Mini](https://developers.openai.com/api/docs/models/gpt-5.4-mini).
- Codex: [models page](https://learn.chatgpt.com/docs/models) (reasoning names, Luna has no Ultra, GPT-5.5 and GPT-5.4 retirements) and the [CLI model catalogue at rust-v0.159.0](https://github.com/openai/codex/blob/rust-v0.159.0/codex-rs/models-manager/models.json). GPT-6 Sol and Luna first appear in the bundled catalogue at rust-v0.157.0 (2026-09-25).

Raw copies of every page and the probe transcripts are kept with the TE-59 run results.
