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
    /// the model can actually do. Defaults to the full <see cref="EffortLevel.Low"/>/<see cref="EffortLevel.Medium"/>/<see cref="EffortLevel.High"/> range.
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

    /// <summary>Optional human note explaining the tier choice or a routing caveat.</summary>
    public string? Note { get; init; }
}
