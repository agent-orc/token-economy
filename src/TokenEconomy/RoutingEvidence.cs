using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS1591
namespace TokenEconomy;

[JsonConverter(typeof(JsonStringEnumConverter<RoutingEvidenceSource>))]
public enum RoutingEvidenceSource { ControlledBenchmark, ObservationalHistory }

[JsonConverter(typeof(JsonStringEnumConverter<RoutingQualificationLevel>))]
public enum RoutingQualificationLevel { Unknown, BelowConfidenceGate, ObservationalSupport, Validated }

/// <summary>Versioned correctness gates retained beside every derived qualification.</summary>
public sealed record RoutingEvidenceConfidenceGates
{
    public const int CurrentVersion = 1;
    public const int PolicyMinimumSampleSize = 20;
    public const decimal PolicyMinimumCoverage = .70m;
    public const decimal PolicyMinimumFavorableGradeRate = .70m;
    public const decimal PolicyMaximumSemanticReissueRate = .10m;
    public int Version { get; init; } = CurrentVersion;
    public int MinimumSampleSize { get; init; } = PolicyMinimumSampleSize;
    public decimal MinimumGradeCoverage { get; init; } = PolicyMinimumCoverage;
    public decimal MinimumDurationCoverage { get; init; } = PolicyMinimumCoverage;
    public decimal MinimumTokenCoverage { get; init; } = PolicyMinimumCoverage;
    public decimal MinimumSemanticReissueCoverage { get; init; } = PolicyMinimumCoverage;
    public decimal MinimumFavorableGradeRate { get; init; } = PolicyMinimumFavorableGradeRate;
    public decimal MaximumSemanticReissueRate { get; init; } = PolicyMaximumSemanticReissueRate;
}

public sealed record RoutingEvidenceProvenance
{
    public required string ArtifactReference { get; init; }
    public string? ArtifactSha256 { get; init; }
}

public sealed record RoutingQualification
{
    public required RoutingQualificationLevel Level { get; init; }
    public required bool ClaimsValidation { get; init; }
    public required IReadOnlyList<string> GateFailures { get; init; }
}

/// <summary>A routing cohort grouped only by canonical and explicitly recorded dimensions.</summary>
public sealed record RoutingEvidenceCohort
{
    public string? CanonicalModel { get; init; }
    public string? ThinkingLevel { get; init; }
    public string? PolicyVersion { get; init; }
    public string? TaskClass { get; init; }
    public string? Capability { get; init; }
    public required int SampleSize { get; init; }
    public required int AttemptLevelRouteCount { get; init; }
    public required int CardLevelRouteCount { get; init; }
    public required int UnknownRouteCount { get; init; }
    public int DecisionJoinAvailableCount { get; init; }
    public required int OutcomeAvailableCount { get; init; }
    public required int SuccessCount { get; init; }
    public decimal? SuccessRate { get; init; }
    public required decimal OutcomeCoverage { get; init; }
    public required int GradeAvailableCount { get; init; }
    public required int FavorableGradeCount { get; init; }
    public required decimal GradeCoverage { get; init; }
    public decimal? FavorableGradeRate { get; init; }
    public required int SemanticReissueAvailableCount { get; init; }
    public required int SemanticReissueCount { get; init; }
    public required decimal SemanticReissueCoverage { get; init; }
    public decimal? SemanticReissueRate { get; init; }
    public IReadOnlyDictionary<AgentStudioAttemptOutcomeCategory, int> OutcomeCategoryCounts { get; init; }
        = new Dictionary<AgentStudioAttemptOutcomeCategory, int>();
    public IReadOnlyList<int> OutcomeClassificationVersions { get; init; } = [];
    public required int DurationAvailableCount { get; init; }
    public required decimal DurationCoverage { get; init; }
    public long? TotalDurationMs { get; init; }
    public decimal? AverageDurationMs { get; init; }
    public required int TokenAvailableCount { get; init; }
    public required decimal TokenCoverage { get; init; }
    public long? TotalTokens { get; init; }
    public decimal? AverageTokens { get; init; }
    public required int CostAvailableCount { get; init; }
    public required decimal CostCoverage { get; init; }
    public decimal? TotalCostUsd { get; init; }
    public DateOnly? ObservedFrom { get; init; }
    public DateOnly? ObservedThrough { get; init; }
    public required IReadOnlyList<RoutingEvidenceProvenance> Provenance { get; init; }
    public required RoutingQualification Qualification { get; init; }
}

public sealed record RoutingEvidenceReport
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string EvidenceVersion { get; init; }
    public int OutcomeClassificationVersion { get; init; } = AgentStudioOutcomeClassification.CurrentVersion;
    public required RoutingEvidenceConfidenceGates ConfidenceGates { get; init; }
    public required IReadOnlyList<RoutingEvidenceCohort> ControlledCohorts { get; init; }
    public required IReadOnlyList<RoutingEvidenceCohort> ObservationalCohorts { get; init; }
}

public sealed record ControlledBenchmarkEvidence(BenchmarkRunResult Run, string ArtifactReference, string? ArtifactSha256 = null);
public sealed record DocumentTextRoutingEvidence(DocumentTextBenchmarkResult Run, string ArtifactReference, string? ArtifactSha256 = null);

/// <summary>Pure deterministic aggregation over retained controlled and observational evidence.</summary>
public sealed class RoutingEvidenceAggregator
{
    private readonly ModelPriceCatalog _models;

    public RoutingEvidenceAggregator(ModelPriceCatalog? models = null) => _models = models ?? ModelPriceCatalog.Default;

    public RoutingEvidenceReport Aggregate(
        IEnumerable<ControlledBenchmarkEvidence> benchmarkRuns,
        IEnumerable<DocumentTextRoutingEvidence> documentRuns,
        IEnumerable<AgentStudioRunRecord> observedRuns,
        RoutingEvidenceConfidenceGates? gates = null,
        string evidenceVersion = "routing-evidence-v2")
    {
        ArgumentNullException.ThrowIfNull(benchmarkRuns);
        ArgumentNullException.ThrowIfNull(documentRuns);
        ArgumentNullException.ThrowIfNull(observedRuns);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceVersion);
        gates ??= new();
        Validate(gates);

        var controlled = DeduplicateBenchmarks(benchmarkRuns).SelectMany(ToObservations)
            .Concat(DeduplicateDocuments(documentRuns).SelectMany(ToObservations));
        var observational = DeduplicateObserved(observedRuns).Select(ToObservation);
        return new()
        {
            EvidenceVersion = evidenceVersion,
            ConfidenceGates = gates,
            ControlledCohorts = Build(controlled, RoutingEvidenceSource.ControlledBenchmark, gates),
            ObservationalCohorts = Build(observational, RoutingEvidenceSource.ObservationalHistory, gates),
        };
    }

    private IEnumerable<Observation> ToObservations(ControlledBenchmarkEvidence evidence)
    {
        foreach (var item in evidence.Run.Cases)
            yield return new(
                Canonical(item.Model), Thinking(item.ThinkingLevel), null, TaskClass(evidence.Run.TaskClass), Value(evidence.Run.Capability),
                RoutePrecision.Attempt, true, item.Succeeded, true, item.Succeeded, true, false,
                true, item.DurationMs, true, Total(item.Usage), item.CostUsd,
                AgentStudioAttemptOutcomeCategory.Unknown, null, false, ObservationDate(evidence.Run.CompletedAtUtc), new()
                {
                    ArtifactReference = evidence.ArtifactReference, ArtifactSha256 = evidence.ArtifactSha256,
                });
    }

    private IEnumerable<Observation> ToObservations(DocumentTextRoutingEvidence evidence)
    {
        foreach (var item in evidence.Run.Cases)
            yield return new(
                Canonical(item.Model), null, null, "document-to-text", $"document-to-text/{item.DocumentType.ToString().ToLowerInvariant()}",
                RoutePrecision.Attempt, true, item.Succeeded, true, item.Succeeded, true, false,
                true, item.DurationMs, true, Total(item.Usage), item.CostUsd,
                AgentStudioAttemptOutcomeCategory.Unknown, null, false, ObservationDate(evidence.Run.CompletedAtUtc), new()
                {
                    ArtifactReference = evidence.ArtifactReference, ArtifactSha256 = evidence.ArtifactSha256,
                });
    }

    private Observation ToObservation(AgentStudioRunRecord item)
    {
        var duration = item.StartedAtUtc is { } started && item.ExecutedAtUtc >= started
            ? (long?)(item.ExecutedAtUtc - started).TotalMilliseconds : null;
        var route = item.RouteGranularity switch
        {
            AgentStudioRouteGranularity.Attempt => RoutePrecision.Attempt,
            AgentStudioRouteGranularity.Card => RoutePrecision.Card,
            _ => RoutePrecision.Unknown,
        };
        var gradeKnown = item.Grade is "A" or "B" or "C" or "D";
        var category = item.OutcomeCategory;
        var classifiedModelOutcome = category is AgentStudioAttemptOutcomeCategory.Successful
            or AgentStudioAttemptOutcomeCategory.SemanticFailure
            or AgentStudioAttemptOutcomeCategory.SubstantiveReview;
        var outcomeKnown = classifiedModelOutcome
            || category == AgentStudioAttemptOutcomeCategory.Unknown && item.Outcome != OutcomeQualitySignal.Unknown;
        var succeeded = category == AgentStudioAttemptOutcomeCategory.Successful
            || category == AgentStudioAttemptOutcomeCategory.Unknown && item.Outcome == OutcomeQualitySignal.Successful;
        var reissueKnown = (item.OutcomeClassification is not null
            && category != AgentStudioAttemptOutcomeCategory.Unknown) || item.SemanticReissue is not null;
        var provenance = string.IsNullOrWhiteSpace(item.ProvenanceReference)
            ? new RoutingEvidenceProvenance { ArtifactReference = "unknown" }
            : new RoutingEvidenceProvenance { ArtifactReference = item.ProvenanceReference, ArtifactSha256 = item.ProvenanceSha256 };
        return new(
            Canonical(item.ActualModel), Thinking(item.ActualThinkingLevel), item.RoutingPolicyVersion,
            TaskClass(item.TaskType), Value(item.Capability), route,
            outcomeKnown, succeeded,
            gradeKnown, item.Grade is "A" or "B",
            reissueKnown, item.OutcomeClassification?.IsSemanticFailure ?? item.SemanticReissue == true,
            duration is not null, duration ?? 0, item.TokenUsageAvailable, Total(item.Usage), item.CostEstimate,
            category, item.OutcomeClassification?.Version, item.RoutingDecisionId is not null,
            ObservationDate(item.ObservedAtUtc), provenance);
    }

    private static IReadOnlyList<RoutingEvidenceCohort> Build(
        IEnumerable<Observation> source, RoutingEvidenceSource evidenceSource, RoutingEvidenceConfidenceGates gates)
        => source.GroupBy(item => (item.Model, item.Thinking, item.PolicyVersion, item.TaskClass, item.Capability))
            .Select(group => Cohort(group, evidenceSource, gates))
            .OrderBy(item => item.CanonicalModel, NullLastComparer.Instance)
            .ThenBy(item => item.ThinkingLevel, NullLastComparer.Instance)
            .ThenBy(item => item.PolicyVersion, NullLastComparer.Instance)
            .ThenBy(item => item.TaskClass, NullLastComparer.Instance)
            .ThenBy(item => item.Capability, NullLastComparer.Instance)
            .ToArray();

    private static RoutingEvidenceCohort Cohort(
        IGrouping<(string? Model, string? Thinking, string? PolicyVersion, string? TaskClass, string? Capability), Observation> group,
        RoutingEvidenceSource source,
        RoutingEvidenceConfidenceGates gates)
    {
        var rows = group.ToArray();
        var sample = rows.Length;
        var outcome = rows.Count(row => row.OutcomeAvailable);
        var grades = rows.Count(row => row.GradeAvailable);
        var reissues = rows.Count(row => row.ReissueAvailable);
        var durations = rows.Where(row => row.DurationAvailable).ToArray();
        var tokens = rows.Where(row => row.TokenAvailable).ToArray();
        var costs = rows.Where(row => row.CostUsd is not null).ToArray();
        var dates = rows.Where(row => row.ObservedOn is not null).Select(row => row.ObservedOn!.Value).ToArray();
        var provisional = new RoutingEvidenceCohort
        {
            CanonicalModel = group.Key.Model, ThinkingLevel = group.Key.Thinking, PolicyVersion = group.Key.PolicyVersion,
            TaskClass = group.Key.TaskClass, Capability = group.Key.Capability, SampleSize = sample,
            AttemptLevelRouteCount = rows.Count(row => row.Route == RoutePrecision.Attempt),
            CardLevelRouteCount = rows.Count(row => row.Route == RoutePrecision.Card),
            UnknownRouteCount = rows.Count(row => row.Route == RoutePrecision.Unknown),
            DecisionJoinAvailableCount = rows.Count(row => row.DecisionJoinAvailable),
            OutcomeAvailableCount = outcome, SuccessCount = rows.Count(row => row.OutcomeAvailable && row.Succeeded),
            SuccessRate = outcome == 0 ? null : Ratio(rows.Count(row => row.OutcomeAvailable && row.Succeeded), outcome),
            OutcomeCoverage = Ratio(outcome, sample), GradeAvailableCount = grades,
            FavorableGradeCount = rows.Count(row => row.GradeAvailable && row.FavorableGrade),
            GradeCoverage = Ratio(grades, sample),
            FavorableGradeRate = grades == 0 ? null : Ratio(rows.Count(row => row.GradeAvailable && row.FavorableGrade), grades),
            SemanticReissueAvailableCount = reissues,
            SemanticReissueCount = rows.Count(row => row.ReissueAvailable && row.SemanticReissue),
            SemanticReissueCoverage = Ratio(reissues, sample),
            SemanticReissueRate = reissues == 0 ? null : Ratio(rows.Count(row => row.ReissueAvailable && row.SemanticReissue), reissues),
            OutcomeCategoryCounts = Enum.GetValues<AgentStudioAttemptOutcomeCategory>()
                .ToDictionary(category => category, category => rows.Count(row => row.Category == category)),
            OutcomeClassificationVersions = rows.Where(row => row.ClassificationVersion is not null)
                .Select(row => row.ClassificationVersion!.Value).Distinct().Order().ToArray(),
            DurationAvailableCount = durations.Length, DurationCoverage = Ratio(durations.Length, sample),
            TotalDurationMs = durations.Length == 0 ? null : durations.Sum(row => row.DurationMs),
            AverageDurationMs = durations.Length == 0 ? null : DecimalAverage(durations.Select(row => row.DurationMs)),
            TokenAvailableCount = tokens.Length, TokenCoverage = Ratio(tokens.Length, sample),
            TotalTokens = tokens.Length == 0 ? null : tokens.Sum(row => row.Tokens),
            AverageTokens = tokens.Length == 0 ? null : DecimalAverage(tokens.Select(row => row.Tokens)),
            CostAvailableCount = costs.Length, CostCoverage = Ratio(costs.Length, sample),
            TotalCostUsd = costs.Length == 0 ? null : costs.Sum(row => row.CostUsd!.Value),
            ObservedFrom = dates.Length == 0 ? null : dates.Min(), ObservedThrough = dates.Length == 0 ? null : dates.Max(),
            Provenance = rows.Select(row => row.Provenance)
                .DistinctBy(item => (item.ArtifactReference, item.ArtifactSha256))
                .OrderBy(item => item.ArtifactReference, StringComparer.Ordinal)
                .ThenBy(item => item.ArtifactSha256, StringComparer.Ordinal).ToArray(),
            Qualification = new() { Level = RoutingQualificationLevel.Unknown, ClaimsValidation = false, GateFailures = [] },
        };
        return provisional with { Qualification = Qualify(provisional, source, gates) };
    }

    private static RoutingQualification Qualify(
        RoutingEvidenceCohort cohort, RoutingEvidenceSource source, RoutingEvidenceConfidenceGates gates)
    {
        var failures = new List<string>();
        if (cohort.CanonicalModel is null) failures.Add("canonical model is unknown");
        if (cohort.ThinkingLevel is null) failures.Add("thinking level is unknown");
        if (cohort.TaskClass is null) failures.Add("task class is unknown");
        if (cohort.Capability is null) failures.Add("capability is unknown");
        if (cohort.ObservedThrough is null) failures.Add("observation date is unknown");
        if (cohort.Provenance.Count == 0 || cohort.Provenance.Any(item => item.ArtifactReference == "unknown")) failures.Add("provenance is unknown");
        if (cohort.SampleSize < gates.MinimumSampleSize) failures.Add($"sample size {cohort.SampleSize} is below {gates.MinimumSampleSize}");
        if (cohort.GradeCoverage < gates.MinimumGradeCoverage) failures.Add($"grade coverage {cohort.GradeCoverage:0.###} is below {gates.MinimumGradeCoverage:0.###}");
        if (cohort.DurationCoverage < gates.MinimumDurationCoverage) failures.Add($"duration coverage {cohort.DurationCoverage:0.###} is below {gates.MinimumDurationCoverage:0.###}");
        if (cohort.TokenCoverage < gates.MinimumTokenCoverage) failures.Add($"token coverage {cohort.TokenCoverage:0.###} is below {gates.MinimumTokenCoverage:0.###}");
        if (cohort.SemanticReissueCoverage < gates.MinimumSemanticReissueCoverage) failures.Add($"semantic reissue coverage {cohort.SemanticReissueCoverage:0.###} is below {gates.MinimumSemanticReissueCoverage:0.###}");
        if (cohort.FavorableGradeRate is null || cohort.FavorableGradeRate < gates.MinimumFavorableGradeRate) failures.Add("favorable grade rate is unavailable or below the declared gate");
        if (cohort.SemanticReissueRate is null || cohort.SemanticReissueRate > gates.MaximumSemanticReissueRate) failures.Add("semantic reissue rate is unavailable or above the declared gate");

        if (failures.Any(failure => failure.Contains("unknown", StringComparison.Ordinal)))
            return new() { Level = RoutingQualificationLevel.Unknown, ClaimsValidation = false, GateFailures = failures };
        if (failures.Count > 0)
            return new() { Level = RoutingQualificationLevel.BelowConfidenceGate, ClaimsValidation = false, GateFailures = failures };
        var level = source == RoutingEvidenceSource.ControlledBenchmark
            ? RoutingQualificationLevel.Validated : RoutingQualificationLevel.ObservationalSupport;
        return new() { Level = level, ClaimsValidation = level == RoutingQualificationLevel.Validated, GateFailures = [] };
    }

    private string? Canonical(string? model) => _models.Find(model)?.ModelId;
    private static string? Thinking(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "minimal" => "minimal", "low" => "low", "medium" => "medium", "high" => "high",
        "xhigh" => "xhigh", "extra-high" => "xhigh", "ultra" => "ultra", _ => null,
    };
    private static string? TaskClass(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "feature" or "bug" or "bugfix" or "bug-fix" => "feature",
        "chore" or "mechanical" or "mechanical-chore" => "mechanical-chore",
        "doc" or "docs" or "documentation" or "doc-edit" => "doc-edit",
        "research" or "analysis" or "investigation" => "research",
        "design" or "architecture" or "heavy-design" => "heavy-design",
        "document-to-text" => "document-to-text",
        _ => null,
    };
    private static string? Value(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static long Total(TokenUsage usage) => Math.Max(0, usage.Input) + Math.Max(0, usage.Output) + Math.Max(0, usage.CacheRead) + Math.Max(0, usage.CacheWrite);
    private static DateOnly? ObservationDate(DateTime value) => value == DateTime.UnixEpoch ? null : DateOnly.FromDateTime(value.ToUniversalTime());
    private static decimal Ratio(int numerator, int denominator) => denominator == 0 ? 0 : Math.Round((decimal)numerator / denominator, 6, MidpointRounding.AwayFromZero);
    private static decimal DecimalAverage(IEnumerable<long> values)
    {
        var materialized = values.ToArray();
        return Math.Round((decimal)materialized.Sum() / materialized.Length, 3, MidpointRounding.AwayFromZero);
    }

    private static IEnumerable<ControlledBenchmarkEvidence> DeduplicateBenchmarks(IEnumerable<ControlledBenchmarkEvidence> runs)
        => runs.GroupBy(item => (item.Run.SetupId, item.Run.RunId))
            .Select(group => group.OrderBy(item => item.ArtifactReference, StringComparer.Ordinal).First())
            .OrderBy(item => item.Run.SetupId, StringComparer.Ordinal).ThenBy(item => item.Run.RunId, StringComparer.Ordinal);

    private static IEnumerable<DocumentTextRoutingEvidence> DeduplicateDocuments(IEnumerable<DocumentTextRoutingEvidence> runs)
        => runs.GroupBy(item => (item.Run.CorpusId, item.Run.RunId))
            .Select(group => group.OrderBy(item => item.ArtifactReference, StringComparer.Ordinal).First())
            .OrderBy(item => item.Run.CorpusId, StringComparer.Ordinal).ThenBy(item => item.Run.RunId, StringComparer.Ordinal);

    private static IEnumerable<AgentStudioRunRecord> DeduplicateObserved(IEnumerable<AgentStudioRunRecord> runs)
        => runs.GroupBy(item => (item.TaskKey, item.Run))
            .Select(MergeObserved)
            .OrderBy(item => item.TaskKey, StringComparer.Ordinal).ThenBy(item => item.Run);

    private static AgentStudioRunRecord MergeObserved(IGrouping<(string TaskKey, int Run), AgentStudioRunRecord> group)
    {
        var ordered = group.OrderByDescending(item => item.ObservedAtUtc)
            .ThenBy(item => item.ProvenanceReference, StringComparer.Ordinal).ToArray();
        var newest = ordered[0];
        var peers = ordered.Where(item => item.ObservedAtUtc == newest.ObservedAtUtc).ToArray();
        return peers.Select(item => (item.Model, item.ThinkingLevel)).Distinct().Count() <= 1
            ? newest
            : newest with
            {
                Model = null, ThinkingLevel = null, Provider = null,
                RouteGranularity = AgentStudioRouteGranularity.Unknown,
                CostEstimate = null, Currency = null, CostCaveat = null, CostStatus = PriceStatus.UnknownModel,
                RoutingDecision = newest.RoutingDecision is { } decision
                    ? decision with { SelectedModel = null, SelectedThinkingLevel = null } : null,
                OutcomeObservation = newest.OutcomeObservation is { } observation
                    ? observation with { ActualModel = null, ActualThinkingLevel = null, CostEstimate = null, CostStatus = PriceStatus.UnknownModel } : null,
            };
    }

    private static void Validate(RoutingEvidenceConfidenceGates gates)
    {
        if (gates.Version != RoutingEvidenceConfidenceGates.CurrentVersion) throw new ArgumentException($"Unsupported confidence-gate version {gates.Version}.", nameof(gates));
        if (gates.MinimumSampleSize < RoutingEvidenceConfidenceGates.PolicyMinimumSampleSize)
            throw new ArgumentException($"Minimum sample size cannot be below the routing-policy floor of {RoutingEvidenceConfidenceGates.PolicyMinimumSampleSize}.", nameof(gates));
        foreach (var value in new[] { gates.MinimumGradeCoverage, gates.MinimumDurationCoverage, gates.MinimumTokenCoverage,
                     gates.MinimumSemanticReissueCoverage, gates.MinimumFavorableGradeRate, gates.MaximumSemanticReissueRate })
            if (value is < 0 or > 1) throw new ArgumentException("Confidence rates must be between zero and one.", nameof(gates));
        if (gates.MinimumGradeCoverage < RoutingEvidenceConfidenceGates.PolicyMinimumCoverage
            || gates.MinimumDurationCoverage < RoutingEvidenceConfidenceGates.PolicyMinimumCoverage
            || gates.MinimumTokenCoverage < RoutingEvidenceConfidenceGates.PolicyMinimumCoverage
            || gates.MinimumSemanticReissueCoverage < RoutingEvidenceConfidenceGates.PolicyMinimumCoverage
            || gates.MinimumFavorableGradeRate < RoutingEvidenceConfidenceGates.PolicyMinimumFavorableGradeRate
            || gates.MaximumSemanticReissueRate > RoutingEvidenceConfidenceGates.PolicyMaximumSemanticReissueRate)
            throw new ArgumentException("Confidence gates cannot weaken the routing-policy correctness floors.", nameof(gates));
    }

    private enum RoutePrecision { Unknown, Card, Attempt }
    private sealed record Observation(
        string? Model, string? Thinking, string? PolicyVersion, string? TaskClass, string? Capability, RoutePrecision Route,
        bool OutcomeAvailable, bool Succeeded, bool GradeAvailable, bool FavorableGrade,
        bool ReissueAvailable, bool SemanticReissue, bool DurationAvailable, long DurationMs,
        bool TokenAvailable, long Tokens, decimal? CostUsd, AgentStudioAttemptOutcomeCategory Category,
        int? ClassificationVersion, bool DecisionJoinAvailable, DateOnly? ObservedOn, RoutingEvidenceProvenance Provenance);

    private sealed class NullLastComparer : IComparer<string?>
    {
        public static NullLastComparer Instance { get; } = new();
        public int Compare(string? left, string? right) => left is null ? right is null ? 0 : 1
            : right is null ? -1 : StringComparer.Ordinal.Compare(left, right);
    }
}

/// <summary>Loads immutable raw artifacts and regenerates the versioned derived routing report.</summary>
public sealed class RoutingEvidencePipeline
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly RoutingEvidenceAggregator _aggregator;
    private readonly AgentStudioTaskStorageImporter _importer;

    public RoutingEvidencePipeline(ModelPriceCatalog? models = null)
    {
        var catalog = models ?? ModelPriceCatalog.Default;
        _aggregator = new(catalog);
        _importer = new(catalog);
    }

    public RoutingEvidenceReport Run(
        string repositoryRoot,
        string agentStudioStorageDirectory,
        string? outputPath = null,
        RoutingEvidenceConfidenceGates? gates = null)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var benchmarks = new List<ControlledBenchmarkEvidence>();
        var documents = new List<DocumentTextRoutingEvidence>();
        LoadControlled(repositoryRoot, benchmarks, documents);
        var store = new InMemoryAgentStudioRunStore();
        _importer.ImportDirectory(agentStudioStorageDirectory, store);
        var report = _aggregator.Aggregate(benchmarks, documents, store.Records, gates);
        outputPath ??= Path.Combine(repositoryRoot, "results", "routing-evidence", "v2", "routing-evidence.json");
        WriteDerived(outputPath, report);
        return report;
    }

    public static void WriteDerived(string path, RoutingEvidenceReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(report);
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = JsonSerializer.Serialize(report, Json) + "\n";
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal)) return;
        File.WriteAllText(path, content);
    }

    private static void LoadControlled(
        string repositoryRoot,
        ICollection<ControlledBenchmarkEvidence> benchmarks,
        ICollection<DocumentTextRoutingEvidence> documents)
    {
        var root = Path.Combine(repositoryRoot, "benchmarks", "results");
        if (!Directory.Exists(root)) return;
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (path.EndsWith(".report.json", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".capabilities.json", StringComparison.OrdinalIgnoreCase)) continue;
            var bytes = File.ReadAllBytes(path);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            using var document = JsonDocument.Parse(bytes);
            var value = document.RootElement;
            var reference = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            if (value.TryGetProperty("setupId", out _) && value.TryGetProperty("runId", out _) && value.TryGetProperty("cases", out _))
            {
                var run = JsonSerializer.Deserialize<BenchmarkRunResult>(value.GetRawText(), Json)
                    ?? throw new InvalidDataException($"Could not deserialize controlled benchmark result: {path}");
                benchmarks.Add(new(run, reference, hash));
            }
            else if (value.TryGetProperty("corpusId", out _) && value.TryGetProperty("models", out _) && value.TryGetProperty("cases", out _))
            {
                var run = JsonSerializer.Deserialize<DocumentTextBenchmarkResult>(value.GetRawText(), Json)
                    ?? throw new InvalidDataException($"Could not deserialize document benchmark result: {path}");
                documents.Add(new(run, reference, hash));
            }
        }
    }
}
