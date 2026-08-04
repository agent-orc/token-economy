# Agent Studio task admission integration

`AgentStudioTaskAdmission.PrepareAttempt` is the host boundary that joins the
completed Token Economy APIs without moving filesystem, quota probes, or
launcher side effects into the library. The authoritative recommendation,
model, thinking level, floor, and override behavior remain those in
[`model-routing-policy.md`](system/domains/model-routing-policy.md).

## Host loop

For every attempt, including a reissue, Agent Studio performs this sequence:

1. Import the latest durable `task.json` outcome observations and
   classifications into one `IAgentStudioRunLedger` with
   `AgentStudioTaskStorageImporter`. A production ledger must durably implement
   `RecordDecision`; the in-memory implementation is intended for tests and
   bounded jobs.
2. Build `ComplexityCard` from intake-only card facts. `PrepareAttempt` reuses a
   stored schema-version-2 estimate for the card, or computes and upserts it
   through `ITaskComplexityEstimateStore` when none exists.
3. Capture a fresh `ProviderAvailabilitySnapshot` for this run and pass it in
   `ModelRoutingCapacity`. Its decision timestamp and deterministic snapshot ID
   are retained in the routing-decision record. A prior attempt's snapshot must
   not be reused as though it described the new run.
4. Call `PrepareAttempt` before creating or launching the worker. The adapter
   reads the newest classified observation from a lower run number and applies
   semantic promotion only for semantic failure or substantive C/D review.
   Environmental, stale-base, broken-host, cancellation, quota, and delivery
   outcomes remain visible but do not promote the route.
5. Persist `PersistedDecision` with the attempt. `LaunchRoute` is non-null only
   when the disposition is `Selected`; pass that attempt-local model, thinking
   level, and CLI to the launcher. Never copy it back over the card's configured
   route.
6. For `Wait` or `OverrideRequired`, do not launch. Show the persisted wait or
   override reason. An explicit operator pin remains visible and wins where the
   canonical policy permits it, with a below-policy warning when applicable.
7. When the attempt terminates, write the actual route, usage, raw outcome,
   review result, and reissue reason to task storage. Reimporting appends or
   idempotently replays the observation and versioned classification. The next
   attempt consumes that newest evidence.

The adapter is deterministic for the same task, stored estimate, prior
classifications, policy/evidence graph, available CLIs, and run-scoped quota
snapshot. Attempt decision IDs use
`{taskKey}:attempt:{run}:routing`; trying to rewrite the same decision ID with
different content fails.

## Admission contract

`AgentStudioTaskAdmissionRequest` keeps three route concepts separate:

- `CardConfiguredRoute` is display/audit context and is never mutated;
- `OperatorPin` is an explicit operator instruction evaluated under the pin
  rules in the canonical policy; and
- `AgentStudioAttemptLaunchRoute` is the selected attempt-local route passed to
  the launcher.

`AgentStudioRoutingDecisionRecord` schema version 2 persists:

- recommended and selected route IDs, models, thinking levels, and provisional
  status;
- intake and effective score, correctness-floor route, and applied floor IDs;
- selection source, policy version, policy reason, and semantic-promotion flag;
- configured route, operator pin, below-policy flag, and pin warning;
- quota-snapshot decision time/ID and quota-fallback flag/reason; and
- terminal disposition plus the wait or override reason.

The task-storage importer accepts these direct fields and the equivalent nested
`recommendedRoute`, `selectedRoute`, `correctnessFloor`, `operatorPin`,
`configuredRoute`, and `quotaSnapshot` shapes. Legacy records remain readable;
fields absent from an older decision stay explicitly unknown.

## Operator surface

Render the persisted decision, not a newly recomputed route.
`AgentStudioRoutingDecisionHtmlRenderer.Render` emits an accessible fragment
with recommendation, selection, score, floor, source, policy version,
provisional states, quota fallback, configured route, pin warning, wait reason,
and quota snapshot identity. It supplies semantic class hooks and no local
colors, spacing, badge geometry, or inline layout, so Agent Studio should wrap
or style it with its standard components and central design tokens.

## Executable fixture

[`AgentStudioTaskAdmissionTests.cs`](../tests/TokenEconomy.Tests/AgentStudioTaskAdmissionTests.cs)
contains the end-to-end fixture. It proves an initial Terra/medium route,
run-scoped quota fallback to the policy-declared Claude Sonnet 5/high provider
route, semantic promotion to Sol/medium, terminal outcome ingestion through the
real task-storage importer, unchanged card configuration, and deterministic
decision/observation replay. Those exact model and thinking combinations come
from the canonical policy; the fixture does not define a separate policy.
