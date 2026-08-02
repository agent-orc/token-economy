using System.Diagnostics;
using System.Text.RegularExpressions;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Routing-oriented complexity bands. They describe expected work, not task importance.</summary>
public enum TaskComplexityLevel { Trivial, Standard, Demanding, Critical }

/// <summary>Optional measurable repository/card facts. Values are normalized to 0..1.</summary>
public sealed record ComplexitySignals
{
    public double? Novelty { get; init; }
    public double? ConstraintDensity { get; init; }
    public double? SpecificationAmbiguity { get; init; }
    public double? VerificationCost { get; init; }
    public double? RequiredReading { get; init; }
}

/// <summary>An explicit, evidenced pre-launch score for one authoritative routing criterion.</summary>
public sealed record ComplexityCriterionOverride(int Score, string Evidence);

/// <summary>
/// Optional scored intake facts. Overrides are useful when a host already ran a structured intake
/// rubric; every override must carry the pre-launch evidence that justified it.
/// </summary>
public sealed record ComplexityRoutingOverrides
{
    public ComplexityCriterionOverride? CorrectnessRisk { get; init; }
    public ComplexityCriterionOverride? ExpectedScope { get; init; }
    public ComplexityCriterionOverride? ContextDemand { get; init; }
    public ComplexityCriterionOverride? TaskTypeAndUncertainty { get; init; }
    public ComplexityCriterionOverride? EmpiricalConfidence { get; init; }
    public ComplexityCriterionOverride? QuotaAndCostHeadroom { get; init; }
}

/// <summary>The information available before an agent run starts.</summary>
public sealed record ComplexityCard
{
    public required string TaskKey { get; init; }
    public required string Prompt { get; init; }
    public string? Project { get; init; }
    public string? Area { get; init; }
    public string? TaskType { get; init; }
    public string? EpicContext { get; init; }
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];
    public IReadOnlyList<string> ReferencedFiles { get; init; } = [];
    public IReadOnlyList<string> ReferencedSubsystems { get; init; } = [];
    /// <summary>Files expected at intake to change. Never populate this from the eventual diff.</summary>
    public IReadOnlyList<string> ExpectedChangedFiles { get; init; } = [];
    /// <summary>Runtime subsystems expected at intake to change. Never populate this from the eventual diff.</summary>
    public IReadOnlyList<string> ExpectedRuntimeSubsystems { get; init; } = [];
    public int? ExpectedChangedLines { get; init; }
    public int? DependencyFanOut { get; init; }
    /// <summary>Total repository files. This deliberately has only a small, indirect influence.</summary>
    public int? RepositoryFileCount { get; init; }
    /// <summary>Known trigger ids from the authoritative policy, recorded before launch.</summary>
    public IReadOnlyList<string> HardFloorTriggers { get; init; } = [];
    /// <summary>Run-scoped 0..5 policy points. Null keeps missing quota evidence visible.</summary>
    public int? QuotaAndCostHeadroom { get; init; }
    public ComplexityRoutingOverrides RoutingOverrides { get; init; } = new();
    public ComplexitySignals Signals { get; init; } = new();
}

/// <summary>An already observed task used for calibration and nearest-neighbour inheritance.</summary>
public sealed record ComplexityHistorySample
{
    public required ComplexityCard Card { get; init; }
    public required long ActualTokens { get; init; }
    public required TimeSpan ActualDuration { get; init; }
    public required int ReissueCount { get; init; }
    public bool TokenUsageAvailable { get; init; } = true;
    public bool DurationAvailable { get; init; } = true;
    public bool SemanticReissueEvidenceAvailable { get; init; } = true;
    public string? FinalGrade { get; init; }
}

/// <summary>Optional result of a cheap rubric call. The caller owns the provider invocation.</summary>
public sealed record LlmComplexityAssessment(double Score, double Confidence, string? RubricVersion = null);

public sealed record ComplexityDimension(string Name, double Score, double Weight, string Evidence);
public sealed record ComplexityNeighbour(string TaskKey, double Similarity, long ActualTokens, int ReissueCount)
{
    public bool TokenUsageAvailable { get; init; } = true;
    public bool DurationAvailable { get; init; } = true;
    public bool SemanticReissueEvidenceAvailable { get; init; } = true;
    public string? FinalGrade { get; init; }
}
public sealed record ComplexityRoutingFeature(string Name, int Score, int MaximumScore, string Evidence);
public sealed record LongForecastRange(long Lower, long Upper);
public sealed record DurationForecastRange(TimeSpan Lower, TimeSpan Upper);
public sealed record DoubleForecastRange(double Lower, double Upper);
public enum ComplexityHistoryEvidenceStatus { Missing, LowConfidence, FavorableSmallCohort, Sufficient, Unfavorable }
public sealed record TaskComplexityEstimationEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>Serializable, per-card routing input. SchemaVersion allows durable stores to evolve safely.</summary>
public sealed record TaskComplexityEstimate
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string TaskKey { get; init; }
    public required TaskComplexityLevel Level { get; init; }
    public required double Score { get; init; }
    public required double Confidence { get; init; }
    public required long PredictedTokens { get; init; }
    public required TimeSpan PredictedDuration { get; init; }
    public required double PredictedReissues { get; init; }
    public required LongForecastRange PredictedTokenRange { get; init; }
    public required DurationForecastRange PredictedDurationRange { get; init; }
    public required DoubleForecastRange PredictedReissueRange { get; init; }
    public required IReadOnlyList<ComplexityDimension> Dimensions { get; init; }
    public required IReadOnlyList<ComplexityNeighbour> Neighbours { get; init; }
    public required ComplexityRoutingFeature CorrectnessRisk { get; init; }
    public required ComplexityRoutingFeature ExpectedScope { get; init; }
    public required ComplexityRoutingFeature ContextDemand { get; init; }
    public required ComplexityRoutingFeature TaskUncertainty { get; init; }
    public required ComplexityRoutingFeature EmpiricalConfidence { get; init; }
    public required ComplexityRoutingFeature QuotaAndCostHeadroom { get; init; }
    public required IReadOnlyList<ComplexityRoutingFeature> RoutingFeatures { get; init; }
    public required ComplexityHistoryEvidenceStatus HistoryEvidenceStatus { get; init; }
    public required string HistoryEvidence { get; init; }
    public required IReadOnlyList<string> HardFloorTriggers { get; init; }
    public required IReadOnlyList<string> AppliedHardFloors { get; init; }
    public required string RecommendedRouteId { get; init; }
    public TaskComplexityLevel ComplexityBand => Level;
    public string? LlmRubricVersion { get; init; }
}

public interface ITaskComplexityEstimateStore
{
    void Upsert(TaskComplexityEstimate estimate);
    IReadOnlyCollection<TaskComplexityEstimate> Estimates { get; }
}

public sealed class InMemoryTaskComplexityEstimateStore : ITaskComplexityEstimateStore
{
    private readonly Dictionary<string, TaskComplexityEstimate> _estimates = new(StringComparer.Ordinal);
    public IReadOnlyCollection<TaskComplexityEstimate> Estimates => _estimates.Values;
    public void Upsert(TaskComplexityEstimate estimate) => _estimates[estimate.TaskKey] = estimate;
}

/// <summary>
/// Dependency-free upfront estimator. Policy routing features use only intake facts; sufficiently
/// similar historical tasks calibrate forecast points/ranges and empirical confidence.
/// </summary>
public sealed partial class TaskComplexityEstimator
{
    private const double MinNeighbourSimilarity = .41;
    public event Action<TaskComplexityEstimationEvent>? EventOccurred;

    public TaskComplexityEstimate Estimate(
        ComplexityCard card,
        IEnumerable<ComplexityHistorySample>? history = null,
        LlmComplexityAssessment? llmAssessment = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(card.TaskKey);
        ArgumentNullException.ThrowIfNull(card.Prompt);
        var timer = Stopwatch.StartNew();

        var dimensions = Dimensions(card);
        // Exclude the evaluated key before similarity, cohort scoring, or forecast aggregation. This
        // also protects callers that accidentally supply several attempts/samples for that card.
        var candidates = (history ?? [])
            .Where(h => !string.Equals(h.Card.TaskKey, card.TaskKey, StringComparison.Ordinal))
            .Select(h => (Sample: h, Similarity: Similarity(card, h.Card)))
            .Where(n => n.Similarity >= MinNeighbourSimilarity)
            .OrderByDescending(n => n.Similarity)
            .ThenBy(n => n.Sample.Card.TaskKey, StringComparer.Ordinal)
            .ToArray();
        var neighbours = candidates.Take(5).ToArray();
        var empirical = EmpiricalFeature(candidates);
        var hardFloorTriggers = HardFloorTriggers(card);
        var routingFeatures = RoutingFeatures(card, hardFloorTriggers, empirical.Feature);
        var score = routingFeatures.Sum(feature => feature.Score);
        var scorecard = new ModelRoutingScorecard
        {
            CorrectnessRisk = routingFeatures[0].Score,
            ExpectedScope = routingFeatures[1].Score,
            ContextDemand = routingFeatures[2].Score,
            TaskTypeAndUncertainty = routingFeatures[3].Score,
            EmpiricalConfidence = routingFeatures[4].Score,
            QuotaAndCostHeadroom = routingFeatures[5].Score,
        };
        var routingDecision = ModelRoutingPolicy.Default.RecommendCore(new()
        {
            Scorecard = scorecard,
            CorrectnessTriggers = hardFloorTriggers,
        });

        var tokenNeighbours = neighbours.Where(n => n.Sample.TokenUsageAvailable).ToArray();
        var durationNeighbours = neighbours.Where(n => n.Sample.DurationAvailable).ToArray();
        var predictedTokens = tokenNeighbours.Length == 0
            ? TokensFromScore(score)
            : (long)Math.Round(WeightedAverage(tokenNeighbours, n => n.Sample.ActualTokens));
        var predictedDuration = durationNeighbours.Length == 0
            ? TimeSpan.FromMinutes(5 * Math.Pow(1.04, score))
            : TimeSpan.FromTicks((long)Math.Round(WeightedAverage(durationNeighbours, n => n.Sample.ActualDuration.Ticks)));
        var predictedReissues = neighbours.Length == 0
            ? Math.Max(0, (score - 35) / 35)
            : WeightedAverage(neighbours, n => n.Sample.ReissueCount);

        var measurable = new[]
        {
            card.Signals.Novelty, card.Signals.ConstraintDensity, card.Signals.SpecificationAmbiguity,
            card.Signals.VerificationCost, card.Signals.RequiredReading,
        }.Count(v => v is not null);
        var intakeFacts = new object?[]
        {
            card.ExpectedChangedLines, card.ExpectedChangedFiles.Count > 0 ? card.ExpectedChangedFiles : null,
            card.ExpectedRuntimeSubsystems.Count > 0 ? card.ExpectedRuntimeSubsystems : null,
            card.DependencyFanOut, card.QuotaAndCostHeadroom,
        }.Count(value => value is not null);
        var confidence = .34 + measurable * .035 + intakeFacts * .035 + (empirical.Status switch
        {
            ComplexityHistoryEvidenceStatus.Sufficient => .2,
            ComplexityHistoryEvidenceStatus.FavorableSmallCohort => .13,
            ComplexityHistoryEvidenceStatus.LowConfidence => .05,
            ComplexityHistoryEvidenceStatus.Unfavorable => .08,
            _ => 0,
        });
        if (llmAssessment is not null)
        {
            var agreement = 1 - Math.Abs(Clamp(llmAssessment.Score, 0, 100) - score) / 100;
            confidence += .1 * Clamp01(llmAssessment.Confidence) * agreement;
        }
        confidence = Round(Clamp01(confidence));
        predictedTokens = Math.Max(1, predictedTokens);
        predictedDuration = predictedDuration <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : predictedDuration;
        predictedReissues = Round(Math.Max(0, predictedReissues));
        var tokenRange = TokenRange(predictedTokens, confidence);
        var durationRange = DurationRange(predictedDuration, confidence);
        var reissueRange = ReissueRange(predictedReissues, confidence);

        var result = new TaskComplexityEstimate
        {
            TaskKey = card.TaskKey,
            Level = Level(score),
            Score = score,
            Confidence = confidence,
            PredictedTokens = predictedTokens,
            PredictedDuration = predictedDuration,
            PredictedReissues = predictedReissues,
            PredictedTokenRange = tokenRange,
            PredictedDurationRange = durationRange,
            PredictedReissueRange = reissueRange,
            Dimensions = dimensions,
            Neighbours = neighbours.Select(n => new ComplexityNeighbour(
                n.Sample.Card.TaskKey, Round(n.Similarity), n.Sample.ActualTokens, n.Sample.ReissueCount)
            {
                TokenUsageAvailable = n.Sample.TokenUsageAvailable,
                DurationAvailable = n.Sample.DurationAvailable,
                SemanticReissueEvidenceAvailable = n.Sample.SemanticReissueEvidenceAvailable,
                FinalGrade = n.Sample.FinalGrade,
            }).ToArray(),
            CorrectnessRisk = routingFeatures[0], ExpectedScope = routingFeatures[1], ContextDemand = routingFeatures[2],
            TaskUncertainty = routingFeatures[3], EmpiricalConfidence = routingFeatures[4], QuotaAndCostHeadroom = routingFeatures[5],
            RoutingFeatures = routingFeatures, HistoryEvidenceStatus = empirical.Status, HistoryEvidence = empirical.Evidence,
            HardFloorTriggers = hardFloorTriggers, AppliedHardFloors = routingDecision.AppliedHardFloors,
            RecommendedRouteId = routingDecision.Route.Id,
            LlmRubricVersion = llmAssessment?.RubricVersion,
        };
        timer.Stop();
        EventOccurred?.Invoke(new("task_complexity.estimated", new Dictionary<string, object?>
        {
            ["taskKey"] = card.TaskKey, ["level"] = result.Level.ToString().ToLowerInvariant(),
            ["score"] = result.Score, ["confidence"] = result.Confidence,
            ["recommendedRouteId"] = result.RecommendedRouteId,
            ["hardFloorTriggers"] = result.HardFloorTriggers,
            ["historyEvidenceStatus"] = result.HistoryEvidenceStatus.ToString(),
            ["neighbourCount"] = result.Neighbours.Count, ["usedLlmAssessment"] = llmAssessment is not null,
            ["elapsedMs"] = timer.Elapsed.TotalMilliseconds,
        }));
        return result;
    }

    private sealed record EmpiricalResult(
        ComplexityRoutingFeature Feature,
        ComplexityHistoryEvidenceStatus Status,
        string Evidence);

    private static EmpiricalResult EmpiricalFeature(
        (ComplexityHistorySample Sample, double Similarity)[] candidates)
    {
        var count = candidates.Length;
        if (count == 0)
        {
            const string missing = "No comparable historical cohort met the similarity threshold; policy assigns 10 uncertainty points.";
            return new(Feature("empirical_confidence", 10, 10, missing), ComplexityHistoryEvidenceStatus.Missing, missing);
        }

        var knownGrades = candidates.Where(candidate => candidate.Sample.FinalGrade is not null).ToArray();
        var favorableGrades = knownGrades.Count(candidate => candidate.Sample.FinalGrade is "A" or "B");
        var gradeCoverage = (double)knownGrades.Length / count;
        var favorableRate = knownGrades.Length == 0 ? (double?)null : (double)favorableGrades / knownGrades.Length;
        var semanticKnown = candidates.Where(candidate => candidate.Sample.SemanticReissueEvidenceAvailable).ToArray();
        var semanticCoverage = (double)semanticKnown.Length / count;
        var reissueRate = semanticKnown.Length == 0 ? (double?)null
            : (double)semanticKnown.Count(candidate => candidate.Sample.ReissueCount > 0) / semanticKnown.Length;
        var favorableRuns = candidates.Count(candidate => candidate.Sample.FinalGrade is "A" or "B"
            && candidate.Sample.SemanticReissueEvidenceAvailable && candidate.Sample.ReissueCount == 0);
        var unfavorable = candidates.Any(candidate => candidate.Sample.ReissueCount >= 2)
            || favorableRate is < .7 || reissueRate is >= .25;
        var evidence = $"{count} comparable task(s); grade coverage {gradeCoverage:P0}; "
            + $"A/B among known grades {(favorableRate is null ? "unknown" : favorableRate.Value.ToString("P0"))}; "
            + $"semantic-reissue coverage {semanticCoverage:P0}; reissue rate {(reissueRate is null ? "unknown" : reissueRate.Value.ToString("P0"))}.";

        if (unfavorable)
            return new(Feature("empirical_confidence", 10, 10, evidence + " Repeated reissues or an unfavorable cohort assigns 10 points."),
                ComplexityHistoryEvidenceStatus.Unfavorable, evidence);
        if (count >= 20 && gradeCoverage >= .5 && favorableRate >= .7 && semanticCoverage >= .5 && reissueRate < .1)
            return new(Feature("empirical_confidence", 0, 10, evidence + " The policy's sufficient favorable-cohort gate is met."),
                ComplexityHistoryEvidenceStatus.Sufficient, evidence);
        if (favorableRuns >= 5)
            return new(Feature("empirical_confidence", 3, 10, evidence + " At least five favorable comparable runs assign 3 points."),
                ComplexityHistoryEvidenceStatus.FavorableSmallCohort, evidence);
        return new(Feature("empirical_confidence", 6, 10, evidence + " Sparse, mixed, or incompletely observed evidence assigns 6 points."),
            ComplexityHistoryEvidenceStatus.LowConfidence, evidence);
    }

    private static IReadOnlyList<ComplexityRoutingFeature> RoutingFeatures(
        ComplexityCard card,
        IReadOnlyList<string> hardFloorTriggers,
        ComplexityRoutingFeature empirical)
        =>
        [
            Criterion(card.RoutingOverrides.CorrectnessRisk, "correctness_risk", 35, () => CorrectnessRisk(card, hardFloorTriggers)),
            Criterion(card.RoutingOverrides.ExpectedScope, "expected_scope", 20, () => ExpectedScope(card)),
            Criterion(card.RoutingOverrides.ContextDemand, "context_demand", 20, () => ContextDemand(card)),
            Criterion(card.RoutingOverrides.TaskTypeAndUncertainty, "task_uncertainty", 10, () => TaskUncertainty(card, hardFloorTriggers)),
            Criterion(card.RoutingOverrides.EmpiricalConfidence, "empirical_confidence", 10,
                () => (empirical.Score, empirical.Evidence)),
            Criterion(card.RoutingOverrides.QuotaAndCostHeadroom, "quota_and_cost_headroom", 5, () => QuotaHeadroom(card)),
        ];

    private static ComplexityRoutingFeature Criterion(
        ComplexityCriterionOverride? scoreOverride,
        string name,
        int maximum,
        Func<(int Score, string Evidence)> derive)
    {
        if (scoreOverride is not null)
        {
            if (scoreOverride.Score < 0 || scoreOverride.Score > maximum)
                throw new ArgumentOutOfRangeException(name, scoreOverride.Score, $"Policy points must be between 0 and {maximum}.");
            if (string.IsNullOrWhiteSpace(scoreOverride.Evidence))
                throw new ArgumentException($"An explicit {name} score requires evidence.", name);
            return Feature(name, scoreOverride.Score, maximum, "Explicit pre-launch assessment: " + scoreOverride.Evidence.Trim());
        }
        var derived = derive();
        return Feature(name, derived.Score, maximum, derived.Evidence);
    }

    private static ComplexityRoutingFeature Feature(string name, int score, int maximum, string evidence)
        => new(name, score, maximum, evidence);

    private static (int Score, string Evidence) CorrectnessRisk(ComplexityCard card, IReadOnlyList<string> triggers)
    {
        var critical = new[] { "p0", "fencing", "leaseOwnership", "staleWriteRejection", "distributedAuthority", "securityBoundary", "credibleDataLoss" };
        if (triggers.Intersect(critical, StringComparer.Ordinal).Any())
            return (35, "A correctness-critical hard-floor trigger is present: " + string.Join(", ", triggers.Intersect(critical, StringComparer.Ordinal)) + ".");
        var consequential = new[] { "publicProtocol", "persistentStateMigration", "unclearBug" };
        if (triggers.Intersect(consequential, StringComparer.Ordinal).Any())
            return (24, "Persistent state, a public contract, or an unclear bug is expected: " + string.Join(", ", triggers.Intersect(consequential, StringComparer.Ordinal)) + ".");
        if (Is(card.TaskType, "feature", "bug", "fix") || Contains(card.Prompt, "behavior", "implement", "change"))
            return (12, "The intake describes reversible behavior and has a verification path; no consequential correctness trigger is present.");
        return (0, "The intake describes prose, formatting, copy, or another non-behavioral local edit.");
    }

    private static (int Score, string Evidence) ExpectedScope(ComplexityCard card)
    {
        var expectedFiles = card.ExpectedChangedFiles.Count;
        var expectedSubsystems = card.ExpectedRuntimeSubsystems.Count;
        var lines = card.ExpectedChangedLines;
        var score = lines > 500 || expectedSubsystems >= 4 ? 20
            : lines > 200 || expectedSubsystems >= 3 ? 14
            : lines > 50 || expectedSubsystems >= 2 ? 8 : 0;
        var lineEvidence = lines is null ? "expected lines missing" : $"{lines} expected changed lines";
        return (score, $"{lineEvidence}; {expectedFiles} expected changed file(s); {expectedSubsystems} expected runtime subsystem(s). Generated and eventual changed files are not inputs.");
    }

    private static (int Score, string Evidence) ContextDemand(ComplexityCard card)
    {
        if (card.Signals.RequiredReading is { } measured)
        {
            var normalized = Clamp01(measured);
            var points = normalized > .75 ? 20 : normalized > .45 ? 14 : normalized > .2 ? 8 : 0;
            return (points, $"Measured pre-launch required-reading signal {normalized:0.###} maps to the {points}-point anchor.");
        }
        var subsystems = DistinctCount(card.ReferencedSubsystems.Concat(card.ExpectedRuntimeSubsystems));
        var broad = Contains(card.Prompt, "cross-repository", "distributed invariant", "architecture history", "broad codebase");
        var historical = Contains(card.Prompt, "historical", "legacy", "reconcile");
        var score = broad ? 20 : subsystems >= 3 || historical ? 14
            : subsystems >= 2 || !string.IsNullOrWhiteSpace(card.EpicContext) ? 8 : 0;
        return (score, $"{subsystems} referenced/expected subsystem(s); epic context {(string.IsNullOrWhiteSpace(card.EpicContext) ? "absent" : "present")}; broad or historical reconciliation language {(broad || historical ? "present" : "absent")}.");
    }

    private static (int Score, string Evidence) TaskUncertainty(ComplexityCard card, IReadOnlyList<string> triggers)
    {
        if (triggers.Contains("unclearBug", StringComparer.Ordinal)
            || Contains(card.Prompt, "unknown root cause", "architecture decision", "requirements must be derived", "investigate", "explore"))
            return (10, "The intake requires root-cause discovery, an architecture decision, or derived requirements.");
        if (Is(card.TaskType, "feature", "bug", "fix"))
            return (6, $"Task type '{card.TaskType}' is a well-specified bug or feature prior.");
        if (Is(card.TaskType, "refactor", "content", "documentation", "docs"))
            return (3, $"Task type '{card.TaskType}' is a clear refactor or content prior.");
        return (0, $"Task type '{card.TaskType ?? "unspecified"}' and the prompt indicate a mechanical chore or copy change.");
    }

    private static (int Score, string Evidence) QuotaHeadroom(ComplexityCard card)
    {
        var points = card.QuotaAndCostHeadroom;
        if (points is null)
            return (0, "Quota/cost headroom was not captured before launch; it remains visibly missing and contributes 0 points.");
        if (points < 0 || points > 5)
            throw new ArgumentOutOfRangeException(nameof(card.QuotaAndCostHeadroom), points, "Policy points must be between 0 and 5.");
        return (points.Value, $"Pre-launch quota/cost headroom supplied as {points.Value} of 5 policy points; it cannot lower a hard floor.");
    }

    private static IReadOnlyList<string> HardFloorTriggers(ComplexityCard card)
    {
        var triggers = card.HardFloorTriggers.Where(trigger => !string.IsNullOrWhiteSpace(trigger)).Distinct(StringComparer.Ordinal).ToList();
        if (card.ExpectedRuntimeSubsystems.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 3
            && !triggers.Contains("threeOrMoreRuntimeSubsystems", StringComparer.Ordinal))
            triggers.Add("threeOrMoreRuntimeSubsystems");
        return triggers.Order(StringComparer.Ordinal).ToArray();
    }

    private static LongForecastRange TokenRange(long point, double confidence)
    {
        var factor = 1.2 + (1 - confidence) * 1.8;
        return new(Math.Max(1, (long)Math.Floor(point / factor)), Math.Max(point, (long)Math.Ceiling(point * factor)));
    }

    private static DurationForecastRange DurationRange(TimeSpan point, double confidence)
    {
        var factor = 1.2 + (1 - confidence) * 1.8;
        return new(TimeSpan.FromTicks(Math.Max(1, (long)Math.Floor(point.Ticks / factor))),
            TimeSpan.FromTicks(Math.Max(point.Ticks, (long)Math.Ceiling(point.Ticks * factor))));
    }

    private static DoubleForecastRange ReissueRange(double point, double confidence)
    {
        var spread = .25 + (1 - confidence) * 1.5;
        return new(Round(Math.Max(0, point - spread)), Round(point + spread));
    }

    private static IReadOnlyList<ComplexityDimension> Dimensions(ComplexityCard card)
    {
        var words = WordRegex().Matches(card.Prompt).Count;
        var expectedFiles = card.ExpectedChangedFiles.Count > 0 ? card.ExpectedChangedFiles : card.ReferencedFiles;
        var expectedSubsystems = card.ExpectedRuntimeSubsystems.Count > 0 ? card.ExpectedRuntimeSubsystems : card.ReferencedSubsystems;
        var touched = Clamp01((DistinctCount(expectedFiles) * .10)
            + (DistinctCount(expectedSubsystems) * .18)
            + Math.Min(card.DependencyFanOut ?? 0, 12) * .035);
        var novelty = card.Signals.Novelty ?? KeywordScore(card.Prompt, "new", "design", "architecture", "research", "unknown", "novel");
        var constraints = card.Signals.ConstraintDensity ?? Clamp01(
            card.AcceptanceCriteria.Count * .08 + KeywordScore(card.Prompt, "security", "concurrency", "atomic", "correctness", "migration", "backward compatible"));
        var ambiguity = card.Signals.SpecificationAmbiguity ?? Clamp01(
            KeywordScore(card.Prompt, "investigate", "explore", "work out", "decide", "optional", "tbd")
            - Math.Min(card.AcceptanceCriteria.Count, 5) * .06);
        var verification = card.Signals.VerificationCost ?? Clamp01(
            KeywordScore(card.Prompt, "backtest", "benchmark", "integration", "end-to-end", "performance", "30 historical")
            + card.AcceptanceCriteria.Count * .04);
        // Repository size is intentionally capped at 0.12 here. Cross-linking/touched surface dominates.
        var repositoryRetrieval = card.RepositoryFileCount is null ? 0 : Math.Min(.12, Math.Log10(Math.Max(10, card.RepositoryFileCount.Value)) * .03);
        var reading = card.Signals.RequiredReading ?? Clamp01(
            words / 1600d + (string.IsNullOrWhiteSpace(card.EpicContext) ? 0 : .12)
            + DistinctCount(expectedSubsystems) * .07 + repositoryRetrieval);

        return
        [
            Dimension("touched_surface", touched, .20, $"{DistinctCount(expectedFiles)} expected/referenced files, {DistinctCount(expectedSubsystems)} expected/referenced subsystems, fan-out {card.DependencyFanOut ?? 0}"),
            Dimension("novelty", novelty, .18, card.Signals.Novelty is null ? "prompt-derived" : "measured override"),
            Dimension("constraint_density", constraints, .18, $"{card.AcceptanceCriteria.Count} acceptance criteria"),
            Dimension("specification_ambiguity", ambiguity, .15, card.Signals.SpecificationAmbiguity is null ? "prompt-derived" : "measured override"),
            Dimension("verification_cost", verification, .14, card.Signals.VerificationCost is null ? "prompt and criteria-derived" : "measured override"),
            Dimension("required_reading", reading, .15, $"{words} prompt words; repository size is capped indirect input"),
        ];
    }

    private static ComplexityDimension Dimension(string name, double normalized, double weight, string evidence)
        => new(name, Round(Clamp01(normalized) * 100), weight, evidence);

    private static double Similarity(ComplexityCard left, ComplexityCard right)
    {
        var score = 0d;
        if (Same(left.Project, right.Project)) score += .20;
        if (Same(left.Area, right.Area)) score += .24;
        if (Same(left.TaskType, right.TaskType)) score += .16;
        score += .12 * Jaccard(left.ReferencedSubsystems, right.ReferencedSubsystems);
        score += .10 * Jaccard(left.ReferencedFiles.Select(path => Path.GetExtension(path) ?? ""), right.ReferencedFiles.Select(path => Path.GetExtension(path) ?? ""));
        var leftWords = Math.Max(1, WordRegex().Matches(left.Prompt).Count);
        var rightWords = Math.Max(1, WordRegex().Matches(right.Prompt).Count);
        score += .08 * ((double)Math.Min(leftWords, rightWords) / Math.Max(leftWords, rightWords));
        var a = Dimensions(left); var b = Dimensions(right);
        score += .10 * (1 - a.Zip(b).Average(pair => Math.Abs(pair.First.Score - pair.Second.Score)) / 100);
        return Clamp01(score);
    }

    private static long TokensFromScore(double score)
        => (long)Math.Round(10_000 * Math.Pow(10, score / 32));

    private static TaskComplexityLevel Level(double score) => score switch
    {
        <= 20 => TaskComplexityLevel.Trivial,
        <= 50 => TaskComplexityLevel.Standard,
        <= 69 => TaskComplexityLevel.Demanding,
        _ => TaskComplexityLevel.Critical,
    };

    private static double WeightedAverage<T>((T Sample, double Similarity)[] values, Func<(T Sample, double Similarity), double> selector)
    {
        var weight = values.Sum(v => v.Similarity * v.Similarity);
        return values.Sum(v => selector(v) * v.Similarity * v.Similarity) / weight;
    }

    private static bool Same(string? left, string? right) => !string.IsNullOrWhiteSpace(left)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool Is(string? value, params string[] candidates) => value is not null
        && candidates.Contains(value, StringComparer.OrdinalIgnoreCase);
    private static bool Contains(string value, params string[] fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    private static int DistinctCount(IEnumerable<string> values) => values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    private static double Jaccard(IEnumerable<string> left, IEnumerable<string> right)
    {
        var a = left.Where(v => !string.IsNullOrWhiteSpace(v)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = right.Where(v => !string.IsNullOrWhiteSpace(v)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (a.Count == 0 || b.Count == 0) return 0;
        return (double)a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count() / a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
    }
    private static double KeywordScore(string prompt, params string[] keywords)
        => Clamp01(keywords.Count(k => prompt.Contains(k, StringComparison.OrdinalIgnoreCase)) * .16);
    private static double Clamp01(double value) => Clamp(value, 0, 1);
    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));
    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"\b[\p{L}\p{N}_-]+\b")]
    private static partial Regex WordRegex();
}
