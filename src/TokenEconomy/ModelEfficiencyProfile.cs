namespace TokenEconomy;

/// <summary>
/// The token-efficiency profile for one model: its capability tier, the reasoning-effort levels it
/// supports, and routing flags. This is the curated judgement metadata that sits <i>beside</i> the
/// pricing catalog — the model's ids/aliases, vendor and price all come from the catalog, so nothing is
/// duplicated here. <see cref="ModelEfficiencyMatrix"/> joins a profile to its catalog listing to derive
/// the <see cref="CostClass"/> and <see cref="Cli"/> and to rate <see cref="TaskClass"/> fit.
/// </summary>
public sealed record ModelEfficiencyProfile
{
    /// <summary>Model id or alias; must resolve in the paired <see cref="ModelPriceCatalog"/>.</summary>
    public required string ModelId { get; init; }

    /// <summary>The model's capability band. A maintained routing estimate, not a benchmark score.</summary>
    public required CapabilityTier Tier { get; init; }

    /// <summary>
    /// The reasoning-effort levels this model accepts, used to clamp a task's suggested effort to what
    /// the model can actually do. Default profiles are projected from the routing knowledge base;
    /// host-supplied profiles default to low/medium/high for compatibility.
    /// </summary>
    public IReadOnlyList<EffortLevel> EffortLevels { get; init; } = [EffortLevel.Low, EffortLevel.Medium, EffortLevel.High];

    /// <summary>
    /// True when the model is not generally selectable (e.g. gated behind a private project), so the
    /// matrix profiles it for completeness but <see cref="ModelEfficiencyMatrix.SuggestModel"/> never
    /// suggests it.
    /// </summary>
    public bool Restricted { get; init; }

    /// <summary>
    /// True when the model is on its way out (deprecated / scheduled for retirement); it stays in the
    /// matrix for costing history but is excluded from suggestions so new work is not routed onto it.
    /// </summary>
    public bool Deprecated { get; init; }

    /// <summary>The explicit routing-policy status. Unsupported models remain profileable for cost history but are never suggested.</summary>
    public ModelRoutingStatus RoutingStatus { get; init; } = ModelRoutingStatus.Selectable;

    /// <summary>Evidence strength copied from the versioned routing knowledge base.</summary>
    public PolicyEvidenceStatus EvidenceStatus { get; init; } = PolicyEvidenceStatus.Unknown;

    /// <summary>Workflow roles for which the authoritative policy permits this model.</summary>
    public IReadOnlyList<RoutingWorkflowRole> WorkflowRoles { get; init; } = [RoutingWorkflowRole.CoreTask];

    /// <summary>True when the route is a visible hypothesis pending stronger controlled evidence.</summary>
    public bool Provisional { get; init; }

    /// <summary>Optional human note explaining the tier choice or a routing caveat.</summary>
    public string? Note { get; init; }

    /// <summary>Evidence-derived review quality; null means no review evidence was composed into this profile.</summary>
    public ModelReviewQualitySummary? ReviewQuality { get; init; }
}
