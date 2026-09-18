# Long-context cost explorer — findings, 18 September 2026

The explorer is at `website/context-cost/`. It compares session shape at dated API list prices; it does not change or recommend model routing. Prices and quota must never lower the correctness-risk floors in [the routing policy](system/domains/model-routing-policy.md).

The available records do **not** identify a statistically typical session in all three requested workloads. The figures below are reproducible workload-scale forecasts, with unchanged quality and output assumed across models. They must not be presented as observed successful-task costs or subscription invoices.

## Data and scope

- **Coding, N=2 isolated runs**: the retained palindrome fixture has Sol/medium input 3,339 fresh + 9,984 cached and output 227. The preset uses 13,323 starting/reconstructed tokens, 3,339 tool/file tokens per later turn, 227 output, 4 turns/task and 12 tasks. Growth and task structure are extrapolations, not measured repeated-task trajectories. [Raw fixture](../benchmarks/results/palindrome-repair/20260722T233105307Z.json).
- **Orchestrator, N=1 session / 3 calls**: 33,550 initial input, 23,015 stable cached prefix, write growth 1,649 over two follow-ups, and mean output ≈380. The forecast rounds growth to 825, assigns 445 to tools and 380 to output, and extends to 60 decisions at one-minute request-start intervals. Actual observed gaps were about 577 and 500 minutes; they did not prevent stable-prefix hits, so TTL cannot be inferred from this trace.
- **General topics, N=0 matched sessions**: N=713 card-summary calls supply a workload-size proxy (22,533 cached + 28,276 write + 9 fresh tokens; 1,175 output). The preset uses 50,818 starting tokens, 28,276 later tool tokens and 12 one-turn topics. Topic switching and growth are assumptions. This cohort cannot establish general-task behavior.
- The orchestrator and summary figures are retained in the [earlier local analysis](analyses/long-vs-short-session-cost.md); the underlying live bus responses and ad-hoc ledger are not in this checkout. The 30-card backtest has 98 token entries, but its raw totals double-count cached Codex input and are not used to fit these scenarios. No new live telemetry was available.
- **AGT-2840, N=1 operator report**: 26.6M reads, 0.31M writes, 0.12M output and 406 fresh tokens reprice to **$18.23953** on Opus 5, matching the reported $18.24 after rounding. Reads contribute $13.30 (72.9%). No turn count, duration or raw run is supplied; it cannot identify initial context or growth.

All base scenarios start cold, reuse an identical shared prefix across fresh sessions, have one-minute request-start gaps, and have no compaction or separately added reasoning budget. Output is the reported token field; extra reasoning is an optional user input. Fresh sessions reset every task. Provider cache minima are respected.

## Dated prices

USD per million tokens, selected at 2026-09-18 from `src/TokenEconomy/catalog/model-prices.json`, verified there on 2026-09-12. No prices were changed by this card.

| Model | Input | Cache read | Cache write | Output | Valid from |
|---|---:|---:|---:|---:|---|
| Claude Opus 5 | $5 | $0.5 | $6.25 | $25 | 2026-07-24 |
| Claude Sonnet 5 | $2 | $0.2 | $2.5 | $10 | 2026-06-30 |
| GPT-5.6 Sol | $4 | $0.4 | $5 | $20 | 2026-08-21 |
| GPT-6 Astra | $10 | $1 | $12.5 | $50 | 2026-09-03 |

Claude uses the 5-minute write tariff; choosing its 1-hour policy changes writes to 2× input. GPT-5.6+ uses a 30-minute minimum retention policy; expiry at exactly 30 minutes is a conservative simulation, not a claim that real entries are always evicted then. Zero TTL disables caching; unknown TTL or missing required rates produce an unavailable result. Custom TTL/write rates are explicitly labeled assumptions. [Claude caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching), [OpenAI caching](https://developers.openai.com/api/docs/guides/prompt-caching).

## Repeated programming tasks

12 tasks, 48 calls. At these small reconstruction costs and reusable prefix, fresh sessions win from completed **task 2**. At fixed 12 tasks, varying both starting and fresh reconstruction size together while capping the shared prefix at 9,984 gives the first whole-token boundary at **43,319 tokens**: above it the long strategy is cheaper. This is a context-size sensitivity result, not an observed threshold. Practical rule: keep a session only while its avoided reconstruction exceeds repeated history cost; measure how much of a fresh prompt already hits cache.

| Model | Long USD | Fresh USD | Long USD / task | Long with 10m task gaps | Long with 60m every-call gaps |
|---|---:|---:|---:|---:|---:|
| Claude Opus 5 | $3.6437 | $1.7465 | $0.3036 | $9.6740 | $29.4096 |
| Claude Sonnet 5 | $1.4575 | $0.6986 | $0.1215 | $3.8696 | $11.7638 |
| GPT-5.6 Sol | $2.9150 | $1.3972 | $0.2429 | $2.9150 | $23.5277 |
| GPT-6 Astra | $7.2874 | $3.4930 | $0.6073 | $7.2874 | $58.8192 |

Ten-minute pauses expire the modeled Claude 5-minute cache, but stay inside the OpenAI minimum retention. The 60-minute column assumes every request rewrites the whole context. Shared prefixes refreshed by other traffic can make real costs lower. Warm break-even counts coincide because all four selected models share the same read/write/output price ratios.

## Orchestrator decisions

60 one-turn decisions. Warm reuse beats a fresh session per decision. Extending the same uncompacted recurrence, the **next individual decision** first costs more than a fresh one at **turn 137**, at 145,750 context tokens; the cumulative long-versus-always-fresh crossover is **task 271**. These long horizons are mathematical extrapolations, not context-window or quality guarantees. For an ongoing session, sunk costs do not determine whether to restart: compare future cost and handoff quality. Compacting at 50,325 tokens to 24,000, with a 2,000-token summary, first triggers on turn 22 and repays its incremental cost by turn 37 (15 later calls). Do not compact for cost alone if fewer calls remain, or if a summary loses required state.

| Model | Long USD | Fresh USD | Long USD / task | Long with 10m task gaps | Long with 60m every-call gaps |
|---|---:|---:|---:|---:|---:|
| Claude Opus 5 | $2.7794 | $5.3434 | $0.0463 | $22.2778 | $22.2778 |
| Claude Sonnet 5 | $1.1118 | $2.1374 | $0.0185 | $8.9111 | $8.9111 |
| GPT-5.6 Sol | $2.2235 | $4.2747 | $0.0371 | $2.2235 | $17.8223 |
| GPT-6 Astra | $5.5588 | $10.6868 | $0.0926 | $5.5588 | $44.5556 |

Compacted totals over 60 calls (same order): Claude Opus 5 **$2.5896**, Claude Sonnet 5 **$1.0358**, GPT-5.6 Sol **$2.0717**, GPT-6 Astra **$5.1792**.

Ten-minute pauses expire the modeled Claude 5-minute cache, but stay inside the OpenAI minimum retention. The 60-minute column assumes every request rewrites the whole context. Shared prefixes refreshed by other traffic can make real costs lower. Warm break-even counts coincide because all four selected models share the same read/write/output price ratios.

## Switching between general tasks

12 one-turn topics. With the summary-call size proxy, retained unrelated history makes the long strategy costlier at **task 2**. The fixed-horizon context boundary is **67,350 tokens**, with shared prefix capped at 22,533. Practical rule: separate independent topics when a compact brief and shared system prefix suffice. Repeated reconstruction, useful cross-topic state or lower restart success could reverse the result.

| Model | Long USD | Fresh USD | Long USD / task | Long with 10m task gaps | Long with 60m every-call gaps |
|---|---:|---:|---:|---:|---:|
| Claude Opus 5 | $3.7843 | $2.7386 | $0.3154 | $16.3124 | $16.3124 |
| Claude Sonnet 5 | $1.5137 | $1.0955 | $0.1261 | $6.5250 | $6.5250 |
| GPT-5.6 Sol | $3.0274 | $2.1909 | $0.2523 | $3.0274 | $13.0499 |
| GPT-6 Astra | $7.5685 | $5.4773 | $0.6307 | $7.5685 | $32.6248 |

Ten-minute pauses expire the modeled Claude 5-minute cache, but stay inside the OpenAI minimum retention. The 60-minute column assumes every request rewrites the whole context. Shared prefixes refreshed by other traffic can make real costs lower. Warm break-even counts coincide because all four selected models share the same read/write/output price ratios.

## What the studies do and do not establish

The dated [evidence catalogue](../src/TokenEconomy/catalog/context-cost-evidence.json) stores URLs, retrieval dates, excerpts, confidence, sample conditions and limitations. The site links controls to these records. Its [schema](../src/TokenEconomy/catalog/context-cost-evidence.schema.json) describes the data contract.

- [Requesty, The Coding Agent Economy (May 2026)](https://www.requesty.ai/papers/coding-agents/The-Coding-Agent-Economy.pdf) reports production gateway observations across nine coding agents, May 2025–April 2026: 86% platform cache-hit input, 92% for Claude Code and an 84k mean Claude Code prompt. This supports the importance of cache economics, not a universal local hit rate or causal success benefit. No approximate cost multiplier from the paper is used in the engine.
- [Lost in the Middle (2023)](https://arxiv.org/abs/2307.03172) examines position effects in multi-document QA and key-value retrieval. [RULER (2024)](https://arxiv.org/abs/2404.06654) evaluates 17 models on 13 synthetic tasks including aggregation and multi-hop tracing. Neither evaluates the four models here as coding agents or supplies a transferable success probability.
- [Chroma Context Rot](https://www.trychroma.com/research/context-rot) varies length while holding task complexity constant across 18 models and studies distractors. It motivates measuring quality, not an invented linear failure penalty.
- [Factory compression evaluation (16 December 2025)](https://factory.com/news/evaluating-compression) uses more than 36,000 production messages, four probes per compression point and a GPT-5.2 judge on six dimensions. Overall scores of 3.70, 3.44 and 3.35 are probe-quality scores, not task completion rates. Vendor self-evaluation and artifact-retention weaknesses limit generalization.
- [Claude compaction documentation](https://platform.claude.com/docs/en/build-with-claude/compaction) establishes billable additional sampling; it does not guarantee preservation of every required detail.
- [OpenAI reasoning documentation](https://developers.openai.com/api/docs/guides/reasoning) bills hidden reasoning as output and describes model-dependent retention. It does not provide a universal tokens-per-effort conversion. The level switch therefore loads user-owned extra-token assumptions; it does not silently multiply tokens or relax a routing floor.

Cost per **successful** task is unavailable without comparable outcome data. Optional separate long/fresh success probabilities show `cost / tasks / probability`, with equal-cost independent attempts assumed. No study score is converted into a success probability. Subscription quota share likewise remains **unknown** unless the user supplies a measured percentage-point-per-API-USD conversion for the particular model, plan, workload and quota window. A plan's monthly fee is not such a conversion.

## Cost recurrence and API

`ContextSessionCost.Forecast(options, catalog)` is pure and dependency-free. `StartUtc` is UTC. Counts are disjoint; negative counts and malformed gap vectors are rejected. A turn has context `C`, previously cached prefix `K`, fresh suffix `U` and billed output `O`. With an eligible live cache, `read=min(C,K)` and `write=C-read`. When disabled or below minimum, all `C+U` tokens are uncached. Reads refresh the TTL. At `gap >= TTL`, cached history is rewritten. The next context is `C + U + tools + visible output + retained reasoning`; reasoning billed without retention does not grow history. All four costs are count × dated rate / 1,000,000. The dated catalogue is resolved at each call, including compaction, so price boundaries within a session are honored.

A restart reconstructs `RestartContext` and may reuse only `SharedPrefixTokens` while warm. A threshold compaction first bills an additional full-context call and `CompactionOutputTokens`, then sets `CompactedContext`, invalidates the old prefix and bills its rewrite on the normal call. This deliberately conservative assumption can overstate costs when an implementation preserves a stable prefix. Compaction duration advances the call timestamp. In the result, `Usage` combines normal and compaction tokens, `Cost` is the ordinary call, and `CompactionCost` is separate; `CumulativeCost` includes both.

`FirstMoreExpensiveTask` returns the first strict long-cost excess at a completed-task boundary within the supplied horizon. Null means no crossing found or costs unknown, not "never". `PaybackTurns` gives a warm-cache bound from upfront cost and discarded tokens. A user must supply the incremental upfront cost relative to continuing, including any rewrite; it is not a quality recommendation.

The browser's module mirrors the recurrence. A deterministic report tool produces 108 C# cases (48 preset/strategy cases and 60 varied cases), and the browser smoke script compares per-turn contexts, usage categories, cost components, cumulative cost and totals. Prices are loaded from the existing checked website projection. Evidence/presets are projected by the same generator, which checks source references and missing provenance.

## Reproduce and review

```sh
dotnet test -c Release
python3 scripts/generate-website-data.py --check
python3 scripts/check-website-links.py
JOB_RESULTS_DIR=/absolute/path/to/results node scripts/test-context-cost.mjs
```

The smoke script requires Node 22, .NET 10 and Chromium (`CHROME_PATH` if not found). It uses a local HTTP server and the Chrome DevTools protocol, so no npm dependencies or external app services are needed. It tests three presets, four models, reasoning controls, quality/quota inputs, unknown prices/TTL, one-hour eligibility, custom gap validation, CSV/JSON downloads, and overflow at 390 and 1440 pixels in light and dark themes. It writes source-linked forecast JSON, all model findings and full-page plus viewport screenshots to the explicit results directory.

Limitations: this is a standard text-price sensitivity engine, not a provider eligibility/window simulator. It does not infer context premiums, cache routing misses, concurrent refresh traffic, hidden prompt sizes, success rates, retry costs or quota weights. Beyond a provider's supported context window, curves are mathematical extrapolations. Mixed cache TTL segments and partial-prefix-preserving compaction require measured usage or more detailed inputs in a future extension. Unknown values are not zero-dollar claims.
