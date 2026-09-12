# Model publication dates

Verified against provider documentation on 2026-09-12. The price catalog stores
these facts in `ModelListing.ReleaseDate` and `ReleaseDateSource`.

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
| GPT-6 Astra | `2026-09-03` | [Provider source](https://openai.com/index/safety-overview-gpt-6-astra/) |
| Claude Fable 5.1 | `2026-09-01` | [Provider source](https://platform.claude.com/docs/en/models/fable-5-1/overview) |
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
