using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenEconomy;

/// <summary>The native media abilities tracked for a coding CLI and selected-model scope.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MediaCapability>))]
public enum MediaCapability
{
    /// <summary>Generate a raster image from a text prompt.</summary>
    ImageGeneration,
    /// <summary>Edit an existing raster image using a model-backed image tool.</summary>
    ImageEdit,
    /// <summary>Use one or more images as visual references for image output.</summary>
    ReferenceImages,
    /// <summary>Understand images or screenshots supplied as input.</summary>
    ImageUnderstanding,
    /// <summary>Generate video as a native CLI output.</summary>
    Video,
    /// <summary>Generate music as a native CLI output.</summary>
    Music,
    /// <summary>Generate spoken audio from text as a native CLI output.</summary>
    Tts,
    /// <summary>Dictate a CLI prompt by voice and have it transcribed to text.</summary>
    VoiceDictation,
}

/// <summary>How strongly the relative cost factor in a media capability record is established.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MediaCostFactorStatus>))]
public enum MediaCostFactorStatus
{
    /// <summary>The capability is unsupported, so a relative cost does not apply.</summary>
    NotApplicable,
    /// <summary>The capability is supported but no comparable meter observation is available.</summary>
    Unknown,
    /// <summary>A range was reported but could not be reproduced with a comparable meter.</summary>
    UnverifiedClaim,
    /// <summary>The range was derived from retained comparable observations.</summary>
    Measured,
}

/// <summary>Evidence provenance for a native media capability assertion.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MediaEvidenceSource>))]
public enum MediaEvidenceSource
{
    /// <summary>Current vendor documentation.</summary>
    OfficialDocumentation,
    /// <summary>A local CLI, skill, or tool inventory captured on the observation date.</summary>
    LocalCapabilityInventory,
    /// <summary>A retained controlled benchmark artifact.</summary>
    ControlledBenchmark,
}

/// <summary>
/// A relative media-operation cost. The factor is deliberately separate from USD API pricing:
/// subscription CLIs commonly expose neither tokens nor credits for a built-in media tool.
/// </summary>
public sealed record MediaCostFactor
{
    /// <summary>Whether the factor is measured, merely claimed, unknown, or not applicable.</summary>
    public required MediaCostFactorStatus Status { get; init; }
    /// <summary>Denominator for the comparison, for example <c>normal Codex turn</c>.</summary>
    public string? RelativeTo { get; init; }
    /// <summary>Lower end of the claimed or measured multiplier.</summary>
    public decimal? Minimum { get; init; }
    /// <summary>Upper end of the claimed or measured multiplier.</summary>
    public decimal? Maximum { get; init; }
    /// <summary>Required caveat when the numbers are unknown or not measured.</summary>
    public string? Note { get; init; }
}

/// <summary>One dated, resolvable source for a media capability assertion.</summary>
public sealed record MediaCapabilityEvidence
{
    /// <summary>How the evidence was obtained.</summary>
    public required MediaEvidenceSource Source { get; init; }
    /// <summary>UTC observation timestamp.</summary>
    public required DateTime ObservedAtUtc { get; init; }
    /// <summary>Official URL or repository-relative retained artifact path.</summary>
    public required string Reference { get; init; }
    /// <summary>Short statement of what the source establishes or fails to establish.</summary>
    public required string Note { get; init; }
}

/// <summary>
/// One native-media assertion for a CLI and model scope. A <see cref="ModelId"/> of <c>*</c>
/// means the ability belongs to the CLI host/tool and is independent of the selected coding model.
/// External provider APIs do not count as native CLI support.
/// </summary>
public sealed record MediaCapabilityRecord
{
    /// <summary>Stable CLI id, for example <c>codex</c>, <c>antigravity</c>, or <c>claude-code</c>.</summary>
    public required string CliId { get; init; }
    /// <summary>Exact model id, or <c>*</c> for a CLI-hosted ability shared by selected models.</summary>
    public required string ModelId { get; init; }
    /// <summary>The media ability described by this row.</summary>
    public required MediaCapability Capability { get; init; }
    /// <summary>Whether a native invocation path was established as of the evidence date.</summary>
    public required bool Supported { get; init; }
    /// <summary>Natural-language, flag, slash-command, or explicit unsupported invocation path.</summary>
    public required string InvocationPath { get; init; }
    /// <summary>Relative cost information with an explicit evidence status.</summary>
    public required MediaCostFactor CostFactor { get; init; }
    /// <summary>Dated sources supporting the row.</summary>
    public required IReadOnlyList<MediaCapabilityEvidence> Evidence { get; init; }
}

internal sealed record MediaCapabilityCatalogDocument
{
    public required int SchemaVersion { get; init; }
    public required DateOnly AsOfDate { get; init; }
    public required IReadOnlyList<MediaCapabilityRecord> Records { get; init; }
}

/// <summary>
/// Repository-owned native-media catalog. Consumers pull the same embedded JSON used by the other
/// Token Economy catalogs; this adds no service or HTTP API.
/// </summary>
public sealed class MediaCapabilityCatalog
{
    private const string ResourceName = "TokenEconomy.catalog.media-capabilities.json";
    private readonly IReadOnlyList<MediaCapabilityRecord> _records;
    private readonly Dictionary<string, MediaCapabilityRecord> _byKey;

    /// <summary>Create a catalog from host-supplied records, primarily for imports and tests.</summary>
    public MediaCapabilityCatalog(
        IEnumerable<MediaCapabilityRecord> records,
        int schemaVersion = 1,
        DateOnly? asOfDate = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        SchemaVersion = schemaVersion;
        AsOfDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        _records = records.ToArray();
        _byKey = new(StringComparer.Ordinal);

        foreach (var record in _records)
        {
            Validate(record);
            var key = Key(record.CliId, record.ModelId, record.Capability);
            if (!_byKey.TryAdd(key, record))
                throw new ArgumentException(
                    $"Duplicate media capability '{record.Capability}' for CLI '{record.CliId}' and model '{record.ModelId}'.",
                    nameof(records));
        }
    }

    /// <summary>Schema version of the pulled catalog document.</summary>
    public int SchemaVersion { get; }
    /// <summary>Date through which the default assertions were reviewed.</summary>
    public DateOnly AsOfDate { get; }
    /// <summary>All catalog rows in source order.</summary>
    public IReadOnlyList<MediaCapabilityRecord> Records => _records;
    /// <summary>The embedded repository-owned catalog.</summary>
    public static MediaCapabilityCatalog Default { get; } = LoadDefault();

    /// <summary>
    /// Pull one capability for a CLI/model. Exact model rows win; otherwise the CLI's <c>*</c>
    /// host-tool row is returned. Unknown combinations return null.
    /// </summary>
    public MediaCapabilityRecord? Find(string? cliId, string? modelId, MediaCapability capability)
    {
        if (string.IsNullOrWhiteSpace(cliId) || string.IsNullOrWhiteSpace(modelId))
            return null;

        return _byKey.GetValueOrDefault(Key(cliId, modelId, capability))
            ?? _byKey.GetValueOrDefault(Key(cliId, "*", capability));
    }

    /// <summary>
    /// Pull the complete eight-row native-media matrix for a CLI/model, applying the same exact-then-
    /// host-tool fallback as <see cref="Find"/>.
    /// </summary>
    public IReadOnlyList<MediaCapabilityRecord> Pull(string? cliId, string? modelId) =>
        Enum.GetValues<MediaCapability>()
            .Select(capability => Find(cliId, modelId, capability))
            .Where(record => record is not null)
            .Cast<MediaCapabilityRecord>()
            .ToArray();

    private static MediaCapabilityCatalog LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded media catalog '{ResourceName}' was not found.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        var document = JsonSerializer.Deserialize<MediaCapabilityCatalogDocument>(stream, json)
            ?? throw new InvalidOperationException("Embedded media capability catalog contains no document.");
        return new(document.Records, document.SchemaVersion, document.AsOfDate);
    }

    private static void Validate(MediaCapabilityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Require(record.CliId, nameof(record.CliId));
        Require(record.ModelId, nameof(record.ModelId));
        Require(record.InvocationPath, nameof(record.InvocationPath));
        ArgumentNullException.ThrowIfNull(record.CostFactor);
        if (record.Evidence is null || record.Evidence.Count == 0)
            throw new ArgumentException("At least one dated evidence entry is required.", nameof(record.Evidence));
        foreach (var evidence in record.Evidence)
        {
            Require(evidence.Reference, nameof(evidence.Reference));
            Require(evidence.Note, nameof(evidence.Note));
            if (evidence.ObservedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Media evidence timestamps must be UTC.", nameof(record.Evidence));
        }

        var cost = record.CostFactor;
        if (cost.Minimum is < 0 || cost.Maximum is < 0 || cost.Minimum > cost.Maximum)
            throw new ArgumentException("Media cost factor bounds must be non-negative and ordered.", nameof(record.CostFactor));
        if (cost.Status == MediaCostFactorStatus.NotApplicable && (cost.Minimum is not null || cost.Maximum is not null))
            throw new ArgumentException("A not-applicable media cost factor cannot have numeric bounds.", nameof(record.CostFactor));
        if (cost.Status is MediaCostFactorStatus.Measured or MediaCostFactorStatus.UnverifiedClaim
            && (cost.Minimum is null || cost.Maximum is null || string.IsNullOrWhiteSpace(cost.RelativeTo)))
            throw new ArgumentException("Measured or claimed media cost factors require bounds and a comparison basis.", nameof(record.CostFactor));
    }

    private static string Key(string cliId, string modelId, MediaCapability capability) =>
        $"{Normalize(cliId)}\u001f{Normalize(modelId)}\u001f{capability}";

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static void Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty value is required.", paramName);
    }
}
