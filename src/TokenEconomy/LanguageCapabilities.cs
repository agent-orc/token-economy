using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenEconomy;

#pragma warning disable CS1591 // Public records mirror the adjacent documented JSON schemas.

public enum LanguageEvidenceStatus { UnverifiedClaim, Provisional, Observed }
public enum LanguageEvidenceKind { Study, LocalBenchmark }
public enum LanguageDimension { Readability, Warmth, Directness, AbsenceOfAiIsms, ToneAdherence, FactualRestraint }

/// <summary>Six independent scores or thresholds. Null means unknown (or no threshold), never zero.</summary>
public sealed record LanguageDimensionScores
{
    public decimal? Readability { get; init; }
    public decimal? Warmth { get; init; }
    public decimal? Directness { get; init; }
    public decimal? AbsenceOfAiIsms { get; init; }
    public decimal? ToneAdherence { get; init; }
    public decimal? FactualRestraint { get; init; }

    public decimal? this[LanguageDimension dimension] => dimension switch
    {
        LanguageDimension.Readability => Readability,
        LanguageDimension.Warmth => Warmth,
        LanguageDimension.Directness => Directness,
        LanguageDimension.AbsenceOfAiIsms => AbsenceOfAiIsms,
        LanguageDimension.ToneAdherence => ToneAdherence,
        LanguageDimension.FactualRestraint => FactualRestraint,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
    };

    internal void Validate()
    {
        foreach (var dimension in Enum.GetValues<LanguageDimension>())
            ValidateScore(this[dimension]);
    }

    internal static void ValidateScore(decimal? score)
    {
        if (score is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(score), "Language scores and thresholds must be in [0, 1].");
    }

    internal decimal? Mean => Enum.GetValues<LanguageDimension>().All(d => this[d].HasValue)
        ? Enum.GetValues<LanguageDimension>().Average(d => this[d]!.Value) : null;
}

public sealed record LanguageCapabilityEvidence
{
    public required LanguageEvidenceKind Kind { get; init; }
    public required string Reference { get; init; }
    public required DateTime ObservedAtUtc { get; init; }
    public required string Note { get; init; }
}

public sealed record LanguageEvidenceProvenance
{
    public required string SourceRepository { get; init; }
    public required string SourcePath { get; init; }
    public required string SourceSha256 { get; init; }
    public required string MirrorPath { get; init; }
}

/// <summary>An append-only model/language/effort observation, retaining its own cost and provenance.</summary>
public sealed record LanguageCapabilityRecord
{
    public required string Id { get; init; }
    public required string ModelId { get; init; }
    public required string CliId { get; init; }
    public required string Language { get; init; }
    public required EffortLevel ThinkingLevel { get; init; }
    public required LanguageEvidenceStatus Status { get; init; }
    public required LanguageDimensionScores Scores { get; init; }
    public required decimal? Overall { get; init; }
    public required decimal? CostPerSampleUsd { get; init; }
    public required string Notes { get; init; }
    public required IReadOnlyList<LanguageCapabilityEvidence> Evidence { get; init; }
    public LanguageEvidenceProvenance? Provenance { get; init; }
    [JsonIgnore]
    public DateTime ObservedAtUtc => Evidence.Max(e => e.ObservedAtUtc);
}

public sealed record LanguageStudy
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Authors { get; init; }
    public required int Year { get; init; }
    public required string Url { get; init; }
    public required string Kind { get; init; }
    public required string Claim { get; init; }
    public required string Relevance { get; init; }
    public required LanguageEvidenceStatus Status { get; init; }
    public DateTime? VerifiedAtUtc { get; init; }
}

/// <summary>
/// A conjunctive language constraint, applied after correctness routing. Observed evidence is required
/// unless provisional measurements are explicitly allowed; unverified claims never qualify.
/// </summary>
public sealed record HumanFriendlyLanguageRequirement
{
    public required string Language { get; init; }
    public decimal? MinimumOverall { get; init; }
    public LanguageDimensionScores MinimumScores { get; init; } = new();
    /// <summary>Requested effort; null uses the matrix's existing suggested or explicitly evaluated effort.</summary>
    public EffortLevel? ThinkingLevel { get; init; }
    public bool AllowProvisional { get; init; }

    public void Validate()
    {
        if (Language is not ("de" or "en"))
            throw new ArgumentException("Language must be de or en.", nameof(Language));
        ArgumentNullException.ThrowIfNull(MinimumScores);
        LanguageDimensionScores.ValidateScore(MinimumOverall);
        MinimumScores.Validate();
        if (MinimumOverall is null && Enum.GetValues<LanguageDimension>().All(d => MinimumScores[d] is null))
            throw new ArgumentException("At least one overall or dimension threshold is required.");
        if (ThinkingLevel is { } effort && (!Enum.IsDefined(effort) || effort == EffortLevel.Unspecified))
            throw new ArgumentException("A requirement must name a supported, concrete thinking level.");
    }

    public bool IsSatisfiedBy(LanguageCapabilityRecord? record)
    {
        Validate();
        if (record is null || record.Language != Language
            || (ThinkingLevel is { } level && record.ThinkingLevel != level)
            || record.ThinkingLevel == EffortLevel.Unspecified
            || !(record.Status == LanguageEvidenceStatus.Observed
                || (AllowProvisional && record.Status == LanguageEvidenceStatus.Provisional)))
            return false;
        if (MinimumOverall is { } overall && !(record.Overall >= overall)) return false;
        return Enum.GetValues<LanguageDimension>().All(d => MinimumScores[d] is not { } threshold
            || record.Scores[d] >= threshold);
    }
}

/// <summary>Dated language evidence. Scores never transfer between models, languages, or thinking levels.</summary>
public sealed class LanguageCapabilityCatalog
{
    public int SchemaVersion { get; }
    public DateOnly AsOfDate { get; }
    public IReadOnlyList<LanguageCapabilityRecord> Records { get; }
    public IReadOnlyList<LanguageStudy> Studies { get; }

    public LanguageCapabilityCatalog(IEnumerable<LanguageCapabilityRecord> records, int schemaVersion = 1,
        DateOnly? asOfDate = null, IEnumerable<LanguageStudy>? studies = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (schemaVersion != 1) throw new ArgumentException("Unsupported language catalog schema.", nameof(schemaVersion));
        SchemaVersion = schemaVersion;
        AsOfDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        Records = Array.AsReadOnly(records.Select(row =>
        {
            Validate(row);
            if (DateOnly.FromDateTime(row.ObservedAtUtc) > AsOfDate)
                throw new ArgumentException("Evidence cannot be later than the catalog asOfDate.");
            return row with { Evidence = Array.AsReadOnly(row.Evidence.ToArray()) };
        }).ToArray());
        if (Records.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() != Records.Count)
            throw new ArgumentException("Duplicate language evidence record id.", nameof(records));
        Studies = Array.AsReadOnly((studies ?? []).ToArray());
        foreach (var study in Studies)
        {
            if (study is null || string.IsNullOrWhiteSpace(study.Id) || string.IsNullOrWhiteSpace(study.Title)
                || string.IsNullOrWhiteSpace(study.Authors) || string.IsNullOrWhiteSpace(study.Claim)
                || string.IsNullOrWhiteSpace(study.Relevance) || study.Year is < 1900 or > 2100
                || study.Kind is not ("peer-reviewed" or "preprint" or "vendor" or "community")
                || study.Status is not (LanguageEvidenceStatus.Provisional or LanguageEvidenceStatus.UnverifiedClaim)
                || !Uri.TryCreate(study.Url, UriKind.Absolute, out var url) || url.Scheme != "https"
                || (study.Status == LanguageEvidenceStatus.Provisional && study.VerifiedAtUtc is null)
                || (study.VerifiedAtUtc is { } verified && (verified.Kind != DateTimeKind.Utc
                    || DateOnly.FromDateTime(verified) > AsOfDate)))
                throw new ArgumentException("Study inventory needs complete, dated source provenance.");
        }
        if (Studies.Select(study => study.Id).Distinct(StringComparer.Ordinal).Count() != Studies.Count)
            throw new ArgumentException("Duplicate language study id.", nameof(studies));
    }

    /// <summary>
    /// Latest observation of the strongest evidence status at or before the UTC cutoff. Specify effort
    /// for routing; omitting it is an inspection across efforts. Newer failures do not fall back to older passes.
    /// </summary>
    public LanguageCapabilityRecord? Find(string? modelId, string? language,
        EffortLevel? thinkingLevel = null, DateTime? atUtc = null)
    {
        if (atUtc is { Kind: not DateTimeKind.Utc }) throw new ArgumentException("atUtc must be UTC.", nameof(atUtc));
        var canonical = ModelPriceCatalog.Default.Find(modelId)?.ModelId;
        if (canonical is null || string.IsNullOrWhiteSpace(language)) return null;
        return Records.Where(row => row.ModelId == canonical
                && row.Language == language.Trim().ToLowerInvariant()
                && (thinkingLevel is null || row.ThinkingLevel == thinkingLevel)
                && (atUtc is null || row.ObservedAtUtc <= atUtc))
            .OrderByDescending(row => row.Status)
            .ThenByDescending(row => row.ObservedAtUtc)
            .ThenBy(row => row.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    public LanguageCapabilityRecord? Find(ModelId modelId, string language,
        EffortLevel? thinkingLevel = null, DateTime? atUtc = null)
        => Find(modelId.Value, language, thinkingLevel, atUtc);

    private static LanguageCapabilityCatalog LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TokenEconomy.catalog.language-capabilities.json")
            ?? throw new InvalidOperationException("Embedded language catalog is missing.");
        var document = JsonSerializer.Deserialize<CatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Embedded language catalog is empty.");
        if (document.Dimensions is null || !document.Dimensions.SequenceEqual(DimensionNames)
            || document.Weights is null || document.Weights.Count != 6
            || DimensionNames.Any(d => document.Weights.GetValueOrDefault(d) != 1m))
            throw new InvalidDataException("Language rubric must contain six equally weighted dimensions.");
        return new(document.Records, document.SchemaVersion, document.AsOfDate, document.Studies);
    }

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    internal static readonly string[] DimensionNames =
        ["readability", "warmth", "directness", "absenceOfAiIsms", "toneAdherence", "factualRestraint"];

    public static LanguageCapabilityCatalog Default { get; } = LoadDefault();

    private static void Validate(LanguageCapabilityRecord row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Notes))
            throw new ArgumentException("Record id and notes are required.");
        var model = ModelPriceCatalog.Default.Find(row.ModelId);
        if (model is null || model.ModelId != row.ModelId)
            throw new ArgumentException("Language evidence requires a canonical model id.");
        if (row.CliId != (model.Vendor == "openai" ? "codex" : "claude-code")
            || row.Language is not ("de" or "en") || !Enum.IsDefined(row.ThinkingLevel) || !Enum.IsDefined(row.Status))
            throw new ArgumentException("Invalid CLI, language, thinking level, or evidence status.");
        ArgumentNullException.ThrowIfNull(row.Scores);
        row.Scores.Validate();
        LanguageDimensionScores.ValidateScore(row.Overall);
        if (row.CostPerSampleUsd < 0) throw new ArgumentException("Sample cost cannot be negative.");
        if (row.Overall is { } overall && (row.Scores.Mean is not { } mean || Math.Abs(mean - overall) > .000001m))
            throw new ArgumentException("Overall must equal the six-dimension mean (tolerance 0.000001).");
        if (row.Evidence is null || row.Evidence.Count == 0)
            throw new ArgumentException("At least one dated evidence entry is required.");
        foreach (var evidence in row.Evidence)
            if (evidence is null || !Enum.IsDefined(evidence.Kind) || string.IsNullOrWhiteSpace(evidence.Reference)
                || string.IsNullOrWhiteSpace(evidence.Note) || evidence.ObservedAtUtc.Kind != DateTimeKind.Utc
                || evidence.ObservedAtUtc == DateTime.MinValue)
                throw new ArgumentException("Evidence requires kind, reference, note, and a UTC observation date.");
        if (row.Status == LanguageEvidenceStatus.Observed)
        {
            if (row.Overall is null || row.Scores.Mean is null || row.CostPerSampleUsd is null
                || row.ThinkingLevel == EffortLevel.Unspecified
                || !row.Evidence.Any(e => e.Kind == LanguageEvidenceKind.LocalBenchmark))
                throw new ArgumentException("Observed language scores require complete measured results and local benchmark evidence.");
            var provenance = row.Provenance;
            if (provenance is null || !Uri.TryCreate(provenance.SourceRepository, UriKind.Absolute, out var uri)
                || uri.Scheme != "https" || provenance.SourcePath != "benchmarks/human-friendly-language/results.json"
                || provenance.SourceSha256 is not { Length: 64 } || provenance.SourceSha256.Any(c => !char.IsAsciiHexDigit(c))
                || provenance.MirrorPath != $"src/TokenEconomy/catalog/language-evidence/{provenance.SourceSha256}.json")
                throw new ArgumentException("Observed language results require source and mirrored-file provenance.");
        }
    }

    private sealed record CatalogDocument(int SchemaVersion, DateOnly AsOfDate, string[] Dimensions,
        Dictionary<string, decimal> Weights, LanguageCapabilityRecord[] Records, LanguageStudy[] Studies);
}

#pragma warning restore CS1591
