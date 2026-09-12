using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public records below are the strongly typed form of the documented JSON schema.

[JsonConverter(typeof(JsonStringEnumConverter<BenchmarkCapabilityClass>))]
public enum BenchmarkCapabilityClass
{
    CodingAgent,
    Reasoning,
    InstructionFollowing,
    LongContext,
    ControlledSetup,
    CodeReview,
}

[JsonConverter(typeof(JsonStringEnumConverter<BenchmarkScoreDirection>))]
public enum BenchmarkScoreDirection
{
    HigherIsBetter,
    LowerIsBetter,
}

[JsonConverter(typeof(JsonStringEnumConverter<BenchmarkRetrievalMethod>))]
public enum BenchmarkRetrievalMethod
{
    Manual,
    Script,
    Api,
}

[JsonConverter(typeof(JsonStringEnumConverter<BenchmarkConfidence>))]
public enum BenchmarkConfidence
{
    PublisherReported,
    ThirdParty,
    OwnRun,
}

/// <summary>A versioned definition of what one benchmark measures and how its score is interpreted.</summary>
public sealed record BenchmarkType
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public required string Publisher { get; init; }
    public required BenchmarkCapabilityClass CapabilityClass { get; init; }
    public required string Unit { get; init; }
    public required decimal MinimumScore { get; init; }
    public required decimal MaximumScore { get; init; }
    public required BenchmarkScoreDirection Direction { get; init; }
    public required string MethodologyUrl { get; init; }
    public required string CitationNote { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly CapturedAt { get; init; }
    public required DateOnly RetrievedAt { get; init; }
}

/// <summary>Optional resource and price measurements published beside a benchmark score.</summary>
public sealed record BenchmarkSecondaryMetrics
{
    public long? InputTokensPerTask { get; init; }
    public long? OutputTokensPerTask { get; init; }
    public decimal? CostPerTaskUsd { get; init; }
    public decimal? LatencyMilliseconds { get; init; }
}

/// <summary>Measurement provenance; omitted fields are unknown, not zero or defaults.</summary>
public sealed record BenchmarkEvidenceContext
{
    public required string SourceKind { get; init; }
    public required string SourcePublisher { get; init; }
    public string? RunnerOrganization { get; init; }
    public required string DateBasis { get; init; }
    public required DateOnly ObservedAt { get; init; }
    public DateTimeOffset? SourceCreatedAt { get; init; }
    public DateTimeOffset? SourceUpdatedAt { get; init; }
    public required string Harness { get; init; }
    public string? HarnessVersion { get; init; }
    public int? TaskCount { get; init; }
    public int? TrialCount { get; init; }
    public int? AttemptsPerTask { get; init; }
    public decimal? ConfidenceIntervalHalfWidth { get; init; }
    public decimal? ReportedErrorHalfWidth { get; init; }
    public decimal? ConfidenceIntervalLevel { get; init; }
    public decimal? TotalCostUsd { get; init; }
    public string? CostBasis { get; init; }
    public string? FallbackPolicy { get; init; }
    public string? SampleNotes { get; init; }
    public IReadOnlyList<string>? AdditionalSourceUrls { get; init; }
}

/// <summary>One append-only, sourced model/effort measurement.</summary>
public sealed record BenchmarkResult
{
    public required string Id { get; init; }
    public required string BenchmarkTypeId { get; init; }
    public required ModelId ModelId { get; init; }
    public required EffortLevel ReasoningEffort { get; init; }
    public required decimal Score { get; init; }
    public BenchmarkSecondaryMetrics? SecondaryMetrics { get; init; }
    public BenchmarkEvidenceContext? Context { get; init; }
    /// <summary>Publication date, or conservative availability date for an explicitly marked first-observed snapshot.</summary>
    public required DateOnly PublishedAt { get; init; }
    public required DateOnly RetrievedAt { get; init; }
    public required string SourceUrl { get; init; }
    public required BenchmarkRetrievalMethod RetrievalMethod { get; init; }
    public required string EvidenceExcerpt { get; init; }
    public required BenchmarkConfidence Confidence { get; init; }
}

internal sealed record BenchmarkTypeDocument
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required IReadOnlyList<BenchmarkType> Records { get; init; }
}

internal sealed record BenchmarkResultDocument
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required IReadOnlyList<BenchmarkResultJson> Records { get; init; }
}

internal sealed record BenchmarkResultJson
{
    public required string Id { get; init; }
    public required string BenchmarkTypeId { get; init; }
    public required string ModelId { get; init; }
    public required EffortLevel ReasoningEffort { get; init; }
    public required decimal Score { get; init; }
    public BenchmarkSecondaryMetrics? SecondaryMetrics { get; init; }
    public BenchmarkEvidenceContext? Context { get; init; }
    public required DateOnly PublishedAt { get; init; }
    public required DateOnly RetrievedAt { get; init; }
    public required string SourceUrl { get; init; }
    public required BenchmarkRetrievalMethod RetrievalMethod { get; init; }
    public required string EvidenceExcerpt { get; init; }
    public required BenchmarkConfidence Confidence { get; init; }
}

/// <summary>
/// Immutable benchmark definitions and raw measurements. The default catalog is loaded from the two
/// schema-backed embedded JSON files; callers can inject fixtures without filesystem or network I/O.
/// </summary>
public sealed class BenchmarkEvidenceCatalog
{
    private const string TypesResource = "TokenEconomy.catalog.benchmark-types.json";
    private const string ResultsResource = "TokenEconomy.catalog.benchmark-results.json";
    private readonly IReadOnlyList<BenchmarkType> _types;
    private readonly IReadOnlyList<BenchmarkResult> _results;
    private readonly Dictionary<string, BenchmarkType> _typesById;

    public BenchmarkEvidenceCatalog(
        IEnumerable<BenchmarkType> types,
        IEnumerable<BenchmarkResult> results,
        ModelPriceCatalog? modelCatalog = null,
        int schemaVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(results);
        SchemaVersion = schemaVersion;
        modelCatalog ??= ModelPriceCatalog.Default;
        _types = types.ToArray();
        _results = results.ToArray();
        _typesById = new(StringComparer.Ordinal);
        var resultIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in _types)
        {
            Validate(type);
            if (!_typesById.TryAdd(type.Id, type))
                throw new ArgumentException($"Duplicate benchmark type id '{type.Id}'.", nameof(types));
        }

        foreach (var result in _results)
        {
            Validate(result);
            if (!resultIds.Add(result.Id))
                throw new ArgumentException($"Duplicate benchmark result id '{result.Id}'.", nameof(results));
            if (!_typesById.TryGetValue(result.BenchmarkTypeId, out var type))
                throw new ArgumentException($"Unknown benchmark type '{result.BenchmarkTypeId}'.", nameof(results));
            if (modelCatalog.Find(result.ModelId) is null)
                throw new ArgumentException($"Unknown benchmark model '{result.ModelId}'.", nameof(results));
            if (result.Score < type.MinimumScore || result.Score > type.MaximumScore)
                throw new ArgumentException($"Result '{result.Id}' score is outside the benchmark range.", nameof(results));
        }
    }

    public int SchemaVersion { get; }
    public IReadOnlyList<BenchmarkType> Types => _types;
    public IReadOnlyList<BenchmarkResult> Results => _results;
    public static BenchmarkEvidenceCatalog Default { get; } = LoadDefault();
    public BenchmarkType? FindType(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : _typesById.GetValueOrDefault(id);
    public IReadOnlyList<BenchmarkResult> ResultsFor(string benchmarkTypeId) =>
        _results.Where(result => result.BenchmarkTypeId == benchmarkTypeId).ToArray();

    private static BenchmarkEvidenceCatalog LoadDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        using var typesStream = Resource(TypesResource);
        using var resultsStream = Resource(ResultsResource);
        var types = JsonSerializer.Deserialize<BenchmarkTypeDocument>(typesStream, options)
            ?? throw new InvalidOperationException("Embedded benchmark type catalog contains no document.");
        var results = JsonSerializer.Deserialize<BenchmarkResultDocument>(resultsStream, options)
            ?? throw new InvalidOperationException("Embedded benchmark result catalog contains no document.");
        if (types.Schema != "benchmark-types.schema.json" || results.Schema != "benchmark-results.schema.json"
            || types.SchemaVersion != 1 || results.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported benchmark catalog schema version.");

        return new(
            types.Records,
            results.Records.Select(result => new BenchmarkResult
            {
                Id = result.Id,
                BenchmarkTypeId = result.BenchmarkTypeId,
                ModelId = ModelId.Of(result.ModelId),
                ReasoningEffort = result.ReasoningEffort,
                Score = result.Score,
                SecondaryMetrics = result.SecondaryMetrics,
                Context = result.Context,
                PublishedAt = result.PublishedAt,
                RetrievedAt = result.RetrievedAt,
                SourceUrl = result.SourceUrl,
                RetrievalMethod = result.RetrievalMethod,
                EvidenceExcerpt = result.EvidenceExcerpt,
                Confidence = result.Confidence,
            }),
            schemaVersion: types.SchemaVersion);
    }

    private static Stream Resource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Embedded benchmark catalog '{name}' was not found.");

    private static void Validate(BenchmarkType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Require(type.Id, nameof(type.Id));
        Require(type.Version, nameof(type.Version));
        Require(type.Name, nameof(type.Name));
        Require(type.Publisher, nameof(type.Publisher));
        Require(type.Unit, nameof(type.Unit));
        Require(type.CitationNote, nameof(type.CitationNote));
        if (type.MaximumScore <= type.MinimumScore)
            throw new ArgumentException("A benchmark score range must be increasing.", nameof(type));
        if (!AbsoluteHttpUri(type.MethodologyUrl))
            throw new ArgumentException("A benchmark methodology URL must be an absolute HTTP(S) URL.", nameof(type));
        if (type.ValidFrom == default || type.CapturedAt == default || type.RetrievedAt == default)
            throw new ArgumentException("Benchmark definition dates are required.", nameof(type));
    }

    private static void Validate(BenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Require(result.Id, nameof(result.Id));
        Require(result.BenchmarkTypeId, nameof(result.BenchmarkTypeId));
        Require(result.ModelId.Value, nameof(result.ModelId));
        Require(result.EvidenceExcerpt, nameof(result.EvidenceExcerpt));
        if (result.PublishedAt == default || result.RetrievedAt == default)
            throw new ArgumentException("Benchmark results require publishedAt and retrievedAt.", nameof(result));
        if (!AbsoluteHttpUri(result.SourceUrl))
            throw new ArgumentException("Benchmark results require an absolute HTTP(S) source URL.", nameof(result));
        if (result.RetrievedAt < result.PublishedAt)
            throw new ArgumentException("A benchmark result cannot be retrieved before publication.", nameof(result));
        ValidateContext(result);
        var metrics = result.SecondaryMetrics;
        if (metrics?.InputTokensPerTask is < 0 || metrics?.OutputTokensPerTask is < 0
            || metrics?.CostPerTaskUsd is <= 0 || metrics?.LatencyMilliseconds is < 0)
            throw new ArgumentException("Benchmark secondary metrics must be non-negative and cost must be positive.", nameof(result));
    }

    private static void ValidateContext(BenchmarkResult result)
    {
        var context = result.Context;
        if (context is null) return;
        Require(context.SourcePublisher, nameof(context.SourcePublisher));
        Require(context.Harness, nameof(context.Harness));
        if (context.SourceKind is not ("benchmarkOwner" or "independentEvaluator" or "officialLeaderboard" or "modelProvider" or "ownRun"))
            throw new ArgumentException("Unknown benchmark source kind.", nameof(result));
        if (context.DateBasis is not ("publishedDate" or "firstObservedPublicSnapshot"))
            throw new ArgumentException("Unknown benchmark date basis.", nameof(result));
        if (context.ObservedAt == default || context.ObservedAt > result.RetrievedAt || context.ObservedAt < result.PublishedAt)
            throw new ArgumentException("Benchmark observation must be between availability and retrieval.", nameof(result));
        if (context.DateBasis == "firstObservedPublicSnapshot" && context.ObservedAt != result.PublishedAt)
            throw new ArgumentException("A first-observed snapshot must use its observation date as availability.", nameof(result));
        if (context.TaskCount is <= 0 || context.TrialCount is <= 0 || context.AttemptsPerTask is <= 0
            || context.ConfidenceIntervalHalfWidth is < 0 || context.ReportedErrorHalfWidth is < 0 || context.ConfidenceIntervalLevel is <= 0 or > 1
            || context.TotalCostUsd is < 0)
            throw new ArgumentException("Benchmark sample, interval and cost context is invalid.", nameof(result));
        if (context.TaskCount is { } tasks && context.AttemptsPerTask is { } attempts && context.TrialCount is { } trials
            && (long)tasks * attempts != trials)
            throw new ArgumentException("Balanced benchmark attempts must match task and trial counts.", nameof(result));
        if (context.AdditionalSourceUrls?.Any(url => !AbsoluteHttpUri(url)) == true)
            throw new ArgumentException("Additional benchmark sources must be absolute HTTP(S) URLs.", nameof(result));
    }

    private static bool AbsoluteHttpUri([NotNullWhen(true)] string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static void Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty value is required.", paramName);
    }
}

#pragma warning restore CS1591
