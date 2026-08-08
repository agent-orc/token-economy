namespace TokenEconomy;

/// <summary>
/// Compatibility profiles projected from <see cref="ModelRoutingKnowledgeBase.Default"/>. Model
/// identity, reasoning support, routing status, capability tier, evidence, and workflow roles are not
/// separately curated here; price remains derived from <see cref="ModelPriceCatalog.Default"/>.
/// </summary>
internal static class ModelEfficiencySeed
{
    public static IReadOnlyList<ModelEfficiencyProfile> Profiles(ModelRoutingKnowledgeBase? knowledge = null)
    {
        knowledge ??= ModelRoutingKnowledgeBase.Default;
        return knowledge.Models.Select(model => new ModelEfficiencyProfile
        {
            ModelId = model.PriceCatalogId,
            Tier = model.CapabilityTier,
            EffortLevels = model.SupportedThinkingLevels.Select(ToEffortLevel).ToArray(),
            Restricted = model.RoutingStatus == ModelRoutingStatus.Restricted,
            Deprecated = model.RoutingStatus == ModelRoutingStatus.Deprecated,
            RoutingStatus = model.RoutingStatus,
            EvidenceStatus = model.EvidenceStatus,
            WorkflowRoles = model.WorkflowRoles,
            Provisional = model.Provisional,
            Note = model.Note,
            ReviewQuality = knowledge.ReviewQualityFor(model.CanonicalId),
        }).ToArray();
    }

    private static EffortLevel ToEffortLevel(string level) => level switch
    {
        "minimal" => EffortLevel.Minimal,
        "low" => EffortLevel.Low,
        "medium" => EffortLevel.Medium,
        "high" => EffortLevel.High,
        "xhigh" => EffortLevel.XHigh,
        "ultra" => EffortLevel.Ultra,
        "max" => EffortLevel.Max,
        _ => throw new InvalidOperationException($"Unknown policy thinking level '{level}'."),
    };
}
