# Model Routing Policy

Version: 2026-09-25

Status: GPT-6 operator baseline, effective 2026-09-25; local completion evidence remains provisional

Owner: Pipeline and CLI domains

This page is the authoritative answer to two questions:

1. Which model and thinking level should perform a task or bounded pipeline
   decision?
2. Why is that route proportionate to the task's correctness risk, expected
   scope, context demand, available quota, and observed outcomes?

The policy selects the cheapest tier that clears the required capability floor.
It does not claim that a larger model repairs a vague task, a broken gate, or
missing evidence. Explicit operator pins still win, but the UI or orchestrator
should explain when a pin is below the policy floor.

## September 25 decision and prices

New fleet cards prefer GPT-6 Sol from 2026-09-25. This is an **operator decision
backed by dated prices and vendor claims, not yet by a local completion cohort**.
GPT-6 Luna replaces GPT-5.6 Luna for mechanical work; GPT-6 Sol replaces both
Sol routes. Historical GPT-5.6 outcomes remain attributed to GPT-5.6.

Prices below are standard USD per million input / cached-input / output tokens,
as of 2026-09-25. They describe equal token volumes, not measured cost per card.

| Tier / score | Before | After | Before price | After price | Delta input / cached / output |
|---|---|---|---|---|---|
| `luna-medium`, 0–20 | gpt-5.6-luna / medium | gpt-6-luna / medium | 0.20 / 0.02 / 1.20 | 0.10 / 0.01 / 0.50 | −50% / −50% / −58.33% |
| `terra-medium`, 21–50 | gpt-5.6-terra / medium | gpt-5.6-terra / medium | 2 / 0.20 / 12 | 2 / 0.20 / 12 | 0% / 0% / 0% |
| `sol-medium`, 51–69 | gpt-5.6-sol / medium | gpt-6-sol / medium | 4 / 0.40 / 20 | 2 / 0.20 / 10 | −50% / −50% / −50% |
| `sol-xhigh`, 70–100 | gpt-5.6-sol / xhigh | gpt-6-sol / xhigh | 4 / 0.40 / 20 | 2 / 0.20 / 10 | −50% / −50% / −50% |

**Retain Terra for this revision.** There is no GPT-6 Terra. Its June 26 price
remains dated in the catalogue. It is no longer a cheaper alternative to Sol:
input/cache rates match and $12 output is 20% above Sol's $10. At a reference
10,000 uncached input plus 1,000 output tokens, Luna changes $0.0032 → $0.0015
(−53.125%), Terra stays $0.032, and either Sol tier changes $0.060 → $0.030
(−50%). This mix is an illustration, not a measured workload.

Retaining Terra preserves four score bands and a substantive one-tier semantic
reissue step while the new class priors prefer Sol for new work. A collapsed
Luna/Sol/Astra ladder would introduce Astra at $10 / $1 / $50, five times Sol's
token rates ($0.150 for that reference mix), without a validated cohort and
with access errors. That is not justified now. Review Terra's removal after
the cohort below; do not describe it as the price sweet spot. Scored tasks may
still use Terra, and its bounded review pilot remains historical evidence.

No score weights, score thresholds, hard-floor triggers, quota boundary, or
reissue counts change. The changed numbers are the dated price comparisons,
policy/evidence dates, and supported CLI levels. Changed Sol route evidence
labels are `provisional`: the correctness floor is still enforced independently
by `hardFloors`, and the old Sol/medium observational evidence does not transfer.
GPT-5.6 Sol's $4 / $0.40 / $20 price is promotional at least through 2026-11-21.
Bare aliases `sol` and `luna` retain their GPT-5.6 identities to avoid silently
rewriting pins; new cards should store canonical GPT-6 ids.

## Routing tiers

| Route | Default use | Do not use for | Evidence and rationale |
|---|---|---|---|
| `gpt-6-luna` / `medium` | Trivial, mechanical, locally specified changes with a small diff and deterministic verification. | Unclear bugs, public contracts, migrations, security, concurrency, distributed state. | Provisional operator prior; no qualifying local completion cohort. Add all ten empirical-uncertainty points until comparable evidence exists. |
| `gpt-5.6-terra` / `medium` | Standard, reversible changes within one subsystem scoring 21–50. | P0, fencing, distributed authority, data loss, broad architecture. | Retained transitional tier; eight historical task records had no known grade or trustworthy terminal cohort. It offers no token-price advantage over GPT-6 Sol. |
| `gpt-6-sol` / `medium` | New-card prior for features, bugs, demanding implementation, research, broad context, and two to three subsystems. | Work triggering the correctness-critical hard floor. | Provisional operator decision backed by prices and external claims. GPT-5.6 Sol's seven favorable historical runs are not GPT-6 measurements. |
| `gpt-6-sol` / `xhigh` | P0, fencing, leases, distributed authority, security boundaries, destructive migrations, data-loss prevention, subtle concurrent state machines. | Routine work solely because quota is available. | Retains the correctness floor; GPT-6 local quality remains provisional. No price or quota rule lowers it. |
| `gpt-5.4-mini` / `high` | Bounded pipeline decisions over compact structured evidence and a deterministic output contract. | Core implementation, unbounded architecture, ambiguous consequential decisions. | Unchanged role exception; move to Sol/medium when authorizing evidence is ambiguous or unbounded. |

Task-class priors are independent of the score ladder: starting new cards on
Sol is an operator preference, not permission for a class hint or legacy
fallback to bypass concrete-card scoring. Every class publishes GPT-6 candidates
first and separately lists GPT-5.6 fallbacks with catalogue-derived dated prices.
Legacy fallback sets are explicit operator alternatives, not automatic
no-regression or provider-equivalence declarations. Bounded graphical judgment
and consistency checks retain Mini/high; their GPT-6 Sol candidate applies to
unbounded or consequential analysis. Historical measured class baselines stay
separately attributed, with unknown GPT-6 outcome rates and costs.

## Model support and vendor overrides

Operator observations of 2026-09-25 supersede the September 24 discovery notes:

- `gpt-6-sol` and `gpt-6-luna`: Codex CLI 0.155.0 exposes `minimal`, `low`,
  `medium`, `high`, `xhigh`; Sol's CLI default is `xhigh`. Policy routes pin
  `medium` or `xhigh` explicitly. Sol runs on the workstation and agent-runner-01
  since September 24; Luna passed the runner probe on September 25.
- `gpt-6-astra`: `low`, `medium`, `high`, `xhigh`, `max`, `ultra`; selectable
  and provisional. Two `access_programs.cyber` HTTP 400s in about eight runs on
  September 18 are an access warning, not a quality failure rate. Keep it an
  explicit evaluation or operator pin, outside the default ladder.
- Source GPT-5.6 `ultra` pins cannot silently become GPT-6 Sol/Luna `xhigh`.
  That is a **reasoning downgrade**, requires explicit operator selection,
  and blocks unconditional safe-auto migration. Both migrations remain
  proposal-only until all [five facts](../../model-migrations.md) hold.

API and Codex ladders are different evidence scopes. The
[Sol model page](https://developers.openai.com/api/docs/models/gpt-6-sol) and
[Luna model page](https://developers.openai.com/api/docs/models/gpt-6-luna),
retrieved 2026-09-25, list API `none` through `max` with default `medium`.
External API `max` results must retain that effort label; they neither add
Codex `max` support nor measure local `medium`/`xhigh` routes.

Anthropic vendor overrides retain `claude-haiku-4-5`, `claude-sonnet-5`, and
`claude-opus-5`. Haiku and Opus are selectable for explicit pins with provisional
fit; Sonnet/high remains the declared provisional fallback for Terra/medium and
Sol/medium only. **No provider equivalence to Sol/xhigh is established.** Pins
below the floor must be flagged. Fable 5.1 remains separately selectable and
provisional, without adding an automatic fallback.

Add `claude-opus-5-5` as the priced Opus alternative: Anthropic's
[September 22 changes](https://platform.claude.com/docs/en/models/opus-5-5/whats-new-opus-5-5),
retrieved September 25, document $4 / $20 input/output versus Opus 5's $5 / $25,
a 20% reduction, and cached reads of $0.20 versus $0.50 (60% lower). The catalogue
retains dated prices. Its `low`/`medium`/`high`/`xhigh`/`max` ladder and successful
Claude Code 2.1.281 observation remain provisional; 2.1.270 silently fell back.
Always-on thinking and tool-use changes require compatibility checks. Lower
prices alone do not authorize automatic migration or correctness-floor equivalence.

## External evidence, retrieved 2026-09-25

The [September 22 OpenAI announcement](https://openai.com/index/introducing-gpt-6-sol-and-luna/)
reports roughly half the factual errors for Sol versus its predecessor on an
internal, error-selected conversation evaluation. This is a vendor claim,
not a local coding failure rate. It reports DeepSWE v1.1 scores of 68.8% for
Sol and 66.6% for Luna at API `max`. The
[Artificial Analysis Sol page](https://artificialanalysis.ai/models/gpt-6-sol)
and [Luna page](https://artificialanalysis.ai/models/gpt-6-luna) show Intelligence
Index v4.3.2 scores of 48 and 37, at API `max`, respectively. These live pages
are recorded as September 25 first-observed public snapshots, not invented run
dates. Sample denominators and confidence intervals are unknown.

Dated, sourced rows live in
[benchmark-results.json](../../../src/TokenEconomy/catalog/benchmark-results.json).
Their local-routing `evidenceStatus` is `provisional`; publisher attribution
and external numeric scores remain intact. Do not merge different index versions
or infer local success rates from these results.

## Weighted decision

Score the task at intake from information available before implementation. Use
the expected diff and affected contracts, not the eventual diff. The maximum is
100 points.

| Criterion | Weight | Scoring anchors |
|---|---:|---|
| Correctness risk | 35 | `0`: prose, formatting, or a non-behavioral local edit. `12`: reversible local behavior with a clear test. `24`: persistent state, a public contract, an unclear bug, or a consequential migration. `35`: P0, fencing or lease authority, security boundary, distributed concurrency, or plausible data loss. |
| Expected scope | 20 | `0`: up to about 50 changed lines in one subsystem. `8`: about 51-200 lines or two tightly related components. `14`: about 201-500 lines or three subsystems. `20`: more than 500 lines, four or more subsystems, or a repository-wide migration. Generated files do not count. |
| Context demand | 20 | `0`: exact file and behavior are known. `8`: one adjacent component or contract must be read. `14`: several layers or historical behavior must be reconciled. `20`: broad codebase references, architecture history, and cross-repository or distributed invariants are required. |
| Task type and uncertainty | 10 | `0`: mechanical chore or copy change. `3`: clear refactor or content task. `6`: well-specified bug or feature. `10`: unknown root cause, architecture decision, or requirements that must be derived. Task type is a prior, not a verdict. |
| Empirical confidence | 10 | `0`: a comparable cohort has at least 20 runs, useful grade coverage, at least 70% A/B among known grades, and under 10% reissue. `3`: at least five favorable comparable runs. `6`: sparse or mixed evidence. `10`: no comparable cohort, repeated reissues, or an unfavorable cohort. |
| Quota and cost headroom | 5 | `5`: the preferred provider is comfortably below its caps. `3`: a quota window is nearing its cap. `0`: the preferred route is capped or unavailable. This criterion may move a borderline task down, but never below a hard floor. |

For core task execution, map the total to the ladder:

| Score | Route |
|---:|---|
| `0-20` | Luna / medium |
| `21-50` | Terra / medium |
| `51-69` | Sol / medium |
| `70-100` | Sol / xhigh |

The Mini route is a role exception, not the bottom rung of the core-task
ladder. Select it only when the call is a bounded pipeline decision with
structured evidence and a parseable output contract.

### Hard floors

Apply these after scoring:

- P0, fencing, lease ownership, stale-write rejection, distributed authority,
  security boundaries, and credible data-loss paths require Sol/xhigh.
- A public protocol, persistent-state migration, or change spanning three or
  more runtime subsystems requires at least Sol/medium.
- An unclear bug requires at least Terra/medium even when the expected diff is
  tiny.
- A bounded decision that can itself authorize a destructive, security, or
  lane-affecting action must move from Mini to Sol/medium when its evidence is
  ambiguous or unbounded.
- Quota and cost never lower a hard floor. Prefer an equivalent-capability
  provider fallback, wait for quota, or request an explicit human override.

### Reissue rule

Re-score from the newest evidence. A substantive C/D review or a semantic
reissue sets empirical confidence to `10` and raises the next attempt by at
least one core tier. Do not promote for an environmental failure, stale base,
broken test host, cancellation, quota truncation, or missing delivery path. Fix
that substrate instead.

After two semantic failures at the stronger tier, stop model escalation. Narrow
the task, improve its evidence, or ask for a human decision.

## Historical GPT-5.6 benchmark basis

AGT-2243 produced `results/model-benchmark.md` and
`results/model-benchmark.json` from a read-only snapshot on 2026-07-23. The
snapshot found 152 task records across nine projects, included 121 records with
run evidence, and formed 19 model, thinking-level, and task-type cohorts.

The report is observational history, not a controlled benchmark:

- Grade coverage was 33.9%.
- Duration coverage was 91.7%.
- Token coverage was 36.4%.
- A record retains only its final model and thinking level, so attempts cannot
  be split when a card changed route.
- Reissues also reflect task quality, gate defects, stale bases, and historical
  orchestrator behavior.

These are the policy-relevant aggregates:

| Cohort | Runs | Known grade result | Reissue result | Policy reading |
|---|---:|---|---|---|
| Sol/medium, chores and features | 7 | 5 known, all A/B | 0/7 | Supports the historical GPT-5.6 Sol/medium baseline only. |
| Sol/high, chores and features | 6 | 5 known: A2, B2, C1 | 0/6 | Favorable but too small to justify a separate default tier. |
| Sol/xhigh, all task types | 78 | 22 known: A2, B2, C2, D16 | 32/78 | Strong selection bias and pipeline churn. Keep it as a risk floor, not a blanket default. |
| Terra/medium, chores and features | 8 | 0 known | 0/8; records were backlog or progress | Insufficient terminal evidence. Terra remains provisional. |
| Mini/medium, chores and features | 2 | Both B | 0/2 | Too small and the wrong role to validate Mini for core implementation. |
| Claude Sonnet 5/high, features | 4 | 3 known, all A/B | 0/4 | A reasonable equivalent-provider signal when Codex quota is constrained, still with a small sample. |

The single Sol/medium bug record had an unknown grade and was reissued, so it
does not support a bug-quality conclusion. Token coverage is also too low to use
the reported token medians as routing thresholds.

There was no Luna cohort. AGT-2200 had not run and its 2026-07-23 scope update
moved controlled model comparisons to the Token Economy A/B harness. Therefore
the Luna and Terra tiers must remain visibly provisional until fresh, identical
scenario runs exist.

## Five historical cards (original score interpretation)

The score below is the route that would have been chosen at intake from the
card text. The observed route and later outcome are evidence, not inputs
silently used to rewrite the initial estimate.

| Card | Risk | Scope | Context | Type | Empirical | Quota | Initial route | Why |
|---|---:|---:|---:|---:|---:|---:|---|---|
| AGT-2241, remove the chat paperclip control while preserving paste | 0 | 0 | 0 | 0 | 10 | 5 | `15`, Luna/medium | A local mechanical removal with a named regression spec. Luna was unvalidated, so all uncertainty points remain. |
| AGT-2268, copy the task key from detail and board surfaces | 12 | 8 | 8 | 6 | 10 | 5 | `49`, Terra/medium | Reversible UI behavior across two surfaces, clipboard interaction, feedback, and Playwright proof. Its later semantic reissues would promote the next attempt to Sol/medium under the reissue rule. |
| AGT-2249, align pipeline settings rows and expose all step toggles | 12 | 8 | 8 | 6 | 6 | 5 | `45`, Terra/medium | A standard frontend feature in one subsystem with several related components and an explicit visual test. Later semantic reissues would promote it to Sol/medium. |
| AGT-2243, aggregate model history across storage variants | 12 | 14 | 20 | 6 | 10 | 5 | `67`, Sol/medium | The source change is a script, but correctness depends on broad task-schema history, legacy fields, lane semantics, idempotency, and data-quality interpretation. The observed Terra run required reissues and ended grade D, which is consistent with choosing a stronger initial route, not proof of causality. |
| AGT-2182, persist restart-safe RunAttempt and ReviewAttempt fencing | 35 | 20 | 20 | 10 | 10 | 5 | `100`, Sol/xhigh | P0 distributed authority, stale-write rejection, leases, idempotency, restart behavior, and many interacting runtime paths trigger the hard floor independently of quota. |

For every one of these cards, bounded supporting aspect and orchestrator calls
may still use Mini/high. The table selects the core implementation route.

## Quota and provider handling

1. Establish the correctness floor and score before consulting quota.
2. If the preferred model is available, use the scored route.
3. If a quota window is near its cap, first select a benchmark-supported,
   equivalent-capability provider route. Record that fallback in the run.
4. Downgrade one core tier only when the score is within five points of the
   lower threshold, no hard floor applies, and verification is deterministic.
5. If no safe route is available, wait or ask for an explicit override. Never
   silently spend correctness margin.

Quota state is run-scoped. It must not rewrite the card's configured model, and
the decision log must retain the recommended route, selected route, selection
source, score, and reason.

## Follow-up: local cohort and migration confirmation

Before qualifying GPT-6 routing fit, collect **80 completed cards**: 20 each
for mechanical/document edits on Luna/medium, reversible features/UI, bugs and
research on Sol/medium, and correctness-critical/security work on Sol/xhigh.
For the Sol/medium groups retain source-code review and research slices explicitly.
Keep separate per-model, effort and task-class denominators; no blended class
average can certify another class. Twenty per stratum matches the existing
empirical-confidence gate; it is a minimum pilot, not statistical proof.

Run identical repository fixtures in fresh workspaces with GPT-5.6 and GPT-6
Sol/Luna, at the same supported effort, with at least three repetitions of
all four curated hard coding cases (boundary, Unicode/locale, cross-file,
underspecified), then expand to at least five independent scenarios and 20
attempts per candidate/class. Separately compare Sol/xhigh for security and
concurrency; medium-only coding results cannot certify that floor. Retain
setup hashes, actual returned model/effort, CLI version, prompts, raw outputs,
verification logs, timeout/access failures, and dated usage/cost.

Record first-attempt verified completion, final completion, A/B/C/D review
coverage, semantic reissues versus environmental retries, critical defects,
input/cache/output tokens, wall-clock median and p95, and retry-adjusted dollars
per accepted outcome including verification. Unknown usage or grades stay null.
Use blinded review on paired card snapshots; do not pool later escalated attempts
under their final model. Target at least 70% A/B among known grades, under 10%
semantic reissues, and complete deterministic critical checks. Require no new
critical regression and no loss on paired deterministic cases before claiming
`noRegression`; publish uncertainty intervals and any excluded runs.

The TE-57 controlled probe ran four curated cases twice at medium, requesting
GPT-5.6 Sol, GPT-6 Sol, GPT-5.6 Luna and GPT-6 Luna (32 attempts total).
Requested Sol models and GPT-5.6 Luna each passed 6/8; GPT-6 Luna passed 5/8,
including one boundary output-contract failure that passed on repeat. All four
failed the deliberately underspecified case on both passes. These are fixture
outcomes, not completed-card rates. Only the second pass retained final responses
and transport metadata; none of its 16 records exposed actual returned model
identity. Consequently the comparison is **inconclusive**, and cannot establish
model-specific noRegression. The delivery retains raw results, setup hashes,
response metadata and logs separately from catalogue quality claims.

Both migrations remain proposal-only: unknown returned model identity, small
medium-only coverage and the Luna contract failure require investigation;
`ultra` → `xhigh` remains an incompatible silent downgrade regardless of quality.
Re-run with verified model identity, adequate repetitions and xhigh coverage,
then satisfy the five migration facts and runtime checks before promotion.

Review the Terra tier with a paired Terra/medium versus GPT-6 Sol/medium slice;
remove it only with an explicit score-band and reissue-rule revision. Qualify
Astra separately after access reliability is established and its fivefold Sol
price earns a measured benefit. Revisit Opus 5.5 equivalence using identical
cases and current CLI identity checks before promoting its migration.
