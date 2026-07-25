# Agent Studio 30-card complexity backtest

Generated 2026-07-25 12:36:06 UTC from the live Agent Studio task API.

## Result

| Metric | Result |
|---|---:|
| Cards | 30 |
| Complexity-band accuracy | 13.3 % |
| Token median absolute percentage error | 78.6 % |
| Reissue mean absolute error | 1.971 |
| Token-cost Spearman rank correlation | 0.473 |

This is an observational leave-one-card-out backtest of the deterministic-plus-historical estimator. Each estimate was produced with the other 29 cards only.

## Cohort and measurements

The generator scanned 220 newest live and archived candidates to obtain 30 non-fixture cards with both prompt text and measured token entries. Cards are ordered by task last activity. Current lane is retained in the JSON audit rows but is not an eligibility condition: durable run metrics remain usable after a card is archived or moved between lanes.

- Actual tokens are the sum of input, output, cache-read, and cache-creation tokens for `agent:*` entries. For legacy cards without agent attribution, all token entries are used and this fallback is visible in the source data.
- Reissues are measured entry count minus one. This is a measurable attempt proxy, not a semantic classification of why a retry happened.
- Duration is the span between first and last measured token entries. Single-entry cards therefore have a zero-hour span; duration accuracy is not reported.
- Prompt bullet items and path-like strings are extracted as acceptance criteria and touched-surface hints. No post-run changed-file data is used as an input.
- Repository file count and dependency fan-out are unavailable from this API snapshot. The repository-size term is therefore absent; this cohort cannot validate whether it improves prediction.
- The cohort is recent rather than temporally held out, and historical routing is confounded with task difficulty. These figures are calibration evidence, not a causal model-comparison claim.

## Per-card evidence

| Card | Project | Type | Actual tokens | Reissues | Predicted tokens | Predicted reissues | Actual band | Estimated band | Confidence |
|---|---|---|---:|---:|---:|---:|---|---|---:|
| TE-7 | Token Economy | feature | 5,605,755 | 2 | 4,095,380 | 1.64 | critical | demanding | 65.0 % |
| CAC-12 | Coding Agent Chat | feature | 11,908,118 | 4 | 9,075,814 | 4.90 | critical | standard | 55.8 % |
| CAC-13 | Coding Agent Chat | bug | 8,623,827 | 1 | 53,821,307 | 5.46 | critical | standard | 51.2 % |
| CAC-15 | Coding Agent Chat | feature | 14,651,167 | 11 | 8,339,594 | 2.39 | critical | standard | 55.8 % |
| CAC-16 | Coding Agent Chat | bug | 26,460,121 | 5 | 47,200,403 | 3.48 | critical | standard | 51.2 % |
| TE-12 | Token Economy | feature | 4,501,308 | 6 | 3,759,092 | 0.97 | critical | standard | 65.0 % |
| TE-6 | Token Economy | feature | 886,074 | 0 | 4,538,044 | 2.24 | standard | standard | 65.0 % |
| TE-14 | Token Economy | feature | 7,325,857 | 0 | 3,325,902 | 2.17 | critical | standard | 65.0 % |
| TE-11 | Token Economy | feature | 1,360,276 | 1 | 4,463,154 | 2.08 | critical | standard | 65.0 % |
| AOW-8 | Agent Orchestrator Website | feature | 7,698,107 | 1 | 435,839 | 0.37 | critical | standard | 55.8 % |
| AOW-5 | Agent Orchestrator Website | feature | 282,219 | 0 | 10,431,460 | 0.98 | standard | standard | 65.0 % |
| AOW-2 | Agent Orchestrator Website | feature | 623,294 | 0 | 2,162,548 | 0.79 | standard | standard | 55.8 % |
| WEB-12 | Agent Studio Website | feature | 40,281,216 | 2 | 7,577,267 | 1.28 | critical | standard | 55.8 % |
| WEB-10 | Agent Studio Website | chore | 17,087,465 | 3 | 40,281,216 | 2.00 | critical | demanding | 46.6 % |
| AIP-11 | AI Patterns | feature | 3,426,835 | 2 | 2,706,945 | 1.55 | critical | standard | 65.0 % |
| AIP-6 | AI Patterns | feature | 3,413,821 | 2 | 3,934,692 | 1.99 | critical | standard | 65.0 % |
| AIP-3 | AI Patterns | feature | 10,041,637 | 1 | 5,288,095 | 3.00 | critical | demanding | 65.0 % |
| TE-13 | Token Economy | feature | 3,459,557 | 3 | 3,975,018 | 1.59 | critical | standard | 65.0 % |
| CAC-11 | Coding Agent Chat | feature | 2,274,918 | 2 | 12,434,887 | 5.50 | critical | demanding | 55.8 % |
| TE-10 | Token Economy | feature | 6,239,757 | 1 | 3,968,441 | 1.76 | critical | demanding | 65.0 % |
| CAC-6 | Coding Agent Chat | feature | 10,531,234 | 1 | 9,872,917 | 5.89 | critical | demanding | 55.8 % |
| AIP-9 | AI Patterns | feature | 2,089,870 | 0 | 5,286,397 | 3.00 | demanding | standard | 65.0 % |
| AIP-8 | AI Patterns | feature | 6,676,326 | 2 | 4,367,089 | 2.60 | critical | standard | 65.0 % |
| AIP-4 | AI Patterns | feature | 6,607,185 | 5 | 4,371,059 | 1.99 | critical | standard | 65.0 % |
| CAC-10 | Coding Agent Chat | bug | 86,293,270 | 6 | 18,244,854 | 3.16 | critical | demanding | 51.2 % |
| AIP-10 | AI Patterns | feature | 2,864,611 | 3 | 5,146,084 | 2.40 | critical | standard | 65.0 % |
| AIP-7 | AI Patterns | feature | 6,866,102 | 3 | 4,330,021 | 2.40 | critical | standard | 65.0 % |
| AOW-4 | Agent Orchestrator Website | feature | 379,080 | 1 | 2,360,170 | 0.26 | demanding | standard | 55.8 % |
| AIP-5 | AI Patterns | feature | 1,516,628 | 0 | 4,320,360 | 2.40 | demanding | standard | 65.0 % |
| AIP-2 | AI Patterns | feature | 764,798 | 0 | 3,401,928 | 1.79 | standard | standard | 65.0 % |

The adjacent JSON artifact contains titles, state, duration proxy, score, and neighbour keys for audit and machine consumption.
