# Public evidence for Opus 5, Astra and Fable 5.1

Research snapshot: **2026-09-12**. The structured companion contains **44 measurements across nine catalog models**, with **8 additional benchmark definitions** that separate evaluator and agent configurations. No inference was purchased or executed. Model recommendations are interpretations of the measurements, not a new quality score or a claim of local qualification.

## What the measurements support

**Opus 5 deserves a substantive place in the comparison.** The Datacurve owner leaderboard measures it at 74% on DeepSWE 1.1, versus 59% for Opus 4.8 at the same max effort and shared mini-swe-agent setup. Reported average task cost falls from $13.22 to $11.84. Astra/xHigh also scores 74%, at $6.52: the same rounded result does not establish an Opus advantage, but it clearly establishes Opus as a capable alternative. Error bars overlap. [DeepSWE owner leaderboard](https://deepswe.datacurve.ai/), [methodology](https://deepswe.datacurve.ai/blog/deepswe).

Scale's task-specific owner measurements provide a more useful selection basis than one universal ranking:

| Task and exact configuration | Opus 5, Claude Code xHigh | Astra, Codex xHigh | Fable 5.1, Claude Code xHigh |
|---|---:|---:|---:|
| Codebase QnA, 124 tasks | 63.17 ±5.01% | 59.14 ±4.88% | 59.95 ±4.85% |
| Test Writing, 90 tasks, 3 trials each | 62.22 ±5.58% | 50.74 ±5.91% | 67.04 ±5.33% |
| Refactoring, 70 tasks | No exact row found | 59.05 ±6.43% | 56.67 ±6.52% |

Scores are resolve rates. Error widths are in **percentage points**, and their confidence level was not established in this review. The QnA intervals overlap; the Fable/Opus test-writing intervals overlap. These native-agent comparisons include agent behavior. QnA and refactoring use required rubric checks; test writing combines manifest, mutation-test and rubric requirements. Opus 4.5 serves as a rubric judge. Exact September row publication dates and agent versions are not supplied, so these are snapshots, not asserted test dates. [Codebase QnA](https://labs.scale.com/leaderboard/sweatlas-qna), [Test Writing](https://labs.scale.com/leaderboard/sweatlas-tw), [Refactoring](https://labs.scale.com/leaderboard/sweatlas-refactoring).

**Astra/high is supported as a practical terminal-coding starting point.** The official Terminal-Bench 4.0 board gives a same-agent effort sweep, preserving both resource use and uncertainty:

| Model / effort / native agent | Resolved attempts | Accuracy | 95% CI half-width | Total USD |
|---|---:|---:|---:|---:|
| Astra low / Codex | 167/330 | 50.61% | 2.75 pp | 1,557.30 |
| Astra medium / Codex | 179/330 | 54.24% | 2.66 pp | 1,914.80 |
| Astra high / Codex | 191/330 | 57.88% | 2.97 pp | 2,269.42 |
| Astra xHigh / Codex | 191/330 | 57.88% | 2.72 pp | 2,350.51 |
| Astra max / Codex | 192/330 | 58.18% | 2.79 pp | 3,267.18 |
| Opus 5 max / Claude Code | 171/330 | 51.82% | 3.39 pp | 5,969.11 |
| Fable 5.1 max / Claude Code | 191/330 | 57.88% | 3.76 pp | 6,243.50 |

The dataset has 66 tasks. The table's 330 is **attempts**, not distinct tasks. High costs 30.5% less than max for a 0.30-point difference in observed accuracy, with overlapping intervals. That calculation supports a starting-point recommendation, not a claim that high and max are statistically equivalent everywhere. The public row records identify the model and agent vendors but **do not identify who executed the evaluation**. Label them official leaderboard results, not proven independently executed measurements. [Terminal-Bench publisher](https://www.tbench.ai/), [dataset](https://hub.harborframework.com/datasets/terminal-bench/terminal-bench/4?tab=tasks), [Astra high row](https://hub.harborframework.com/datasets/terminal-bench/terminal-bench/4/leaderboards/4-0-0/rows/3475050c-bf5e-4261-a3f6-0af5350af13f), [Astra max row](https://hub.harborframework.com/datasets/terminal-bench/terminal-bench/4/leaderboards/4-0-0/rows/5c537be4-7fc3-449b-8bfc-ceb9061c2535).

**Fable 5.1 is especially worth considering for test creation.** Scale measures its xHigh configuration directly. Its AA results also support broad capability, but AA's default fallback can route work to Opus models; those measurements describe the production configuration. The September 1 assessment reports approximately 4% fallback output tokens across its Intelligence Index. This is not a per-task or universal fallback probability. [Fable 5.1 independent assessment](https://artificialanalysis.ai/articles/claude-fable-5-1).

## Independent evaluator checks and version boundaries

Artificial Analysis's September 7 **Intelligence Index v4.3** gives max-effort Astra and Fable 5.1 53 points, and Opus 5 51. The index replaced components, including Terminal-Bench 2.1 with 4.0. Earlier launch figures are not measurements on this same scale. [Dated v4.3 announcement](https://artificialanalysis.ai/articles/artificial-analysis-intelligence-index-v4-3).

The September 9 **Coding Agent Index v1.5** comparison gives Astra/Codex max and Fable 5.1/Claude Code max 62 points, Opus 5/Claude Code max 60, and Sol/Codex max 55. Its methodology equally weights DeepSWE 1.1 (113 tasks), Terminal-Bench 4.0 (66), and Codebase QnA (124), with three attempts per task. That is 303 tasks and 909 attempts, **not pooled accuracy over 909 trials**. Index costs are weighted suite costs, not the cost of one identical workload across unrelated benchmarks. [Dated comparison](https://artificialanalysis.ai/articles/benchmarking-gpt-6-astra), [coding methodology](https://artificialanalysis.ai/methodology/coding-agents-benchmarking/).

Opus 5 has a current general-reasoning effort comparison: AA v4.3 high 48 points at $3.61 per weighted task; xHigh 50/$4.88; max 51/$5.86. This supports considering high when cost matters for that suite. It does **not** establish a coding-task default without a controlled coding effort sweep. [High](https://artificialanalysis.ai/models/claude-opus-5-high), [xHigh](https://artificialanalysis.ai/models/claude-opus-5-xhigh), [max](https://artificialanalysis.ai/models/claude-opus-5).

Historical AA Terminal-Bench **2.1** measurements are retained separately: Opus 5/max 89% on July 24 and Fable 5.1/max 91.4% on September 1. Their historical harness revision and attempt counts are not established by those articles. They cannot replace current TB4 or owner-board results. [Opus 5 launch evaluation](https://artificialanalysis.ai/articles/opus-5), [Fable 5.1 launch evaluation](https://artificialanalysis.ai/articles/claude-fable-5-1).

Provider claims remain identifiable. OpenAI's Astra launch table reports different TB4 and DeepSWE numbers, says it takes the maximum across effort settings, and warns that research configurations can differ from production. Existing provider rows are preserved with unspecified effort rather than relabeled as max or overwritten by owner scores. [OpenAI launch evaluation](https://openai.com/index/gpt-6-astra/).

## What was checked but not imported as a score

- **SWE-bench Verified:** parsed the publisher's embedded JSON (180 Verified rows, plus 24 Test, 13 Multilingual, 84 Lite and 22 Multimodal). No exact Opus 5, Astra or Fable 5.1 row matched. The raw response and extracted empty target set are retained. This is a scoped search result, not proof no private evaluation exists. [Official leaderboard](https://www.swebench.com/).
- **SWE-bench Pro:** the official public and private tables did not contain those exact model identities. Older Opus 4.6 and GPT-5.4 scores were not substituted. [Public](https://labs.scale.com/leaderboard/swe_bench_pro_public), [private](https://labs.scale.com/leaderboard/swe_bench_pro_private).
- **Terminal-Bench 2.0:** none of the 142 public API rows matched the three target models. The 2.1 response contains Astra; the 4.0 response contains all three. All raw snapshots are saved. [Publisher](https://www.tbench.ai/).
- **DeepSWE owner:** the displayed model list had no exact Fable 5.1 row. The provider's DeepSWE result is a separate run. **Scale Refactoring:** no exact Opus 5 row; its QnA and DeepSWE results do not become a refactoring measurement.

## Catalog and UI contracts

The `context` object records source kind, publisher, known runner, agent, sample size, error bounds, cost basis, fallback information and date provenance. Unknown values stay absent. `confidence` remains the existing provenance category, not a numerical quality estimate. `reportedErrorHalfWidth` carries an error bar whose statistical interpretation was not established; `confidenceIntervalHalfWidth` and `confidenceIntervalLevel` are reserved for stated confidence intervals.

`publishedAt` remains the compatibility field used for as-of selection. Where the real publication date is unknown, it holds **2026-09-12**, `context.dateBasis` is `firstObservedPublicSnapshot`, and `context.observedAt` is the same date. Render **Snapshot**, not Published or Run. This conservative date prevents a current undated row from leaking into earlier as-of results. Actual dated AA articles retain their publication dates. TB record creation/update timestamps remain separate metadata; its `metadata.date` is a **model release date** and is never a benchmark date.

Terminal-Bench secondary task metrics divide publisher totals by **all attempts, including failures**. Output-token averages are rounded. Latency is the published average trial duration, not first-token latency. Costs are observed ledger amounts, not recomputed current tariffs. The raw input-token fields have ambiguous cached/uncached naming and were not combined or used to reconstruct prices.

Every existing catalog record is preserved. New type IDs separate official native-agent TB results from provider and AA runs; owner DeepSWE from provider and AA harnesses; and Scale's three task classes from AA's aggregate. DeepSWE 1.1's owner changelog establishes June 15 as the version release; the old mixed type's September 3 date is not reused for the new definition. [Owner changelog](https://deepswe.datacurve.ai/changelog).

[The checked-in model assessments](analyses/model-assessments-2026-09-12.json) supply task-specific conclusions with evidence IDs and URLs. Public benchmark evidence is enough for these bounded assessments. Local AGT checks remain useful for environment fit, retries and workflow behavior, without becoming the prerequisite for acknowledging all external empirical evidence. No subscription quota conversion or invented aggregate score is produced.

## Data and local research files

The repository contains the [benchmark results](../src/TokenEconomy/catalog/benchmark-results.json), [benchmark definitions](../src/TokenEconomy/catalog/benchmark-types.json) and [model assessments](analyses/model-assessments-2026-09-12.json).

Raw publisher snapshots, intermediate recommendations, pre-merge copies and transformation scripts are retained in the local development workspace under `artifacts/token-economy-empirical-research/`. Those auxiliary files are not included in this repository. Each checked-in measurement links its public source.
