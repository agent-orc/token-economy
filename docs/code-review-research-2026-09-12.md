# Direct code-review evidence, 12 September 2026

Direct review measurements are available for Astra, Sol, Opus 5 and Fable 5.1. They establish different tradeoffs under specific review pipelines. They do not support one universal review ranking or a conversion of coding benchmark scores into review quality. No model inference, paid evaluation or private repository review was run for this research.

## Findings that can inform a model decision

| Study | Observed result | Practical reading |
|---|---|---|
| CodeRabbit, 4 September | Overall known-bug coverage: Astra 61.3%, Sol 59.0%, Opus 5 50.2%. Cross-file subset: 57.1%, 47.6%, 42.9%. | Astra is the coverage candidate in this shared study. Precision, sample size and effort are absent, so it does not establish a false-alarm advantage. |
| CodeRabbit, 24 July | Opus 5 senior profile: high actionable precision/coverage 35.6%/55.6%; xHigh 39.3%/55.2%. | xHigh improves observed precision with slightly lower coverage. Three-run averages over 96 evaluation patterns, without intervals; PR count unknown. |
| CodeRabbit, 1 September | Fable 5.1 internal Low: recall/precision 61.0%/37.3%; High: 57.1%/36.4%. | Lower internal configuration did better on these 45 tasks / 105 known issues, with lower latency. These labels do not prove API effort levels. |
| CodeRabbit, 9 July | Sol: 69/99 known issues caught actionably, 69.7% recall and 31.6% actionable precision. | Strong known-issue coverage came with substantial comment filtering. This is a different snapshot from the September comparison. |

The four rows have separate primary sources: [Astra evaluation](https://www.coderabbit.ai/blog/gpt-6-astra-code-review-evaluation), [Opus evaluation](https://www.coderabbit.ai/blog/opus-5-model-review) and its [result table](https://www.coderabbit.ai/content/assets/opus-5-results-table.png), [Fable evaluation](https://www.coderabbit.ai/blog/fable-5-1-model-review), and [Sol evaluation](https://www.coderabbit.ai/blog/gpt-5-6-sol-and-terra-benchmark). CodeRabbit owns the benchmark and review pipeline and is a third party to the model provider; this is not an independently audited evaluation.

The Opus actionable and full comment streams differ: xHigh full-stream precision is 28.6%, not 39.3%. Its junior/medium configuration also changes the prompt profile, so it is retained in research context rather than treated as a senior-profile effort ablation. Fable's 92 review-file calls include retries and batches, not 92 independent reviews. Sol's 231 raw comments are not a valid denominator for reconstructing actionable true positives.

## A reproducible count source and false-alarm burden

Kodus CodeReviewBench publishes per-case scorecards for a frozen tool replay. At pinned commit `531297bf50e5f065e3888d7e07b55dcacbf8df64`, Luna has 28/95 known issues matched and 30/52 findings judged true; Terra has 22/95 and 22/50. These are different issue and finding numerators. Original micro ratios remain on the source scale 0–1. The source contains 30 PRs, uses Haiku 4.5 matching and runs vendor-default settings with no requested effort. Unserved tool requests limit replay coverage. [Method overview](https://www.codereviewbench.com/), [Luna scorecard](https://github.com/kodustech/codereviewbench/blob/531297bf50e5f065e3888d7e07b55dcacbf8df64/scorecards/gpt-5.6-luna.json), [Terra scorecard](https://github.com/kodustech/codereviewbench/blob/531297bf50e5f065e3888d7e07b55dcacbf8df64/scorecards/gpt-5.6-terra.json).

Derived from published counts, judge-classified false findings per reviewed PR are **22/30 = 0.7333** for Luna and **28/30 = 0.9333** for Terra. This denominator includes all reviewed PRs, including those with no findings. Reviewed-file counts are not established, so no per-file rate is calculated. These are judge classifications under the publisher's goldens, not independently executed bug verdicts. No scorecard for Astra, Sol, Opus 5 or Fable 5.1 was found in that pinned tree. Run/scoring timestamps in August are not publication dates; catalog availability conservatively uses the first observed snapshot, 12 September.

## A candidate report excluded from the catalog

Entelligence's 9 September comparison reports 91/96 Astra findings and 107/126 Sol findings confirmed by a two-model judge, with review-cost and latency figures over 50 seeded PRs. This measures confirmation of emitted findings, not recall against exhaustive ground truth. The evaluated models also act as judges; only one pass/seed is reported. Its severity totals sum to 97 and 137, inconsistent with the respective 96 and 126 raised totals, and the article does not link its claimed raw benchmark directory. The claims remain in the research artifact, excluded from clean catalog comparisons pending case-level verification. [Primary report](https://entelligence.ai/blogs/gpt-6-astra-cost-1.6x-more-per-verified-bug-than-gpt-5.6-sol).

## What a review evaluation should measure

The following is a proposed integration design, informed by the cited studies. It is not a claim that Token Economy or Quality Studio already implements every field.

1. **Known-issue recall:** distinct matched ground-truth issues / known issues in the declared review scope. One underlying issue counts once. Partial human review goldens are not an exhaustive inventory of defects.
2. **Adjudicated precision:** confirmed findings / (confirmed + false findings), accompanied by verification coverage: adjudicated / all emitted findings. Plausible, unverified findings stay explicit rather than being silently counted false. Track duplicate comments separately as review burden.
3. **False-alarm burden:** false findings per reviewed PR, task or actually reviewed file, naming the denominator. The false-discovery fraction is `FP/(TP+FP)`. Calling it a false-positive rate would imply a defined true-negative universe (`FP/(FP+TN)`), which these review datasets do not provide.
4. **Severity and location:** report counts and recall/false findings per adjudicated severity. Record claimed severity separately from verified impact; do not invent one severity-weighted score. Check file and line ranges against the reviewed commit, then separately verify whether the cited code supports the finding.
5. **Outcome and expense:** confirmed fixes at a later commit, time per PR, and cost per unique confirmed issue are separate measures. Include zero-finding reviews. Undefined denominators remain absent; no fabricated zero cost or perfect precision.

SWE-PRBench distinguishes confirmed, plausible and fabricated comments and uses one-to-one issue matching. Its reported experiment evaluates a 100-PR sample, although the corpus contains 350 PRs. Judge agreement checks show why a judge label needs its own provenance. This supports retaining novel plausible findings rather than treating every unmatched comment as a false alarm. [Paper](https://arxiv.org/html/2603.26130v1), [dataset](https://huggingface.co/datasets/foundry-ai/swe-prbench).

MCR-Bench makes defect identity, location, severity and lifecycle explicit across review rounds. Its 2,269 tasks and human-validated annotation process concern multiround review, not a new-model leaderboard for the four target models. A finding that persists, resolves or reopens should keep its identity instead of receiving repeated discovery credit. [Paper](https://arxiv.org/html/2608.27442v1), [repository](https://github.com/DeepSoftwareAnalytics/MCR-bench).

Cursor's Bugbot evaluation uses resolution at merge and bugs flagged per run. Resolution reflects a later code outcome; it is not interchangeable with precision or recall. Greptile's older benchmark explicitly requires a localized, impact-explaining comment to count a caught bug, while unrelated comments do not reduce catch rate. Together these illustrate why location, burden and outcome require separate metrics. [Cursor methodology](https://cursor.com/blog/building-bugbot), [Greptile methodology](https://www.greptile.com/benchmarks).

SARIF provides interoperable result identities, fingerprints, artifact locations and line regions. A valid file location alone does not prove a defect. Preserve repository snapshot, base/head commit, diff identity, primary and related locations, evidence, judge identity, judgment date and finding lifecycle when exchanging review findings with Quality Studio or Code Studio. [OASIS SARIF 2.1.0 specification](https://docs.oasis-open.org/sarif/sarif/v2.1.0/os/sarif-v2.1.0-os.html).

For uncertainty estimates, resample PRs rather than individual comments from the same PR; repeat model runs to estimate run variation. This is a research design recommendation. Never infer a confidence interval from one rounded published percentage.

## Import and artifact boundaries

The library import adds the `CodeReview` capability, 14 protocol/metric definitions and 24 measurements. The existing 16 types and 63 results remain unchanged. Each metric keeps its source scale and provenance; different internal settings use distinct type IDs when provider effort is unknown. No composite review quality score is added. The unbounded false-findings-per-review measure is retained as a derived research measurement rather than given an arbitrary maximum to fit the current bounded-score catalog.

The repository contains the benchmark catalogs and `docs/analyses/code-review-studies-2026-09-12.json`, whose `evidenceIds` resolve to the catalog. Research scripts, downloaded scorecards, source-tree snapshot, image, recommendations and validation logs are local development artifacts under `artifacts/token-economy-code-review-research/` in the devspace. They are not claimed to be beside this report or available in a public repository.
