# Model migration catalog

Version: 2026-09-06

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
   cases and reports `noRegression`. `evidence: "none"`, `inconclusive`, or an
   open material regression blocks automation.
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
generation, changes the ladder, has no repository benchmark, and is unpriced.
No model name or CLI availability claim is capability evidence by itself.

## Task-class view

The catalog publishes the five Agent Studio task classes directly. Each
`recommendedModelSet` is conditional on the canonical score and hard floors;
it is not an unordered menu from which quota may pick a weaker model.

| Task class | Normal route | Escalation | Quota-aware alternative |
| --- | --- | --- | --- |
| `chore` | Terra/medium | Sol/medium, then Sol/xhigh only at its floor | Claude Sonnet 5/high for Terra or Sol/medium |
| `feature` | Terra/medium for clear one-subsystem work | Sol/medium for demanding work; Sol/xhigh at its floor | Claude Sonnet 5/high for Terra or Sol/medium |
| `bug` | At least Terra/medium for an unclear bug | Sol/medium for broad investigation; Sol/xhigh for critical risk | Claude Sonnet 5/high for Terra or Sol/medium |
| `dossier` | Sol/medium | Sol/xhigh only when a hard floor applies | Claude Sonnet 5/high for Sol/medium |
| `mechanical` | Luna/medium | Terra/medium when scope or uncertainty raises the score | Claude Sonnet 5/high only for the Terra route |

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
An entry without comparable evidence uses the exact sentinel `"none"` and
cannot set `safeAuto` to true.

The current safe entries use the controlled document-to-text corpus. That
evidence proves no regression on those cases, not universal superiority. The
canonical routing score, task-class qualification, trust restrictions, and
hard floors still govern the concrete attempt.
