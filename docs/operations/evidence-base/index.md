# TokenEconomy evidence base

Status: decision-pending

Phase: decision-ready

Evidence reviewed through: 2026-08-12

Source card: TE-43

## Decision in one page

TokenEconomy should recommend the least expensive **evidence-qualified set**
that clears the task's correctness floor. It should not pretend that every task
needs a flagship model, or that one leaderboard identifies a universal winner.

The proposed decisions are:

1. Add `Planning` and `DecisionMaking` as explicit task classes. Their `Ideal`
   suitability is the strong capability band. Cost or quota must not make a
   smaller band ideal for these classes.
2. Make recommendation and selection separate operations. Recommendation
   returns a set of task-equivalent model-and-thinking routes without consulting
   run-scoped quota. Selection may choose one route only when the caller supplies
   current, trustworthy quota state.
3. Treat `MechanicalChore`, `DocEdit`, tightly scoped local repair, and bounded
   structured transformations as the principal smaller-model territory. The
   claim is conditional on a clear contract and deterministic verification.
   Only simple local repair currently has direct, if very small, TokenEconomy
   comparison data; the other smaller-model defaults remain provisional.
4. Keep demanding cross-file implementation, open-ended research, real pull
   request review, visual or interactive frontend work, planning, and
   consequential decision-making at a strong floor until task-local evidence
   establishes a non-inferior smaller set.
5. Add outcome metrics over retained attempts: billed tokens per accepted
   outcome, estimated list-price cost per accepted delivery, observed
   retry-adjusted cost, retry uplift, and an evidence-gated expected cost to
   acceptance. All cost calculations must use the attempt timestamp and the
   shipped pricing catalog.

These decisions extend rather than silently replace the canonical
[model-routing policy](../../system/domains/model-routing-policy.md). TE-42 or a
later policy card must version any executable policy change.

## Why this is the defensible boundary

Realistic evaluations point in both directions.

- SWE-Bench Pro, SWE-Lancer, METR time horizons, PaperBench, and RE-Bench show
  that long, ambiguous, multi-step work remains materially harder than short,
  well-specified work. SWE-Lancer also shows a large improvement from higher
  reasoning effort on its implementation track and incomplete performance on
  managerial choices.
- Aider's polyglot benchmark contains an observed tie between a mini model and a
  flagship model after two attempts, at substantially different list-price
  spend. FrugalGPT and RouteLLM show that learned routing can preserve aggregate
  quality while sending many requests to cheaper models.
- Those efficiency results do not validate blanket down-routing. They use
  exercises or general question-answering distributions, and their success
  depends on a verifier or a router calibrated to the target distribution.
- Code-review and frontend suites show particularly low absolute performance
  and strong sensitivity to context and scaffolding. These classes should not
  inherit conclusions from function-level coding tests.

The evidence therefore supports task-specific sets and measured outcomes. It
does not support one global model order.

## Confidence of the proposed defaults

| Proposed default | Confidence | Basis |
| --- | --- | --- |
| Planning is strong-ideal | Moderate | Direct planning and managerial-choice evaluations show persistent failure; TokenEconomy has no controlled planning comparison. |
| Decision-making is strong-ideal | Moderate | SWE-Lancer has 724 real proposal-selection tasks and a 99% validation agreement, but it is one repository and vendor-authored. |
| Hard cross-file implementation needs strong models | Moderate | Large external repository benchmarks agree with TokenEconomy's small hard-case suite. |
| Simple local repair can use a smaller/balanced set | Low to moderate | All three tested routes passed the current three-repeat palindrome case; one fixture cannot establish general equivalence. |
| Mechanical chores and doc edits are smaller-ideal | Low | Sensible cost/risk policy and indirect routing evidence; no controlled TokenEconomy cohort yet. |
| Real PR review should keep a strong floor | Moderate | Two recent PR-review suites show low recall and context sensitivity; TokenEconomy has no non-fixture review evidence. |
| Visual or interactive frontend work needs strong capability | Moderate | Three external suites cover real pages or visual JavaScript issues; no TokenEconomy HTML/model comparison exists. |
| Set-valued recommendation is preferable to a total order | Moderate to high | Routing research shows heterogeneous per-query winners; review and coding suites contain close or tied results. |

## Terms used in this dossier

**Strong** means the highest capability band required by the task, represented
by the current Sol routes or a separately qualified provider peer. It is a
capability floor, not a hard-coded vendor.

**Smaller** means a balanced or light route, including the GPT-5.5 tier and
below when a concrete model is selectable and qualified. Mentioning a cataloged
model here does not change its routing status.

**Equivalent** means that the available task-local evidence cannot distinguish
a candidate from the best qualified candidate beyond a declared
non-inferiority margin. Sharing a capability label is not enough.

**Accepted delivery** means one task delivery that passed its declared external
gate or received an explicit human acceptance outcome. A model's self-report is
not acceptance.

## Dossier contents

- [Study collection](studies.md): 16 external studies, suites, and independent
  leaderboards with methodology ratings and transfer limits.
- [Evidence map](evidence-map.md): claim-by-task mapping to external evidence,
  TokenEconomy evidence, and explicit gaps.
- [Recommendation semantics](recommendation-semantics.md): task-class defaults,
  equivalence, set-valued results, and quota-aware selection.
- [Efficiency metrics](efficiency-metrics.md): input contract, formulas, null
  semantics, and proposed pure-library surface.

## Decisions requested

The operator can approve these independently:

1. Approve the strong-ideal semantics for `Planning` and `DecisionMaking`.
2. Approve recommendation sets as the quota-independent API result and concrete
   selection as a separate quota-aware function.
3. Approve the conditional smaller-model territory and its explicit
   provisional labels.
4. Approve the efficiency metric definitions and require attempt-level inputs
   before implementation.
5. Commission the gap-closing benchmark wave listed in the evidence map before
   any named model is declared equivalent for review, planning, decisions, or
   HTML/frontend work.

## Non-claims

This dossier does not rank every catalog model, certify current vendor claims,
or make external benchmark scores interchangeable with TokenEconomy task
outcomes. It does not supersede correctness floors, convert missing prices to
zero, or infer that a model failure caused an infrastructure failure. External
results are transfer evidence; model qualification still requires comparable,
retained local cohorts.
