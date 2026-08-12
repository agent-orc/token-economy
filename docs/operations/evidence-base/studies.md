# Study collection

Reviewed through 2026-08-12. References point to the paper, official benchmark
site, or maintainer methodology rather than a secondary summary.

## Rating method

The overall rating is a transfer-quality judgment for TokenEconomy, not a
judgment of the authors.

| Rating | Meaning for this dossier |
| --- | --- |
| A | Strong decision evidence: substantial sample, realistic outcomes, current method, and manageable bias. |
| B | Useful evidence with one material limitation or an indirect transfer to our task classes. |
| C | Context only: old models, artificial tasks, small macro sample, unvalidated judge, or strong conflict risk. |
| D | Do not use for a routing claim. No retained source in this collection has this rating. |

Sample size is rated on the independent unit that can vary, not on the number of
rubric rows derived from one task. Task realism distinguishes executable work in
a repository from exercises, screenshots, or question answering. Recency is
measured at the review date. Vendor bias records whether an organization that
builds or sells evaluated models authored the evaluation; it does not imply
misconduct.

## Coding and software-engineering evaluations

### E01 — SWE-Bench Pro

Reference: Xiang Deng et al., 2025, [SWE-Bench Pro: Can AI Agents Solve
Long-Horizon Software Engineering Tasks](https://arxiv.org/abs/2509.16941).

What it measured: 1,865 human-verified problems from 41 maintained public,
held-out, and commercial repositories. Tasks require multi-file patches under a
unified SWE-Agent scaffold and pass human-reviewed tests. The public evaluation
has 731 tasks; the commercial evaluation has 276. Top reported pass@1 remained
below 25%, and smaller models were substantially less consistent across
repositories and languages.

Quality: **A-** — sample: large; realism: high; recency: current; vendor bias:
moderate (Scale authors and operates evaluations, but is not the evaluated model
provider). Strengths include held-out/private repositories, human augmentation,
three-run flaky-test filtering, and a fixed scaffold. Limits include incomplete
language coverage, private subsets that cannot be independently inspected, and
scaffold-dependent results.

Supports for our classes: strong support for a strong floor on demanding
`Feature`, unclear bug, cross-file work, and `HeavyDesign`; evidence against
using a small-model result from a compact exercise as a general coding route.
It does not address prose, review comments, or planning-only deliverables.

### E02 — SWE-Lancer

Reference: Samuel Miserendino et al., 2025, [SWE-Lancer: Can Frontier LLMs Earn
$1 Million from Real-World Freelance Software
Engineering?](https://arxiv.org/abs/2502.12115).

What it measured: 1,488 paid freelance tasks from one full-stack repository:
764 implementation tasks graded with triple-verified Playwright tests and 724
manager tasks choosing among real technical proposals. The managerial ground
truth had 99% agreement in an additional engineer validation. In the Diamond
implementation subset, higher reasoning effort raised one model from 9.3% to
16.5% pass@1. The paper also measured five-attempt API spend plus human fallback.

Quality: **A-** — sample: large; realism: high; recency: current; vendor bias:
high (OpenAI authored the benchmark and evaluated its models). Real payouts,
hidden end-to-end tests, 100 professional annotators, a private holdout, and
explicit cost repetitions are strong. Concentration in one repository,
predominantly bug work, single-attempt main scores, and provider conflict limit
generalization.

Supports for our classes: the best direct external evidence for
`DecisionMaking`; strong support for strong-ideal `Planning`/proposal evaluation,
demanding implementation, retry-aware cost, and browser-verified frontend work.
It also shows that more attempts can improve pass rate while changing total
economics. It does not show that every decision needs a flagship; bounded
classification remains a different class.

### E03 — SWE-bench Original and Verified

References: Carlos Jimenez et al., 2024, [SWE-bench: Can Language Models Resolve
Real-World GitHub Issues?](https://arxiv.org/abs/2310.06770); OpenAI and the
SWE-bench authors, 2024, [Introducing SWE-bench
Verified](https://openai.com/index/introducing-swe-bench-verified/); OpenAI,
2026, [Why SWE-bench Verified no longer measures frontier coding
capabilities](https://openai.com/index/why-we-no-longer-evaluate-swe-bench-verified/).

What it measured: the original suite contains 2,294 issue-resolution tasks from
12 Python repositories. Verified is a 500-task human-filtered subset. Patches
are scored by fail-to-pass and pass-to-pass tests. A 2026 audit of 138
frequently failed Verified tasks found material problem/test issues in at least
59.4% of that audited slice.

Quality: **B** for evaluation shape, **C** for current model ranking — sample:
large; realism: high; recency: aging and now partly superseded; vendor bias:
mixed (academic original, OpenAI co-produced Verified and its audit). Public
repositories, reproducible containers, and executable tests are valuable.
Contamination, Python concentration, benchmark saturation, and demonstrated
grader defects prevent treating its leaderboard as current ground truth.

Supports for our classes: strong methodological support for repository-level
`Feature` and bug fixtures and for retaining regression tests. It supports our
choice to prefer newer, held-out, task-local evidence. It should not qualify a
current named model by itself.

### E04 — Aider polyglot benchmark and leaderboard

References: Aider maintainers, [leaderboard and per-run
details](https://aider.chat/docs/leaderboards/) and [benchmark
harness](https://github.com/Aider-AI/aider/blob/main/benchmark/README.md).

What it measured: 225 challenging Exercism exercises across six languages,
with native tests, edit-format correctness, two attempts, tokens, duration, and
run cost. One published comparison reported the same 72.0% two-attempt score
for o4-mini/high and Claude Opus 4 with 32K thinking, while recorded run cost
was $19.64 versus $65.75; their first-attempt rates differed materially.

Quality: **B** — sample: medium; realism: medium; recency: current as a living
leaderboard; vendor bias: low. The runnable corpus and resource reporting are
valuable. The tasks are compact exercises rather than repository work; model
runs occur on different dates and sometimes use different edit formats; the
leaderboard does not publish non-inferiority tests.

Supports for our classes: direct evidence that a smaller model can be an
economically equivalent candidate on a bounded coding distribution, and that
pass@2 can hide a poor pass@1. It supports set-valued `Feature` candidates and
retry-adjusted metrics, not a blanket small-model coding default.

## Review evaluations

### E05 — SWRBench

Reference: Zhengran Zeng et al., 2026, [Benchmarking and Studying the LLM-based
Code Review](https://arxiv.org/abs/2509.01494) (PACMSE/FSE 2026).

What it measured: review-comment generation over 1,000 manually verified GitHub
pull requests with project context and structured ground truth. Its automated
coverage evaluation reports about 90% agreement with human judgment. The study
found low absolute performance and up to a 43.67% F1 improvement from
multi-review aggregation.

Quality: **A-** — sample: large; realism: high; recency: current; vendor bias:
low. PR-level context, manual verification, publication review, and human judge
validation are strengths. Ground truth remains limited to known issues, and an
LLM-based evaluator can miss aspects not represented in the structure.

Supports for our classes: strong evidence that `Review` is not interchangeable
with code generation, that single-model review is not yet dependable, and that
multiple independent review attempts may improve recall. It does not identify a
TokenEconomy-qualified review route.

### E06 — SWE-PRBench

Reference: 2026, [SWE-PRBench: Benchmarking AI Code Review Quality Against Pull
Request Feedback](https://arxiv.org/abs/2603.26130).

What it measured: eight frontier models reviewing 350 human-annotated pull
requests under diff-only, file-context, and full-context conditions. The judge
was validated at Cohen's kappa 0.75. Models detected only 15–31% of human-flagged
issues in the diff-only configuration; the top four were statistically
indistinguishable, and every tested model degraded with more supplied context.

Quality: **B** — sample: medium; realism: high; recency: current; vendor bias:
low. Human ground truth, frozen context ablations, and reported agreement are
strong. It is a preprint, uses an LLM judge, and human review comments are an
imperfect recall oracle.

Supports for our classes: direct support for recommendation sets when several
review models are indistinguishable, a strong provisional floor for real PR
`Review`, compact task-relevant context, and explicit review recall/precision
metrics. It argues against ranking review models from generic coding scores.

## HTML and visual frontend evaluations

### E07 — SWE-bench Multimodal

Reference: John Yang et al., ICLR 2025, [SWE-bench Multimodal: Do AI Systems
Generalize to Visual Software Domains?](https://arxiv.org/abs/2410.03859).

What it measured: 619 real visual issues from 17 user-facing JavaScript
repositories spanning JavaScript, TypeScript, HTML, and CSS. Expert validation
found images necessary for 83.5% of instances. The leading tested system solved
12%, versus 6% for the next system.

Quality: **A-** — sample: large; realism: high; recency: recent; vendor bias:
low. Real issues, executable repository work, expert visual-necessity checks,
and peer review are strong. Results are tied to older systems and a particular
agent interface; the task is visual issue repair, not greenfield page creation.

Supports for our classes: strong evidence that visual frontend/HTML work needs
its own capability evidence and currently warrants strong models plus visual
verification. Text-only coding rank is not enough.

### E08 — Design2Code

Reference: Chenglei Si et al., NAACL 2025, [Design2Code: Benchmarking Multimodal
Code Generation for Automated Front-End
Engineering](https://arxiv.org/abs/2403.03163).

What it measured: screenshot-to-HTML/CSS generation for 484 manually curated,
diverse real webpages. Automatic visual/content metrics were supplemented by
human evaluation. Error analysis found persistent difficulty recalling visual
elements and reproducing layouts.

Quality: **B+** — sample: medium; realism: medium to high; recency: recent;
vendor bias: low. Real pages, human validation, and peer review help. The
single-page screenshot reconstruction task omits repository conventions,
behavior, accessibility, maintainability, and backend integration.

Supports for our classes: evidence for strong visual capability and rendered
browser evaluation in HTML tasks. It does not support a model for ordinary
hand-authored doc HTML or prove production code quality.

### E09 — Vision2Web

Reference: Zehai He et al., 2026, [Vision2Web: A Hierarchical Benchmark for
Visual Website Development with Agent
Verification](https://arxiv.org/abs/2603.26648).

What it measured: 193 real-site tasks across 16 categories, 918 prototype
images, and 1,255 test cases, ranging from static UI reproduction through
interactive multi-page and full-stack development. Evaluation combines a GUI
agent verifier with a vision-language judge; current systems still struggled on
the full-stack level.

Quality: **B-** — sample: medium at the task level; realism: high; recency:
current; vendor bias: moderate (model-producing organizations among authors).
Hierarchical tasks and workflow tests are valuable. It is a recent preprint and
partly judge-based, with only 193 independent tasks.

Supports for our classes: current corroboration that static HTML and
interactive/full-stack frontend work must be separated. Only the former is a
candidate for smaller models after deterministic rendering and behavior gates.

## Planning, decisions, and research

### E10 — METR task-completion time horizons

Reference: Thomas Kwa et al., NeurIPS 2025, [Measuring AI Ability to Complete
Long Tasks](https://arxiv.org/abs/2503.14499).

What it measured: 13 frontier models on 170 software and research tasks with
skilled-human completion-time baselines. It estimates the human task duration
at which an agent succeeds 50% of the time, and separately reports an 80%
horizon roughly five times shorter. Longer and messier tasks were less reliable.

Quality: **B+** — sample: medium; realism: medium to high; recency: current;
vendor bias: low (independent nonprofit). Human time baselines, repeated model
history, and explicit uncertainty are strengths. Tasks overrepresent clean,
self-contained technical work; a time horizon is not a productivity forecast
for a normal organization.

Supports for our classes: strong support for escalating long, messy,
open-ended `Planning`, `Research`, and implementation work, and for using a
high-reliability outcome rather than any-success as the route target.

### E11 — RE-Bench

Reference: Hjalmar Wijk et al., ICML 2025, [RE-Bench: Evaluating Frontier AI
R&D Capabilities of Language Model Agents against Human
Experts](https://proceedings.mlr.press/v267/wijk25a.html).

What it measured: seven novel open-ended ML research-engineering environments,
with 71 eight-hour attempts from 61 human experts and public agent trajectories.
Humans and agents used the same resources; agents were competitive at short
budgets but humans continued improving over longer runs.

Quality: **B** — sample: small at the environment level, rich at the attempt
level; realism: high for ML research; recency: current; vendor bias: low. Novel
tasks and faithful human comparison are strong. Seven environments cannot
represent research generally, and best-of-k/scaffold choices affect results.

Supports for our classes: direct support for strong `Research` and planning on
novel, open-ended technical work, and for measuring retries/attempt budgets. It
does not transfer to routine documentation research.

### E12 — PaperBench

Reference: Giulio Starace et al., 2025, [PaperBench: Evaluating AI's Ability to
Replicate AI Research](https://openai.com/index/paperbench/).

What it measured: end-to-end replication of 20 ICML 2024 papers, decomposed into
8,316 author-co-developed rubric items and graded by a separately evaluated LLM
judge. The best tested agent scored 21.0% and did not beat the recruited ML PhD
baseline.

Quality: **B-** — sample: small at 20 independent papers; realism: high;
recency: current; vendor bias: high (OpenAI authored and evaluated the suite).
Author-created rubrics, runnable artifacts, and human baselines are valuable.
The small, AI-specific domain and automated judge limit transfer.

Supports for our classes: corroborates a strong floor for open-ended
`Research`, research planning, and long artifact production. It does not
justify strong models for bounded source lookup or summarization.

### E13 — PlanBench

Reference: Karthik Valmeekam et al., [PlanBench: An Extensible Benchmark for
Evaluating Large Language Models on Planning and Reasoning about
Change](https://arxiv.org/abs/2206.10498).

What it measured: automatically verified plan generation, optimal planning,
replanning, reuse, and goal reformulation in Blocksworld. Principal model runs
used 500 instances per task, with a 50-person human baseline. The evaluated 2022
models performed poorly on plan generation and replanning.

Quality: **C+** — sample: large generated sample but one domain; realism: low;
recency: old model cohort; vendor bias: low. Deterministic plan validation and a
human baseline are useful. Synthetic Blocksworld and obsolete models prevent a
current model-tier conclusion.

Supports for our classes: supports treating planning as a distinct capability
with executable checks where possible. It is contextual, not current proof that
one named strong model is sufficient.

## Model-efficiency and independent comparison work

### E14 — FrugalGPT

Reference: Lingjiao Chen, Matei Zaharia, and James Zou, 2023, [FrugalGPT: How to
Use Large Language Models While Reducing Cost and Improving
Performance](https://arxiv.org/abs/2305.05176).

What it measured: learned cascades over 12 APIs on three datasets with 10,000,
2,400, and 7,982 examples. At matched aggregate accuracy, reported savings
ranged from 59.2% to 98.3%; cheap models sometimes answered cases that the
expensive model missed.

Quality: **B-** — sample: large; realism: low to medium; recency: old model and
price cohort; vendor bias: low. Held-out task splits and explicit price-quality
optimization are strong. The tasks are classification and reading
comprehension, not agentic coding; results require a trained confidence model.

Supports for our classes: establishes that per-task or per-query routing can
outperform a single global winner and supports retaining multiple candidates.
It is indirect evidence for smaller `DocEdit`, `MechanicalChore`, or bounded
transform routes, not proof of equivalence on those classes.

### E15 — RouteLLM

Reference: Isaac Ong et al., 2024/2025, [RouteLLM: Learning to Route LLMs with
Preference Data](https://arxiv.org/abs/2406.18665).

What it measured: learned routing between a strong and weak model using human
preference data, evaluated on MT-Bench, MMLU, and GSM8K. Maintainers report up
to 85% cost reduction while retaining 95% of GPT-4 performance in one MT-Bench
setting, with some transfer to different model pairs.

Quality: **B-** — sample: medium to large depending on suite; realism: low for
our tasks; recency: recent; vendor bias: low to moderate (academic collaboration
with a serving provider). Reproducible code and explicit threshold calibration
are strengths. Results are aggregate, pair-specific, and not about repository
work or quota.

Supports for our classes: supports separating qualification from selection and
calibrating on the caller's task distribution. It does not support crossing a
correctness floor or selecting from unknown quota.

### E16 — Artificial Analysis

Reference: Artificial Analysis, 2026, [Intelligence Index v4.1.1
methodology](https://artificialanalysis.ai/methodology/intelligence-benchmarking).

What it measured: an independently executed composite of nine current agent,
coding, reasoning, and knowledge evaluations, including 89 Terminal-Bench tasks
with three repeats and 220 GDPval tasks. The service publishes capability,
price, speed, and score-versus-cost views under one methodology.

Quality: **B** — sample: mixed; realism: mixed; recency: current; vendor bias:
low for model vendors but moderate commercial-methodology interest. Standardized
independent runs, repeats on several components, and visible weights are useful.
Some tasks and judges are proprietary, individual evaluations have wider
uncertainty than the composite, and the index weights encode a product choice.

Supports for our classes: useful triangulation for current capability and cost
frontiers and a reminder to retain component scores. It must not replace
task-local TokenEconomy qualification or turn a composite rank into a universal
route.

## Cross-study conclusions

1. Capability and cost form a task-conditioned frontier, not a single ranking.
2. Attempt count, reasoning effort, scaffold, context, and verifier materially
   change both quality and spend.
3. Realistic long-horizon, review, management, and frontend tasks remain far
   from saturated even when compact coding exercises look strong.
4. Ties and statistically indistinguishable groups occur. A set is a more
   honest public result than a forced winner.
5. External results define priors and benchmark shapes. Local controlled
   outcomes remain necessary to qualify a concrete TokenEconomy route.
