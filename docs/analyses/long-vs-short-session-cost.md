# Token Cost Dynamics: Long Sessions vs. Short Sessions

**Analysis date:** 2026-07-25

**Question:** How do token costs develop in one long agent session compared with
the same work split across short sessions?

## Finding

A long session is not inherently cheaper or more expensive.

- Without prompt caching, retained history makes cumulative input grow
  quasi-quadratically with turn count. Short sessions grow linearly, but pay
  task reconstruction repeatedly.
- With a warm cache, the priced models in Token Economy charge cached input at
  10% of fresh input. This reduces the coefficient of the long-session curve
  by a factor of ten, but does not change its quadratic order.
- Cache writes and cache breaks matter. In the measured three-call Agent Studio
  orchestrator session below, cache writes were 77.1% of the theoretical
  list-price cost. Treating "cached" as simply "cheap" would miss most of the
  bill.
- There is no universal turn-count break-even. In the reduced model it is
  `2 * (short re-prime price / long history price) * (re-prime tokens / history
  added per turn)`. The required token measurements are workload-specific.
- Compaction has a visible additional sampling cost. It is economical only
  when enough same-task calls remain to repay that cost by reading a smaller
  context.

The practical decision is therefore a measured one: preserve a coherent warm
session while its re-priming savings exceed its retained-history cost; compact
when the expected remaining calls repay compaction; start a short session when
the topic changes or retained history is mostly irrelevant.

This is the cost layer adjacent to **AIP-13, Topic Switching / Long Context**
(in preparation in the
[AI Patterns repository](https://github.com/RobertMischke/ai-patterns.dev)). AIP-13
owns the quality side: topic coherence, warmth, repair density, and the risk of
carrying irrelevant state. This document does not duplicate that pattern. Cost
and quality are orthogonal signals.

## 1. Accounting Contract

Use four mutually exclusive token buckets:

| Symbol | Token bucket | Catalog field |
|---|---|---|
| `U` | Fresh, non-cached input | `Input` |
| `C` | Cache-read input | `CacheRead` |
| `W` | Cache-creation input | `CacheWrite` |
| `O` | Output, including billed reasoning output where applicable | `Output` |

For prices per million tokens `p_u`, `p_c`, `p_w`, and `p_o`:

```text
cost(U, C, W, O)
  = (p_u * U + p_c * C + p_w * W + p_o * O) / 1,000,000
```

This is the same component model implemented by
[`ModelPriceCatalog.ComputeCost`](../../src/TokenEconomy/ModelPriceCatalog.cs).
Anthropic confirms that total input is the sum of its fresh, cache-read, and
cache-creation fields in its
[prompt-caching usage contract](https://platform.claude.com/docs/en/build-with-claude/prompt-caching#tracking-cache-performance).

Provider schemas are not interchangeable. OpenAI's total input
(`input_tokens` or `prompt_tokens`) includes the subsets reported as
`cached_tokens` and, on GPT-5.6 and later, `cache_write_tokens`. A fully
componentized API record therefore uses
`U = max(0, totalInput - cachedTokens - cacheWriteTokens)`. The current Codex
CLI stream exposes cached input but not a cache-write field, so the local
benchmark adapter stores fresh input as
`input_tokens - cached_input_tokens`; see
[`Program.cs`](../../src/TokenEconomy.Benchmarks/Program.cs). Agent Studio's
current
[`CodexUsageParser`](https://github.com/agent-orc/agent-studio/blob/main/backend/Features/Cli/OutputParsing/Usage/CliUsageParser.cs)
maps total input and cached input into two fields, while its aggregate view adds
them. Consequently, the live Agent Studio Codex `totalTokens` figures and the
30-card report overcount cached Codex input. This analysis uses the component
fields and normalizes Codex fresh input as
`max(0, input - cacheRead - cacheWrite)` whenever it discusses cost. Raw
aggregate totals remain labelled as raw.

## 2. Context Growth

Let:

- `n` be the number of model calls;
- `P` be system, tool, repository, and task-brief context required by both
  strategies on every call;
- `R` be additional restart-only re-priming input after the first short
  session, such as a hand-off brief, memory retrieval, or repeated repository
  reinspection;
- `x` be the current-turn suffix that both strategies need; and
- `h` be the average retained history added after each completed turn.

For a constant-increment model, the input presented to turn `i` of a long
session is:

```text
I_long(i) = P + x + (i - 1)h
```

Its cumulative presented input is:

```text
T_long(n)
  = n(P + x) + h * n(n - 1) / 2
  = Theta(n^2) when h > 0
```

For `n` isolated sessions:

```text
T_short(n) = n(P + x) + (n - 1)R = Theta(n)
```

The common `P + x` terms do not decide the comparison. Without a price
difference between retained history and reconstruction:

```text
T_long(n) - T_short(n)
  = (n - 1) * (h*n/2 - R)

raw-token break-even: n_BE = 2R / h, for n > 1
```

Before that point, the long session saves more reconstruction than it replays
as history. After that point, accumulated history dominates. Real sessions have
variable `h_i`, tool results, and compactions, so production calculation should
sum the observed per-call buckets rather than fit a quadratic by assumption.
If a "re-prime" merely resends the same task brief already included in `P`, its
incremental `R` is zero and short sessions are cheaper in raw input from the
second call. The long-session advantage then has to come from cache pricing,
avoided setup calls, output, latency, or quality rather than token count.

## 3. Prompt Caching Changes the Coefficient

### Catalog prices

The following values are from Token Economy's dated
[`model-prices.json`](../../src/TokenEconomy/catalog/model-prices.json), resolved
at 2026-07-25 UTC. Dollar amounts are list prices per million tokens.

| Catalog model(s) | Fresh input | Cache read | 5-minute cache write | Output | Read / fresh | Re-cache penalty (`write - read`) |
|---|---:|---:|---:|---:|---:|---:|
| `claude-fable-5`, `claude-mythos-5` | $10.00 | $1.00 | $12.50 | $50.00 | 0.10 | $11.50 / MTok |
| `claude-sonnet-5`, through 2026-08-31 | $2.00 | $0.20 | $2.50 | $10.00 | 0.10 | $2.30 / MTok |
| `claude-opus-4-8`, `-4-7`, `-4-6` | $5.00 | $0.50 | $6.25 | $25.00 | 0.10 | $5.75 / MTok |
| `claude-opus-4-5` | $5.00 | $0.50 | $6.25 | $25.00 | 0.10 | $5.75 / MTok |
| `claude-sonnet-4-6` | $3.00 | $0.30 | $3.75 | $15.00 | 0.10 | $3.45 / MTok |
| `claude-sonnet-4-5` | $3.00 | $0.30 | $3.75 | $15.00 | 0.10 | $3.45 / MTok |
| `claude-haiku-4-5` | $1.00 | $0.10 | $1.25 | $5.00 | 0.10 | $1.15 / MTok |

The catalog marks the Opus 4.5 and Sonnet 4.5 rates unconfirmed. It has no
price valid on the analysis date for `claude-opus-4-1`, `gpt-5.6`, `gpt-5.5`,
`gpt-5`, or `gpt-5-codex`; their cost must remain unknown, not zero. Sonnet 5's
absolute prices rise on 2026-09-01, but the read/fresh and write/fresh ratios
remain 0.10 and 1.25.

The catalog ratios match Anthropic's
[published prompt-caching prices](https://platform.claude.com/docs/en/build-with-claude/prompt-caching#pricing):
a 5-minute write is 1.25 times fresh input, a 1-hour write is 2 times fresh
input, and a hit is 0.1 times fresh input.

### Cache-aware break-even

Let `p_H` be the effective price of processing retained long-session history,
`p_R` the effective price of reconstructing a short session, and `p_M` the
price applied on a long-history cache miss. A plain miss uses `p_M = p_u`;
automatic cache creation or re-creation can instead use `p_M = p_w`. If a
token-weighted fraction `q` of long history hits cache:

```text
p_H = q*p_c + (1 - q)*p_M, where p_M is p_u or p_w
```

After accounting for cache-write tokens through `p_M`, and ignoring equal
output and current-turn costs, the continuous price-aware equality is:

```text
n_BE = 2 * (p_R / p_H) * (R / h)

retained-history size at equality:
H_BE = (n_BE - 1)h = 2 * (p_R / p_H)R - h
```

For integer calls, the first call at which long-session retained-history cost
is greater is `max(2, floor(n_BE) + 1)`. The effective input size at the
continuous crossing is `P + x + H_BE`.

All priced models in the current catalog have the same cache ratios, so the
reduced break-even multiplier is the same for every priced tier:

| Regime | `p_R / p_H` | Reduced break-even |
|---|---:|---:|
| Both reconstruction and retained history are fresh | 1 | `2R/h` |
| Both are cache reads | 1 | `2R/h` |
| Long history hits, short task reconstruction is fresh | 10 | `20R/h` |
| Long history is re-cached at 1.25x, reconstruction is fresh | 0.8 | `1.6R/h` |
| Long history misses, reusable short prefix hits | 0.1 | `0.2R/h` |
| Long history is re-cached at 1.25x, reusable short prefix hits | 0.08 | `0.16R/h` |

Absolute dollar cost still scales by model tier. Output differences, cache
writes, and repairs can also move the actual crossing. The full calculation
must therefore evaluate each observed `(U_i, C_i, W_i, O_i)` tuple and select
the smallest `n` for which cumulative `cost_long(n) > cost_short(n)`.

### AHP-style cost-curve view

```text
  cumulative
  token cost
      ^
      |                                      long session
      |                                  _.-'  retained history:
      |                              _.-'      quadratic term at p_H
      |                          _.-'
      |                      _.-X  break-even n_BE
      |                  _.-'  /
      |              _.-'     /  n short sessions
      |          _.-'        /   repeated R: linear at p_R
      |      _.-'           /
      |  _.-'              /
      +-------------------------------> model calls n
                         n_BE = 2(p_R/p_H)(R/h)

  Warm-cache envelope: use p_H = p_c.
  Cold or broken-cache envelope: use p_H = p_u or p_w, as actually reported.
  Conceptual shape only; the crossing is parameterized, not a measured constant.
```

### TTL and cache breaks

Anthropic's default TTL is five minutes and each hit refreshes it at no
additional write cost. A one-hour TTL is available at the higher 2 times input
write rate. Exact prefix identity matters; tool, image, thinking, and
configuration changes can invalidate cache segments. Anthropic explicitly
recommends a separate breakpoint after the system prompt so compaction only
rewrites the summary rather than the system prefix. See the
[TTL and invalidation documentation](https://platform.claude.com/docs/en/build-with-claude/prompt-caching#how-prompt-caching-works).

For one million previously cached tokens, a 5-minute re-cache costs the
`write - read` penalty in the catalog table. A cold non-cached read costs
`p_u - p_c` more than a hit. Long pauses therefore do not merely remove a
discount; if the prefix is cached again they can replace a 0.1 times read with
a 1.25 times write.

OpenAI caching is automatic for eligible prefixes of at least 1,024 tokens.
Its current documentation says pre-5.6 in-memory entries usually survive 5 to
10 minutes of inactivity, up to one hour; GPT-5.6 and later use at least a
30-minute TTL and charge 1.25 times input for cache writes. These facts explain
the observed Codex usage fields, but Token Economy intentionally has no
published OpenAI dollar prices in the current catalog, so this analysis does
not manufacture a dollar cost for them. See OpenAI's
[prompt-caching guide](https://developers.openai.com/api/docs/guides/prompt-caching).

## 4. Compaction Cost and Payback

Compaction is not free deletion. Anthropic's server-side compaction performs an
additional sampling iteration to summarize the old context. The provider
states that it contributes to billing and rate limits, and that consumers must
sum the `usage.iterations` array because top-level usage excludes the
compaction iteration. See
[Understanding usage for compaction](https://platform.claude.com/docs/en/build-with-claude/compaction#understanding-usage).

For a measured compaction iteration:

```text
C_comp = cost(U_comp, C_comp_read, W_comp, O_summary)
```

Let:

- `X` be effective context immediately before compaction;
- `S` be the summary context retained afterwards;
- `D = X - S` be discarded context;
- `p_H` be the expected future read price for those tokens;
- `C_rewrite` be a summary cache write not already present in the compaction
  iteration; and
- `m` be the number of later same-session calls.

Then:

```text
saving per later call = D * p_H / 1,000,000

m_BE
  = ceil((C_comp + C_rewrite)
         / (D * p_H / 1,000,000))
```

Compact for cost only when expected remaining same-task calls are at least
`m_BE`. A quality-driven compaction may still be justified earlier under
AIP-13.

The catalog has `p_c = 0.1p_u`, `p_w = 1.25p_u`, and `p_o = 5p_u` for every
priced tier. Under the deliberately reduced boundary where the entire
pre-compaction context is a cache read, the summary is the only output, and
the summary is written to a 5-minute cache:

```text
m_BE
  = ceil((0.1X + 5S + 1.25S) / (0.1(X - S)))
  = ceil((X + 62.5S) / (X - S))
```

This is a sensitivity equation, not an empirical summary-size claim. Use the
actual compaction iteration and subsequent cache-write fields in production.
It also shows why concise summaries matter: output is expensive relative to a
warm cache read.

## 5. Empirical Evidence

The requested telemetry sources do not have equal reproducibility. The
orchestrator and `support:adhoc` cohorts below were queried from the live Agent
Studio bus. Their endpoint, filter, observation time, bounded result count, and
stable event anchors are reported, but the raw responses are not checked into
this repository. The Codex one-shots and 30-card aggregate are checked-in
artifacts. No `.quality/usage` artifact exists in this Token Economy checkout,
so it is not silently treated as evidence. Reproducing the live cohorts
requires a new snapshot and may produce different rows as the bounded bus
window advances. The queries and all derived totals were re-run successfully
at **2026-07-25 14:25:07 UTC**.

### 5.1 Long-lived orchestrator session

Read-only Agent Studio telemetry was queried at
`GET /api/bus/Agent%20Studio/messages?participantId=orchestrator%3AAgent%20Studio&kind=token-usage&limit=1000`
and
`GET /api/runner/Agent%20Studio/orchestrator-session`. The bus returned 20
events. The selected session is the real `claude-haiku-4-5` session
`18b22f45-7e98-4ce2-b8f5-7d6c1bbb7bf5`; filtering by its reported
`bootedAt..lastUsedAt` interval selected one boot and two later steering turns.
Their stable event IDs are `019f4ee0eb5d79bab1a6253a765af56f`,
`019f50f134ba7da0a4f504615b78e053`, and
`019f52bb412d73b8af4a22a71b4dd518`. The read contract is implemented by Agent
Studio's
[`BusEndpoints`](https://github.com/agent-orc/agent-studio/blob/main/backend/Features/Bus/BusEndpoints.cs).

| Turn | Completed UTC | Gap | Fresh input | Cache read | Cache write | Output | List-price cost |
|---:|---|---:|---:|---:|---:|---:|---:|
| 1 | 2026-07-11 01:53:11 | first | 9 | 23,015 | 10,526 | 418 | $0.017558 |
| 2 | 2026-07-11 11:30:12 | 9 h 37 m | 10 | 23,015 | 11,362 | 531 | $0.019169 |
| 3 | 2026-07-11 19:50:31 | 8 h 20 m | 10 | 23,015 | 12,175 | 190 | $0.018480 |
| **Total** | 18-hour span | 3 calls | **29** | **69,045** | **34,063** | **1,139** | **$0.055207** |

The cost uses the repository catalog's Haiku 4.5 rates and is a theoretical API
list-price comparison, not the CLI subscription bill.

Observed implications:

- The stable 23,015-token prefix was read from cache on every call, even though
  each inter-turn gap exceeded the default five-minute TTL. The trace cannot
  prove whether another workload refreshed a shared prefix or a longer
  retention policy was active.
- Cache creation grew from 10,526 to 12,175 tokens, a net 1,649-token increase
  across two follow-ups. The long session preserved continuity but accumulated
  more conversation state.
- Cache writes cost $0.042579, or 77.1% of the session's list-price total.
  Output variability kept per-turn total cost from rising monotonically even
  while cache-write volume did rise.
- With only three turns, this trace supports the mechanism, not a fitted
  quadratic curve.

### 5.2 Short card-associated calls

The same read-only bus returned the latest 5,000 workspace ad-hoc events from
`GET /api/bus/_workspace/messages?participantId=support%3Aadhoc&kind=token-usage&limit=5000`.
Filtering that bounded slice by `topic=summary-generation` produced 713
independent calls, fired after card runs, all on `claude-haiku-4-5`, from
2026-06-23 through 2026-07-25. The first and last selected event IDs in
timestamp order are `019ef6a8c0df7d3d9217338bb22ffe51` and
`019f9961b01470c4934cd31dbe87b483`.

| Cohort | Calls | Fresh input | Cache read | Cache write | Output | List-price total | Mean / call |
|---|---:|---:|---:|---:|---:|---:|---:|
| Card summary one-shots | 713 | 6,333 | 16,065,701 | 20,161,071 | 837,435 | $31.001417 | $0.043480 |
| Long-lived orchestrator | 3 | 29 | 69,045 | 34,063 | 1,139 | $0.055207 | $0.018402 |

The short cohort averaged 22,533 cache-read, 28,276 cache-write, and 1,175
output tokens per call. The long session averaged 23,015 cache-read, 11,354
cache-write, and 380 output tokens per call.

This is the requested real long-session versus card-short-run comparison, but
it is **not causal**. The prompts and outputs differ: a summary call is expected
to generate more text, while an orchestrator steering call makes a bounded
decision. The lower observed long-session mean cannot be attributed solely to
session reuse. What the same-model comparison does establish is that
cache-write and output volume, not fresh input, dominated both workloads.

The unbounded lifetime ad-hoc aggregate contained 2,094 summary-generation
calls, but it did not expose model by source. The 713-call same-model slice is
used so the price calculation remains identified and reproducible.

### 5.3 Codex one-shot and card aggregates

The checked-in
[`palindrome-repair` result](../../benchmarks/results/palindrome-repair/20260722T233105307Z.json)
contains two successful, no-session-persistence Codex runs:

| Run | Model | Fresh input | Cache read | Total input processed | Cache share | Output |
|---|---|---:|---:|---:|---:|---:|
| `terra-medium` | `gpt-5.6-terra` | 3,035 | 10,496 | 13,531 | 77.6% | 137 |
| `sol-medium` | `gpt-5.6-sol` | 3,339 | 9,984 | 13,323 | 74.9% | 227 |
| **Combined** | 2 runs | **6,374** | **20,480** | **26,854** | **76.3%** | **364** |

Even isolated one-shots reused a large common prefix. "Short session" does not
mean "zero caching." The models are unpriced in the catalog, so no dollar
amount is reported.

The live
[`Agent Studio 30-card backtest`](../../results/complexity-backtest/agent-studio-30-card-backtest.md)
contains 30 cards, 98 recorded token entries, and a raw reported total of
300,740,433 tokens. Seven single-entry cards have a median raw total of
886,074; 23 multi-entry cards have a median card total of 6,676,326. This
supports the conclusion that retries and multi-entry histories are material,
but it is not a session-continuity experiment. Its summation contract also
double-counts cached Codex input as described in Section 1, so those raw totals
must not be converted to dollars.

## 6. Decision Aid

Use a long session when:

- the task remains on one coherent topic;
- `R/h` is high, meaning reconstruction is large relative to new retained
  history;
- the token-weighted cache-hit rate stays high;
- pauses remain inside the effective TTL, or a stable shared prefix is known to
  stay warm; and
- expected remaining calls are below the measured break-even or compaction can
  reset the curve economically.

Prefer short sessions when:

- the topic changes and most retained context would be irrelevant;
- a small durable brief reconstructs state cheaply;
- long pauses or prefix mutations repeatedly turn reads into cache writes;
- the short-session stable prefix also receives cache hits; or
- AIP-13 shows rising repair density, confusion, or quality loss.

Compact when:

- the discarded token count and actual summary iteration are measured;
- expected future same-task calls meet `m_BE`; and
- the summary preserves the decisions, evidence, and unresolved state that
  AIP-13 requires.

Do not select a weaker model solely to reduce this cost. Agent Studio's
[model-routing policy](https://github.com/agent-orc/agent-studio/blob/main/docs/system/domains/model-routing-policy.md)
requires the cheapest tier that clears the correctness floor and explicitly
forbids quota or cost from lowering a hard floor. This analysis changes session
shape, not that routing rule.

## 7. Minimum Production Calculator

For every session strategy candidate:

1. Preserve provider-native raw usage and normalize it into mutually exclusive
   `U`, `C`, `W`, and `O`.
2. Record `session_id`, `task_id`, turn index, timestamp, model, cache policy,
   compaction marker, and terminal outcome.
3. Estimate `R` from the measured brief/system/task state loaded into a new
   session, not from prompt characters.
4. Estimate each `h_i` from the increase in effective conversation context
   between calls.
5. Compute actual cumulative list-price cost from the dated Token Economy
   catalog. Leave unknown models unpriced.
6. Evaluate long, short, and compacted recurrences over the expected remaining
   calls. Report the first crossing as a range when cache-hit probability or
   future turn count is uncertain.
7. Present cost beside AIP-13 quality and repair signals, never as a standalone
   routing verdict.

## 8. Limitations

- The long-session sample is `N = 1` session and three turns. The short
  same-model sample is large (`N = 713`) but comes from a different prompt
  role. No causal effect size is claimed.
- The long trace spans 18 hours but lacks the cache policy and cross-workload
  prefix-refresh history, so its cache hits cannot be attributed to TTL
  behavior alone.
- The available Agent Studio surfaces expose terminal token events, not every
  internal model sampling iteration of the coding run. They cannot reconstruct
  `h_i` for the long Codex run or detect client-side compaction reliably.
- The raw live-bus responses used for the orchestrator and ad-hoc cohorts are
  not checked in, and this checkout contains no `.quality/usage` artifact.
  Their query contracts are documented, but only the Codex one-shots and
  30-card aggregate are replayable from repository artifacts.
- The 30-card artifact is recent, observational, route-confounded, and uses a
  raw total that double-counts cached Codex input.
- The dollar figures are estimated API list prices. Agent Studio uses CLI
  subscriptions, so they are comparison values rather than invoices or quota
  consumption.
- All priced catalog entries currently share the same 0.1 cache-read and 1.25
  five-minute-write ratios. The reduced model therefore cannot demonstrate a
  cross-model break-even difference beyond absolute price scaling. OpenAI
  models remain unpriced.
- Outputs, reasoning, repairs, tool results, latency, rate limits, and quality
  can dominate the choice. Holding output equal in the closed-form model is a
  simplifying boundary, not an empirical claim.
- A direct controlled test is still needed: identical task and base revision,
  randomized long versus short strategy, at least several repetitions, exact
  cache policy, per-turn normalized usage, explicit compaction events, and the
  same acceptance gate.

## Conclusion

The cost argument does not support a dogma of either permanent warm sessions or
episodic one-shots. Long sessions exchange repeated reconstruction for a
quadratic retained-history term. Prompt caching makes that exchange attractive
for much longer, but cache writes, pauses, compaction, and output can reverse
the result. Measure the terms, calculate the crossing, and combine it with
AIP-13's quality evidence.
