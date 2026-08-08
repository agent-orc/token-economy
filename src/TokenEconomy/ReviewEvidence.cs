using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS1591
namespace TokenEconomy;

[JsonConverter(typeof(JsonStringEnumConverter<ReviewEvidenceQuality>))]
public enum ReviewEvidenceQuality
{
    InsufficientEvidence,
    ObservationalSupport,
}

/// <summary>The Quality Studio drop contract: one immutable artifact per review run.</summary>
public sealed record QualityStudioReviewRun
{
    [JsonPropertyName("$schema")]
    public required string SchemaUri { get; init; }
    public required int SchemaVersion { get; init; }
    public required string SourceRunId { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public required string Model { get; init; }
    public required string ThinkingLevel { get; init; }
    public required string Cli { get; init; }
    public required string ReviewAspect { get; init; }
    public required QualityStudioReviewScope Scope { get; init; }
    public QualityStudioReviewOutcomes? Outcomes { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public bool IsFixture { get; init; }
}

public sealed record QualityStudioReviewScope
{
    public required int FilesReviewed { get; init; }
    public required int FindingsReported { get; init; }
}

public sealed record QualityStudioReviewOutcomes
{
    public required int ConfirmedFindings { get; init; }
    public required int DismissedFindings { get; init; }
}

/// <summary>A normalized append-only review observation with retained source provenance.</summary>
public sealed record QualityStudioReviewEvidence
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string EvidenceVersion { get; init; }
    public required string SourceRunId { get; init; }
    public required DateTimeOffset ObservedAtUtc { get; init; }
    public required string SourceArtifactReference { get; init; }
    public required string SourceArtifactSha256 { get; init; }
    public required string Model { get; init; }
    public string? CanonicalModel { get; init; }
    public string? ThinkingLevel { get; init; }
    public required string Cli { get; init; }
    public required string ReviewAspect { get; init; }
    public required int FilesReviewed { get; init; }
    public required int FindingsReported { get; init; }
    public int? ConfirmedFindings { get; init; }
    public int? DismissedFindings { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public required bool IsFixture { get; init; }
    public required bool EligibleForAggregation { get; init; }
    public required IReadOnlyList<string> EligibilityIssues { get; init; }
}

/// <summary>Conservative gates for turning operational review history into an observational suitability signal.</summary>
public sealed record ReviewEvidenceConfidenceGates
{
    public const int CurrentVersion = 1;
    public const decimal PolicyMinimumFindingOutcomeCoverage = .70m;
    public int Version { get; init; } = CurrentVersion;
    public int MinimumRunCount { get; init; } = 20;
    public int MinimumAssessedFindingCount { get; init; } = 20;
    public decimal MinimumFindingOutcomeCoverage { get; init; } = PolicyMinimumFindingOutcomeCoverage;
    public decimal CapableFindingConfirmationRate { get; init; } = .60m;
    public decimal IdealFindingConfirmationRate { get; init; } = .80m;
}

/// <summary>Review-quality metrics for one model across the task class <c>review</c>.</summary>
public sealed record ModelReviewQualitySummary
{
    public required string CanonicalModel { get; init; }
    public string TaskClass { get; init; } = "review";
    public required int RunCount { get; init; }
    public required int FilesReviewed { get; init; }
    public required int FindingsReported { get; init; }
    public required int OutcomeAvailableRunCount { get; init; }
    public required int ConfirmedFindings { get; init; }
    public required int DismissedFindings { get; init; }
    public required int AssessedFindingCount { get; init; }
    public decimal? FindingOutcomeCoverage { get; init; }
    public decimal? FindingConfirmationRate { get; init; }
    public IReadOnlyList<string> ThinkingLevels { get; init; } = [];
    public IReadOnlyList<string> ReviewAspects { get; init; } = [];
    public DateOnly? ObservedThrough { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public required ReviewEvidenceQuality EvidenceQuality { get; init; }
    public Suitability? Suitability { get; init; }
    public required IReadOnlyList<string> GateFailures { get; init; }
    public required IReadOnlyList<RoutingEvidenceProvenance> Provenance { get; init; }
}

/// <summary>Comparable operational observations grouped by model, thinking level, CLI, and review aspect.</summary>
public sealed record ReviewEvidenceCohort
{
    public required string CanonicalModel { get; init; }
    public required string ThinkingLevel { get; init; }
    public required string Cli { get; init; }
    public required string ReviewAspect { get; init; }
    public required int RunCount { get; init; }
    public required int FilesReviewed { get; init; }
    public required int FindingsReported { get; init; }
    public required int ConfirmedFindings { get; init; }
    public required int DismissedFindings { get; init; }
    public decimal? FindingOutcomeCoverage { get; init; }
    public decimal? FindingConfirmationRate { get; init; }
    public required PolicyEvidenceStatus EvidenceStatus { get; init; }
    public required ReviewEvidenceQuality EvidenceQuality { get; init; }
    public required IReadOnlyList<string> GateFailures { get; init; }
    public required IReadOnlyList<RoutingEvidenceProvenance> Provenance { get; init; }
}

/// <summary>Versioned aggregation of observational Quality Studio review evidence.</summary>
public sealed record ReviewEvidenceReport
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string EvidenceVersion { get; init; }
    public string TaskClass { get; init; } = "review";
    public PolicyEvidenceStatus EvidenceStatus { get; init; } = PolicyEvidenceStatus.Observational;
    public required ReviewEvidenceConfidenceGates ConfidenceGates { get; init; }
    public required int ImportedRunCount { get; init; }
    public required int FixtureRunCount { get; init; }
    public required int EligibleOperationalRunCount { get; init; }
    public required IReadOnlyList<ReviewEvidenceCohort> Cohorts { get; init; }
    public required IReadOnlyList<ModelReviewQualitySummary> ModelSummaries { get; init; }
}

/// <summary>Strict parser for the Quality Studio side of the drop-path contract.</summary>
public sealed class QualityStudioReviewRunImporter
{
    private const string ExpectedSchemaName = "quality-studio-review-run.schema.json";
    private readonly ModelRoutingKnowledgeBase _knowledge;
    private static readonly JsonSerializerOptions Json = Options(writeIndented: false);

    public QualityStudioReviewRunImporter(ModelRoutingKnowledgeBase? knowledge = null)
        => _knowledge = knowledge ?? ModelRoutingKnowledgeBase.PolicyOnly;

    public QualityStudioReviewEvidence Import(string path, string sourceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        var bytes = File.ReadAllBytes(path);
        var run = JsonSerializer.Deserialize<QualityStudioReviewRun>(bytes, Json)
            ?? throw new InvalidDataException($"Quality Studio review artifact contains no run: {path}");
        Validate(run, path);

        var model = _knowledge.FindModel(run.Model);
        var thinking = _knowledge.FindThinkingLevel(run.ThinkingLevel);
        var cli = Normalize(run.Cli);
        var issues = new List<string>();
        if (run.IsFixture) issues.Add("fixture runs do not contribute operational evidence");
        if (model is null) issues.Add("canonical model is unknown");
        if (thinking is null) issues.Add("thinking level is unknown");
        else if (model is not null && !model.SupportedThinkingLevels.Contains(thinking.Id, StringComparer.Ordinal))
            issues.Add("thinking level is unsupported by the model");
        if (model is not null && !string.Equals(model.CliId, cli, StringComparison.Ordinal))
            issues.Add("CLI does not match the model provider");

        return new()
        {
            EvidenceVersion = "quality-studio-review-run-v1",
            SourceRunId = run.SourceRunId,
            ObservedAtUtc = run.CompletedAtUtc.ToUniversalTime(),
            SourceArtifactReference = sourceReference.Replace('\\', '/'),
            SourceArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Model = run.Model,
            CanonicalModel = model?.CanonicalId,
            ThinkingLevel = thinking?.Id,
            Cli = cli,
            ReviewAspect = Normalize(run.ReviewAspect),
            FilesReviewed = run.Scope.FilesReviewed,
            FindingsReported = run.Scope.FindingsReported,
            ConfirmedFindings = run.Outcomes?.ConfirmedFindings,
            DismissedFindings = run.Outcomes?.DismissedFindings,
            EvidenceStatus = run.EvidenceStatus,
            IsFixture = run.IsFixture,
            EligibleForAggregation = issues.Count == 0,
            EligibilityIssues = issues,
        };
    }

    private static void Validate(QualityStudioReviewRun run, string path)
    {
        if (run.SchemaVersion != 1 || !run.SchemaUri.EndsWith(ExpectedSchemaName, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported Quality Studio review schema in {path}.");
        if (!IsSafeRunId(run.SourceRunId))
            throw new InvalidDataException($"sourceRunId must match [A-Za-z0-9][A-Za-z0-9._-]{{0,127}} in {path}.");
        if (run.CompletedAtUtc == default || run.CompletedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException($"completedAtUtc must be an explicit UTC timestamp in {path}.");
        if (string.IsNullOrWhiteSpace(run.Model) || string.IsNullOrWhiteSpace(run.ThinkingLevel)
            || string.IsNullOrWhiteSpace(run.Cli) || string.IsNullOrWhiteSpace(run.ReviewAspect))
            throw new InvalidDataException($"model, thinkingLevel, cli, and reviewAspect are required in {path}.");
        if (run.EvidenceStatus != PolicyEvidenceStatus.Observational)
            throw new InvalidDataException($"Quality Studio review runs must use policy evidenceStatus 'observational' in {path}.");
        if (run.Scope.FilesReviewed < 1 || run.Scope.FindingsReported < 0)
            throw new InvalidDataException($"Review scope is invalid in {path}.");
        if (run.Outcomes is { } outcomes)
        {
            if (outcomes.ConfirmedFindings < 0 || outcomes.DismissedFindings < 0
                || outcomes.ConfirmedFindings + outcomes.DismissedFindings > run.Scope.FindingsReported)
                throw new InvalidDataException($"Reviewed finding outcomes exceed the reported findings in {path}.");
        }
    }

    private static bool IsSafeRunId(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 128
            && char.IsAsciiLetterOrDigit(value[0])
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');

    internal static JsonSerializerOptions Options(bool writeIndented) => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = writeIndented,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/// <summary>Pure aggregation that never promotes observational history to controlled validation.</summary>
public sealed class ReviewEvidenceAggregator
{
    private readonly ModelRoutingKnowledgeBase _knowledge;

    public ReviewEvidenceAggregator(ModelRoutingKnowledgeBase? knowledge = null)
        => _knowledge = knowledge ?? ModelRoutingKnowledgeBase.PolicyOnly;

    public ReviewEvidenceReport Aggregate(
        IEnumerable<QualityStudioReviewEvidence> runs,
        ReviewEvidenceConfidenceGates? gates = null,
        string evidenceVersion = "review-evidence-v1")
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceVersion);
        gates ??= new();
        Validate(gates);
        var all = runs.GroupBy(run => run.SourceRunId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(run => run.SourceArtifactSha256, StringComparer.Ordinal).First())
            .OrderBy(run => run.SourceRunId, StringComparer.Ordinal).ToArray();
        var eligible = all.Where(run => run.EligibleForAggregation).ToArray();

        var cohorts = eligible.GroupBy(run => (run.CanonicalModel!, run.ThinkingLevel!, run.Cli, run.ReviewAspect))
            .Select(group => Cohort(group.Key.Item1, group.Key.Item2, group.Key.Cli, group.Key.ReviewAspect, group.ToArray(), gates))
            .OrderBy(cohort => cohort.CanonicalModel, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.ThinkingLevel, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.Cli, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.ReviewAspect, StringComparer.Ordinal).ToArray();
        var summaries = _knowledge.Models.OrderBy(model => model.CanonicalId, StringComparer.Ordinal)
            .Select(model => Summary(model.CanonicalId,
                eligible.Where(run => run.CanonicalModel == model.CanonicalId).ToArray(), gates)).ToArray();

        return new()
        {
            EvidenceVersion = evidenceVersion,
            ConfidenceGates = gates,
            ImportedRunCount = all.Length,
            FixtureRunCount = all.Count(run => run.IsFixture),
            EligibleOperationalRunCount = eligible.Length,
            Cohorts = cohorts,
            ModelSummaries = summaries,
        };
    }

    private static ReviewEvidenceCohort Cohort(
        string model, string thinking, string cli, string aspect,
        IReadOnlyCollection<QualityStudioReviewEvidence> runs,
        ReviewEvidenceConfidenceGates gates)
    {
        var metrics = Metrics(runs, gates);
        return new()
        {
            CanonicalModel = model,
            ThinkingLevel = thinking,
            Cli = cli,
            ReviewAspect = aspect,
            RunCount = runs.Count,
            FilesReviewed = runs.Sum(run => run.FilesReviewed),
            FindingsReported = metrics.Findings,
            ConfirmedFindings = metrics.Confirmed,
            DismissedFindings = metrics.Dismissed,
            FindingOutcomeCoverage = metrics.OutcomeCoverage,
            FindingConfirmationRate = metrics.ConfirmationRate,
            EvidenceStatus = PolicyEvidenceStatus.Observational,
            EvidenceQuality = metrics.Failures.Count == 0 ? ReviewEvidenceQuality.ObservationalSupport : ReviewEvidenceQuality.InsufficientEvidence,
            GateFailures = metrics.Failures,
            Provenance = Provenance(runs),
        };
    }

    private static ModelReviewQualitySummary Summary(
        string model, IReadOnlyCollection<QualityStudioReviewEvidence> runs, ReviewEvidenceConfidenceGates gates)
    {
        var metrics = Metrics(runs, gates);
        var quality = metrics.Failures.Count == 0
            ? ReviewEvidenceQuality.ObservationalSupport : ReviewEvidenceQuality.InsufficientEvidence;
        return new()
        {
            CanonicalModel = model,
            RunCount = runs.Count,
            FilesReviewed = runs.Sum(run => run.FilesReviewed),
            FindingsReported = metrics.Findings,
            OutcomeAvailableRunCount = runs.Count(run => run.ConfirmedFindings is not null && run.DismissedFindings is not null),
            ConfirmedFindings = metrics.Confirmed,
            DismissedFindings = metrics.Dismissed,
            AssessedFindingCount = metrics.Assessed,
            FindingOutcomeCoverage = metrics.OutcomeCoverage,
            FindingConfirmationRate = metrics.ConfirmationRate,
            ThinkingLevels = runs.Select(run => run.ThinkingLevel!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ReviewAspects = runs.Select(run => run.ReviewAspect).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ObservedThrough = runs.Count == 0 ? null : runs.Max(run => DateOnly.FromDateTime(run.ObservedAtUtc.UtcDateTime)),
            EvidenceStatus = runs.Count == 0 ? PolicyEvidenceStatus.Unknown : PolicyEvidenceStatus.Observational,
            EvidenceQuality = quality,
            Suitability = quality == ReviewEvidenceQuality.ObservationalSupport ? SuitabilityFrom(metrics.ConfirmationRate!.Value, gates) : null,
            GateFailures = metrics.Failures,
            Provenance = Provenance(runs),
        };
    }

    private static MetricsResult Metrics(
        IReadOnlyCollection<QualityStudioReviewEvidence> runs, ReviewEvidenceConfidenceGates gates)
    {
        var findings = runs.Sum(run => run.FindingsReported);
        var confirmed = runs.Sum(run => run.ConfirmedFindings ?? 0);
        var dismissed = runs.Sum(run => run.DismissedFindings ?? 0);
        var assessed = confirmed + dismissed;
        decimal? coverage = findings == 0 ? null : Ratio(assessed, findings);
        decimal? confirmation = assessed == 0 ? null : Ratio(confirmed, assessed);
        var failures = new List<string>();
        if (runs.Count < gates.MinimumRunCount) failures.Add($"run count {runs.Count} is below {gates.MinimumRunCount}");
        if (assessed < gates.MinimumAssessedFindingCount) failures.Add($"assessed finding count {assessed} is below {gates.MinimumAssessedFindingCount}");
        if (coverage is null || coverage < gates.MinimumFindingOutcomeCoverage)
            failures.Add("finding outcome coverage is unavailable or below the declared gate");
        if (confirmation is null) failures.Add("finding confirmation rate is unavailable");
        return new(findings, confirmed, dismissed, assessed, coverage, confirmation, failures);
    }

    private static Suitability SuitabilityFrom(decimal confirmationRate, ReviewEvidenceConfidenceGates gates) => confirmationRate switch
    {
        var value when value >= gates.IdealFindingConfirmationRate => Suitability.Ideal,
        var value when value >= gates.CapableFindingConfirmationRate => Suitability.Capable,
        _ => Suitability.Underpowered,
    };

    private static IReadOnlyList<RoutingEvidenceProvenance> Provenance(IEnumerable<QualityStudioReviewEvidence> runs)
        => runs.Select(run => new RoutingEvidenceProvenance
        {
            ArtifactReference = run.SourceArtifactReference,
            ArtifactSha256 = run.SourceArtifactSha256,
        })
            .DistinctBy(item => (item.ArtifactReference, item.ArtifactSha256))
            .OrderBy(item => item.ArtifactReference, StringComparer.Ordinal)
            .ThenBy(item => item.ArtifactSha256, StringComparer.Ordinal).ToArray();

    private static decimal Ratio(int numerator, int denominator)
        => Math.Round((decimal)numerator / denominator, 6, MidpointRounding.AwayFromZero);

    private static void Validate(ReviewEvidenceConfidenceGates gates)
    {
        if (gates.Version != ReviewEvidenceConfidenceGates.CurrentVersion)
            throw new ArgumentException($"Unsupported review confidence-gate version {gates.Version}.", nameof(gates));
        if (gates.MinimumRunCount < RoutingEvidenceConfidenceGates.PolicyMinimumSampleSize)
            throw new ArgumentException($"Minimum review run count cannot be below the routing-policy floor of {RoutingEvidenceConfidenceGates.PolicyMinimumSampleSize}.", nameof(gates));
        if (gates.MinimumAssessedFindingCount < 1
            || gates.MinimumFindingOutcomeCoverage < ReviewEvidenceConfidenceGates.PolicyMinimumFindingOutcomeCoverage
            || gates.MinimumFindingOutcomeCoverage > 1
            || gates.CapableFindingConfirmationRate is < 0 or > 1
            || gates.IdealFindingConfirmationRate is < 0 or > 1
            || gates.CapableFindingConfirmationRate > gates.IdealFindingConfirmationRate)
            throw new ArgumentException("Review evidence gates are invalid.", nameof(gates));
    }

    private sealed record MetricsResult(
        int Findings, int Confirmed, int Dismissed, int Assessed,
        decimal? OutcomeCoverage, decimal? ConfirmationRate, IReadOnlyList<string> Failures);
}

/// <summary>Imports a drop directory into immutable per-run evidence and regenerates its derived report.</summary>
public sealed class ReviewEvidencePipeline
{
    private static readonly JsonSerializerOptions Json = QualityStudioReviewRunImporter.Options(writeIndented: true);
    private readonly QualityStudioReviewRunImporter _importer;
    private readonly ReviewEvidenceAggregator _aggregator;

    public ReviewEvidencePipeline(ModelRoutingKnowledgeBase? knowledge = null)
    {
        var policy = knowledge ?? ModelRoutingKnowledgeBase.PolicyOnly;
        _importer = new(policy);
        _aggregator = new(policy);
    }

    public ReviewEvidenceReport Run(
        string repositoryRoot,
        string dropDirectory,
        string? outputRoot = null,
        ReviewEvidenceConfidenceGates? gates = null)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        dropDirectory = Path.GetFullPath(dropDirectory);
        if (!Directory.Exists(dropDirectory)) throw new DirectoryNotFoundException($"Quality Studio drop directory was not found: {dropDirectory}");
        outputRoot ??= Path.Combine(repositoryRoot, "results", "routing-evidence", "review");
        var versionRoot = Path.Combine(Path.GetFullPath(outputRoot), "v1");
        var runRoot = Path.Combine(versionRoot, "runs");
        if (IsWithin(versionRoot, dropDirectory))
            throw new ArgumentException("Review evidence output must be outside the Quality Studio drop directory.", nameof(outputRoot));

        var dropArtifacts = Directory.EnumerateFiles(dropDirectory, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();
        foreach (var path in dropArtifacts)
        {
            var relative = Path.GetRelativePath(dropDirectory, path).Replace('\\', '/');
            var imported = _importer.Import(path, $"quality-studio-drop/{relative}");
            WriteAppendOnly(Path.Combine(runRoot, imported.SourceRunId + ".json"), imported);
        }

        var retained = Directory.Exists(runRoot)
            ? Directory.EnumerateFiles(runRoot, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal)
                .Select(LoadRun).ToArray()
            : [];
        var report = _aggregator.Aggregate(retained, gates);
        WriteDerived(Path.Combine(versionRoot, "review-evidence.json"), report);
        return report;
    }

    public static ReviewEvidenceReport LoadReport(string path)
        => JsonSerializer.Deserialize<ReviewEvidenceReport>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidDataException($"Review evidence report contains no data: {path}");

    public static void WriteDerived(string path, ReviewEvidenceReport report)
        => Write(path, JsonSerializer.Serialize(report, Json) + "\n", appendOnly: false);

    private static QualityStudioReviewEvidence LoadRun(string path)
        => JsonSerializer.Deserialize<QualityStudioReviewEvidence>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidDataException($"Normalized review evidence contains no run: {path}");

    private static void WriteAppendOnly(string path, QualityStudioReviewEvidence run)
        => Write(path, JsonSerializer.Serialize(run, Json) + "\n", appendOnly: true);

    private static void Write(string path, string content, bool appendOnly)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal)) return;
            if (appendOnly) throw new InvalidOperationException($"Append-only review evidence already exists with different content: {path}");
        }
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static bool IsWithin(string candidate, string directory)
    {
        var relative = Path.GetRelativePath(directory, candidate);
        return relative == "." || relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}

#pragma warning restore CS1591
