# Evidence map

This map distinguishes external transfer evidence, TokenEconomy measurements,
and policy intuition. “Supported” never means universal; it means the cited
evidence is sufficiently aligned to inform the stated claim.

## TokenEconomy evidence register

| ID | Retained evidence | What it can support | Main limit |
| --- | --- | --- | --- |
| O01 | [`palindrome-repair` 2026-08-09 report](../../../benchmarks/results/palindrome-repair/20260809T092240107Z.report.json) | One simple local repair: Terra/medium, Sol/medium, and Claude Sonnet 5/high each passed 3/3. Terra used slightly fewer average tokens than Sol. | One synthetic fixture and three repeats per route; no equivalence interval; only Claude cost was present in the raw report. |
| O02 | Three specified [curated hard coding reports](../../../benchmarks/results/) for off-by-one, Unicode locale, and cross-file repair | Across nine attempts per route, Sol/medium passed 9/9, Terra/medium 2/9, and Claude Sonnet 5/high 4/9. This favors the strong route on these deliberately awkward cases. | Three synthetic C# fixtures; no blinded task sample; catalog cost was missing for two routes in the reports. |
| O03 | [Underspecified coding report](../../../benchmarks/results/curated-hard-coding-underspecified/20260808T162707845Z.report.json) | Every route failed 0/3 under the deterministic gate. Stronger models do not repair an inadequate task contract automatically. | One intentionally ambiguous fixture; its gate rewards a specific expected behavior. |
| O04 | [Canonical routing policy historical basis](../../system/domains/model-routing-policy.md#benchmark-basis) | 152 observational task records; the Sol/medium chore/feature cohort had 0/7 reissues and 5/5 known grades favorable. Two Mini/medium records were grade B without reissue. | Observational, 33.9% grade coverage, mixed pipeline causes, final-route attribution, and no Luna cohort. It cannot validate causal model superiority. |
| O05 | [Quality Studio review evidence](../../quality-studio-review-evidence.md) | Schema, provenance, and confidence-gate behavior for review evidence. | Only fixture data; no real model qualifies. `SuggestModel(review, ...)` correctly returns no candidate. |
| O06 | [Document-to-text methodology](../../benchmarks.md#document-to-text-capability-benchmark) and retained capability runs | The harness can separate capability misses from infrastructure failures and retain model-by-format outcomes. | One case per document type per run, unknown thinking levels, mixed infrastructure, and no qualifying cohort. It does not prove smaller-model equivalence. |
| O07 | [Website-build evidence](../../../benchmarks/results/website-build/20260725T000000Z.report.json) | Generated website data is reproducible from retained inputs. | It compares no models and says nothing about HTML-generation capability. |
| O08 | No controlled Planning or Decision-Making benchmark | Nothing yet. | The requested strong-ideal rule is an operator risk decision backed only by external transfer evidence. |
| O09 | [`ModelPriceCatalog`](../../../src/TokenEconomy/ModelPriceCatalog.cs), [`CostBreakdown`](../../../src/TokenEconomy/CostBreakdown.cs), and [v0.3.2 price data](../../../src/TokenEconomy/catalog/model-prices.json) | Date-aware list-price costing by fresh input, output, cache read, and cache write; explicit unknown/unpriced status. | Price is not capability, actual invoice cost, subscription quota value, latency, or retry probability. |

The `../../../benchmarks/results/` link for O02 is intentionally a directory
because the evidence is three immutable sibling reports, not one synthesized
artifact. The counts above are a dossier calculation and do not rewrite those
raw reports.

## Claim map

| Task class | Claim under consideration | External evidence | Own evidence | State |
| --- | --- | --- | --- | --- |
| `Planning` | Strong models are `Ideal`; smaller bands are below the default floor. | E02 managerial proposals; E10 long/messy task reliability; E13 deterministic planning failures. | **None: O08.** | **Provisional policy decision.** Good risk basis, no local model comparison. |
| `DecisionMaking` | Strong models are `Ideal`, including technical proposal and architecture choices. | E02 has 724 real manager decisions, 99% validation agreement, and no tested model reached majority accuracy on the full set. | **None: O08.** | **Provisional policy decision.** Strongest aligned external evidence in the dossier; one-repository/vendor caveat remains. |
| `HeavyDesign` | Strong capability is the minimum for broad architecture or multi-subsystem design. | E01, E10, E11, E12. | O04 supplies only observational policy support. | **Provisional.** No design-artifact benchmark with accepted outcomes. |
| `Feature` — demanding | Strong routes outperform smaller routes on cross-file, long-horizon, or context-heavy implementation. | E01–E03; E02 UI/UX slice; E07 for visual repositories. | O02 favors Sol 9/9 over Terra 2/9 and Claude fallback 4/9. | **Supported for a conservative floor**, not a universal named-model rank. |
| `Feature` — simple local repair | A balanced/smaller set can be equally good as a strong route when scope and verification are tight. | E04 observed smaller/flagship equality after two attempts; E14–E15 show task-conditioned routing. | O01 observed 3/3 for all three routes. | **Provisional.** Direct local evidence is one fixture; “equal” is not statistically established. |
| Unclear or underspecified bug | Buying a stronger model is insufficient when requirements or the gate are missing. | E01 deliberately adds human-written requirements; E03 documents ambiguous/grader-broken issues. | O03: all three routes failed all repetitions. | **Supported.** Improve task evidence before repeated escalation. |
| `MechanicalChore` | Smaller/light models are `Ideal`; a flagship is usually overkill. | E14–E15 are indirect evidence that cheap models handle a large simple-query share. | **None.** O04 has no controlled model comparison for chores. | **Intuition / policy hypothesis.** This is the clearest current evidence gap behind an existing recommendation. |
| `DocEdit` | Smaller/light models are `Ideal` for bounded prose edits. | No study here evaluates repository documentation acceptance. E14 is only an indirect query-routing analogy. | **None.** | **Intuition only.** Do not describe this as benchmark-proven. |
| `Research` — open-ended | Strong models are `Ideal` for synthesis that requires experiment planning, code, and iterative correction. | E10–E12. | **None.** | **Externally supported, locally unvalidated.** Bounded lookup/summarization is not covered by this claim. |
| `Review` — real PR | Keep a strong provisional floor; do not infer review quality from coding pass rate. | E05–E06 show low recall, context sensitivity, and task-specific evaluation. | O05 has no real runs. | **Externally supported risk floor; named route is a gap.** |
| `Review` — equivalence set | Several models can be recommendation peers rather than a forced total order. | E06 reports its top four statistically indistinguishable. | **None.** | **Semantics supported; concrete membership unknown.** |
| Visual/interactive HTML and frontend | Use strong multimodal/coding capability and rendered behavior checks. | E02 UI/UX subset, E07–E09. | **None: O07 is only a data-build check.** | **Externally supported, locally unvalidated.** |
| Static template HTML | A smaller model may be equally good under screenshot, DOM, accessibility, and interaction gates. | E08 covers static screenshot reproduction but does not establish small-model parity; E09 separates static from harder levels. | **None.** | **Hypothesis.** No current basis for “equally well.” |
| Bounded structured pipeline decision | A smaller model can serve when evidence is compact, output is parseable, and the call cannot cross a risk floor. | E14–E15 support bounded routing/classification generally. | O04 has two favorable Mini/medium observations, the wrong size and setting for validation. | **Provisional role exception.** Preserve the current ambiguity/destructive-action promotion rule. |
| Document-to-text transform | Smaller models serve equally well. | No aligned source in this collection. | O06 is sparse and contains material model/format differences. | **Not supported.** Keep per-format qualification and explicit `NotAttempted`. |
| Quota-aware concrete choice | Quota should pick among capability-equivalent routes, never redefine capability. | E14–E15 separate quality/candidate behavior from a later routing threshold, but do not study quota. | O04 and the current router encode quota after the floor. | **Design supported; quota benefit not empirically measured.** |
| Cost efficiency | Evaluate tokens and total attempt cost per accepted delivery, including retries. | E02, E04, E14–E16. | O01–O03 retain attempts/tokens; O09 can date-cost usage. | **Supported metric direction.** Cost coverage and acceptance semantics must remain explicit. |

## What currently rests on intuition

The following claims must remain visibly provisional in product copy and API
evidence fields:

- Luna/medium for trivial core work: the canonical policy already states that
  no Luna cohort existed in its historical benchmark.
- Terra/medium as the everyday sweet spot: historical observations have no
  known terminal grades, and the small controlled hard suite is unfavorable.
- Mini/high for bounded pipeline decisions: structurally sensible, but the two
  historical Mini records used medium thinking and do not validate this role.
- Smaller models for mechanical chores and doc edits: no controlled local
  comparison exists.
- Any named model for Planning, Decision-Making, Review, or HTML/frontend work:
  no task-local TokenEconomy qualification exists.
- Equivalence of specific current providers: provider fallback evidence remains
  small and observational. A shared tier label is not proof.
- Cost savings from quota-aware selection: the behavior is safe by construction,
  but no counterfactual replay measures accepted-delivery savings yet.

## Priority evidence wave

1. **Planning and decisions.** Build separate corpora: executable plan creation
   and technical proposal selection. Include realistic repository context,
   blinded expert ground truth, at least the current strong route and two
   smaller candidates, and pre-register a non-inferiority margin. Use a power
   calculation; the routing gate's 20-sample minimum is not automatically enough
   to prove equivalence.
2. **Real pull-request review.** Sample real PRs with adjudicated findings and
   clean diff/file-context conditions. Measure precision, recall, actionable
   finding acceptance, false-positive cost, and repeat aggregation. Fixture runs
   remain excluded.
3. **HTML/frontend split.** Benchmark static HTML, screenshot reproduction, and
   interactive behavior separately. Combine pixel/layout measures with DOM,
   accessibility, and Playwright gates; record human acceptance for visual
   differences that metrics cannot settle.
4. **Small-work equivalence.** Add mechanical chores, doc edits, and local
   repairs sampled from real repositories. Compare candidates with identical
   prompts/scaffolds and at least three stochastic repetitions, then estimate
   paired outcome differences rather than declaring a winner from averages.
5. **Quota replay.** Replay retained attempts through the proposed set selector
   with historical quota snapshots. Compare waits, unsafe-floor violations,
   accepted deliveries, tokens, and retry-adjusted cost against the current
   single recommendation. Do not synthesize healthy quota where the snapshot is
   absent or stale.

Every wave should retain raw attempts, actual model/thinking route, scaffold
version, task/capability, outcome classification, token components, duration,
price status, execution timestamp, and source hash. Controlled and observational
evidence remain separate.
