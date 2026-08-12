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

/// <summary>A model and thinking-level pair exposed to card-creation callers.</summary>
public sealed record TaskClassRouteRecommendation
{
    public string RouteId { get; init; } = "";
    public ModelId Model { get; init; }
    public EffortLevel ThinkingLevel { get; init; }
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
    public TaskClassRouteRecommendation Recommended { get; init; } = new();
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
            Recommended = Route(item.GetProperty("recommended")),
            Downgrade = item.GetProperty("downgrade").ValueKind == JsonValueKind.Null
                ? null : Route(item.GetProperty("downgrade")),
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

    private static TaskClassRouteRecommendation Route(JsonElement item) => new()
    {
        RouteId = Text(item, "routeId"),
        Model = ModelId.Of(Text(item, "model")),
        ThinkingLevel = EnumValue<EffortLevel>(item, "thinkingLevel"),
    };

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
            ValidateRoute(knowledge, recommendation.Recommended, recommendation.Id);
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
        var policyRoute = knowledge.FindRoute(route.RouteId)
            ?? throw new InvalidDataException($"Recommendation '{recommendationId}' uses unknown route '{route.RouteId}'.");
        if (policyRoute.ModelId != (string)route.Model
            || !string.Equals(policyRoute.ThinkingLevel, Thinking(route.ThinkingLevel), StringComparison.Ordinal))
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
}

#pragma warning restore CS1591
