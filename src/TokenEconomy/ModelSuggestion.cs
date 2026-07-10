namespace TokenEconomy;

/// <summary>
/// One ranked candidate from <see cref="ModelEfficiencyMatrix.SuggestModel"/>: the model, the signals
/// that placed it, a suggested reasoning effort, an integer <see cref="Score"/> (higher is better), and
/// a human <see cref="Rationale"/>. The rationale is meant to travel verbatim into the orchestrator's
/// decision event and the Lastverteilung view, so an operator can audit <i>why</i> a model was chosen.
/// </summary>
public sealed record ModelSuggestion
{
    /// <summary>Canonical model id to launch.</summary>
    public required string ModelId { get; init; }

    /// <summary>The CLI that runs the model (always one of the caller's available CLIs).</summary>
    public required Cli Cli { get; init; }

    /// <summary>The model's capability band.</summary>
    public required CapabilityTier Tier { get; init; }

    /// <summary>The model's cost band, derived from the pricing catalog at the requested instant.</summary>
    public required CostClass CostClass { get; init; }

    /// <summary>How well the model fits the requested task class.</summary>
    public required Suitability Suitability { get; init; }

    /// <summary>The reasoning effort to request, from the task class and budget pressure, clamped to what the model supports.</summary>
    public required EffortLevel SuggestedEffort { get; init; }

    /// <summary>The ranking score; higher is a better pick under the given task class and budget pressure. Ordinal only — not a cost.</summary>
    public required int Score { get; init; }

    /// <summary>A one-line English explanation of the placement, for the decision event / transparency view.</summary>
    public required string Rationale { get; init; }

    /// <summary>True when the price used to derive <see cref="CostClass"/> is a not-yet-confirmed placeholder (mirrors <see cref="ModelPrice.Unconfirmed"/>).</summary>
    public bool CostUnconfirmed { get; init; }
}

/// <summary>
/// One row of the token-efficiency matrix as inspectable data: everything known about a model for
/// routing at a point in time — capability tier, derived cost class, supported effort levels, and its
/// <see cref="Suitability"/> for every <see cref="TaskClass"/>. Produced by
/// <see cref="ModelEfficiencyMatrix.Describe"/> to render the matrix (e.g. in the Lastverteilung view).
/// </summary>
public sealed record ModelEfficiencyRow
{
    /// <summary>Canonical model id.</summary>
    public required string ModelId { get; init; }

    /// <summary>Vendor / family grouping from the catalog listing (<c>anthropic</c>, <c>openai</c>, …), if any.</summary>
    public string? Vendor { get; init; }

    /// <summary>The CLI that runs the model, or null when the vendor maps to no known CLI.</summary>
    public required Cli? Cli { get; init; }

    /// <summary>The model's capability band.</summary>
    public required CapabilityTier Tier { get; init; }

    /// <summary>The cost band derived from the pricing catalog at the requested instant.</summary>
    public required CostClass CostClass { get; init; }

    /// <summary>The reasoning-effort levels the model supports.</summary>
    public required IReadOnlyList<EffortLevel> EffortLevels { get; init; }

    /// <summary>The model's suitability for each task class — the "matrix" cells for this row.</summary>
    public required IReadOnlyDictionary<TaskClass, Suitability> Suitability { get; init; }

    /// <summary>True when the model is not generally selectable (see <see cref="ModelEfficiencyProfile.Restricted"/>).</summary>
    public bool Restricted { get; init; }

    /// <summary>True when the model is deprecated / retiring (see <see cref="ModelEfficiencyProfile.Deprecated"/>).</summary>
    public bool Deprecated { get; init; }

    /// <summary>True when the price behind <see cref="CostClass"/> is an unconfirmed placeholder.</summary>
    public bool CostUnconfirmed { get; init; }

    /// <summary>Optional human note from the profile.</summary>
    public string? Note { get; init; }
}
