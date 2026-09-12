# AGT historical complexity audit

Read-only assessment from the existing stable API on 2026-09-12. No AGT product files, task data, server state, or raw evidence were changed. No raw task prompt, title, task key, repository path, context content, or prompt hash is exported.

## Actual coverage

The archive contained 1955 tasks. The newest 200 archive entries contained 174 Agent Studio cards; all were requested. The archive API orders by entry into the archive lane, not original execution time. This is a bounded convenience sample, not a random or representative benchmark.

| Measure | Count |
|---|---:|
| Inspected cards | 174 |
| Read errors | 0 |
| Non-fixture cards | 174 |
| Cards with reported tokens | 11 |
| Token-bearing cards with projected runs | 11 |
| Token-bearing cards with first-recorded-run context available | 0 |
| Token-bearing cards with end/duration fields on every projected run | 11 |
| Token-bearing cards with enrichment audit dated before first recorded run | 3 |
| Matching authored-prompt SHA256 and pre-first-run audit timestamp | 3 |
| Token-entry-minus-one differs from additional projected runs | 8 |

Presence of a duration on every projected run does not prove the entire historical execution ledger is complete. Missing token data is excluded from numerical comparison but retained in coverage denominators. The token ledger has timestamp/model/participant buckets; it does not prove attempt-level completeness or a one-to-one correspondence with CLI runs.

## Replay on authenticated authored prompts

For each qualifying card, SHA256 of the exact UTF-8 current authored body equals enrichmentReport.originalPromptSha256, and enrichmentReport.generatedAtUtc is no later than the earliest observed runs[].startedAt. The current estimator receives only that authenticated body and an anonymous key. No eventual diff, output, token count, runtime, current title, or current task-type field enters the estimate.

This is a replay of today's policy on a surviving pre-dispatch body. It is not the original stored prediction, proof of the original routing-decision input, or proof that no earlier unrecorded run existed. The enrichment audit covers the latest preprocessing event and can follow earlier intake/enrichment decisions. No calibration history is supplied because completeness and semantic reissues are unverified. Forecast confidence therefore remains heuristic.

| Anonymous card | Current score / band | Confidence | Token forecast | Reported tokens | Reported / forecast |
|---|---|---:|---:|---:|---:|
| card-007 | 21 / Standard | 0.38 | 12,087 | 23,623,245 | 1954.434 |
| card-010 | 21 / Standard | 0.38 | 19,797 | 11,432,112 | 577.467 |
| card-011 | 33 / Standard | 0.38 | 16,790 | 13,752,175 | 819.069 |

Reported tokens sum fresh input, output, cache-read, and cache-creation fields from agent-attributed entries where present, otherwise legacy unclassified entries. These are token volumes, not dollars or subscription allowance. Long cached sessions can have large reported token totals despite inexpensive cache reads. The ratio is descriptive; it is not a validated accuracy metric. No “actual complexity band”, quality score, semantic reissue count, temporal-holdout accuracy, or causal model comparison is inferred from these observations.

## Read-only API field map

| Read endpoint | Fields to retain privately | Meaning and caveat |
|---|---|---|
| GET /api/tasks/archive?offset=0&limit=200 | items[].key, projectName, enteredLaneAt; total | Paged archive ordered newest archive entry first; use stable task key privately and filter fixtures. |
| GET /api/tasks/{key} | promptMarkdown; enrichmentReport.generatedAtUtc, originalPromptSha256, enrichedPromptSha256 | Current body is mutable. A matching original hash plus timestamp can authenticate the body before the first recorded dispatch; hashes alone cannot reconstruct missing text. |
| GET /api/tasks/{key} | promptHistory[].index, markdown, writtenAt | Extension prompts, not revision snapshots of the original prompt. writtenAt derives from filesystem mtime. |
| GET /api/tasks/{key} | info.tokenSummary.entries[].ts, model, participantId, inputTokens, outputTokens, cacheReadTokens, cacheCreationTokens | Reported usage buckets. ts is the actual JSON field. Entries are not run/reissue events; preserve missingness and attribution. |
| GET /api/tasks/{key}/runs | runs[].index, startedAt, endedAt, durationSeconds, intent, status, result, closeoutSource, model, thinkingLevel, contextRef | Per-invocation projection. Keep terminal/superseded/fallback provenance and do not confuse runtime with wall-clock lead time. |
| GET /api/tasks/{key}/runs/{index}/context | runIndex, context; optional promptTokenEstimate, contextTokenEstimate or note | Exact final CLI context captured at spawn when available. Missing capture returns context=null. Includes templates, task body, mode and reissue framing; extract the task-specific body using a versioned parser before scoring. |
| GET /api/tasks/{key}/runs | reviewAttemptEpoch, reviewAttemptCycles, refinements | Explicit human review requeues and steering history are distinct from technical retries or CLI starts. They do not by themselves prove a semantic correctness reissue. |

Redacted shape; null is a real absence, not an empty prompt:

```json
{
  "detail": {
    "promptMarkdown": "<private current authored body>",
    "enrichmentReport": {
      "generatedAtUtc": "<recorded UTC timestamp>",
      "originalPromptSha256": "<private SHA256 of authored body>",
      "enrichedPromptSha256": "<private SHA256 of enriched body>"
    },
    "info": { "tokenSummary": { "entries": [{ "ts": "<UTC timestamp>", "participantId": "agent:<redacted>", "inputTokens": 0, "outputTokens": 0, "cacheReadTokens": 0, "cacheCreationTokens": 0 }] } }
  },
  "run": { "index": 1, "startedAt": "<UTC timestamp>", "endedAt": "<UTC timestamp>", "durationSeconds": 0, "intent": "start", "status": "completed", "result": "done", "closeoutSource": "session-event", "contextRef": null },
  "contextResponse": { "runIndex": 1, "context": null, "note": "<missing-capture explanation>" }
}
```

## Required design for a stronger comparison

1. Freeze a task-specific authored intake snapshot before the first routing decision, with input hash, schema/policy version, expected scope and hard-floor facts. Persist the estimate and effective route beside it.
2. Recover first-run spawn context where available, but label it as dispatch context and separate enrichment/template text from authored intake. Hash-authenticated original bodies are a useful fallback with explicit timestamp and first-recorded-run caveats.
3. Join token, runtime, review grade and semantic reissue observations using stable task and attempt IDs. A CLI restart, continuation, superseded execution, scope extension or token ledger entry is not automatically a quality reissue.
4. For replay calibration, split entire cards temporally. A training card must finish before the evaluated card starts; exclude every attempt of the evaluated card. Report project/scenario holdouts and missing coverage. Leave unavailable quality/reissue metrics null.
5. Compare token forecast, observed cost, duration and known grades separately. Do not use reported spending to manufacture ground-truth task difficulty or a claim that a more expensive model was necessary.

The existing tools/ComplexityBacktestReport utility uses current prompts, token-entry count minus one, and first-to-last token timestamp span. Its leave-one-card-out split prevents same-card neighbour leakage, but it does not fix mutable-intake leakage, future-history leakage, or invalid outcome proxies. Its results should remain explicitly retrospective until those inputs are replaced.

## Source anchors and reproduction

- AGT dev source: backend/Shared/Models/TaskDetail.cs (mutable body and extension history); backend/Shared/Models/PromptEnrichmentReport.cs and backend/Features/Runner/PromptEnrichmentService.cs (hash/timestamp audit).
- AGT dev source: backend/Features/Tasks/TaskMutationService.cs:1652 (prompt replacement); backend/Features/Tasks/TaskScannerService.cs:1632 (history mtime).
- AGT dev source: backend/Features/Tasks/TaskRunnerEndpoints.cs:294 (context endpoint); backend/Features/Tasks/TaskSessionLog.cs:110 (best-effort capture); backend/Features/Runner/ProjectRunner.cs:2928 (final prompt captured before CLI invocation).
- AGT dev source: backend/Features/Runner/RunTimeline.cs (run and review projection); backend/Features/Tasks/TaskCrudEndpoints.cs:255 (archive ordering).
- Token Economy: src/TokenEconomy/TaskComplexityEstimator.cs; src/TokenEconomy/ComplexityBacktester.cs; tools/ComplexityBacktestReport/Program.cs.

The collector (`collect_agt_coverage.py`) and helper (`Estimator/EstimatorAudit.csproj`) are retained only in the local development workspace under `artifacts/token-economy-complexity-audit/`; they are not included in this repository. This repository contains the report, [anonymized replay](agt-authenticated-prompt-replay-2026-09-12.json) and [coverage data](agt-intake-coverage-2026-09-12.json), but not the collector needed to repeat the audit. No production endpoint or task mutation was added.
