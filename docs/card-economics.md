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

## Worked answer, 18 September 2026

**There is not yet a defensible local-money winner between Astra/low and
Sol/xhigh from the artifacts available to this implementation.** Use Sol/xhigh
where the correctness floor requires it; for a concept card without that floor,
Astra/low is a candidate for a controlled comparison, not a proven saving.

Evidence available in the dated catalogue: the public AA task figures quoted
in the card are $0.82 for Astra/low and $1.99 for Sol/max. They suggest a public
benchmark hypothesis; they are neither local card costs nor measurements of
Sol/xhigh. The policy keeps Astra selectable and provisional. It also keeps
Sol/medium as the demanding-work default and reserves Sol/xhigh for critical
work. Neither these public results nor this query changes those defaults.

The operator supplied an AGT-2840 usage example: 406 uncached input, 26.6M cache
read, 0.31M cache write, and 120k output tokens. Repricing *that same token shape*
with the September 18 catalogue (including cache creation) gives:

| Counterfactual route | Input USD | Cache read USD | Cache write USD | Output USD | Total USD |
|---|---:|---:|---:|---:|---:|
| Astra/low | 0.00406 | 26.60 | 3.875 | 6.00 | 36.47906 |
| Sol/xhigh | 0.001624 | 10.64 | 1.55 | 2.40 | 14.591624 |

These are **extrapolations of one Opus workload**, not observed Astra or Sol
runs, completed-card estimates, or quota consumption. With identical proportions
and equal completion probability, Astra must use less than 40% of Sol's total
weighted token volume to win. Different context reuse and reasoning output
can change that crossover substantially.

The reported "2 of about 8" Astra access refusals are operator evidence, not a
verified local cohort in this checkout. If a controlled cohort established a
75% success probability and each failure cost as much as a success, a one-run
estimate would need division by 0.75 (a 1.333 multiplier). Mid-run refusals may
cost less; recorded spend should replace that assumption. The schema can record
`unsupported_parameter` with `access_programs.cyber is not enabled for this
organization` and exclude that candidate for the affected organisation.

AGT-2804/2805/2806/2826/2872 and AGT-2873–2880 are the requested cohort candidates.
Their raw attempt/token/review/quota records were not present in the accessible
checkout or result artifacts. Preparation manifests and screenshots are not
completion evidence, so no invented cohort is checked in. Import the original
records through the contract above to replace unknown values with measured
figures. The remaining dependency is an authorised run-storage export, including
organisation identity and quota attribution. A larger matched cohort, resolved
access entitlement, materially fewer Astra turns/context tokens, and measured
weekly-window deltas could each change the answer. Under a subscription the
quota ranking may differ from USD; without attributable probes it stays unknown.
