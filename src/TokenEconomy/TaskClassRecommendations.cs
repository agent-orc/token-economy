using System.Reflection;
using System.Text.Json;
using System.Globalization;

namespace TokenEconomy;

#pragma warning disable CS1591 // Records mirror the documented, schema-backed public contract.

/// <summary>How directly retained evidence supports a task-class recommendation.</summary>
public enum TaskClassRecommendationStatus
{
    /// <summary>A controlled task-class study produced the route, but its sample is below the validation gate.</summary>
    ControlledPilot,

    /// <summary>Existing controlled coding evidence supports the route, below the full validation gate.</summary>
    ControlledCodingEvidence,

    /// <summary>No comparable controlled study has completed; this is the canonical policy baseline.</summary>
    PolicyBaseline,
}

/// <summary>How strongly the catalog can claim that every member of a recommendation set is equivalent.</summary>
public enum TaskClassEquivalenceStatus
{
    Demonstrated,
    Provisional,
    Singleton,
    InsufficientEvidence,
}

/// <summary>The terminal state of quota-aware selection over an unchanged recommendation set.</summary>
public enum TaskClassSelectionDisposition
{
    Selected,
    RecommendationOnly,
    Wait,
}

/// <summary>A model and thinking-level pair exposed to card-creation callers.</summary>
public sealed record TaskClassRouteRecommendation
{
    public string RouteId { get; init; } = "";
    public ModelId Model { get; init; }
    public EffortLevel ThinkingLevel { get; init; }
    public Cli Cli { get; init; }
    public int Rank { get; init; }
    public PolicyEvidenceStatus EvidenceStatus { get; init; }
    public bool Provisional { get; init; }
    public decimal? CostPerSuccessfulOutcomeUsd { get; init; }
    public decimal? RetryAdjustedCostPerOutcomeUsd { get; init; }
    public decimal? TokensPerSuccessfulOutcome { get; init; }
}

/// <summary>Why a route was removed while selecting from an otherwise unchanged recommendation set.</summary>
public sealed record TaskClassCandidateElimination(
    TaskClassRouteRecommendation Candidate,
    string Reason);

/// <summary>Pure quota-aware selection result. The full quota-independent set is always retained.</summary>
public sealed record TaskClassSelectionResult
{
    public required TaskClassSelectionDisposition Disposition { get; init; }
    public required TaskClassRecommendation Recommendation { get; init; }
    public TaskClassRouteRecommendation? Selected { get; init; }
    public required IReadOnlyList<TaskClassCandidateElimination> Eliminated { get; init; }
    public required DateTime EvaluatedAtUtc { get; init; }
    public required string Reason { get; init; }
}

/// <summary>Dated cost per measured successful outcome. Null is retained when usage or pricing is unavailable.</summary>
public sealed record TaskClassOutcomeCost
{
    public decimal? AmountUsd { get; init; }
    public DateTime PricedAtUtc { get; init; }
    public bool IncludesVerification { get; init; }
    public string Note { get; init; } = "";
}

/// <summary>One retained evidence line behind the class recommendation.</summary>
public sealed record TaskClassEvidenceLine
{
    public string EvidenceKind { get; init; } = "";
    public string Reference { get; init; } = "";
    public string Summary { get; init; } = "";
}

/// <summary>Versioned recommendation and downgrade boundary for one task class.</summary>
public sealed record TaskClassRecommendation
{
    public TaskClass TaskClass { get; init; }
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Family { get; init; } = "";
    public string Definition { get; init; } = "";
    public string OutcomeMeasure { get; init; } = "";
    public string BenchmarkStatus { get; init; } = "";
    public TaskClassRecommendationStatus Status { get; init; }
    /// <summary>
    /// Evidence-qualified routes in stable rank order. Rank is used only after quota headroom and
    /// observed efficiency; recommendation itself does not consume quota and does not select index zero.
    /// </summary>
    public IReadOnlyList<TaskClassRouteRecommendation> Candidates { get; init; } = [];
    public TaskClassEquivalenceStatus Equivalence { get; init; }
    public CapabilityTier MinimumCapability { get; init; }
    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    /// <summary>Compatibility projection for callers that have not yet migrated to <see cref="Candidates"/>.</summary>
    public TaskClassRouteRecommendation Recommended => Candidates[0];
    public TaskClassRouteRecommendation? Downgrade { get; init; }
    public IReadOnlyList<string> DowngradeWhen { get; init; } = [];
    public IReadOnlyList<string> NeverDowngradeWhen { get; init; } = [];
    public string Rationale { get; init; } = "";
    public string RationaleVersion { get; init; } = "";
    public string EvidenceVersion { get; init; } = "";
    public int ScenarioCount { get; init; }
    public int AttemptCount { get; init; }
    public decimal? OutcomeRate { get; init; }
    public TaskClassOutcomeCost? CostPerSuccessfulOutcome { get; init; }
    public IReadOnlyList<TaskClassEvidenceLine> Evidence { get; init; } = [];
}

/// <summary>
/// Study-backed task-class advice for card creation. This is a hint over the class prior, not an
/// admission override: callers must still apply <see cref="ModelRouter"/> to the concrete card so
/// uncertainty, correctness floors, prior failures, provider availability, and operator pins win.
/// </summary>
public sealed class TaskClassRecommendationCatalog
{
    private const string ResourceName = "TokenEconomy.catalog.task-class-recommendations.json";
    private readonly IReadOnlyDictionary<TaskClass, TaskClassRecommendation> _byClass;

    private TaskClassRecommendationCatalog(TaskClassRecommendationDocument document)
    {
        if (document.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported task-class recommendation schema {document.SchemaVersion}.");
        SchemaVersion = document.SchemaVersion;
        TaxonomyVersion = Required(document.TaxonomyVersion, nameof(document.TaxonomyVersion));
        RationaleVersion = Required(document.RationaleVersion, nameof(document.RationaleVersion));
        EvidenceAsOfDate = document.EvidenceAsOfDate;
        Recommendations = document.Recommendations.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        if (Recommendations.Select(item => item.TaskClass).Distinct().Count() != Recommendations.Count)
            throw new InvalidDataException("Task-class recommendations must be unique by enum value.");
        if (Recommendations.Count != Enum.GetValues<TaskClass>().Length)
            throw new InvalidDataException("Task-class recommendations must cover every TaskClass value.");
        if (Recommendations.Any(item => item.RationaleVersion != RationaleVersion))
            throw new InvalidDataException("Every recommendation must use the document rationale version.");
        ValidateRoutes(Recommendations);
        _byClass = Recommendations.ToDictionary(item => item.TaskClass);
    }

    public int SchemaVersion { get; }
    public string TaxonomyVersion { get; }
    public string RationaleVersion { get; }
    public DateOnly EvidenceAsOfDate { get; }
    public IReadOnlyList<TaskClassRecommendation> Recommendations { get; }

    public static TaskClassRecommendationCatalog Default { get; } = LoadEmbedded();

    /// <summary>Return the versioned recommendation for a taxonomy class.</summary>
    public TaskClassRecommendation Recommend(TaskClass taskClass)
        => _byClass.TryGetValue(taskClass, out var recommendation)
            ? recommendation
            : throw new ArgumentOutOfRangeException(nameof(taskClass), taskClass, "Unknown task class.");

    /// <summary>
    /// Choose one member of an already-qualified set from current provider quota evidence. Unknown,
    /// stale, missing, or suspicious quota returns <see cref="TaskClassSelectionDisposition.RecommendationOnly"/>;
    /// constrained known quota returns <see cref="TaskClassSelectionDisposition.Wait"/>. This method
    /// never introduces a downgrade or a route outside <see cref="TaskClassRecommendation.Candidates"/>.
    /// </summary>
    public TaskClassSelectionResult Select(
        TaskClassRecommendation recommendation,
        ProviderAvailabilitySnapshot quotaState,
        DateTime atUtc)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(quotaState);
        if (atUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("atUtc must be UTC.", nameof(atUtc));
        if (!_byClass.TryGetValue(recommendation.TaskClass, out var retained)
            || retained.RationaleVersion != recommendation.RationaleVersion
            || !retained.Candidates.SequenceEqual(recommendation.Candidates))
            throw new ArgumentException("Recommendation does not belong to this catalog version.", nameof(recommendation));
        if (recommendation.Candidates.Count == 0)
            return Selection(TaskClassSelectionDisposition.Wait, recommendation, null, [], atUtc,
                "The evidence-qualified recommendation set is empty; price cannot invent a safe route.");
        if (quotaState.DecisionAtUtc > atUtc
            || atUtc - quotaState.DecisionAtUtc > quotaState.MaximumObservationAge)
            return Selection(TaskClassSelectionDisposition.RecommendationOnly, recommendation, null, [], atUtc,
                "Quota snapshot is future-dated or stale; recommendation is retained without a concrete selection.");

        var eligible = new List<(TaskClassRouteRecommendation Route, decimal Headroom, int WarningRank)>();
        var eliminated = new List<TaskClassCandidateElimination>();
        foreach (var candidate in recommendation.Candidates)
        {
            var model = ModelRoutingKnowledgeBase.Default.FindModel(candidate.Model.Value)!;
            var provider = quotaState.Providers.SingleOrDefault(row =>
                string.Equals(row.Provider, model.ProviderId, StringComparison.OrdinalIgnoreCase)
                && CliMatches(row.CliType, candidate.Cli));
            if (provider is null || provider.Freshness != SnapshotFreshness.Fresh
                || provider.WarningState == AvailabilityWarningState.Unknown
                || provider.Availability == ProviderCliAvailability.Unknown
                || provider.QuotaWindows.Count == 0
                || provider.QuotaWindows.Any(window => window.Freshness != SnapshotFreshness.Fresh
                    || window.WarningState == AvailabilityWarningState.Unknown || window.Usage is null
                    || window.Usage.LimitTokens <= 0 || window.Usage.UsedTokens < 0
                    || window.Usage.HeadroomTokens < 0))
                return Selection(TaskClassSelectionDisposition.RecommendationOnly, recommendation, null,
                    eliminated, atUtc,
                    $"Quota evidence for {candidate.Model}/{candidate.ThinkingLevel} is missing, stale, suspicious, or unknown.");
            if (provider.Availability == ProviderCliAvailability.Unavailable
                || provider.WarningState == AvailabilityWarningState.Critical
                || provider.QuotaWindows.Any(window => window.WarningState == AvailabilityWarningState.Critical))
            {
                eliminated.Add(new(candidate, provider.Availability == ProviderCliAvailability.Unavailable
                    ? provider.AvailabilityDetail ?? "Provider CLI is unavailable."
                    : "At least one provider quota window is critical or exhausted."));
                continue;
            }

            var headroom = provider.QuotaWindows.Min(window =>
                window.Usage!.LimitTokens == 0 ? 0m
                    : 100m * window.Usage.HeadroomTokens / window.Usage.LimitTokens);
            var warningRank = provider.QuotaWindows.Any(window => window.WarningState == AvailabilityWarningState.Warning) ? 1 : 0;
            eligible.Add((candidate, headroom, warningRank));
        }

        var selected = eligible.OrderBy(item => item.WarningRank)
            .ThenByDescending(item => item.Headroom)
            .ThenBy(item => item.Route.RetryAdjustedCostPerOutcomeUsd is null ? 1 : 0)
            .ThenBy(item => item.Route.RetryAdjustedCostPerOutcomeUsd)
            .ThenBy(item => item.Route.CostPerSuccessfulOutcomeUsd is null ? 1 : 0)
            .ThenBy(item => item.Route.CostPerSuccessfulOutcomeUsd)
            .ThenBy(item => item.Route.TokensPerSuccessfulOutcome is null ? 1 : 0)
            .ThenBy(item => item.Route.TokensPerSuccessfulOutcome)
            .ThenBy(item => item.Route.Rank)
            .ThenBy(item => item.Route.Model.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        return selected.Route is null
            ? Selection(TaskClassSelectionDisposition.Wait, recommendation, null, eliminated, atUtc,
                "Every equivalent route is unavailable or at a critical quota boundary; wait rather than cross the class floor.")
            : Selection(TaskClassSelectionDisposition.Selected, recommendation, selected.Route, eliminated, atUtc,
                $"Selected {selected.Route.Model}/{Thinking(selected.Route.ThinkingLevel)} from the unchanged set using fresh quota headroom, then measured efficiency and stable rank.");
    }

    private static TaskClassRecommendationCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded task-class recommendations '{ResourceName}' were not found.");
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var document = new TaskClassRecommendationDocument
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            TaxonomyVersion = Text(root, "taxonomyVersion"),
            RationaleVersion = Text(root, "rationaleVersion"),
            EvidenceAsOfDate = DateOnly.Parse(Text(root, "evidenceAsOfDate"), CultureInfo.InvariantCulture),
            Recommendations = root.GetProperty("recommendations").EnumerateArray().Select(ParseRecommendation).ToArray(),
        };
        return new(document);
    }

    private static TaskClassRecommendation ParseRecommendation(JsonElement item)
    {
        var cost = item.GetProperty("costPerSuccessfulOutcome");
        var primary = Route(item.GetProperty("recommended"), 0,
            item.GetProperty("status").GetString() == "policyBaseline" ? PolicyEvidenceStatus.Provisional : PolicyEvidenceStatus.Observed,
            cost.ValueKind == JsonValueKind.Null || cost.GetProperty("amountUsd").ValueKind == JsonValueKind.Null
                ? null : cost.GetProperty("amountUsd").GetDecimal());
        var equivalent = item.TryGetProperty("equivalentRoutes", out var equivalents)
            ? equivalents.EnumerateArray().Select((route, index) => Route(route, index + 1, PolicyEvidenceStatus.Provisional, null)).ToArray()
            : [];
        var candidates = new[] { primary }.Concat(equivalent).ToArray();
        var minimumCapability = candidates.Select(candidate =>
            ModelRoutingKnowledgeBase.Default.FindModel(candidate.Model.Value)!.CapabilityTier).Min();
        return new()
        {
            TaskClass = EnumValue<TaskClass>(item, "taskClass"),
            Id = Text(item, "id"),
            Label = Text(item, "label"),
            Family = Text(item, "family"),
            Definition = Text(item, "definition"),
            OutcomeMeasure = Text(item, "outcomeMeasure"),
            BenchmarkStatus = Text(item, "benchmarkStatus"),
            Status = EnumValue<TaskClassRecommendationStatus>(item, "status"),
            Candidates = candidates,
            Equivalence = candidates.Length == 1 ? TaskClassEquivalenceStatus.Singleton : TaskClassEquivalenceStatus.Provisional,
            MinimumCapability = minimumCapability,
            UncertaintyReasons = candidates.Length == 1
                ? ["No task-qualified equivalent provider route is retained in this rationale version."]
                : ["Equivalent-provider membership is provisional until a task-local non-inferiority gate passes."],
            Downgrade = item.GetProperty("downgrade").ValueKind == JsonValueKind.Null
                ? null : Route(item.GetProperty("downgrade"), 0, PolicyEvidenceStatus.Provisional, null),
            DowngradeWhen = Strings(item, "downgradeWhen"),
            NeverDowngradeWhen = Strings(item, "neverDowngradeWhen"),
            Rationale = Text(item, "rationale"),
            RationaleVersion = Text(item, "rationaleVersion"),
            EvidenceVersion = Text(item, "evidenceVersion"),
            ScenarioCount = item.GetProperty("scenarioCount").GetInt32(),
            AttemptCount = item.GetProperty("attemptCount").GetInt32(),
            OutcomeRate = item.GetProperty("outcomeRate").ValueKind == JsonValueKind.Null
                ? null : item.GetProperty("outcomeRate").GetDecimal(),
            CostPerSuccessfulOutcome = cost.ValueKind == JsonValueKind.Null ? null : new()
            {
                AmountUsd = cost.GetProperty("amountUsd").ValueKind == JsonValueKind.Null
                    ? null : cost.GetProperty("amountUsd").GetDecimal(),
                PricedAtUtc = cost.GetProperty("pricedAtUtc").GetDateTime(),
                IncludesVerification = cost.GetProperty("includesVerification").GetBoolean(),
                Note = Text(cost, "note"),
            },
            Evidence = item.GetProperty("evidence").EnumerateArray().Select(line => new TaskClassEvidenceLine
            {
                EvidenceKind = Text(line, "evidenceKind"),
                Reference = Text(line, "reference"),
                Summary = Text(line, "summary"),
            }).ToArray(),
        };
    }

    private static TaskClassRouteRecommendation Route(
        JsonElement item, int rank, PolicyEvidenceStatus evidenceStatus, decimal? retryAdjustedCost)
    {
        var model = ModelId.Of(Text(item, "model"));
        return new()
        {
            RouteId = Text(item, "routeId"),
            Model = model,
            ThinkingLevel = EnumValue<EffortLevel>(item, "thinkingLevel"),
            Cli = ModelEfficiencyMatrix.Default.CliOf(model)
                ?? throw new InvalidDataException($"Recommendation model '{model}' has no known CLI."),
            Rank = rank,
            EvidenceStatus = evidenceStatus,
            Provisional = evidenceStatus == PolicyEvidenceStatus.Provisional,
            CostPerSuccessfulOutcomeUsd = retryAdjustedCost,
        };
    }

    private static T EnumValue<T>(JsonElement item, string property) where T : struct, Enum
        => Enum.TryParse<T>(Text(item, property), ignoreCase: true, out var value)
            ? value
            : throw new InvalidDataException($"Unknown {typeof(T).Name} value '{Text(item, property)}'.");

    private static string Text(JsonElement item, string property)
        => item.GetProperty(property).GetString()
            ?? throw new InvalidDataException($"Task-class recommendation property '{property}' is required.");

    private static IReadOnlyList<string> Strings(JsonElement item, string property)
        => item.GetProperty(property).EnumerateArray().Select(value => value.GetString()!).ToArray();

    private static void ValidateRoutes(IEnumerable<TaskClassRecommendation> recommendations)
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        foreach (var recommendation in recommendations)
        {
            if (recommendation.Candidates.Count == 0)
                throw new InvalidDataException($"Recommendation '{recommendation.Id}' has no candidates.");
            foreach (var route in recommendation.Candidates)
                ValidateRoute(knowledge, route, recommendation.Id);
            if (recommendation.Downgrade is { } downgrade)
                ValidateRoute(knowledge, downgrade, recommendation.Id);
            if (recommendation.Status == TaskClassRecommendationStatus.ControlledPilot
                && (recommendation.ScenarioCount < 1 || recommendation.AttemptCount < 2 || recommendation.OutcomeRate is null))
                throw new InvalidDataException($"Controlled pilot '{recommendation.Id}' lacks measured outcomes.");
        }
    }

    private static void ValidateRoute(
        ModelRoutingKnowledgeBase knowledge, TaskClassRouteRecommendation route, string recommendationId)
    {
        var policyRoute = knowledge.FindRoute(route.RouteId);
        var fallback = knowledge.ProviderFallbacks.SingleOrDefault(item => item.Id == route.RouteId);
        if (policyRoute is null && fallback is null)
            throw new InvalidDataException($"Recommendation '{recommendationId}' uses unknown route '{route.RouteId}'.");
        var model = policyRoute?.ModelId ?? fallback!.ModelId;
        var thinking = policyRoute?.ThinkingLevel ?? fallback!.ThinkingLevel;
        if (model != (string)route.Model
            || !string.Equals(thinking, Thinking(route.ThinkingLevel), StringComparison.Ordinal))
            throw new InvalidDataException($"Recommendation '{recommendationId}' disagrees with canonical route '{route.RouteId}'.");
    }

    private static string Thinking(EffortLevel level) => level switch
    {
        EffortLevel.XHigh => "xhigh",
        _ => level.ToString().ToLowerInvariant(),
    };

    private static string Required(string value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"{name} is required.") : value;

    private sealed record TaskClassRecommendationDocument
    {
        public int SchemaVersion { get; init; }
        public string TaxonomyVersion { get; init; } = "";
        public string RationaleVersion { get; init; } = "";
        public DateOnly EvidenceAsOfDate { get; init; }
        public IReadOnlyList<TaskClassRecommendation> Recommendations { get; init; } = [];
    }

    private static bool CliMatches(string cliType, Cli cli) => cli switch
    {
        Cli.Codex => string.Equals(cliType, "codex", StringComparison.OrdinalIgnoreCase),
        Cli.Claude => string.Equals(cliType, "claude", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cliType, "claude-code", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static TaskClassSelectionResult Selection(
        TaskClassSelectionDisposition disposition,
        TaskClassRecommendation recommendation,
        TaskClassRouteRecommendation? selected,
        IReadOnlyList<TaskClassCandidateElimination> eliminated,
        DateTime atUtc,
        string reason) => new()
        {
            Disposition = disposition,
            Recommendation = recommendation,
            Selected = selected,
            Eliminated = eliminated.ToArray(),
            EvaluatedAtUtc = atUtc,
            Reason = reason,
        };
}

#pragma warning restore CS1591
