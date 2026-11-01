# Model publication dates

Verified against provider documentation on 2026-09-12. On 2026-09-29 the Fable
5.1, Opus 5.5, Sonnet 5, Haiku 4.5 and GPT-6 rows were re-checked unchanged and
Claude Sonnet 5.5 was added. GPT-6.1 Sol was verified and added on 2026-10-04. Claude Haiku 5.5, Gemini 4 Argon, and Gemini 3.8 Flash were added on 2026-10-16 and rechecked against their provider announcements on 2026-10-27. The price catalog stores these facts in
`ModelListing.ReleaseDate` and `ReleaseDateSource`.

A release date is the provider's initial public release or limited-preview
announcement for the model, including an initial rollout in ChatGPT or Codex.
It is not the training cutoff, the date embedded in a snapshot alias, a later
general-availability milestone, or a price period's `ValidFrom`. GPT-5.5 and
GPT-5.5 Pro illustrate the distinction: their initial rollout was April 23;
the catalog's public API pricing starts April 24. GPT-5.6 dates refer to the
initial limited preview, not its later broader rollout.

Sort known dates newest first and retain a stable model-name order for equal
dates. Unknown dates are nullable and belong after dated models; do not derive
them from model names or price history. Lifecycle status controls whether a
model is collapsed, independently of its release date.

| Model | Initial publication | Evidence |
| --- | --- | --- |
| Gemini 4 Carbon | `2026-10-28` | [Google announcement](https://blog.google/innovation-and-ai/models-and-research/gemini-models/introducing-gemini-4-carbon/) |
| GPT-6.1 Terra | `2026-10-26` | [OpenAI announcement](https://openai.com/index/introducing-gpt-6-1-terra/) |
| Gemini 4 Argon | `2026-10-13` | [Google API release notes](https://ai.google.dev/gemini-api/docs/changelog) and [Antigravity announcement](https://www.antigravity.google/blog/gemini-4-argon-in-google-antigravity) |
| Claude Haiku 5.5 | `2026-10-07` | [Anthropic release notes](https://platform.claude.com/docs/en/release-notes/overview#october-7-2026) |
| GPT-6.1 Sol | `2026-09-29` | [OpenAI API changelog](https://developers.openai.com/api/docs/changelog) |
| Claude Sonnet 5.5 | `2026-09-28` | [Provider source](https://platform.claude.com/docs/en/models/sonnet-5-5/overview) |
| Claude Opus 5.5 | `2026-09-22` | [Provider source](https://platform.claude.com/docs/en/models/opus-5-5/overview) |
| GPT-6 Luna | `2026-09-22` | [Provider source](https://developers.openai.com/api/docs/changelog) |
| GPT-6 Sol | `2026-09-22` | [Provider source](https://developers.openai.com/api/docs/changelog) |
| GPT-6 Astra | `2026-09-03` | [Provider source](https://openai.com/index/safety-overview-gpt-6-astra/) |
| Claude Fable 5.1 | `2026-09-01` | [Provider source](https://platform.claude.com/docs/en/models/fable-5-1/overview) |
| Gemini 3.8 Flash | `2026-09-01` | [Antigravity announcement](https://www.antigravity.google/blog/gemini-3-8-flash-in-google-antigravity); API GA followed September 2 |
| Claude Opus 5 | `2026-07-24` | [Provider source](https://platform.claude.com/docs/en/models/opus-5/overview) |
| Claude Sonnet 5 | `2026-06-30` | [Provider source](https://platform.claude.com/docs/en/models/sonnet-5/overview) |
| GPT-5.6 Terra | `2026-06-26` | [Provider source](https://openai.com/index/previewing-gpt-5-6-sol/) |
| GPT-5.6 Sol | `2026-06-26` | [Provider source](https://openai.com/index/previewing-gpt-5-6-sol/) |
| GPT-5.6 Luna | `2026-06-26` | [Provider source](https://openai.com/index/previewing-gpt-5-6-sol/) |
| Claude Fable 5 | `2026-06-09` | [Provider source](https://platform.claude.com/docs/en/models/fable-5/overview) |
| Claude Opus 4.8 | `2026-05-28` | [Provider source](https://platform.claude.com/docs/en/release-notes/overview#may-28-2026) |
| GPT-5.5 Cyber (Preview) | `2026-05-07` | [Provider source](https://openai.com/index/gpt-5-5-with-trusted-access-for-cyber/) |
| GPT-5.5 Pro | `2026-04-23` | [Provider source](https://openai.com/index/introducing-gpt-5-5/) |
| GPT-5.5 | `2026-04-23` | [Provider source](https://openai.com/index/introducing-gpt-5-5/) |
| Claude Opus 4.7 | `2026-04-16` | [Provider source](https://platform.claude.com/docs/en/release-notes/overview#april-16-2026) |
| GPT-5.4 Mini | `2026-03-17` | [Provider source](https://openai.com/index/introducing-gpt-5-4-mini-and-nano/) |
| Claude Sonnet 4.6 | `2026-02-17` | [Provider source](https://platform.claude.com/docs/en/models/sonnet-4-6/overview) |
| Claude Opus 4.6 | `2026-02-05` | [Provider source](https://platform.claude.com/docs/en/models/opus-4-6/overview) |
| Claude Opus 4.5 | `2025-11-24` | [Provider source](https://platform.claude.com/docs/en/models/opus-4-5/overview) |
| Claude Haiku 4.5 | `2025-10-15` | [Provider source](https://platform.claude.com/docs/en/models/haiku-4-5/overview) |
| Claude Sonnet 4.5 | `2025-09-29` | [Provider source](https://platform.claude.com/docs/en/release-notes/overview#september-29-2025) |
| GPT-5 Codex | `2025-09-15` | [Provider source](https://openai.com/index/introducing-upgrades-to-codex/) |
| GPT-5 | `2025-08-07` | [Provider source](https://openai.com/index/introducing-gpt-5/) |
| Claude Opus 4.1 | `2025-08-05` | [Provider source](https://platform.claude.com/docs/en/release-notes/overview#august-5-2025) |

## Cost Frontier release posts, 2026-11-01

The price catalog now stores a `releasePost` beside `releaseDate` and
`releaseDateSource`: direct lab title, URL, date, approximate flag, lab claim,
and availability. The explorer projects 18 announcements for current models
from those records; superseded posts remain in the append-only catalog.
A claim in a lab announcement is labelled as a claim, not a Token Economy
measurement. Carbon is restricted to selected cyber defenders and partners.
GPT-6.1 Terra's [OpenAI post](https://openai.com/index/introducing-gpt-6-1-terra/)
announces $1 input, $0.05 cache read and $5 output per million tokens.

Date conflicts remain visible: Anthropic's Fable 5.1 release notes and system
card support **1 September 2026**, while its newsroom page lists **15 September**.
The [Kimi K3.1 Pro post](https://www.kimi.com/en/blog/kimi-k3-1-pro) and
[MiMo-V2.6 page](https://mimo.xiaomi.com/mimo-v2-6) do not confirm a primary
publication date; their timeline dates are approximate. The
[Qwen3.8-Max page](https://qwen.ai/blog?id=qwen3.8) loaded only a generic Qwen
page in our browser check, so its 3 August date is also approximate and not a
confirmed `ReleaseDate`.
