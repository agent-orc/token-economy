# Historical model-trust evidence

This ledger is evidence, not a provider capability ranking. A model's trust
level is derived from retained successful evidence and open material incidents;
the operational violation rate is a separate descriptive measure:

`recorded non-rejected violations / retained observed runs`

The result is `null` if the denominator is absent. It is not a zero-percent
rate and must not be used for a routing decision. Rates with fewer than 30
retained runs are labelled as small samples and must not be generalized.

## Recovered historical incidents

| Record | Classification | Source reference | Attribution | Status |
| --- | --- | --- | --- | --- |
| AIP-7 (2026-07) | agent-git-violation | `decision-journal/AIP-7` | Model and CLI not recorded | Resolved |
| AIP-10 (2026-07) | agent-git-violation | `decision-journal/AIP-10` | Model and CLI not recorded | Resolved |
| TE shared-checkout collision | shared-checkout-collision | `escalation-records/TE-shared-checkout-collision` | Model and CLI not recorded | Resolved |

The three records are available in `HistoricalModelTrustEvidence` under the
explicit `unattributed` model bucket. The incident sources identify month only;
the API normalizes their timestamp to `2026-07-01T00:00:00Z` solely for stable
ordering, not as an assertion of the day of occurrence.

## Current rate analysis

| Model / CLI | Violations | Retained runs (N) | Rate | Interpretation |
| --- | ---: | ---: | --- | --- |
| `unattributed` / all CLIs | 3 | 0 | unavailable | The journals prove three incidents but provide neither model/CLI attribution nor a run denominator. No per-model or per-CLI rate can honestly be reported. |

No named model or CLI has a historical rate yet. Import a durable run record
for every denominator and attach the incident to its model, CLI, and (when
known) run id before reporting a model/CLI-specific rate. Rejected incidents
are excluded from the numerator; resolved incidents remain in the historical
rate so the evidence is not erased.

## Routing qualifications

Versioned routing cohorts are described in
[`routing-evidence.md`](routing-evidence.md). `RoutingEvidenceTrust.FromReport`
converts controlled cohorts to ledger entries. Only a cohort that clears the
declared confidence gates has a supporting outcome; below-gate cohorts remain
inconclusive, and observational support is not converted into independent
proof. Independent proof count is the count of distinct retained artifacts,
not the count of capability rows derived from one report.
