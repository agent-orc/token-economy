namespace TokenEconomy;

/// <summary>
/// Seed profiles for <see cref="ModelEfficiencyMatrix.Default"/>: one <see cref="ModelEfficiencyProfile"/>
/// for every model in <see cref="ModelPriceCatalog.Default"/>. Tiers and effort levels are maintained
/// routing estimates (not benchmark scores); ids, aliases, vendor and price all come from the catalog,
/// so no pricing is duplicated here.
/// </summary>
/// <remarks>
/// Declaration order doubles as the curator's tiebreak preference when two candidates score equally, so
/// the strongest default for coding leads each tier (Opus before Fable, newest Sonnet first, gpt-5.6
/// before the coding-specialised gpt-5-codex).
/// </remarks>
internal static class ModelEfficiencySeed
{
    private static readonly IReadOnlyList<EffortLevel> FullEffort = [EffortLevel.Low, EffortLevel.Medium, EffortLevel.High];
    private static readonly IReadOnlyList<EffortLevel> LightEffort = [EffortLevel.Low, EffortLevel.Medium];

    /// <summary>The seeded profiles. Called once by <see cref="ModelEfficiencyMatrix.Default"/>.</summary>
    public static IReadOnlyList<ModelEfficiencyProfile> Profiles() =>
    [
        // ---- Frontier (Anthropic) — architecture / hardest problems ----
        new() { ModelId = "claude-opus-4-8", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort },
        new() { ModelId = "claude-opus-4-7", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort },
        new() { ModelId = "claude-opus-4-6", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort },
        new() { ModelId = "claude-opus-4-5", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort },
        new()
        {
            ModelId = "claude-opus-4-1", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort, Deprecated = true,
            Note = "Retires 2026-08-05; profiled for costing but excluded from suggestions so new work is not routed onto it.",
        },
        new()
        {
            ModelId = "claude-fable-5", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort,
            Note = "Flagship Claude 5 generalist; the priciest option, so budget pressure drops it quickly.",
        },
        new()
        {
            ModelId = "claude-mythos-5", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort, Restricted = true,
            Note = "Project Glasswing only; profiled for costing but not generally selectable.",
        },

        // ---- Balanced (Anthropic) — everyday feature work / research ----
        new() { ModelId = "claude-sonnet-5", Tier = CapabilityTier.Balanced, EffortLevels = FullEffort },
        new() { ModelId = "claude-sonnet-4-6", Tier = CapabilityTier.Balanced, EffortLevels = FullEffort },
        new() { ModelId = "claude-sonnet-4-5", Tier = CapabilityTier.Balanced, EffortLevels = FullEffort },

        // ---- Light (Anthropic) — rote edits / prose ----
        new()
        {
            ModelId = "claude-haiku-4-5", Tier = CapabilityTier.Light, EffortLevels = LightEffort,
            Note = "Fast and cheapest; ideal for mechanical chores and doc edits, underpowered for design/feature work.",
        },

        // ---- OpenAI (Codex) — known models, unpriced in the catalog, so cost class is Unknown ----
        new()
        {
            ModelId = "gpt-5.6", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort,
            Note = "Cost not published in the catalog, so it is deprioritised whenever budget is under pressure.",
        },
        new()
        {
            ModelId = "gpt-5-codex", Tier = CapabilityTier.Frontier, EffortLevels = FullEffort,
            Note = "Coding-specialised; cost not published in the catalog.",
        },
        new() { ModelId = "gpt-5.5", Tier = CapabilityTier.Balanced, EffortLevels = FullEffort },
        new() { ModelId = "gpt-5", Tier = CapabilityTier.Balanced, EffortLevels = FullEffort },
    ];
}
