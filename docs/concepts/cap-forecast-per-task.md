# Forecast each task as a percentage of a five-hour cap

**Status:** research plan only, 2026-07-11. **No implementation is authorised.**
Every implementation slice below is blocked until the operator gives an explicit
GO. The separate Agent Studio boundary-observation card is an input to this plan;
this document does not expand or re-authorise that card.

**Handoff update, 2026-07-12:** Studio now emits the cached quota state and its
age at run start and end. Those boundary events do not force a fresh probe and
do not preserve every raw meter semantic needed below. The
**calibration-grade fresh/settled start/end pair is therefore still missing**.

The operator-facing goal is simple to say:

> Before launch, show the central forecast for what percentage of one full
> five-hour quota window the next run attempt will consume on every candidate
> model, with an uncertainty range, so the operator can choose a model/CLI or
> wait.

The measurement problem is not simple. Providers expose a percentage meter and
a reset label, but generally do not expose an absolute subscription token cap.
This plan therefore proposes an **empirical forecast**, not a reverse-engineered
provider promise.

## Read this plan

- [Visual overview](../../website/cap-forecast/index.html)
- [What “% of cap” means](../../website/cap-forecast/problem.html)
- [Observations and calibration](../../website/cap-forecast/data-plan.html)
- [Forecast model v1](../../website/cap-forecast/forecast-v1.html)
- [Library, Studio, and website boundaries](../../website/cap-forecast/architecture.html)
- [Proposed implementation slices](../../website/cap-forecast/slices.html)

The Markdown page is canonical. The small HTML pages are self-contained,
human-readable explainers of the same plan.

## 1. Define the metric before estimating it

For a candidate model and thinking level, “**4.2% of the 5h window**” means:

> The next run attempt's median forecast is equivalent to 4.2 percentage points
> of one full effective five-hour quota window under the named
> CLI/account-plan/model calibration.

It does **not** mean:

- 4.2% of the quota currently remaining;
- an API dollar cost (that remains the pricing catalog's job);
- that the provider publishes a 4.2%-equivalent number of tokens;
- that the task is guaranteed to finish or fit before the next reset; or
- that the five-hour window is the only binding limit. Weekly and other windows
  remain separate admission inputs.

The UI should display current headroom and reset time beside the forecast, never
fold them into the forecast. Values above 100% must remain visible: they are
useful evidence that the task is unlikely to fit in one full window. A run that
crosses a reset can consume two window instances even though its full-window
equivalent is still meaningful.

Every selectable, nonrestricted, nondeprecated model with its proposed effort
is evaluated **independently of current CLI headroom**. Availability is attached
afterward, so a dry CLI remains visible as a reason to choose another model or
wait. A row may honestly say `Uncalibrated`; missing evidence must never become
zero cost or a hidden row.

### Why `task tokens / provider limit` does not work

There are two unknowns:

1. **Effective window capacity.** The provider's absolute token allowance and
   any internal weights for model, cache, reasoning, or tool use are not
   published by the percentage snapshot.
2. **Attempt demand.** A prompt's initial size does not determine how many agent
   turns, tool results, context reads, and output tokens one launched attempt
   will consume on a particular model and thinking level.

In addition, a “five-hour” meter may have different topologies:

- A **usage-anchored bucket** may start its reset clock with the first use,
  rather than at a fixed wall-clock boundary.
- A **sliding/rolling window** can remove old usage while a task adds new usage.
  The endpoint change is then *new use minus expired use* and can be zero or
  negative even when the task consumed quota.
- A reset label can be rounded, lagged, or reparsed. It is evidence about a
  window, not proof of its topology.

Consequently, the useful object is an **effective token-equivalent capacity**
estimated under controlled conditions. It must not be labelled an official
provider token limit.

## 2. What Agent Studio can observe

The plan uses signals already present in Agent Studio, including the newly
landed cached boundary events, and one still-missing calibration-grade capture
contract.

| Signal | Exists today | Relevant fields | Use here |
|---|---:|---|---|
| Per-turn token events on the bus | Yes | model, input, output, cache read/write, reliable run correlation on the rich event path, optional latency/context | Reconstruct observed attempt consumption by run and candidate model. Legacy events without an unambiguous run join are ineligible. |
| Quota snapshots | Yes | CLI, plan, fetched time, used %, reset time/label, source/raw sample, error/suspicious state | Observe the provider meter and identify a quota scope/window. |
| Run lifecycle | Yes | run start, run finish, duration, terminal outcome | Bracket tokens, detect overlap/reset crossing, and distinguish exact from censored attempt demand. |
| Task/run configuration | Yes | selected model, thinking level, task metadata | Build model/effort/task cohorts. |
| Calibration-grade quota at run start and end | **Partial: fresh/settled pair missing** | cached boundary events now carry run/phase and snapshot age; calibration still needs fresh raw snapshots tied to the same run/window, meter direction/resolution, and settle probes | Cached events are useful audit evidence but are not, alone, defensible calibration pairs; completion remains with the parallel Studio card. |

Primary Studio anchors: [`AgentMessageBusBridge`](https://github.com/agent-orc/agent-studio/blob/main/backend/Features/Bus/AgentMessageBusBridge.cs),
[`OrchestratorTokenUsage`](https://github.com/agent-orc/agent-studio/blob/main/backend/Shared/Runner/OrchestratorTokenUsage.cs),
and [`QuotaModels`](https://github.com/agent-orc/agent-studio/blob/main/backend/Shared/Models/QuotaModels.cs).

Only token events that can be joined to the measured run without ambiguity are
eligible. A legacy event without a dependable run ID may be used only when the
Studio adapter can prove an equivalent unique join; timestamp proximity alone
is not enough.

Run duration helps establish whether a reset, expiry, overlap, or probe lag is
plausible. Duration does not reveal quota capacity by itself.

### Required observation record

Studio should retain an append-only raw record and derive a normalized record.
At minimum it needs:

- observation/run/window IDs; CLI/provider, opaque account/plan scope, quota
  window kind and label;
- start/end sample times, raw meter direction (`used` or `remaining`), displayed
  resolution/rounding semantics, normalized used percentage, reset
  timestamp/label, raw sample, source, parser version, and quality/suspicion
  flags;
- exact model and thinking level; task type, repository identifier, prompt size,
  prompt-measure definition, routing/timeout policy version,
  start/finish/outcome, and duration;
- token components with parser semantics preserved, plus one canonical observed
  token total that does not double-count cached input;
- overlap/concurrency and completeness flags, and exclusion reason(s); and
- calibration version and capture time so later forecasts are reproducible.

Raw observations are never silently rewritten. A parser fix produces a new
normalization/calibration version.

## 3. Estimating an effective window

For one clean observation span, first normalize the provider's meter direction.
Convert a raw remaining percentage into an increasing `usedPct`, while retaining
the original reading, direction, and display resolution. Then calculate:

```text
deltaPct_i = usedPct_end - usedPct_start
tokens_i   = canonical observed token units in the bracketed run(s)
```

For a calibration scope `s` (CLI/account-plan/window kind/exact model/thinking
level), fit the direct relationship:

```text
deltaPct_i = beta_s * tokens_i + error_i
effectiveWindowTokens_s = 100 / beta_s
```

`beta_s`—percentage points per observed token—is the identified quantity. The
inverted capacity is an explanatory equivalent. Fitting `beta` directly is more
stable than averaging `100 * tokens / deltaPct`, which explodes when a rounded
percentage barely changes. `effectiveWindowTokens` exists only when `beta` is
positive and estimable; otherwise the scope is `Uncalibrated`.

V1 assumes the meter is locally stable and approximately linear over the token
and percentage range represented by the calibration data. It must test a free
intercept, curvature/residual patterns, and bias by task/token-component mix.
Forecasts outside observed support, or from a materially different
input/output/cache/reasoning mix, are downgraded or returned uncalibrated. The
single total-token slope absorbs the calibration cohort's component mix; it is
not proof that every token component has the same provider weight.

If several models share one provider meter, their effective capacities describe
equivalent all-one-model workloads against that same shared window. They are not
independent model quotas.

### Pair eligibility

A pair is usable only when all of these are true:

1. Start and end have the same CLI, account/plan scope, quota-window kind, and
   compatible reset/window identity. If the measured run's first use creates a
   usage-anchored window, a trusted pre-use/zero observation and the newly
   established reset anchor must identify that transition explicitly.
2. No reset is crossed and no older usage can expire during the interval. A
   truly sliding window therefore needs a fresh/quiet controlled interval or a
   reconstructed expiry ledger; two arbitrary snapshots are insufficient.
3. All account usage in the span is either the measured run or fully
   attributed. Unknown concurrent CLI use disqualifies the pair.
4. Token events are complete and non-duplicated; model, thinking level, and
   terminal outcome are known.
5. The end meter has settled according to a documented polling/timeout policy.
6. Neither snapshot is parser-suspicious, stale, directionally inconsistent, or
   otherwise invalid.

Calibration does **not** require a successful task. A failed or cancelled run
can still provide a valid meter pair when its interval is closed, all consumed
tokens are present, and the quota meter is settled and unsaturated. A
quota-truncated run is often floor/ceiling-censored and may provide only a bound;
exclude it from the simple slope fit unless an interval-aware estimator handles
that bound. Outcome censoring belongs to the attempt-demand model, not to meter
pair eligibility.

The calibration campaign should mix:

- **controlled spans** soon after a fresh/quiet window, one run at a time, over
  several task sizes, models, and thinking levels; and
- **opportunistic spans** from normal operation that pass the same strict gates.

Multiple distinct window instances matter more than many pairs from one window.

### Rounding, glitches, and outliers

- Preserve the quota display resolution. A zero delta on an integer percentage
  meter is **interval-censored**, not evidence of zero consumption. V1 uses an
  interval-aware fit: each displayed endpoint becomes an interval derived from
  documented rounding semantics (or a conservative one-resolution envelope
  when the capture contract can defend that bound), and each true delta is the
  difference of those intervals. If no defensible display-error bound exists,
  the scope is `Uncalibrated`. Eligible consecutive spans may be aggregated for
  conditioning and diagnostics, but a rounded zero never becomes an exact zero
  or disappears.
- Treat observations at the meter floor or ceiling as potentially saturated.
  Quarantine them from the simple point fit or retain them explicitly as
  one-sided interval bounds; never pretend the displayed endpoint is an exact
  unconstrained delta.
- Hard-quarantine impossible values, unexplained downward jumps, reset changes,
  incomplete tokens, known parser failures, and direction/sign mistakes. Store
  the raw record and a machine-readable exclusion reason.
- After hard validation, fit a positive, interval-aware weighted robust slope
  through the origin (a Huber penalty on distance outside each feasible delta
  interval is sufficient for v1). If quantization leaves the slope
  unidentified, return `Uncalibrated`. Fit a free intercept only as a
  diagnostic: a material intercept suggests background usage, lag, or a broken
  zero assumption.
- Before replay, TE-F1 must freeze the weighting rule, Huber tuning, minimum
  evidence and estimability rules, and the diagnostic thresholds that map to
  high/medium/low confidence. A later change creates a new estimator version
  and needs a new untouched temporal hold-out; known parser faults remain
  quarantined regardless of robust-fit settings.
- Bootstrap by **distinct window**, not by pair, to avoid pretending correlated
  samples are independent.
- Version by scope and parser and detect material changes. A detected provider
  or plan change closes the old calibration regime; do not let recency weights
  blur two regimes into one slope. Recency weighting is allowed only within a
  stable regime.

### Calibration confidence

Confidence is not sample count alone. It should combine:

- number of distinct windows and cumulative observed percentage movement;
- movement relative to display quantization;
- recency and stability since the last detected change;
- residual error and the diagnostic intercept;
- parser, token-completeness, overlap, and settle quality; and
- temporal hold-out error, bias, and interval coverage, including residual bias
  by task type and token-component mix and whether the forecast lies inside the
  calibrated support.

Sparse or stale scopes return `LowConfidence` or `Uncalibrated`; they do not
borrow a precise-looking number from another plan.

## 4. Forecast model v1: deliberately simple

V1 is an empirical cohort heuristic, not a causal model.

### Forecast estimand

V1 forecasts the cap consumption of **one next launched attempt**, from run
start until its terminal state under the current failure/cancellation policy.
Succeeded, failed, and cancelled attempts all consumed quota and belong in that
empirical outcome mix when their token records are complete. This is not a
forecast of tokens required for eventual successful task completion; retries,
reissues, and review loops are separate future signals.

Still-running or quota-truncated attempts are right-censored lower bounds on
unconstrained attempt demand. V1 does not insert those bounds as exact point
samples: it reports their rate, downgrades confidence, and returns uncalibrated
when censoring is material. A future censoring-aware model may use them directly.

### Inputs

For the pending task:

- initial `promptSize` at the actual forecast boundary (a stable measure such as
  UTF-8 bytes, with an explicit/versioned rule for templates, attachments, and
  other wrapper text);
- `taskType`, mapped to Token Economy's existing `TaskClass` where possible;
- every selectable candidate's exact model, CLI, and proposed thinking/effort
  level, before filtering on current availability; and
- terminal historical attempts with the same features and calibrated quota
  scope, including successful, failed, and cancelled outcomes with complete
  observed tokens and the routing/timeout/cancellation policy version that
  shaped those outcomes.

Historical routing is not random: harder tasks may have been sent to stronger
models. V1 therefore produces an **observational conditional forecast** for
each model's own historical cohort, not a causal answer to “what would this
identical task have cost on every model?” Every non-`Uncalibrated` candidate
forecast requires temporal replay validation; controlled crossover or matched
evidence is a later upgrade.

### Attempt-demand heuristic

For each candidate separately:

1. Select closed attempts from the same task type, exact model, thinking level,
   and compatible routing/timeout/cancellation policy version. Include
   successful, failed, and cancelled terminal outcomes when their consumed
   tokens are exact; track quota-truncated/still-running lower bounds separately.
2. Prefer runs in the nearest prompt-size band. Prompt size is only an initial
   complexity proxy; multi-turn context and tools may dominate.
3. Use the cohort median as the central attempt-token estimate and empirical
   quantiles as the attempt-demand range. Disclose effective sample size,
   terminal-outcome mix, and censored rate. Repeated attempts/reissues from one
   task family are dependent: cluster demand resampling and effective sample
   size by originating task/attempt family, and by repository when its
   dependence is material, rather than treating raw rows as independent.
4. V1 has no cross-model, model-family, task-type, or repository fallback. If
   the exact cohort is insufficient, return `Uncalibrated`. A later fallback
   hierarchy may be introduced only after replay validates and freezes its
   transfer rules and interval widening. Never reuse one model's token demand
   as if it were valid for every model.

Then compute:

```text
capPctDraw_s,j = betaDraw_s,j * attemptTokensDraw_s,j
centralCapPct_s = median(capPctDraw_s,*)
range80_s = [quantile10(capPctDraw_s,*), quantile90(capPctDraw_s,*)]
```

Combine uncertainty by sampling both the calibration slope and historical
attempt demand, then report the median and central **80% prediction range** of
the product draws. When the calibration and demand distributions reuse
overlapping runs/windows, resampling must preserve that dependence (or use
disjoint evidence); it must not silently multiply independent draws. The range
is a forecast interval, not a guarantee. Retain more precise values in the API;
the card should normally show at most one decimal place and avoid precision
below the meter's resolution.

### Forecast result contract

Every candidate receives a result row containing:

| Field | Meaning |
|---|---|
| Candidate | canonical model, CLI, thinking/effort |
| Attempt tokens | median attempt estimate and empirical range |
| Five-hour forecast | central percentage and prediction range; may exceed 100% |
| Calibration | plan/window scope, version, as-of time, distinct-window count |
| Demand evidence | effective sample size, terminal-outcome mix, censored rate, policy version |
| Confidence | high/medium/low/uncalibrated plus reason codes |
| Availability | current CLI/headroom/reset, supplied separately by Studio |

Illustrative card copy (not measured data):

| Candidate | Forecast | 80% range | Calibration | Demand evidence | Confidence | Current state |
|---|---:|---:|---|---|---|---|
| CLI A · balanced model · medium | 4.2% of 5h | 2.7–6.8% | Example Pro · 5h · cal-v3 · as of 2026-07-10 UTC | effective n=42 · mixed outcomes · 5% censored · policy-v4 | Medium — 7 windows, quantized meter | 31% left · resets 2026-07-11 14:20 UTC |
| CLI B · economy model · medium | 2.1% of 5h | 1.2–4.4% | Example Plus · 5h · cal-v2 · as of 2026-07-09 UTC | effective n=19 · mixed outcomes · 16% censored · policy-v4 | Low — 3 windows, wide residuals | 64% left · resets 2026-07-11 16:05 UTC |
| CLI A · new model · high | — | — | No compatible calibration | No compatible cohort | Uncalibrated — no safe transfer | Available; reset shown separately |

The current state is not part of the forecast. Admission may later compare the
upper range with current headroom, alongside weekly quota and other policy.
Nor is low forecast consumption the same as good model utility: a model that
fails quickly can look cheap per attempt. Studio must present outcome mix and
`SuggestModel` suitability beside this signal and must never rank candidates by
lowest cap percentage alone.

### Named upgrade paths

- **V2 per-repository/per-task-type regression:** prompt size, repo, task type,
  model, thinking, and interactions; temporally evaluated and regularized.
- Matched or controlled crossover evidence to measure model-routing bias and
  support stronger same-task comparisons.
- Hierarchical pooling for sparse models/plans, but only with measured transfer
  error and visibly wider intervals.
- Separate input/output/cache/reasoning coefficients when observations can
  identify them; the v1 total-token slope absorbs the cohort's component mix.
- Survival/censoring models for interrupted tasks and richer scope features such
  as changed files, acceptance gates, or tool intensity.

## 5. Architecture split

| Owner | Owns | Must not own |
|---|---|---|
| **TokenEconomy library** | Pure contracts and structural/statistical validation of already-normalized observations (finite ranges, compatible scope, estimability); robust effective-capacity estimator; forecast v1 API/DTOs; uncertainty and confidence calculation; candidate result statuses; deterministic fixtures/tests; canonical model/task/effort vocabulary alongside the existing pricing catalog and `SuggestModel`. | Contextual claims about parser trust, window identity, overlap, or account attribution; quota probes/parsers, persistence, clocks, Studio event-bus reads, account identifiers, UI, logging side effects, or the decision to launch/wait. Dollar price must not be used as a quota-cap proxy. |
| **Agent Studio** | Start/end capture and settle policy; token/run/config joins; contextual eligibility gates for parser trust, reset/window identity, concurrency, and account attribution; raw/normalized storage; calibration cohort assembly; scheduled recomputation; card metric for all candidates; current headroom/reset; audit events; replay/shadow validation; eventual AGT-2055 advisory integration. | Duplicated forecast math or a second model catalog. It must not interpret an uncalibrated row as free. |
| **Website** | Public method explainer and, after validation/privacy review, timestamped aggregated “live-ish” calibration snapshots with scope, sample size, range, confidence, and as-of time. Generate/in-line snapshots at publish time so pages remain self-contained. | Raw task/account data, provider-official wording, admission decisions, or an unlabeled number that looks live forever. |

Token Economy already provides the dated [pricing catalog](../../src/TokenEconomy/ModelPriceCatalog.cs)
(TE-1) and pure [`SuggestModel`](../../src/TokenEconomy/ModelEfficiencyMatrix.cs)
matrix (TE-2). The forecast is a separate quantitative signal; it complements,
but does not silently change, that ordinal ranking. Studio's control loop remains
the policy owner, as described by
[`token-budget-load-management.md`](https://github.com/agent-orc/agent-studio/blob/main/docs/concepts/token-budget-load-management.md)
and AGT-2055.

The first TokenEconomy NuGet publish is still an operator policy step. Studio
package consumption is additionally blocked on [that publish](../PUBLISHING.md),
even after an implementation GO.

## 6. Proposed implementation slices

These are card-sized proposals, not active work. **Every row is GO-blocked.**

| # | Proposed card / owner | Inputs | Outputs | Dependencies | Gate |
|---:|---|---|---|---|---|
| 1 | **TE-F1 — Calibration contracts and estimator** / TokenEconomy | Context-cleared normalized spans, scope, token components, percent resolution | Structural/statistical validation; `beta`, effective capacity, interval, confidence, diagnostics; deterministic tests | Agreed normalized contract, operator GO | **Blocked: GO** |
| 2 | **AGT-F1 — Observation adapter and calibration store** / Studio | Parallel card's cached boundary events plus calibration-grade fresh/settled start/end snapshots; reliably run-correlated bus token events; run/config metadata | Versioned raw + normalized spans; contextual parser/window/overlap/attribution quarantine reasons; TE estimator input | Calibration-grade capture completion, TE-F1, operator GO | **Blocked: GO** |
| 3 | **TE-F2 — Forecast v1 API** / TokenEconomy | Task features/attempt history, candidate model+effort, calibration result | Per-candidate median attempt tokens, cap %, prediction range, confidence/status/reasons; tests | TE-F1, operator GO | **Blocked: GO** |
| 4 | **AGT-F2 — Offline replay and shadow forecast** / Studio | Historical eligible cohorts, TE-F2 outputs, later realized meter deltas | Temporal hold-out report: percentage-point MAE/bias, interval coverage, direct-delta baseline comparison, stale/change alerts, frozen evidence gates plus later untouched confirmation | AGT-F1, TE-F2, operator GO | **Blocked: GO** |
| 5 | **AGT-F3 — Card metric and AGT-2055 advisory input** / Studio | All candidate forecast rows, live headroom/reset, weekly windows, `SuggestModel` rationale | Human card table and an auditable advisory field/event for AGT-2055; **no admission behavior change** | AGT-F2 passes, NuGet publish, AGT-2055, operator GO | **Blocked: GO** |
| 6 | **WEB-F1 — Promote plan pages with aggregate snapshot** / website | The delivered explainer pages plus a privacy-reviewed, generated calibration aggregate | Extended self-contained docs and timestamped “live-ish” numbers with confidence/sample labels | Validated data export, privacy review, operator GO | **Blocked: GO** |

Suggested order after a GO: TE-F1 and the Studio adapter contract can be designed
together; TE-F2 follows; Studio runs shadow validation before any card influences
admission; the website publishes numbers only after those numbers have passed the
same validation and privacy gates.

### Acceptance gates before admission use

The operator should set numeric thresholds after the first shadow report rather
than invent them now. That report is threshold-design evidence, not a pass: once
thresholds are frozen, they must be confirmed on a later untouched temporal
hold-out. At minimum the reports must show:

- temporal hold-out MAE and signed bias in percentage points;
- empirical coverage of the advertised 80% prediction range;
- comparison with a direct empirical cap-delta cohort baseline, to show whether
  the token-demand × slope decomposition adds calibration value;
- scoring against later clean, eligible, temporally held-out start/end meter
  deltas with quantization bounds preserved, never against the circular proxy
  `beta * realizedTokens`; the direct-delta baseline uses those same pairs;
- results by plan/model/thinking/task type, including sparse/uncalibrated rates;
- terminal-outcome mix, quota-truncated/still-running censoring rate, and error
  inside versus outside calibration support;
- parser exclusions, overlaps, reset/rolling-window exclusions, and calibration
  staleness; and
- examples where the forecast would have changed a launch/wait decision.

Until those gates pass, forecasts are advisory and cannot make AGT-2055 launch,
downshift, throttle, or wait decisions.

Any later admission-enforcement change is outside AGT-F3 and requires its own
explicit operator authorization after the evidence gate.

## 7. Explicit non-goals for this plan

- No provider limit scraping or claim of an official absolute token budget.
- No change to the pricing seed, `SuggestModel`, Studio admission, or task card.
- No synthetic “live” website values and no raw operational data publication.
- No implementation, package release, or NuGet policy action before operator GO.

This page and its HTML explainers are the complete TE-4 deliverable. Work stops
at the plan boundary.
