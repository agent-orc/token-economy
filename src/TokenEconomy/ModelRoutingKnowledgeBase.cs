using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public members mirror the documented machine-readable policy fields.

/// <summary>Whether a known model may be used by the routing policy.</summary>
public enum ModelRoutingStatus
{
    Selectable,
    FallbackOnly,
    Unsupported,
    Restricted,
    Deprecated,
}

/// <summary>Strength and kind of evidence behind a policy fact.</summary>
public enum PolicyEvidenceStatus
{
    Unknown,
    Provisional,
    Observational,
    Observed,
    CorrectnessFloor,
}

/// <summary>A workflow role whose route has a distinct correctness contract.</summary>
public enum RoutingWorkflowRole
{
    CoreTask,
    BoundedPipelineDecision,
}

/// <summary>The explicit outcome of resolving a model and optional thinking level.</summary>
public enum ModelRouteResolutionStatus
{
    Resolved,
    UnknownModel,
    MissingThinkingLevel,
    UnknownThinkingLevel,
    UnsupportedThinkingLevel,
    UnsupportedModel,
    RestrictedModel,
    DeprecatedModel,
    WorkflowRoleMismatch,
}

/// <summary>Canonical policy authority and its exact synchronized upstream snapshot.</summary>
public sealed record ModelRoutingAuthority
{
    public required string RepositoryPath { get; init; }
    public required string UpstreamRepository { get; init; }
    public required string UpstreamPath { get; init; }
    public required string ContentSha256 { get; init; }
    public required string Synchronization { get; init; }
}

/// <summary>Provider and CLI pair known to the routing policy.</summary>
public sealed record ModelRoutingProvider
{
    public required string Id { get; init; }
    public required string CliId { get; init; }
    public required string CliLabel { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
}

/// <summary>One reasoning level in the cross-provider vocabulary.</summary>
public sealed record ModelThinkingLevel
{
    public required string Id { get; init; }
    public required int Rank { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
}

/// <summary>Canonical model identity and its reconciled routing, catalog, workflow, and evidence facts.</summary>
public sealed record ModelRoutingModel
{
    public required string CanonicalId { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public required string ProviderId { get; init; }
    public required string CliId { get; init; }
    public required string PriceCatalogId { get; init; }
    public required string MediaCatalogModelId { get; init; }
    public required string TrustModelId { get; init; }
    public IReadOnlyList<string> SupportedThinkingLevels { get; init; } = [];
    public required ModelRoutingStatus RoutingStatus { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public bool Provisional { get; init; }
    public required CapabilityTier CapabilityTier { get; init; }
    public IReadOnlyList<RoutingWorkflowRole> WorkflowRoles { get; init; } = [];
    public required string Note { get; init; }
}

/// <summary>A policy route, including its score band when it belongs to the core-task ladder.</summary>
public sealed record ModelRoutingTier
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string ModelId { get; init; }
    public required string ThinkingLevel { get; init; }
    public required RoutingWorkflowRole WorkflowRole { get; init; }
    public int? MinimumScore { get; init; }
    public int? MaximumScore { get; init; }
    public required int Rank { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public IReadOnlyList<string> UseFor { get; init; } = [];
    public IReadOnlyList<string> DoNotUseFor { get; init; } = [];
}

/// <summary>An evidence-scoped equivalent-provider fallback that may replace only named policy routes.</summary>
public sealed record ModelProviderFallback
{
    public required string Id { get; init; }
    public IReadOnlyList<string> ForRouteIds { get; init; } = [];
    public IReadOnlyList<string> NotForRouteIds { get; init; } = [];
    public required string ModelId { get; init; }
    public required string ThinkingLevel { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public required bool Provisional { get; init; }
    public required string Note { get; init; }
}

/// <summary>A weighted policy scoring criterion.</summary>
public sealed record RoutingScoringCriterion
{
    public required string Id { get; init; }
    public required int MaximumPoints { get; init; }
}

/// <summary>A correctness trigger and the minimum core route it mandates.</summary>
public sealed record RoutingHardFloor
{
    public required string Id { get; init; }
    public required string MinimumRouteId { get; init; }
    public IReadOnlyList<string> Triggers { get; init; } = [];
}

/// <summary>Special routing behavior for bounded pipeline decisions.</summary>
public sealed record RoutingWorkflowConstraints
{
    public required string BoundedPipelineDefaultRouteId { get; init; }
    public required string AmbiguousOrUnboundedAuthorizingDecisionMinimumRouteId { get; init; }
    public IReadOnlyList<string> AuthorizingDecisionTriggers { get; init; } = [];
}

/// <summary>Semantic reissue escalation and stopping rules.</summary>
public sealed record RoutingReissueRules
{
    public IReadOnlyList<string> PromoteOn { get; init; } = [];
    public IReadOnlyList<string> DoNotPromoteOn { get; init; } = [];
    public required int EmpiricalConfidencePointsAfterSemanticFailure { get; init; }
    public required int MinimumCoreTierIncrease { get; init; }
    public required int StopAfterSemanticFailuresAtStrongerTier { get; init; }
}

/// <summary>Paths and invariants that reconcile routing with adjacent Token Economy catalogs.</summary>
public sealed record RoutingCatalogContracts
{
    public required string PriceCatalog { get; init; }
    public required string MediaCatalog { get; init; }
    public required string TrustEvidence { get; init; }
    public required string NamedModelTrustEvidenceStatus { get; init; }
    public required string HistoricalIncidentAttribution { get; init; }
    public required string GeneratedPublicView { get; init; }
    public required bool UnknownFactsRemainUnknown { get; init; }
    public required bool PriceAndQuotaMayNotLowerHardFloors { get; init; }
}

/// <summary>Result of resolving a requested model and thinking level without silent normalization.</summary>
public sealed record ModelRouteResolution
{
    public required ModelRouteResolutionStatus Status { get; init; }
    public ModelRoutingModel? Model { get; init; }
    public ModelThinkingLevel? ThinkingLevel { get; init; }
    public required string Reason { get; init; }
    public bool IsResolved => Status == ModelRouteResolutionStatus.Resolved;
}

/// <summary>
/// The single versioned machine-readable model-routing knowledge base. It owns identities, supported
/// thinking levels, policy routes, correctness floors, workflow constraints, and evidence status;
/// pricing and quota data are deliberately outside the route-selection method.
/// </summary>
public sealed class ModelRoutingKnowledgeBase
{
    private const string ResourceName = "TokenEconomy.catalog.model-routing-policy.json";
    private const string ReviewEvidenceResourceName = "TokenEconomy.catalog.review-evidence.json";
    private readonly Dictionary<string, ModelRoutingModel> _modelsByKey;
    private readonly Dictionary<string, ModelThinkingLevel> _thinkingById;
    private readonly Dictionary<string, ModelRoutingTier> _routesById;

    [JsonPropertyName("$schema")]
    public required string SchemaUri { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateOnly PolicyVersion { get; init; }
    public required DateOnly EvidenceAsOfDate { get; init; }
    public required ModelRoutingAuthority Authority { get; init; }
    public IReadOnlyList<ModelRoutingProvider> Providers { get; init; } = [];
    public IReadOnlyList<ModelThinkingLevel> ThinkingLevels { get; init; } = [];
    public IReadOnlyList<ModelRoutingModel> Models { get; init; } = [];
    public IReadOnlyList<ModelRoutingTier> Routes { get; init; } = [];
    public IReadOnlyList<ModelProviderFallback> ProviderFallbacks { get; init; } = [];
    public IReadOnlyList<RoutingScoringCriterion> ScoringCriteria { get; init; } = [];
    public IReadOnlyList<RoutingHardFloor> HardFloors { get; init; } = [];
    public required RoutingWorkflowConstraints WorkflowConstraints { get; init; }
    public required RoutingReissueRules ReissueRules { get; init; }
    public required RoutingCatalogContracts CatalogContracts { get; init; }

    /// <summary>The review-evidence version composed into this knowledge snapshot.</summary>
    [JsonIgnore]
    public string ReviewEvidenceVersion { get; private set; } = "review-evidence-unavailable";

    /// <summary>Per-model Quality Studio review metrics; insufficient data retains a null suitability.</summary>
    [JsonIgnore]
    public IReadOnlyList<ModelReviewQualitySummary> ReviewQuality { get; private set; } = [];

    [JsonConstructor]
    [SetsRequiredMembers]
    public ModelRoutingKnowledgeBase(
        string schemaUri,
        int schemaVersion,
        DateOnly policyVersion,
        DateOnly evidenceAsOfDate,
        ModelRoutingAuthority authority,
        IReadOnlyList<ModelRoutingProvider> providers,
        IReadOnlyList<ModelThinkingLevel> thinkingLevels,
        IReadOnlyList<ModelRoutingModel> models,
        IReadOnlyList<ModelRoutingTier> routes,
        IReadOnlyList<ModelProviderFallback> providerFallbacks,
        IReadOnlyList<RoutingScoringCriterion> scoringCriteria,
        IReadOnlyList<RoutingHardFloor> hardFloors,
        RoutingWorkflowConstraints workflowConstraints,
        RoutingReissueRules reissueRules,
        RoutingCatalogContracts catalogContracts)
    {
        SchemaUri = schemaUri;
        SchemaVersion = schemaVersion;
        PolicyVersion = policyVersion;
        EvidenceAsOfDate = evidenceAsOfDate;
        Authority = authority;
        Providers = providers;
        ThinkingLevels = thinkingLevels;
        Models = models;
        Routes = routes;
        ProviderFallbacks = providerFallbacks;
        ScoringCriteria = scoringCriteria;
        HardFloors = hardFloors;
        WorkflowConstraints = workflowConstraints;
        ReissueRules = reissueRules;
        CatalogContracts = catalogContracts;

        _modelsByKey = new(StringComparer.Ordinal);
        _thinkingById = new(StringComparer.Ordinal);
        _routesById = new(StringComparer.Ordinal);
        ValidateAndIndex();
    }

    /// <summary>The embedded repository policy without any operational review-evidence composition.</summary>
    public static ModelRoutingKnowledgeBase PolicyOnly { get; } = LoadPolicy();

    /// <summary>The embedded repository policy composed with the committed review-evidence report.</summary>
    public static ModelRoutingKnowledgeBase Default => DefaultKnowledge.Value;

    private static Lazy<ModelRoutingKnowledgeBase> DefaultKnowledge { get; } = new(LoadDefault);

    /// <summary>Find a known model by canonical id or alias; unknown input returns null.</summary>
    public ModelRoutingModel? FindModel(string? model)
        => string.IsNullOrWhiteSpace(model) ? null : _modelsByKey.GetValueOrDefault(Normalize(model));

    /// <summary>Find a declared reasoning level; unknown input returns null.</summary>
    public ModelThinkingLevel? FindThinkingLevel(string? level)
        => string.IsNullOrWhiteSpace(level) ? null : _thinkingById.GetValueOrDefault(Normalize(level));

    /// <summary>Find a policy route by stable id.</summary>
    public ModelRoutingTier? FindRoute(string? routeId)
        => string.IsNullOrWhiteSpace(routeId) ? null : _routesById.GetValueOrDefault(Normalize(routeId));

    /// <summary>Return only explicitly qualified provider fallbacks for a route; an empty result means wait or request an override.</summary>
    public IReadOnlyList<ModelProviderFallback> FallbacksFor(string? routeId)
        => FindRoute(routeId) is not { } route
            ? []
            : ProviderFallbacks.Where(fallback => fallback.ForRouteIds.Contains(route.Id, StringComparer.Ordinal)).ToArray();

    /// <summary>Return the review-quality summary for a known model or alias.</summary>
    public ModelReviewQualitySummary? ReviewQualityFor(string? model)
        => FindModel(model) is { } canonical
            ? ReviewQuality.FirstOrDefault(summary => summary.CanonicalModel == canonical.CanonicalId)
            : null;

    /// <summary>Compose a deterministic policy snapshot with one versioned Quality Studio evidence report.</summary>
    public ModelRoutingKnowledgeBase WithReviewEvidence(ReviewEvidenceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var copy = new ModelRoutingKnowledgeBase(
            SchemaUri, SchemaVersion, PolicyVersion, EvidenceAsOfDate, Authority, Providers, ThinkingLevels,
            Models, Routes, ProviderFallbacks, ScoringCriteria, HardFloors, WorkflowConstraints, ReissueRules,
            CatalogContracts);
        copy.AttachReviewEvidence(report);
        return copy;
    }

    /// <summary>
    /// Resolve a model and reasoning level canonically. Unsupported, restricted, deprecated, unknown,
    /// and workflow-incompatible inputs remain explicit and are never clamped to a different value.
    /// </summary>
    public ModelRouteResolution Resolve(string? model, string? thinkingLevel, RoutingWorkflowRole? workflowRole = null)
    {
        var canonicalModel = FindModel(model);
        if (canonicalModel is null)
            return Unresolved(ModelRouteResolutionStatus.UnknownModel, $"Unknown model '{model ?? "<null>"}'.");

        var modelStatus = canonicalModel.RoutingStatus switch
        {
            ModelRoutingStatus.Unsupported => ModelRouteResolutionStatus.UnsupportedModel,
            ModelRoutingStatus.Restricted => ModelRouteResolutionStatus.RestrictedModel,
            ModelRoutingStatus.Deprecated => ModelRouteResolutionStatus.DeprecatedModel,
            _ => ModelRouteResolutionStatus.Resolved,
        };
        if (modelStatus != ModelRouteResolutionStatus.Resolved)
            return Unresolved(modelStatus, $"Model '{canonicalModel.CanonicalId}' is {canonicalModel.RoutingStatus.ToString().ToLowerInvariant()}.", canonicalModel);

        if (workflowRole is { } role && !canonicalModel.WorkflowRoles.Contains(role))
            return Unresolved(ModelRouteResolutionStatus.WorkflowRoleMismatch, $"Model '{canonicalModel.CanonicalId}' is not selectable for {role}.", canonicalModel);

        if (string.IsNullOrWhiteSpace(thinkingLevel))
            return Unresolved(ModelRouteResolutionStatus.MissingThinkingLevel, "A thinking level is required.", canonicalModel);

        var canonicalThinking = FindThinkingLevel(thinkingLevel);
        if (canonicalThinking is null)
            return Unresolved(ModelRouteResolutionStatus.UnknownThinkingLevel, $"Unknown thinking level '{thinkingLevel}'.", canonicalModel);

        if (!canonicalModel.SupportedThinkingLevels.Contains(canonicalThinking.Id, StringComparer.Ordinal))
            return Unresolved(ModelRouteResolutionStatus.UnsupportedThinkingLevel, $"Model '{canonicalModel.CanonicalId}' does not support '{canonicalThinking.Id}'.", canonicalModel, canonicalThinking);

        return new ModelRouteResolution
        {
            Status = ModelRouteResolutionStatus.Resolved,
            Model = canonicalModel,
            ThinkingLevel = canonicalThinking,
            Reason = $"Resolved to {canonicalModel.CanonicalId} / {canonicalThinking.Id} via {canonicalModel.CliId}.",
        };
    }

    private static ModelRouteResolution Unresolved(ModelRouteResolutionStatus status, string reason, ModelRoutingModel? model = null, ModelThinkingLevel? thinking = null)
        => new() { Status = status, Model = model, ThinkingLevel = thinking, Reason = reason };

    private void ValidateAndIndex()
    {
        if (SchemaVersion != 1) throw new InvalidOperationException($"Unsupported routing schema version {SchemaVersion}.");
        if (SchemaUri != "model-routing-policy.schema.json") throw new InvalidOperationException($"Unexpected routing schema '{SchemaUri}'.");
        if (ScoringCriteria.Sum(criterion => criterion.MaximumPoints) != 100)
            throw new InvalidOperationException("Routing scoring criteria must total 100 points.");
        var expectedCriteria = new[] { "correctnessRisk", "expectedScope", "contextDemand", "taskTypeAndUncertainty", "empiricalConfidence", "quotaAndCostHeadroom" };
        if (!ScoringCriteria.Select(criterion => criterion.Id).Order(StringComparer.Ordinal).SequenceEqual(expectedCriteria.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("Routing scoring criteria must define each scorecard axis exactly once.");
        if (!CatalogContracts.UnknownFactsRemainUnknown || !CatalogContracts.PriceAndQuotaMayNotLowerHardFloors)
            throw new InvalidOperationException("Routing catalog safety invariants must be enabled.");

        foreach (var level in ThinkingLevels)
        {
            AddUnique(_thinkingById, level.Id, level, "thinking level");
            if (level.Rank < 0) throw new InvalidOperationException($"Thinking level '{level.Id}' has a negative rank.");
        }

        var providerPairs = Providers.ToDictionary(provider => provider.Id, StringComparer.Ordinal);
        foreach (var model in Models)
        {
            if (!providerPairs.TryGetValue(model.ProviderId, out var provider) || provider.CliId != model.CliId)
                throw new InvalidOperationException($"Model '{model.CanonicalId}' has an unknown provider/CLI pair.");
            AddUnique(_modelsByKey, model.CanonicalId, model, "model key");
            foreach (var alias in model.Aliases) AddUnique(_modelsByKey, alias, model, "model key");
            if (model.SupportedThinkingLevels.Count == 0)
                throw new InvalidOperationException($"Model '{model.CanonicalId}' has no thinking levels.");
            foreach (var level in model.SupportedThinkingLevels)
                if (!_thinkingById.ContainsKey(Normalize(level)))
                    throw new InvalidOperationException($"Model '{model.CanonicalId}' references unknown thinking level '{level}'.");
            if (model.Provisional && model.EvidenceStatus != PolicyEvidenceStatus.Provisional)
                throw new InvalidOperationException($"Model '{model.CanonicalId}' is provisional without provisional evidence status.");
        }

        foreach (var route in Routes)
        {
            AddUnique(_routesById, route.Id, route, "route id");
            var resolution = Resolve(route.ModelId, route.ThinkingLevel, route.WorkflowRole);
            if (!resolution.IsResolved)
                throw new InvalidOperationException($"Route '{route.Id}' does not resolve: {resolution.Reason}");
        }

        var core = Routes.Where(route => route.WorkflowRole == RoutingWorkflowRole.CoreTask).OrderBy(route => route.MinimumScore).ToArray();
        var nextScore = 0;
        foreach (var route in core)
        {
            if (route.MinimumScore != nextScore || route.MaximumScore is null || route.MaximumScore < route.MinimumScore)
                throw new InvalidOperationException("Core route score bands must be contiguous from 0 through 100.");
            nextScore = route.MaximumScore.Value + 1;
        }
        if (nextScore != 101) throw new InvalidOperationException("Core route score bands must end at 100.");

        foreach (var floor in HardFloors)
            if (FindRoute(floor.MinimumRouteId) is not { WorkflowRole: RoutingWorkflowRole.CoreTask })
                throw new InvalidOperationException($"Hard floor '{floor.Id}' references an unknown core route.");

        var fallbackIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fallback in ProviderFallbacks)
        {
            if (!fallbackIds.Add(fallback.Id))
                throw new InvalidOperationException($"Duplicate provider fallback id '{fallback.Id}'.");
            if (fallback.ForRouteIds.Count == 0)
                throw new InvalidOperationException($"Provider fallback '{fallback.Id}' does not name an eligible route.");
            foreach (var routeId in fallback.ForRouteIds.Concat(fallback.NotForRouteIds))
                if (FindRoute(routeId) is null)
                    throw new InvalidOperationException($"Provider fallback '{fallback.Id}' references unknown route '{routeId}'.");
            var resolution = Resolve(fallback.ModelId, fallback.ThinkingLevel, RoutingWorkflowRole.CoreTask);
            if (!resolution.IsResolved || resolution.Model!.RoutingStatus != ModelRoutingStatus.FallbackOnly)
                throw new InvalidOperationException($"Provider fallback '{fallback.Id}' does not resolve to a fallback-only core model.");
        }
    }

    private static void AddUnique<T>(Dictionary<string, T> index, string key, T value, string label)
    {
        var normalized = Normalize(key);
        if (normalized.Length == 0 || !index.TryAdd(normalized, value))
            throw new InvalidOperationException($"Duplicate or blank {label} '{key}'.");
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('.', '-');

    private static ModelRoutingKnowledgeBase LoadPolicy()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded routing policy '{ResourceName}' was not found.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        return JsonSerializer.Deserialize<ModelRoutingKnowledgeBase>(stream, options)
            ?? throw new InvalidOperationException("Embedded routing policy contains no document.");
    }

    private static ModelRoutingKnowledgeBase LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ReviewEvidenceResourceName)
            ?? throw new InvalidOperationException($"Embedded review evidence '{ReviewEvidenceResourceName}' was not found.");
        var options = QualityStudioReviewRunImporter.Options(writeIndented: false);
        var report = JsonSerializer.Deserialize<ReviewEvidenceReport>(stream, options)
            ?? throw new InvalidOperationException("Embedded review evidence contains no report.");
        return PolicyOnly.WithReviewEvidence(report);
    }

    private void AttachReviewEvidence(ReviewEvidenceReport report)
    {
        if (report.SchemaVersion != ReviewEvidenceReport.CurrentSchemaVersion
            || report.TaskClass != "review"
            || report.EvidenceStatus != PolicyEvidenceStatus.Observational)
            throw new InvalidOperationException("The review-evidence report is incompatible with this knowledge base.");
        var byModel = report.ModelSummaries.ToDictionary(summary => summary.CanonicalModel, StringComparer.Ordinal);
        if (byModel.Count != report.ModelSummaries.Count
            || byModel.Keys.Except(Models.Select(model => model.CanonicalId), StringComparer.Ordinal).Any()
            || Models.Any(model => !byModel.ContainsKey(model.CanonicalId)))
            throw new InvalidOperationException("Review evidence must contain exactly one summary for every knowledge-base model.");
        if (report.ModelSummaries.Any(summary => summary.TaskClass != "review"
            || summary.EvidenceQuality == ReviewEvidenceQuality.InsufficientEvidence && summary.Suitability is not null
            || summary.EvidenceQuality == ReviewEvidenceQuality.ObservationalSupport && summary.Suitability is null))
            throw new InvalidOperationException("Review evidence contains a contradictory suitability or task-class claim.");

        ReviewEvidenceVersion = report.EvidenceVersion;
        ReviewQuality = Models.Select(model => byModel[model.CanonicalId]).ToArray();
    }
}

#pragma warning restore CS1591
