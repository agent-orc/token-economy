# Agent Studio routing integration

## Host boundary

Agent Studio calls `AgentStudioTaskRoutingAdmission.Admit` immediately before
each attempt. The adapter composes the existing public boundaries; it does not
move filesystem, quota probing, or launch behavior into Token Economy:

1. Agent Studio extracts the intake-only `ComplexityCard`, estimates it, and
   persists the schema-version-2 result through
   `ITaskComplexityEstimateStore.Upsert`.
2. The task-storage importer upserts prior attempts into an
   `IAgentStudioRunLedger`. Raw observations remain append-only and outcome
   classifications remain versioned.
3. Immediately before attempt `N`, the host builds a new
   `ProviderAvailabilitySnapshot` whose `DecisionAtUtc` belongs to that run and
   calls `Admit`.
4. Admission reads the stored estimate and the newest observation from an
   earlier attempt, using the highest classification version for that
   observation. Semantic failure or substantive C/D review promotes the next
   route according to the canonical policy; infrastructure, stale-base,
   broken-host, cancellation, quota, and delivery failures do not.
5. Admission persists the immutable decision before it returns. Only a
   `Selected` result has a `LaunchRoute`; `Wait` and `OverrideRequired` prohibit
   launch.
6. On completion, Agent Studio writes the actual model/thinking level, resource
   use, terminal outcome, and review result into task storage. Re-import closes
   the loop for the next attempt and for routing-evidence reports.

The authoritative route ladder, thinking levels, hard floors, fallback rule,
pin behavior, and explanations are defined in
[`model-routing-policy.md`](system/domains/model-routing-policy.md). Hosts must
not synthesize alternative model/thinking recommendations in their views.

## Launch example

```csharp
var admission = new AgentStudioTaskRoutingAdmission(estimateStore, runLedger);

AgentStudioTaskLaunchAdmission result = admission.Admit(new()
{
    Task = card,
    Run = nextRun,
    ConfiguredModel = task.Model,                  // retained, never rewritten
    ConfiguredThinkingLevel = task.ThinkingLevel,  // retained, never rewritten
    Capacity = new()
    {
        ProviderAvailability = runScopedQuotaSnapshot,
        DeterministicVerificationAvailable = hasDeterministicGate,
    },
    AvailableClis = availableClis,
    BenchmarkQualification = routingEvidence,
    TrustEvidence = trustEvidence,
    HumanOverride = explicitHumanPin,
});

if (!result.MayLaunch)
{
    // Surface result.Decision.WaitReason and do not spawn a worker.
    return;
}

LaunchAttempt(result.LaunchRoute.ModelId, result.LaunchRoute.ThinkingLevel);
```

`ConfiguredModel` and `ConfiguredThinkingLevel` describe the durable card.
`LaunchRoute` describes only this attempt. The host must write the selected
route into the attempt record and must not silently replace the card defaults.

## Persisted and operator-visible decision

`AgentStudioRoutingDecisionRecord` schema version 2 retains the complete
admission explanation. `AgentStudioRoutingDecisionHtmlRenderer` projects the
same fields without re-running policy:

| Operator label | Persisted field(s) |
|---|---|
| Recommended route | `RecommendedRouteId`, `RecommendedModel`, `RecommendedThinkingLevel` |
| Selected route | `SelectedRouteId`, `SelectedModel`, `SelectedThinkingLevel` |
| Score | `UpfrontScore`, `Score` |
| Hard floor | `HardFloorRouteId`, `HardFloorModel`, `HardFloorThinkingLevel`, `IsHardFloor`, `AppliedHardFloorIds` |
| Selection source and explanation | `SelectionSource`, `SelectionReason`, `Reason` |
| Policy version | `PolicyVersion` |
| Provisional status | `RecommendedProvisional`, `SelectedProvisional` |
| Quota fallback | `QuotaFallback` |
| Run-scoped quota provenance | `QuotaSnapshotAtUtc`, `QuotaSnapshotState` |
| Pin warning | `OperatorPinBelowPolicy`, `PinWarning` |
| Wait/override reason | `Disposition`, `WaitReason` |
| Unchanged card configuration | `ConfiguredModel`, `ConfiguredThinkingLevel` |

The task-storage importer accepts both these flattened fields and the existing
nested `recommendedRoute` / `selectedRoute` representation. Re-importing the
same decision is idempotent. Reusing a decision ID with different content is
rejected, while later raw observations and new classification versions remain
append-only.

## Replay and safety invariants

- A decision ID defaults to `<task>:attempt:<run>:routing`; a host may supply a
  stable equivalent. Identical card, estimate, prior classified evidence,
  policy/evidence inputs, and quota snapshot produce identical serialized
  decision content.
- Admission considers only observations from earlier runs. Replaying an
  already-decided attempt therefore cannot ingest its own terminal outcome as
  pre-launch evidence.
- Qualified equivalent-provider fallback is attempted before the narrow
  one-tier downgrade window. Neither path crosses a hard floor or a semantic
  promotion floor.
- Missing, stale, suspicious, capped, or otherwise unsafe capacity produces
  `Wait` or `OverrideRequired` with a persisted reason and no launch route.
- A human override is explicit and separate from the configured card route.
  Below-policy overrides remain visible through the pin warning.

`AgentStudioTaskRoutingAdmissionTests.EndToEndFixture_RoutesFallbacksPromotesIngestsAndReplaysDeterministically`
is the executable fixture for initial selection, quota fallback, semantic
promotion, terminal outcome ingestion, and deterministic replay. Its companion
wait fixture proves that an unsafe launch route is never returned.
