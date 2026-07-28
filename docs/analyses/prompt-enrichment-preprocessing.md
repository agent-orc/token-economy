# Prompt Enrichment Before an Agent Run: Cost, Benefit, and Audit Contract

**Analysis date:** 2026-07-28

**Question:** Does it pay to inspect a task prompt before launch and append
curated UI guidance, domain knowledge, or project conventions?

## Decision

Yes, but only as a **budgeted, auditable hybrid** and only for curated context
blocks. Start with deterministic metadata and keyword rules. Use embedding
search to produce candidates when vocabulary is variable, and call a small
classifier only when those candidates remain ambiguous. The classifier may
select known block IDs; it must not invent instructions.

The economic case is plausible even for a small retry reduction. Agent Studio's
2026-07-25 corpus contains 847 tasks and 2,396 attempts. Thus 1,549 attempt
records, or **64.6%**, are retries. Retry work independently accounts for
**66.7% of recorded compute**. Those two percentages describe different
denominators; neither is a task failure rate. Coding carried 1.89 billion
recorded tokens, 98.1% of the token total. In that population, preventing a
small number of coding reruns can repay a tightly capped context addition.

That is a hypothesis, not proof that enrichment causes fewer retries. The
minimum viable rollout is shadow selection followed by a controlled cohort.
Promote only when the measured reduction in retry tokens exceeds preprocessing
and appended-context tokens without worse acceptance, review, or latency.

Every decision, including "nothing appended" and degraded fallback, must produce
an `enrichment-report.json` beside the materialized prompt. Invisible prompt
mutation is not acceptable.

## 1. Evidence Baseline and Meaning of "66% Retry Rate"

The primary evidence is the pinned Agent Studio
[pipeline-time-economy brief](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/brief.md)
and its
[full analysis](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/index.html).
It aggregates `pipeline-execution.json` over all retained attempts as of
2026-07-25.

| Observed quantity | Value | Interpretation |
| --- | ---: | --- |
| Tasks with structured pipeline data | 847 | Observed population, not a randomized cohort |
| All retained attempts | 2,396 | First attempts plus retained retries |
| Retry attempts | `2,396 - 847 = 1,549` | Assumes one first attempt per represented task |
| Retry share of attempt records | `1,549 / 2,396 = 64.6%` | The closest count-based meaning of "about 66% retry rate" |
| Attempts per task | `2,396 / 847 = 2.83` | Population mean |
| Retry compute | 19,871 of 29,808 minutes, 66.7% | Share of compute, not share of tasks or attempts |
| Coding-step tokens | 1.89 billion, 98.1% | Recorded input plus output counters |
| Adapted reissue prompts | 978 across 437 tasks | Evidence that many retries are quality steering, not identical reruns |

The attempt archive is capped at ten previous attempts even though the live API
showed individual tasks reaching 22, so retry counts and retry compute are a
conservative floor. The corpus is also observational: tasks that are harder may
both need more context and retry more. It supports a break-even model and a
pilot, not a causal claim.

For scenario arithmetic below, the recorded mean coding volume is:

```text
mean recorded coding tokens per attempt
  = 1.89 billion / 2,396
  = 788,815 tokens
```

This is a population proxy, not a claim that every prevented retry saves that
amount. It combines models and task sizes, and it is not a retry-only mean.
The current Agent Studio Codex aggregate can also overcount cached input, so
the scenario retains the source's **recorded-token** label and does not convert
that mean directly to a list-price saving. A production experiment must use the
component token buckets.

## 2. Cost and Benefit Model

Let:

- `D` be the per-task selection cost: rules, one query embedding, and any
  classifier input/output;
- `I / n` be the amortized block-index build cost over `n` task prompts;
- `b` be the number of context tokens appended to the launch prompt;
- `A` be expected attempts per task after enrichment;
- `p_in` be the downstream coding model's fresh-input price per million tokens;
- `R_t` and `R_$` be the tokens and money for one coding retry;
- `delta` be the expected number of coding retries avoided per task; and
- `F` be the cost of false-positive context: extra tokens plus any extra retry
  or quality loss caused by irrelevant or conflicting instructions.

Then:

```text
enrichment cost per task
  = D + I/n + (A * b * p_in / 1,000,000) + F

expected retry benefit per task
  = delta * R_$

break-even
  = delta * R_$ > enrichment cost per task
```

On a token-only basis, where selection models are reported separately because
their tokens have different prices:

```text
net downstream tokens
  = avoided coding-retry tokens - (A * b)
```

The important operational fact is that selection normally happens once for an
immutable prompt revision, while the appended block is presented to the coding
model on every fresh attempt. Therefore the recurring context, not embedding
search, is usually the dominant direct cost. Cache behavior can reduce the
price of repeated context, but the report must retain fresh, cache-read, and
cache-write buckets separately rather than assume a cache hit.

### 2.1 Illustrative preprocessing prices

These examples use public OpenAI list prices accessed on the analysis date.
They are examples, not a mandated provider or a permanent price table.

OpenAI's embedding guide gives `text-embedding-3-small` about 62,500 pages per
dollar at about 800 tokens per page. That implies about 50 million tokens per
dollar, or **$0.02 per million input tokens**. It documents embeddings for
text/code search using cosine similarity. See the
[embedding guide](https://developers.openai.com/api/docs/guides/embeddings).

The [OpenAI pricing page](https://developers.openai.com/api/docs/pricing)
lists standard short-context `gpt-5.4-nano` prices of $0.20/M input tokens and
$1.25/M output tokens on the analysis date.

| Mechanism operation | Explicit scenario | Marginal API cost |
| --- | --- | ---: |
| Rules | Local match over versioned metadata | $0 in model tokens; CPU and maintenance remain |
| Query embedding | 1,000 prompt tokens at $0.02/M | $0.000020/task |
| Initial block index | 100 blocks × 500 tokens at $0.02/M | $0.001 once per changed index |
| Small classifier | 1,000 input + 200 output tokens on the cited nano rates | $0.000450/call |
| Hybrid classifier fallback | Classifier needed on 20% of tasks | $0.000090/task, plus retrieval/rule cost |
| Appended context | 1,500 tokens × 2.83 attempts × illustrative $3/M coding input | $0.0127/task |

The final row uses the $3/M fresh-input assumption from the Agent Studio cost
analysis to show scale, not a universal coding price. Even in this compact
example, presenting context to the coding model costs roughly 28 times the
small classifier call. Token caps and selection precision matter more than
micro-optimizing the selector.

The Agent Studio source's order-of-magnitude blended estimate is about $8,400
over the measured corpus. Dividing by 2,396 gives about $3.51 per observed
attempt across all token-bearing steps. Against that non-causal population
average, the $0.0132 example (context plus one classifier call) breaks even at
roughly a **0.38% chance of avoiding one average attempt**. Do not use that
percentage as a production forecast: both the blended price and attempt size
vary, and relevant-task cohorts will differ from the full corpus.

### 2.2 Token sensitivity against the measured corpus

The table below replays three context caps against the observed 2,396-attempt
volume. It deliberately charges the block to every baseline attempt, even
though actually avoided attempts would also avoid their appended context.
Selector tokens and false-positive costs are excluded.

| Appended tokens per attempt | Added tokens over 2,396 attempts | Average coding retries that repay the addition | Share of 1,549 retries | Net tokens if retry count falls 5% |
| ---: | ---: | ---: | ---: | ---: |
| 500 | 1.198M | 1.52 | 0.10% | 59.90M saved |
| 1,500 | 3.594M | 4.56 | 0.29% | 57.50M saved |
| 4,000 | 9.584M | 12.15 | 0.78% | 51.51M saved |

The 5% scenario means 77.45 of the observed 1,549 retry attempts avoided. At
788,815 recorded coding tokens per average attempt, that is 61.09M tokens
before subtracting appended context. It is a sensitivity calculation, not an
expected result.

### 2.3 What must be measured

Compare enriched and control tasks on:

1. attempts per accepted task and probability of acceptance on the first
   attempt;
2. fresh, cache-read, cache-write, output, and total coding tokens per accepted
   task;
3. preprocessing tokens, dollars, latency, errors, and fallback count;
4. appended tokens per task, block-selection frequency, and budget truncation;
5. reviewer findings, deterministic gate outcome, and final human acceptance;
6. false positives (irrelevant/conflicting block) and false negatives (a
   labelled-needed block was missed); and
7. task class, model, repository, and prompt size, so mix changes do not look
   like an enrichment effect.

The economic promotion test is:

```text
upper confidence bound of enriched total tokens per accepted task
  < control total tokens per accepted task

and quality/acceptance is non-inferior
and no unreported prompt mutation occurred
```

## 3. Mechanism Comparison

All mechanisms operate over a trusted, versioned context catalog. A catalog
entry needs an ID, revision, source path, content digest, applicability tags,
priority, exclusion/supersession rules, and a tokenizer-derived token count.
Arbitrary web or repository search results must never be appended as
instructions without entering that curated catalog.

| Mechanism | Direct cost | Strengths | Failure modes | Best fit |
| --- | --- | --- | --- | --- |
| Rule-based keyword/metadata mapping | Local CPU; no model-token bill | Deterministic, fast, explainable, easy to test; exact project/language/file-type metadata can be high precision | Synonyms and implicit intent are missed; broad words such as "UI" can over-trigger; rules grow brittle | Small stable catalogs, mandatory project conventions, exact technology or path signals |
| Embedding retrieval | Query embedding plus amortized index; vector lookup | Finds paraphrases and topic similarity; scales to many blocks | Similarity is not applicability; thresholds drift by corpus; nearest block can still be wrong | Candidate generation for tens/hundreds of overlapping topic blocks |
| Small-model classifier | Input/output tokens plus model latency | Can combine title, prompt, repository metadata, and candidate descriptions; produces a reason | Non-determinism, provider failure, prompt injection, and confident false positives | Semantic ambiguity where a bounded allow-list and structured output are available |
| Hybrid | Rules + embedding query + classifier only on ambiguous cases | Keeps hard decisions deterministic, improves semantic coverage, and limits model calls | More components and versioning; disagreements need a defined precedence rule | Production default after a shadow evaluation |

### Recommended hybrid mechanics

1. Append **mandatory** project conventions through exact project/repository
   metadata, not semantic inference.
2. Apply high-precision rules. A rule must identify its matched signal and the
   catalog revision it selects.
3. If no decisive rule exists, embed the prompt and retrieve at most five
   catalog candidates.
4. Give a small classifier only the prompt, non-secret repository metadata,
   and those candidate IDs/descriptions. Require schema-constrained output:
   selected IDs, confidence, and a short reason. It cannot author a block.
5. Apply deterministic exclusions, ordering, deduplication, and a default
   **1,500-token / two-optional-block cap**. Mandatory policy is accounted
   separately and may not be silently displaced.
6. If confidence is below the calibrated threshold, append nothing. An initial
   `0.80` classifier threshold is only a shadow-mode starting point, not a
   universal constant.
7. Count with the actual downstream tokenizer, render the launch prompt, hash
   it, and persist the report before dispatch.

Project conventions and task-specific knowledge can conflict. Precedence must
be explicit: security and repository policy, then task acceptance constraints,
then project style, then optional domain hints. A lower tier cannot override a
higher tier.

## 4. Selection Rubric

These thresholds are proposed rollout policy and require calibration.

| Situation | Mechanism | Decision |
| --- | --- | --- |
| Required repository instruction identified by repository ID, path, language, or task metadata | Rules | Append the exact revision; report the metadata match |
| Up to about 20 stable blocks with distinctive, tested vocabulary | Rules | Prefer the smaller deterministic system |
| Many blocks, paraphrased topics, or vocabulary that rules miss | Embedding retrieval | Retrieve candidates only; append directly only after labelled precision supports the threshold |
| Several plausible candidates whose applicability depends on task meaning | Small classifier | Select from the candidate allow-list; append nothing below calibrated confidence |
| Mixed mandatory rules and ambiguous semantic topics | Hybrid | Recommended production shape |
| No curated block, weak confidence, token budget exhausted, or conflicting blocks | No enrichment | Launch the unchanged prompt and report why |
| High-secrecy prompt that policy forbids sending to the selector provider | Local rules/local retrieval | Do not make a remote model call |

Use rule-only first if its labelled precision is at least 95% and its miss rate
is acceptable. Add semantic retrieval to fix demonstrated misses, not because
the catalog happens to support embeddings. Add a classifier only when it
resolves a measured ambiguous set more cheaply than the retries or manual
maintenance it prevents.

Roll out in three gates:

- **Shadow:** create reports but do not append; label selections and validate
  secrecy, precision, token counts, and explanations.
- **Canary:** enrich a randomized, stratified eligible cohort with the
  1,500-token cap; keep a contemporaneous control.
- **Promote or remove:** promote only on total tokens per accepted task and
  non-inferior quality. Roll back to rules or no enrichment on regressions.

## 5. Visibility Contract: `enrichment-report.json`

### 5.1 Placement and lifecycle

For the initial `prompt.md`, the task folder must contain
`enrichment-report.json`. It describes the exact prompt revision dispatched.
If the system materializes additional prompts such as `prompt-2.md`, each gets
an immutable adjacent report such as `prompt-2.enrichment-report.json`; the
initial filename remains the required simple name.

The report is written atomically before launch and is keyed by:

```text
(prompt SHA-256, policy version, context-catalog SHA-256, tokenizer/model)
```

An identical key is reusable. A changed prompt, policy, catalog, tokenizer, or
model invalidates it. Selector failure may fail open to the unmodified prompt,
but it must still produce a report with `status: "fallback-unenriched"`.
Failure to persist the report blocks dispatch: a modified prompt without its
audit artifact violates this contract.

### 5.2 Required fields

| Field | Requirement |
| --- | --- |
| `schemaVersion`, `enrichmentId`, `generatedAtUtc`, `status` | Stable format, identity, time, and one of `enriched`, `unchanged`, `fallback-unenriched`, `blocked` |
| `task` | Task ID/key, source prompt path, source digest, and enriched prompt digest |
| `policy` | Mechanism, policy version, catalog digest, downstream tokenizer/model, optional-block token/count caps |
| `detected` | Every found candidate, its source/revision, signals or similarity, classifier confidence if used, final decision, and human-readable reason |
| `appended` | Ordered selected blocks with exact content, source, revision, digest, insertion tier, and token count |
| `tokens` | Original, appended, final prompt, preprocessing input/output, and cache fields where applicable |
| `costUsd` | Nullable selection, appended-input estimate, total estimate, price source/date, and explicit reason when unknown |
| `timingMs` | Total plus rule, retrieval, classifier, and rendering durations |
| `warnings`, `errors` | Arrays present even when empty; no silent fallback or truncation |

`detected` answers **what was found**. `appended` answers **what was actually
attached**. The exact appended text is retained because an ID and mutable source
path alone are not enough to review the launched instructions. Context blocks
must be secret-free; the report inherits the task folder's access control and
must never become a side channel for credentials.

### 5.3 Normative example

```json
{
  "schemaVersion": "1.0",
  "enrichmentId": "enr_01J00000000000000000000000",
  "generatedAtUtc": "2026-07-28T14:20:31.442Z",
  "status": "enriched",
  "task": {
    "taskKey": "TE-24",
    "promptPath": "prompt.md",
    "promptSha256": "<sha256-of-authored-prompt>",
    "enrichedPromptSha256": "<sha256-of-exact-launched-prompt>"
  },
  "policy": {
    "mechanism": "hybrid",
    "version": "prompt-enrichment/1",
    "catalogSha256": "<sha256-of-context-catalog>",
    "tokenizer": "downstream-model-tokenizer-id",
    "model": "downstream-model-id",
    "optionalTokenBudget": 1500,
    "optionalBlockLimit": 2
  },
  "detected": [
    {
      "contextId": "ui-style-guide",
      "revision": "3",
      "source": "contexts/ui-style-guide.md",
      "signals": [
        { "kind": "keyword", "value": "UI", "location": "prompt" }
      ],
      "similarity": null,
      "classifierConfidence": null,
      "decision": "appended",
      "reason": "Exact high-precision UI rule matched."
    },
    {
      "contextId": "accessibility-checklist",
      "revision": "2",
      "source": "contexts/accessibility-checklist.md",
      "signals": [
        { "kind": "embedding", "value": "top-2", "location": "prompt" }
      ],
      "similarity": 0.73,
      "classifierConfidence": 0.61,
      "decision": "rejected-low-confidence",
      "reason": "Below the calibrated classifier threshold."
    }
  ],
  "appended": [
    {
      "contextId": "ui-style-guide",
      "revision": "3",
      "source": "contexts/ui-style-guide.md",
      "contentSha256": "<sha256-of-exact-block-content>",
      "tier": "project-style",
      "order": 1,
      "tokens": 684,
      "content": "Exact context block text as appended to the prompt."
    }
  ],
  "tokens": {
    "originalPrompt": 532,
    "appended": 684,
    "finalPrompt": 1216,
    "preprocessingInput": 0,
    "preprocessingOutput": 0,
    "preprocessingCacheRead": 0,
    "preprocessingCacheWrite": 0
  },
  "costUsd": {
    "selection": 0,
    "appendedInputEstimate": null,
    "totalEstimate": null,
    "priceSource": null,
    "priceDate": null,
    "unknownReason": "Downstream model price unavailable."
  },
  "timingMs": {
    "total": 7,
    "rules": 2,
    "retrieval": 0,
    "classifier": 0,
    "rendering": 5
  },
  "warnings": [],
  "errors": []
}
```

Additional invariants:

- `tokens.finalPrompt = tokens.originalPrompt + tokens.appended` unless the
  renderer adds separately identified framing;
- `tokens.appended` equals the sum of `appended[].tokens`;
- `appended[].order` is unique and contiguous;
- every appended item has one matching `detected` item with decision
  `appended`;
- every digest is calculated over UTF-8 bytes with documented newline
  normalization;
- unknown price is `null` with a reason, never zero; and
- the UI shows status, candidates, decisions, exact blocks, token totals,
  warnings, and errors without requiring raw-file inspection.

## 6. Agent Studio Task-Server Integration Point (Reference Only)

This report does not authorize or implement an Agent Studio change.

The conceptual hook belongs at the Task Server's immutable launch-prompt
preparation boundary:

1. after the authored task body and repository metadata are final for a task
   version;
2. before the task can be leased and returned to a runner; and
3. before runner-specific standing instructions are appended.

The pinned implementation currently selects and leases ready tasks in
[`TaskServerStore.ClaimAsync`](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/task-server/TaskServerStore.cs#L530),
and `ClaimResponse.Task.Body` is the authored body. The standalone runner later
adds routing and completion instructions in
[`RemoteRunPrompt.Build`](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/runner/RemoteRunPrompt.cs#L29).
Enrichment should sit between those two semantic stages.

Do not put embedding or classifier network calls inside the current claim
database transaction. Prepare/cache the launch prompt asynchronously when the
task enters `2-ready`, keyed by task version and catalog/policy digests.
`ClaimAsync` should lease only a prepared revision (or leave it unclaimed while
preparation is pending). The server remains authoritative for the immutable
enriched body and report; the runner materializes `prompt.md` and
`enrichment-report.json` together and verifies their hashes before calling
`RemoteRunPrompt.Build`.

Reissues and operator extensions are new prompt revisions. Re-run enrichment
against the changed text, but deduplicate blocks already supplied by standing
policy or retained session context.

## 7. Standalone Summary for `ai-patterns`

> ### Auditable Prompt Enrichment (pattern candidate)
>
> **Context.** Coding-agent prompts often omit relevant project conventions,
> UI/style guidance, or known domain constraints. Agent Studio's 2026-07-25
> observational corpus had 847 tasks and 2,396 attempts: 1,549 retries (64.6%
> of attempt records), while retry work was 66.7% of compute and coding carried
> 98.1% of recorded tokens. These are two different retry denominators and are
> not a task failure rate.
>
> **Problem.** Missing context can cause expensive correction runs, but blindly
> attaching knowledge increases every attempt's input, creates conflicts, and
> hides what the agent was actually told.
>
> **Decision.** Select only from a trusted, versioned context catalog. Apply
> exact metadata/keyword rules first, embedding retrieval only to shortlist
> semantic candidates, and a small classifier only for ambiguous cases. The
> classifier selects IDs and reasons from an allow-list; it never authors
> instructions. Append nothing below a calibrated confidence threshold. Enforce
> an initial 1,500-token/two-optional-block budget and explicit precedence:
> security/repository policy, task constraints, project style, then domain
> hints.
>
> **Visibility contract.** Before dispatch, atomically write
> `enrichment-report.json` beside `prompt.md`. Record the authored/enriched
> prompt hashes, policy/catalog/tokenizer versions, every candidate and signal,
> every rejected/appended decision and reason, exact appended content and
> digest, token/cost/timing totals, truncation, warnings, and errors. Selector
> failure may fall back to the unchanged prompt only with a report; inability
> to persist the report blocks launch.
>
> **Economics.** Per task, compare selection plus index amortization plus
> `attempts × appended tokens × downstream input price` against avoided retry
> cost and false-positive harm. The repeated appended context normally costs
> more than a query embedding or small classifier call. A sensitivity replay
> over the cited corpus shows a 1,500-token block on every observed attempt adds
> 3.594M tokens and is repaid by about 4.56 average coding retries; a hypothetical
> 5% reduction in retry attempts nets about 57.50M recorded tokens. This is a
> break-even scenario, not causal evidence.
>
> **Validation.** Run shadow selection first, then a randomized stratified
> canary. Decide on total fresh/cache/output tokens per accepted task, attempts,
> quality/review outcomes, false positives/negatives, latency, and fallback
> rate. Promote only when total tokens improve with non-inferior quality; fall
> back to rules or no enrichment otherwise.
>
> **Integration boundary.** Prepare and cache an immutable enriched prompt in
> the Task Server after a task revision is final and before claim/lease and
> runner-specific framing. Key it by prompt, policy, catalog, and tokenizer
> digests. Do not perform model calls inside the claim transaction.
>
> **Primary evidence.** Agent Studio,
> [Pipeline Time Economy, snapshot 2026-07-25](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/brief.md).

## Sources

- [Agent Studio: Pipeline Time Economy brief, pinned 2026-07-25 snapshot](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/brief.md)
- [Agent Studio: Pipeline Time Economy full analysis and method](https://github.com/agent-orc/agent-studio/blob/2e5b52cf06eb11db7ef053a5ddc45d0a26414dfa/docs/quality/pipeline-time-economy/index.html)
- [OpenAI: vector embeddings guide](https://developers.openai.com/api/docs/guides/embeddings)
- [OpenAI: API pricing, accessed 2026-07-28](https://developers.openai.com/api/docs/pricing)
- [Related Token Economy analysis: dynamic workflows and task cutting](dynamic-workflows-task-cutting.md)
