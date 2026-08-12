# Task-class routing studies

Version: `task-class-taxonomy-v2`
Rationale: `task-class-rationale-2026-08-12`
Evidence through: 2026-08-12

Token Economy answers “which model and thinking level for this task class?”
from a versioned evidence record. A controlled pilot may establish a provisional
class prior; a policy baseline is never presented as a study winner. The
concrete card still goes through `ModelRouter`, so the score, uncertainty,
correctness floors, semantic reissues, run-scoped capacity, and operator pins
remain authoritative.

The library contract is `TaskClassRecommendationCatalog.Default.Recommend`;
it returns a ranked set whose order is stable for serialization but is not a
quota-derived winner. `TaskClassRecommendationCatalog.Select` separately uses
a fresh quota snapshot to choose within that set. Unknown or stale quota keeps
the result recommendation-only. A downgrade is not selection from the set: it
requires a new concrete-card policy evaluation and cannot cross a correctness
floor.
The public website view is
[`website/task-class-routing/index.html`](../website/task-class-routing/index.html),
generated from the same embedded recommendation catalog.

## Taxonomy

| Family | Class | Outcome unit |
| --- | --- | --- |
| Coding | Heavy design | Executable contracts, substantive grade, semantic reissue |
| Judgment | Planning | Executable plan, dependency/constraint coverage, expert acceptance |
| Judgment | Decision-making | Blinded expert agreement and downstream decision quality |
| Coding | Feature and bug implementation | Native fixture pass rate |
| Coding | Mechanical chore | Exact diff and deterministic regression |
| Coding | Documentation edit | Fact, link, and terminology checklist |
| Coding | Research and investigation | Hidden fact/inference key and citation precision |
| Review | Quality Studio review | QS confirmed-finding precision; never treated as recall |
| Implementation | HTML/UI implementation | Rendered defect checklist and blinded jury quality |
| Review | Source-code review | Seeded-defect recall and finding precision |
| Review | Security assessment | Severity-weighted vulnerability recall and unsafe remediation |
| Review | Redundancy detection | Semantic-duplicate recall and precision |
| Judgment | Graphical-quality judgment | Human-calibrated defect/ranking agreement |
| Judgment | Consistency checking | Seeded-inconsistency recall and precision |

The complete definitions, current route, rationale version, evidence lines,
outcome cost, downgrade conditions, and no-downgrade boundaries live in
[`task-class-recommendations.json`](../src/TokenEconomy/catalog/task-class-recommendations.json).

## Controlled pilot: HTML/UI implementation

Protocol: [`html-ui-v1.json`](../benchmarks/task-class-studies/html-ui-v1.json)

Two isolated scenarios compare `gpt-5.6-terra`/medium,
`gpt-5.6-sol`/medium, and `gpt-5.6-sol`/xhigh. The subject produces one
self-contained HTML document. Playwright renders 1440×1000 and 390×844
screenshots. Deterministic checks cover required content, semantic structure,
viewport metadata, a responsive rule, an interactive control, and absence of
external assets. A fixed Sol/xhigh jury receives only the screenshots and the
same defect checklist; the producing route is not named.

`qualityScore` is 75% jury score plus 25% deterministic checklist pass rate.
An outcome passes at `qualityScore >= 0.75`, only when every deterministic
check passes and the jury reports no critical defect. Ranking is pass rate,
mean quality, cost, duration, then stable route ID.

The registered candidate matrix also contains `gpt-5.5`/medium and
`gpt-5.4-mini`/high. They are marked `scheduled`, not silently counted as
failed or described as measured. This keeps GPT-5.5-and-below in the next
identical-scenario wave without rewriting the immutable three-route pilot.

| Route | Passed | Mean quality | Critical defects | Subject list-price cost | Reading |
| --- | ---: | ---: | ---: | ---: | --- |
| Sol/medium | 2/2 | 0.9175 | 0 | $0.425909 total; $0.212954/outcome | Pilot recommendation |
| Terra/medium | 2/2 | 0.86125 | 0 | $0.1730888 total; $0.086544/outcome | Downgrade candidate for bounded, deterministic UI work |
| Sol/xhigh | 1/2 | 0.82 | 1 | $0.519849 total; $0.519849/passed outcome | More thinking did not improve this pilot |

Evidence:

- [`html-ui-incident-console`](../benchmarks/results/html-ui-incident-console/20260812T083546713Z.json)
- [`html-ui-billing-onboarding`](../benchmarks/results/html-ui-billing-onboarding/20260812T084429463Z.json)

The Sol/medium cases used 31,074 additional jury tokens. The v1 pilot retained
their total but not the input/output/cache split needed for an authoritative
catalog cost, so the published $0.212954 is explicitly subject-only. The jury
dollar cost remains unknown rather than becoming zero. The evaluator now
retains the component split for subsequent runs.

Downgrade to Terra/medium only for a reversible, single-frontend-subsystem card
with existing design tokens/components and deterministic visual regression,
when the concrete policy score is at most 50 and no floor applies. A public
design-system contract or three runtime subsystems requires at least Sol/medium.

## Controlled pilot: source-code review

Protocol:
[`source-code-review-v1.json`](../benchmarks/task-class-studies/source-code-review-v1.json)

Two isolated C# snapshots contain ten hidden contract defects spanning tenant
isolation, authorization, atomicity, cancellation, time handling, failed-task
retention, concurrency, and secret logging. The evaluator matches findings by
file, line neighborhood, and semantic evidence against an oracle unavailable
in the subject workspace. It reports raw recall, severity-weighted recall,
finding precision, and false positives.

An outcome passes at 60% recall and 50% precision only when all critical seeds
are found. Ranking is pass rate, severity-weighted recall, precision, cost,
duration, then stable route ID.

The registered candidate matrix also schedules `gpt-5.5`/medium and
`gpt-5.4-mini`/high against the same hidden manifests. No result is claimed for
either candidate until immutable attempts exist.

| Route | Passed | Seeds found | Mean precision | Subject list-price cost | Reading |
| --- | ---: | ---: | ---: | ---: | --- |
| Terra/medium | 2/2 | 10/10 | 0.8125 | $0.0608676 total; $0.030434/outcome | Pilot recommendation for bounded review |
| Sol/xhigh | 2/2 | 10/10 | 0.8125 | $0.329428 total; $0.164714/outcome | Equal recall at about 5.4× cost/outcome |
| Sol/medium | 1/2 | 9/10 | 0.677778 | $0.184918 total; $0.184918/passed outcome | Missed one weighted defect |

Evidence:

- [`tenant cache`](../benchmarks/results/source-code-review-tenant-cache-v2/20260812T082910114Z.json)
- [`tenant transfer`](../benchmarks/results/source-code-review-tenant-transfer-v2/20260812T083344557Z.json)

This recommends Terra/medium only for bounded source review with a compact
contract. A security boundary still requires Sol/xhigh. Ambiguous/unbounded
evidence that can authorize a security, destructive, or lane-affecting action
requires at least Sol/medium. Cost never crosses those floors.

The excluded original transfer run is retained under
`benchmarks/results/source-code-review-tenant-transfer/`: nested bubblewrap
prevented source reads, Terra and Sol/medium returned empty reviews, and
Sol/xhigh exceeded the then-active measurement cap before evaluation. It is an
infrastructure/protocol failure, not negative model evidence.

## Quality Studio evidence line

The committed Quality Studio aggregate is observational and separate from the
controlled review pilot. Both routes relevant to the pilot currently have the
same line:

| Model | Eligible operational runs | Evidence quality | Routing use |
| --- | ---: | --- | --- |
| `gpt-5.6-terra` | 0 | `insufficientEvidence` | None |
| `gpt-5.6-sol` | 0 | `insufficientEvidence` | None |

The only QS artifact is a fixture and is excluded. Even a future gated
confirmation rate is precision over findings the reviewer emitted; it does not
measure misses and cannot replace seeded-defect recall.

## Qualification and next slices

The pilots have two scenarios and one attempt per route per scenario. They are
not validated: qualification requires at least five scenarios, 20 attempts per
route, 70% outcome coverage, and at most 10% semantic reissue. Subjective
classes also require human/jury calibration and randomized blinded order.

1. Security assessment: seeded authn/authz, injection, secrets, and unsafe
   remediation; keep Sol/xhigh as the hard floor.
2. Redundancy detection: known clone pairs plus intentional variants; score
   semantic recall, precision, and false merge suggestions.
3. Graphical-quality judgment: human-calibrate the checklist, then compare
   Mini/high against stronger fixed jurors using agreement and pairwise rank.
4. Consistency checking: hidden invariants across schemas, prose, API fields,
   and UI states; separate bounded checks from open-ended research.
5. Existing coding classes: expand feature evidence and add heavy design,
   mechanical, docs, and research scenarios with native/factual oracles.
6. Planning and Decision-Making: build separate realistic corpora with blinded
   expert ground truth and strong routes as the ideal floor; do not collapse
   consequential choices into the Mini bounded-label exception.

This follows delegation economy: select the cheapest route that cleared the
measured outcome, include verification and retry cost, and move upward when the
class prior does not fit the concrete card or uncertainty exceeds the bounded
study.
