# Model migration catalog

Version: 2026-09-25

The versioned machine source is
[`src/TokenEconomy/catalog/model-migrations.v1.json`](../src/TokenEconomy/catalog/model-migrations.v1.json),
validated by its adjacent JSON Schema. Agent Studio can consume that stable
repository path, or the raw URL for the same revision, without translating
model names or maintaining a separate task-class table.

The default strategy is `latestInFamily`. It applies to the attempt-local route
selected by the orchestrator; it does not rewrite the card's configured route.
An explicit operator pin continues to win and receives a migration proposal
instead of a silent change. This preserves the audit contract in the
[authoritative routing policy](system/domains/model-routing-policy.md).

## Safe automatic migration

`safeAuto: true` means that the catalog has established all of these facts:

1. `from` and `to` are members of the same declared family and `to` has a
   strictly greater generation order.
2. The target's cost class is the same or lower at `evidenceAsOfDate`. Cost
   classes are derived from the dated Token Economy price catalog; `unknown`
   can never pass this gate.
3. The target thinking ladder is a superset of the source ladder, or the
   source's configured/default thinking level maps compatibly to the target.
4. Repository benchmark or A/B evidence compares both models on identical
   cases and reports `noRegression`. `evidence.kind: "none"`, `inconclusive`,
   or an open material regression blocks automation.
5. The target is available to the selected CLI at launch time and a dated
   price resolves in the consumer's effective price catalog.

The fifth check is deliberately repeated at runtime. Agent Studio installations
whose older `CodingAgentRunner` price seed does not yet know Claude 5 or GPT-5.6
must treat the entry as proposal-only until their effective catalog resolves
the target price. The recorded cost classes do not authorize a silent zero or
bypass the runtime availability snapshot.

`safeAuto` is necessary but not sufficient for launch. The orchestrator first
applies the card's score and correctness floors, then resolves the migration,
then evaluates current CLI availability, quota, and price. Price or quota may
choose only among routes that already clear the floor.

## Proposal-only migration

A migration remains `safeAuto: false` when it is cross-family, moves to a
higher or unknown cost class, has an incompatible reasoning ladder, has no
comparable evidence, carries an open regression, or crosses into a new model
generation without qualification. A same-model entry such as Haiku 4.5 or
GPT-5.4 Mini is a retention rule, not a migration, and is also false.

GPT-5.6 Sol to GPT-6 Astra is intentionally proposal-only: it crosses a major
generation, changes the ladder, and has no comparable repository no-regression
benchmark. Astra is supported and has dated premium-class pricing; those facts
do not authorize automatic migration. External benchmark scores do not replace
the same-case local evidence required by this gate.

The September 25 policy changes **new-card defaults**, separately from automatic
migration of existing pins. The September 22 successors remain proposal-only:

- Claude Opus 5 → 5.5: $5 / $0.50 / $25 → $4 / $0.20 / $20 per MTok
  input/cache/output; premium-to-premium and ladder-compatible, but no identical
  repository no-regression benchmark. Always-on thinking/tool changes also
  need compatibility checks. Claude Code 2.1.281 is the minimum observed version.
- GPT-5.6 Sol → GPT-6 Sol: $4 / $0.40 / $20 → $2 / $0.20 / $10;
  premium-to-standard. Codex 0.155.0 supports minimal through xhigh, **not ultra**.
  Source ultra → xhigh is a documented reasoning downgrade, not automatic
  compatibility. A controlled medium pilot does not certify xhigh or ultra pins.
- GPT-5.6 Luna → GPT-6 Luna: $0.20 / $0.02 / $1.20 → $0.10 / $0.01 / $0.50;
  economy-to-economy (same band, lower rates). The source catalogue includes
  ultra; the new CLI ladder stops at xhigh. Its operator-observed runner probe
  on September 25 supersedes the old unverified availability note. Controlled
  no-regression qualification remains missing.

These are the operator's September 25 CLI facts; API max benchmarks are separate
and cannot add CLI levels. Runtime availability and dated target prices must be
checked on every launch. Bare sol/luna aliases remain GPT-5.6 to preserve pins.
The [policy](system/domains/model-routing-policy.md) retains scores and floors
and defines an 80-card local completion cohort plus paired fixture comparisons.
The TE-57 delivery retains 32 medium hard-case attempts: requested Sol models
and GPT-5.6 Luna passed 6/8 each; requested GPT-6 Luna passed 5/8, with one
output-contract failure. All models failed the underspecified case twice.
None of the 16 retained transport records exposed actual returned model identity,
so this is inconclusive, not a model-specific noRegression finding. Re-run with
identity evidence, larger coverage and xhigh comparisons. No vendor claim
satisfies the noRegression gate.

## Task-class view

The catalog publishes the five Agent Studio task classes directly. Each
`recommendedModelSet` is conditional on the canonical score and hard floors;
it is not an unordered menu from which quota may pick a weaker model. GPT-6
candidates are listed first; conditional score-band entries remain conditional.
The richer task-class catalogue publishes separate `gpt6Candidates` and
`legacyFallbacks`. Legacy alternatives require explicit operator selection;
they are not silently added to quota-driven equivalent candidates. Website
prices for both sets are derived from the dated catalogue. Historical measured
baselines remain under `historicalBaseline` and do not supply GPT-6 outcome rates.

| Task class | Normal route | Escalation | Quota-aware alternative |
| --- | --- | --- | --- |
| `chore` | GPT-6 Sol/medium new-card prior; Terra remains the 21–50 score tier | Sol/medium, then Sol/xhigh only at its floor | Claude Sonnet 5/high for Terra or Sol/medium |
| `feature` | GPT-6 Sol/medium new-card prior; Terra remains the 21–50 score tier | Sol/medium for demanding work; Sol/xhigh at its floor | Claude Sonnet 5/high for Terra or Sol/medium |
| `bug` | GPT-6 Sol/medium prior; unclear-bug floor remains Terra/medium | Sol/medium for broad investigation; Sol/xhigh for critical risk | Claude Sonnet 5/high for Terra or Sol/medium |
| `dossier` | GPT-6 Sol/medium | Sol/xhigh only when a hard floor applies | Claude Sonnet 5/high for Sol/medium |
| `mechanical` | GPT-6 Luna/medium | Terra/medium when scope or uncertainty raises the score | Claude Sonnet 5/high only for the Terra route |

Claude Sonnet 5/high is a provisional equivalent-provider fallback. It is not
declared equivalent to Sol/xhigh. If no eligible route remains, the
orchestrator waits or requests an explicit override; it never crosses a
correctness floor to conserve quota.

GPT-5.4 Mini remains the separate bounded-pipeline-decision route over compact,
structured evidence with a deterministic output contract. It is not a core
task recommendation and therefore does not appear in the five task-class
model sets.

## Evidence contract

Evidence references are repository-relative and point to append-only raw
benchmark results. A referenced result must contain both `from` and `to` on the
same fixture/case set. The evidence object records the conclusion used by the
migration gate; consumers should retain the reference in their decision log.
An entry without comparable evidence uses either the legacy exact sentinel
`"none"` or a structured `evidence` object whose `kind` is `"none"` and whose
`reason` states the missing comparison. Neither form can set `safeAuto` to
true.

The current safe entries use the controlled document-to-text corpus. That
evidence proves no regression on those cases, not universal superiority. The
canonical routing score, task-class qualification, trust restrictions, and
hard floors still govern the concrete attempt.
