# Cost per completed Agent Studio card

`CardEconomics.Decide(query, records)` is a dependency-free .NET decision API.
It returns a ranked table and a one-sentence advisory recommendation. It does
not mutate routing tiers, fallbacks, pins, or model evidence status. The
[model routing policy](system/domains/model-routing-policy.md) remains authoritative.

## Query and integration

```json
{
  "organisationId": "your-organisation",
  "taskClass": "feature",
  "sinceUtc": "2026-09-18T00:00:00Z",
  "asOfUtc": "2026-09-18T23:59:59Z",
  "currency": "Usd",
  "subscriptionId": "your-codex-pro-account",
  "excludeObservedAccessRefusals": true,
  "candidates": [
    { "model": "gpt-6-astra", "thinkingLevel": "low" },
    { "model": "gpt-5.6-sol", "thinkingLevel": "xhigh" }
  ]
}
```

Save as `query.json`, then run:

```sh
dotnet run --project src/TokenEconomy.Benchmarks -- decide query.json /path/to/task-storage
```

The command recursively reads `task.json` through the existing
`AgentStudioTaskStorageImporter`, writes JSON to stdout, and writes
`card-economics.json` and `card-economics.html` under `JOB_RESULTS_DIR` when set.
There is no provider invocation. Use `WeeklyQuotaPercent` to rank by measured
weekly quota share instead of USD; both columns are always returned.

The optional `card` is the existing `ComplexityCard` contract: `taskKey`,
`prompt`, `taskType`, `area`, `expectedChangedLines`, `referencedSubsystems`,
and correctness signals. Its type must agree with `taskClass`. Its own history
is excluded. Area, expected size bucket (up to 50 / 200 / 500 / larger lines),
and all named subsystems filter comparable cards. Missing matching scope is
not assumed. The existing complexity estimator identifies hard floors;
only declared policy routes or declared provider equivalences clear a floor.
Astra's selectable status does not make it equivalent to Sol/xhigh for fencing.

An Agent Studio host can call:

```csharp
var store = new InMemoryAgentStudioRunStore();
new AgentStudioTaskStorageImporter().ImportDirectory(storageDirectory, store);
var comparison = new CardEconomics().Decide(query, store.Records);
var html = AgentStudioRoutingDecisionHtmlRenderer.Render(persistedDecision, query, store.Records);
```

This renderer overload invokes the decision query and appends its advisory
comparison to the persisted routing decision. The original overload remains
available for hosts without cohort data. This repository ships a library and
CLI, not an HTTP server; a host can expose the same request/result contracts
through its API.

## Import contract and attribution

Each card needs an explicit terminal `finalLane`, `lane`, or `column`. A worker
attempt marked successful while its card is in review is not a completed card.
Use the existing `attempts`/`runAttempts`/`attemptHistory`/`runHistory`/`runs`
arrays. Each attempt carries its actual model and thinking level, `tokenSummary`
with disjoint `inputTokens`, `cacheReadTokens`, `cacheWriteTokens`, `outputTokens`,
start/completion timestamps, outcome, and review grade/verdict. Review and retry
rounds must be retained as separate, costed attempts. Card-level cumulative
telemetry for multiple rounds cannot establish an attempt cohort.

Additional optional evidence is imported without rewriting raw artifacts:

- `organisationId` (or `organizationId`) on the attempt or card. A host must
  supply its verified organisation identity; missing identities never join an
  organisation cohort.
- `providerErrorClass` and `providerErrorMessage`, or
  `providerError: { "code": "unsupported_parameter", "message": "..." }`.
  Explicit `providerErrorClass: "none"` means error telemetry observed no error;
  absence means unknown. Counts/rates use only observed error telemetry, across
  the organisation's model runs at all levels and task classes. Last-seen time
  and the latest detail are retained. Missing telemetry is not a healthy run.
- `weeklyQuota`: a `RunQuotaMeasurement`, or `quotaStart` and `quotaEnd` containing
  Agent Studio's native `QuotaSnapshotEvent` payloads, plus `subscriptionId` and
  `quotaExclusiveAttribution: true`. The native adapter reads `phase`, `runId`,
  `cliType`, `plan`, `fetchedAt`, `snapshotAgeSec`, `ttlSeconds`, freshness flags,
  and the `Weekly` window's `usedPct`/`resetAt`.

`AgentStudioQuotaEvidence.FromSnapshots` is also callable directly after a host
joins the run metadata or `quota-snapshot` bus observations to an attempt.
The host must verify exclusive attribution over the probe interval (including
other machines sharing the subscription). Different resets, stale/missing or
suspicious probes, unchanged probe timestamps, decreasing percentages, and
concurrent consumption yield unknown. Rounded probes can measure a zero delta;
that does not prove no resource consumption. No dollar-to-quota conversion is
made. Subscription identity is required when aggregating quota. A reset period
is identified separately from the subscription and may differ between cards.

## Estimator and honesty boundaries

Attempts deduplicate by project, task key, and run, using the newest observation.
The evidence window is inclusive. Only full, contiguous card histories starting
at run 0 or 1, wholly inside the window, with one model/level and a known terminal
card outcome enter estimates. Mixed-route, truncated and open cards are counted
as excluded rather than crediting a model with another model's completion.
Model refusals still enter the independent organisation availability facts.

For `N` eligible terminal cards, `S` completions, and all their attempts:

```
completion probability = S / N
expected USD per completed card = (sum dated attempt costs / N) / (S / N)
                               = sum dated attempt costs / S
```

The same denominator is used for token components, active attempt duration,
rounds and quota share. This includes failed cards and review/retry attempts
exactly once; do not apply another refusal multiplier to an already measured
cohort. Dated catalogue prices are resolved separately at each execution time.
This is an observed historical-cost estimator, not an invoice or a forecast of
future tariff changes. Cache-write tokens are disjoint, using the catalogue's
retention assumption. Queue time and unrecorded review labour remain unknown.

No completions means no finite cost estimate. Any missing usage or unresolved
price makes the USD aggregate unknown; absent duration or quota makes its own
aggregate unknown independently. Unconfirmed prices retain a visible flag.
Favorable/known review counts do not convert operational completion to a quality
guarantee. Cohorts below 20 cards are explicitly small; larger observational
cohorts still carry selection bias. Rankings put eligible measured candidates
first; unknown is never a zero-cost winner. Provider errors affect measured
failed-card spend; the query can additionally exclude observed access refusals.
It does not claim historical errors prove current permanent unavailability.

## Dated operator export

The accepted implementation is extended with `AgentStudioCohortImporter`, an
adapter for the verbatim [18 September export](../data/agent-studio-cohorts/2026-09-18/run-records.json).
The raw fixture remains unchanged, including the incorrectly recorded USD.
The adapter validates `uncachedIn` against `inputIncludesCached` and subtracts
cache reads exactly once. It reprices every run with its execution-date catalogue
entry and exposes both supplied and recomputed costs in `priceChecks`.
It records a SHA-256 provenance hash and reports any mismatch exceeding $0.005.
No supplied dollar figure replaces the calculated cost.

Run timestamps have no offset. The CLI adapter explicitly assumes Europe/Berlin
(UTC+02 on these dates), records that assumption, and rejects ambiguous local
times. API callers must provide the source timezone. Export observation time is
not used as a refusal event timestamp. `cyberRefusals` stay separate from run
records: the operator identifies Astra as their model, even on cards whose
retained token entries contain only Sol or Opus. No invented failed run, tokens,
reasoning level, duration, or review-to-run join is added. Card reviews and the
zero-run AGT-2880 remain in the evidence output.

```csharp
var importer = new AgentStudioCohortImporter();
var cohort = importer.Import(json, artifactPath,
    TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
var decision = new CardEconomics().Decide(query, cohort);
var html = AgentStudioRoutingDecisionHtmlRenderer.Render(persistedDecision, query, cohort);
```

`decide` accepts the export JSON path instead of a task-storage directory.
`CohortEvidence.WindowModels` describes the query's run-date window, while
`FullHistoryModels` retains older recorded histories. These are **export-wide,
model-only context**, not task-scope-matched or level-specific predictions.
The ranked rows still require exact model/level evidence. The model summary's
`UsdPerCompletedCard` is the mean recorded spend of wholly completed, single-model
histories, conditional on completion. It excludes open histories and is not the
terminal-cohort expected-cost estimator described above. Neither a review Pass
nor `merged-locally` establishes completion. In particular, an escalated card is
not silently classified as a terminal model failure.

To regenerate JSON/HTML in the collected result directory and the measured
worked-example tables below:

```sh
dotnet build --no-restore -c Release
python3 scripts/generate-card-economics.py --results-dir "$JOB_RESULTS_DIR"
```

The generator uses the checked-in [query](../data/agent-studio-cohorts/2026-09-18/query.json)
and the library CLI. It invokes no provider and writes no routing policy.

## Worked answer, 18 September 2026

**Astra has a lower measured cost per recorded run in this small workload, but
there is no evidence-backed answer that Astra/low is cheaper per completed card
than Sol/xhigh.** Per-run reasoning levels are unknown, neither Codex model has
a completed card in this export, and Astra has two organisation access-refusal
events. Keep the policy route and correctness floors; do not pin Sol/xhigh merely
by habit, and do not substitute Astra/low for a required floor on cost grounds.
Astra remains selectable and provisional. No default or fallback changes follow.

<!-- BEGIN GENERATED COHORT MEASUREMENTS -->

Generated from the retained export through the dated catalogue; all reasoning levels are unknown.

**Full exported histories (14–18 September)**

| Model | Recorded runs | Total USD | Mean USD/run | Completed histories | Mean USD/completed history | Weekly %/run | Weekly %/completed card |
|---|---:|---:|---:|---:|---:|---|---|
| claude-opus-5 | 13 | $169.91 | $13.07 | 5 | $17.26 | Unknown | Unknown |
| gpt-5.6-sol | 24 | $147.21 | $6.13 | 0 | Unknown | Unknown | Unknown |
| gpt-6-astra | 5 | $26.96 | $5.39 | 0 | Unknown | Unknown | Unknown |

**Runs on/after 18 September 00:00 UTC**

| Model | Recorded runs | Total USD | Mean USD/run | Completed histories | Mean USD/completed history | Weekly %/run | Weekly %/completed card |
|---|---:|---:|---:|---:|---:|---|---|
| claude-opus-5 | 5 | $47.17 | $9.43 | 3 | $7.05 | Unknown | Unknown |
| gpt-5.6-sol | 24 | $147.21 | $6.13 | 0 | Unknown | Unknown | Unknown |
| gpt-6-astra | 5 | $26.96 | $5.39 | 0 | Unknown | Unknown | Unknown |

Codex recorded USD: **$1487.69**; repriced USD: **$174.17** (8.54× overstatement).

| Account probe interval (+02:00) | Codex weekly window | Claude weekly window |
|---|---:|---:|
| 10:15–13:45 | +5 percentage points | +4 percentage points |
| 13:45–19:15 | +8 percentage points | +5 percentage points |

Across 10:15–19:15, Codex moved from 30% to 43% (+13 points) and Claude from 84% to 93% (+9 points).
<!-- END GENERATED COHORT MEASUREMENTS -->

All 42 run prices match the supplied `usdCorrected` within half a cent, after
repricing rather than summing rounded dollar amounts. **Codex `in` includes
`cacheRead`: Agent Studio priced the cache twice, once at the uncached rate and
again at the cached rate.** Claude's exported input is already uncached. The
recorded Codex figures are therefore unusable for this comparison. This import
corrects the accounting; it does not fix the upstream Agent Studio application.

The five completed Opus cards are AGT-2803, AGT-2866, AGT-2867, AGT-2868 and
AGT-2870. Their seven recorded runs cost $86.2939035 in total. The $17.26 mean
includes the retained repeat rounds on AGT-2803 and AGT-2866, but excludes spend
on open histories. Of these, only AGT-2867/2868/2870 have all runs on September
18: their three single-run completed histories average $7.05. Comparing Opus's
full-history $13.07/run with Codex's same-day runs is an unmatched workload
comparison, not a causal model ranking. The day-only table makes that difference
visible. Earlier Opus runs are preserved rather than discarded or falsely dated
September 18.

Sol's 12 cards with recorded runs and Astra's five remain open, including the
two escalated Astra cards. A zero observed completed-card count is not a 0%
eventual success probability. Review passes are not card completions; mixed-model
cards cannot be credited to one model. Raw review verdicts are retained by card,
but no model-specific quality score can be inferred from these unjoined records.
There is no recorded duration, so neither speed nor latency savings can be measured.

**Availability:** AGT-2806 and AGT-2871 each report one `turn.failed` event with
`unsupported_parameter ... access_programs.cyber is not enabled for this organization`.
The continuation identifies these as Astra refusals. This is two refusal events
alongside five Astra token entries, not a verified 2/8 (25%) run failure rate,
nor a proven 2/7 rate. Events may overlap attempts or lack token entries. Their
exact times, last-seen time, cost and unique-run denominator are unknown; they
were observed by the export timestamp. The query explicitly excludes Astra on
that organisation evidence. It does not claim permanent unavailability or charge
a fabricated retry multiplier. The earlier operator's “about eight” count is
approximate context, not a new denominator for this fixture.

**Subscription quota:** the measured account changes above are shares of the
weekly window in percentage points, not list-price bills. All model-specific
quota-per-run and quota-per-completed-card cells remain unknown. Codex probes
combine Astra and Sol; Claude consumption can include reviews or other activity.
There is no exclusive attribution, reset identity or run/probe join. The run
timestamps' assumed timezone and the stated card-setting schedule are also
insufficient to assign models to probe intervals. Dividing 13 points by the 29
Codex token entries, or 9 points by five Opus completions, would invent a cohort
attribution: the windows do not cover the full histories and shared activity is
unaccounted for. The existing start/end probe adapter supports attributable
future observations in both API and CLI; these three probes do not meet that
contract.

**Evidence versus extrapolation:** Astra's $5.39 and Sol's $6.13 are measured
means at unknown levels over different work, including repeat rounds. At an
identical token mix Astra costs 2.5 times Sol (input, cache read and output), so
Astra would need less than 40% of Sol's weighted token volume to win on USD at
equal completion probability. That crossover is a counterfactual, not a
prediction of their turns or quality. The public AA figures ($0.82 Astra/low and
$1.99 Sol/max) remain external benchmark context, never local card cost or a
measurement of Sol/xhigh.

**What would change the answer:** complete the open histories; retain actual
per-run reasoning levels, start/end timestamps and refusal attempt IDs; establish
whether Astra access is now usable; join reviews to attempts; and collect fresh,
exclusive weekly-window probes for matched task classes and correctness floors.
Those facts could establish a lower USD or quota cost per completed card for
Astra, Sol or Opus. The supplied data resolves the double-pricing error and the
per-run means, but does not settle the product owner's level-specific question.
