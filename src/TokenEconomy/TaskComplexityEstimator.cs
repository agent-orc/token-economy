using System.Diagnostics;
using System.Text.RegularExpressions;

#pragma warning disable CS1591
namespace TokenEconomy;

/// <summary>Policy bands: Luna/medium, Terra/medium, Sol/medium, and Sol/xhigh respectively.</summary>
public enum TaskComplexityLevel { Trivial, Standard, Demanding, Critical }

/// <summary>Hard-floor facts known at intake. They are reported separately from the weighted score.</summary>
public enum ComplexityHardFloorTrigger
{
    P0,
    Fencing,
    LeaseOwnership,
    StaleWriteRejection,
    DistributedAuthority,
    SecurityBoundary,
    CredibleDataLoss,
    PublicProtocol,
    PersistentStateMigration,
    ThreeOrMoreRuntimeSubsystems,
    UnclearBug,
    DestructiveOrSecurityCriticalBoundedDecision,
}

/// <summary>Legacy optional forecast signals. Values are normalized to 0..1.</summary>
public sealed record ComplexitySignals
{
    public double? Novelty { get; init; }
    public double? ConstraintDensity { get; init; }
    public double? SpecificationAmbiguity { get; init; }
    public double? VerificationCost { get; init; }
    public double? RequiredReading { get; init; }
}

/// <summary>
/// Optional, pre-launch policy scores. Values are policy points, not normalized values. A supplied
/// value is clamped to the criterion maximum and remains visible as an explicit intake override.
/// Empirical confidence is deliberately absent because it must be calculated from held-out history.
/// </summary>
public sealed record ComplexityRoutingSignals
{
    public double? CorrectnessRisk { get; init; }
    public double? ExpectedScope { get; init; }
    public double? ContextDemand { get; init; }
    public double? TaskUncertainty { get; init; }
    public double? QuotaAndCostHeadroom { get; init; }
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
    /// <summary>Files named or expected at intake; never populate this from an eventual diff.</summary>
    public IReadOnlyList<string> ReferencedFiles { get; init; } = [];
    /// <summary>Runtime subsystems expected at intake; never populate this from eventual changed scope.</summary>
    public IReadOnlyList<string> ReferencedSubsystems { get; init; } = [];
    public int? ExpectedChangedLines { get; init; }
    public int? DependencyFanOut { get; init; }
    /// <summary>Total repository files. This deliberately has only a small, indirect forecast influence.</summary>
    public int? RepositoryFileCount { get; init; }
    public ComplexitySignals Signals { get; init; } = new();
    public ComplexityRoutingSignals RoutingSignals { get; init; } = new();
    public IReadOnlyList<ComplexityHardFloorTrigger> HardFloorTriggers { get; init; } = [];
}

/// <summary>An already observed task used for calibration and nearest-neighbour inheritance.</summary>
public sealed record ComplexityHistorySample
{
    public required ComplexityCard Card { get; init; }
    public required long ActualTokens { get; init; }
    public required TimeSpan ActualDuration { get; init; }
    public required int ReissueCount { get; init; }
    public bool TokenHistoryComplete { get; init; } = true;
    public bool DurationHistoryComplete { get; init; } = true;
    public bool ReissueHistoryAvailable { get; init; } = true;
    public int KnownGradeCount { get; init; }
    public int FavorableGradeCount { get; init; }
    public int? SemanticReissueCount { get; init; }
}

/// <summary>Optional result of a cheap rubric call. The caller owns the provider invocation.</summary>
public sealed record LlmComplexityAssessment(double Score, double Confidence, string? RubricVersion = null);

public sealed record ComplexityDimension(string Name, double Score, double Weight, string Evidence);
public sealed record ComplexityRoutingCriterion(string Name, double Score, double MaximumScore, string Evidence);
public sealed record ComplexityHardFloor(ComplexityHardFloorTrigger Trigger, TaskComplexityLevel MinimumBand, string Evidence);
public sealed record ComplexityNeighbour(
    string TaskKey,
    double Similarity,
    long? ActualTokens,
    int? ReissueCount,
    TimeSpan? ActualDuration,
    string Evidence)
{
    public ComplexityNeighbour(string taskKey, double similarity, long actualTokens, int reissueCount)
        : this(taskKey, similarity, actualTokens, reissueCount, null, "Legacy neighbour without measurement-coverage evidence.") { }
}
public sealed record ComplexityHistoryEvidence(
    int ComparableCards,
    int TokenCompleteCards,
    int DurationCompleteCards,
    int ReissueAvailableCards,
    int KnownGrades,
    int FavorableGrades,
    int SemanticReissueAvailableCards,
    string Evidence);
public sealed record TokenForecastRange(long LowerBound, long Expected, long UpperBound, string Evidence);
public sealed record DurationForecastRange(TimeSpan LowerBound, TimeSpan Expected, TimeSpan UpperBound, string Evidence);
public sealed record ReissueForecastRange(double LowerBound, double Expected, double UpperBound, string Evidence);
public sealed record TaskComplexityEstimationEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>Serializable, per-card routing input. SchemaVersion allows durable stores to evolve safely.</summary>
public sealed record TaskComplexityEstimate
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string TaskKey { get; init; }
    public required TaskComplexityLevel Level { get; init; }
    public TaskComplexityLevel ComplexityBand => Level;
    public required double Score { get; init; }
    public required string ScoreEvidence { get; init; }
    public required double Confidence { get; init; }
    public required string ConfidenceEvidence { get; init; }
    public required long PredictedTokens { get; init; }
    public required TimeSpan PredictedDuration { get; init; }
    public required double PredictedReissues { get; init; }
    public required TokenForecastRange TokenForecast { get; init; }
    public required DurationForecastRange DurationForecast { get; init; }
    public required ReissueForecastRange ReissueForecast { get; init; }
    public required ComplexityRoutingCriterion CorrectnessRisk { get; init; }
    public required ComplexityRoutingCriterion ExpectedScope { get; init; }
    public required ComplexityRoutingCriterion ContextDemand { get; init; }
    public required ComplexityRoutingCriterion TaskUncertainty { get; init; }
    public required ComplexityRoutingCriterion EmpiricalConfidence { get; init; }
    public required ComplexityRoutingCriterion QuotaAndCostHeadroom { get; init; }
    public required IReadOnlyList<ComplexityHardFloor> HardFloors { get; init; }
    public required ComplexityHistoryEvidence HistoricalEvidence { get; init; }
    /// <summary>Normalized compatibility view of the six canonical policy criteria.</summary>
    public required IReadOnlyList<ComplexityDimension> Dimensions { get; init; }
    public required IReadOnlyList<ComplexityNeighbour> Neighbours { get; init; }
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
/// Dependency-free upfront estimator. The weighted worksheet follows the canonical model-routing
/// policy. Historical outcomes calibrate forecasts and empirical confidence only; they never rewrite
/// the pre-launch risk, expected scope, context demand, or uncertainty facts.
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

        // Exclude by stable card key before similarity, cohort scoring, and forecast aggregation. This
        // also protects callers that accidentally pass several attempt-derived samples for the card.
        var candidates = (history ?? [])
            .Where(sample => !string.Equals(sample.Card.TaskKey, card.TaskKey, StringComparison.OrdinalIgnoreCase))
            .GroupBy(sample => sample.Card.TaskKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Single())
            .Select(sample => (Sample: sample, Similarity: Similarity(card, sample.Card)))
            .Where(item => item.Similarity >= MinNeighbourSimilarity)
            .OrderByDescending(item => item.Similarity)
            .ThenBy(item => item.Sample.Card.TaskKey, StringComparer.Ordinal)
            .Take(20)
            .ToArray();

        var hardFloors = FindHardFloors(card);
        var correctnessRisk = CorrectnessRisk(card, hardFloors);
        var expectedScope = ExpectedScope(card);
        var contextDemand = ContextDemand(card);
        var uncertainty = TaskUncertainty(card, hardFloors);
        var empirical = EmpiricalConfidence(candidates);
        var quota = Criterion("quota_and_cost_headroom", card.RoutingSignals.QuotaAndCostHeadroom ?? 5, 5,
            card.RoutingSignals.QuotaAndCostHeadroom is null
                ? "No run-scoped quota signal supplied; policy default assumes comfortable headroom (5/5)."
                : "Explicit pre-launch quota/cost-headroom score; quota cannot lower a hard floor.");
        var score = Round(correctnessRisk.Score + expectedScope.Score + contextDemand.Score
            + uncertainty.Score + empirical.Score + quota.Score);
        var scoredBand = Level(score);
        var level = hardFloors.Aggregate(scoredBand,
            (current, floor) => (TaskComplexityLevel)Math.Max((int)current, (int)floor.MinimumBand));

        // Keep the established forecast model separate from the routing worksheet. This preserves
        // token/duration/reissue behavior while allowing the policy score to evolve audibly.
        var forecastNeighbours = candidates.Take(5).ToArray();
        var forecastDimensions = ForecastDimensions(card);
        var forecastScore = forecastDimensions.Sum(dimension => dimension.Score * dimension.Weight);
        var completeOutcomeRows = forecastNeighbours.Where(item => item.Sample.TokenHistoryComplete
            && item.Sample.DurationHistoryComplete && item.Sample.ReissueHistoryAvailable).ToArray();
        if (completeOutcomeRows.Length > 0)
            forecastScore = forecastScore * .55 + WeightedAverage(completeOutcomeRows,
                item => ScoreFromActual(item.Sample.ActualTokens, item.Sample.ActualDuration, item.Sample.ReissueCount)) * .45;
        if (llmAssessment is not null)
            forecastScore = forecastScore * (1 - .2 * Clamp01(llmAssessment.Confidence))
                + Clamp(llmAssessment.Score, 0, 100) * .2 * Clamp01(llmAssessment.Confidence);
        forecastScore = Clamp(forecastScore, 0, 100);

        var tokenRows = forecastNeighbours.Where(item => item.Sample.TokenHistoryComplete).ToArray();
        var durationRows = forecastNeighbours.Where(item => item.Sample.DurationHistoryComplete).ToArray();
        var reissueRows = forecastNeighbours.Where(item => item.Sample.ReissueHistoryAvailable).ToArray();
        var allTokenRows = candidates.Where(item => item.Sample.TokenHistoryComplete).ToArray();
        var allDurationRows = candidates.Where(item => item.Sample.DurationHistoryComplete).ToArray();
        var allReissueRows = candidates.Where(item => item.Sample.ReissueHistoryAvailable).ToArray();
        var predictedTokens = tokenRows.Length == 0 ? TokensFromScore(forecastScore)
            : (long)Math.Round(WeightedAverage(tokenRows, item => item.Sample.ActualTokens));
        var predictedDuration = durationRows.Length == 0 ? TimeSpan.FromMinutes(5 * Math.Pow(1.04, forecastScore))
            : TimeSpan.FromTicks((long)Math.Round(WeightedAverage(durationRows, item => item.Sample.ActualDuration.Ticks)));
        var predictedReissues = reissueRows.Length == 0 ? Math.Max(0, (forecastScore - 35) / 35)
            : WeightedAverage(reissueRows, item => item.Sample.ReissueCount);

        var suppliedCriteria = new[]
        {
            card.RoutingSignals.CorrectnessRisk, card.RoutingSignals.ExpectedScope,
            card.RoutingSignals.ContextDemand, card.RoutingSignals.TaskUncertainty,
            card.RoutingSignals.QuotaAndCostHeadroom,
        }.Count(value => value is not null);
        var historyCompleteness = candidates.Length == 0 ? 0 : new[]
        {
            allTokenRows.Length / (double)candidates.Length,
            allDurationRows.Length / (double)candidates.Length,
            allReissueRows.Length / (double)candidates.Length,
        }.Average();
        var confidence = .38 + suppliedCriteria * .045 + Math.Min(.22, candidates.Length * .018) + .12 * historyCompleteness;
        if (llmAssessment is not null)
        {
            var agreement = 1 - Math.Abs(Clamp(llmAssessment.Score, 0, 100) - score) / 100;
            confidence += .08 * Clamp01(llmAssessment.Confidence) * agreement;
        }
        if (candidates.Length == 0) confidence = Math.Min(.55, confidence);
        if (empirical.Score == empirical.MaximumScore) confidence = Math.Min(.65, confidence);
        confidence = Round(Clamp01(confidence));

        var tokenForecast = TokenRange(predictedTokens, tokenRows, confidence);
        var durationForecast = DurationRange(predictedDuration, durationRows, confidence);
        var reissueForecast = ReissueRange(predictedReissues, reissueRows, confidence);
        var criteria = new[] { correctnessRisk, expectedScope, contextDemand, uncertainty, empirical, quota };
        var result = new TaskComplexityEstimate
        {
            TaskKey = card.TaskKey,
            Level = level,
            Score = score,
            ScoreEvidence = $"Weighted policy sum {score}/100; scored band {scoredBand}; "
                + (hardFloors.Count == 0 ? "no hard-floor promotion." : $"{hardFloors.Count} hard floor(s) promoted/confirmed {level}."),
            Confidence = confidence,
            ConfidenceEvidence = $"{suppliedCriteria}/5 explicit intake scores; {candidates.Length} comparable cards; "
                + $"measurement completeness {historyCompleteness:P0}; LLM assessment supplied={llmAssessment is not null}. "
                + (candidates.Length == 0 ? "No-history confidence cap applied." : "History remained visible in empirical confidence."),
            PredictedTokens = tokenForecast.Expected,
            PredictedDuration = durationForecast.Expected,
            PredictedReissues = reissueForecast.Expected,
            TokenForecast = tokenForecast,
            DurationForecast = durationForecast,
            ReissueForecast = reissueForecast,
            CorrectnessRisk = correctnessRisk,
            ExpectedScope = expectedScope,
            ContextDemand = contextDemand,
            TaskUncertainty = uncertainty,
            EmpiricalConfidence = empirical,
            QuotaAndCostHeadroom = quota,
            HardFloors = hardFloors,
            HistoricalEvidence = new(
                candidates.Length, allTokenRows.Length, allDurationRows.Length, allReissueRows.Length,
                candidates.Sum(item => item.Sample.KnownGradeCount),
                candidates.Sum(item => item.Sample.FavorableGradeCount),
                candidates.Count(item => item.Sample.SemanticReissueCount is not null),
                HistoryEvidenceText(candidates, allTokenRows, allDurationRows, allReissueRows)),
            Dimensions = criteria.Select(item => new ComplexityDimension(
                item.Name, Round(item.Score / item.MaximumScore * 100), item.MaximumScore / 100, item.Evidence)).ToArray(),
            Neighbours = candidates.Take(5).Select(item => new ComplexityNeighbour(
                item.Sample.Card.TaskKey,
                Round(item.Similarity),
                item.Sample.TokenHistoryComplete ? item.Sample.ActualTokens : null,
                item.Sample.ReissueHistoryAvailable ? item.Sample.ReissueCount : null,
                item.Sample.DurationHistoryComplete ? item.Sample.ActualDuration : null,
                $"token complete={item.Sample.TokenHistoryComplete}; duration complete={item.Sample.DurationHistoryComplete}; reissue available={item.Sample.ReissueHistoryAvailable}"))
                .ToArray(),
            LlmRubricVersion = llmAssessment?.RubricVersion,
        };
        timer.Stop();
        EventOccurred?.Invoke(new("task_complexity.estimated", new Dictionary<string, object?>
        {
            ["taskKey"] = card.TaskKey,
            ["level"] = result.Level.ToString().ToLowerInvariant(),
            ["score"] = result.Score,
            ["confidence"] = result.Confidence,
            ["neighbourCount"] = result.Neighbours.Count,
            ["hardFloorCount"] = result.HardFloors.Count,
            ["usedLlmAssessment"] = llmAssessment is not null,
            ["elapsedMs"] = timer.Elapsed.TotalMilliseconds,
        }));
        return result;
    }

    private static ComplexityRoutingCriterion CorrectnessRisk(ComplexityCard card, IReadOnlyList<ComplexityHardFloor> floors)
    {
        if (card.RoutingSignals.CorrectnessRisk is { } explicitScore)
            return Criterion("correctness_risk", explicitScore, 35, "Explicit pre-launch correctness-risk score.");
        if (floors.Any(floor => floor.MinimumBand == TaskComplexityLevel.Critical))
            return Criterion("correctness_risk", 35, 35, "Critical hard-floor fact: " + string.Join(", ", floors.Where(f => f.MinimumBand == TaskComplexityLevel.Critical).Select(f => f.Trigger)) + ".");
        if (floors.Any(floor => floor.Trigger is ComplexityHardFloorTrigger.PublicProtocol or ComplexityHardFloorTrigger.PersistentStateMigration or ComplexityHardFloorTrigger.UnclearBug)
            || ContainsAny(CardText(card), "persistent state", "public contract", "public protocol", "consequential migration"))
            return Criterion("correctness_risk", 24, 35, "Persistent state, public contract/protocol, migration, or unclear-bug evidence maps to the 24-point anchor.");
        if (IsBehavioral(card))
            return Criterion("correctness_risk", 12, 35, "Reversible behavior with an acceptance or verification path maps to the 12-point anchor.");
        return Criterion("correctness_risk", 0, 35, "Prose, formatting, or non-behavioral local work maps to the 0-point anchor.");
    }

    private static ComplexityRoutingCriterion ExpectedScope(ComplexityCard card)
    {
        if (card.RoutingSignals.ExpectedScope is { } explicitScore)
            return Criterion("expected_scope", explicitScore, 20, "Explicit pre-launch expected-scope score; it is not derived from the eventual diff.");
        var lines = card.ExpectedChangedLines;
        var subsystems = DistinctCount(card.ReferencedSubsystems);
        var score = lines > 500 || subsystems >= 4 ? 20
            : lines > 200 || subsystems == 3 ? 14
            : lines > 50 || subsystems == 2 ? 8
            : 0;
        var lineEvidence = lines is null ? "expected lines unavailable" : $"{lines} expected changed lines";
        return Criterion("expected_scope", score, 20,
            $"{lineEvidence}; {subsystems} expected runtime subsystem(s); generated/eventual files are excluded.");
    }

    private static ComplexityRoutingCriterion ContextDemand(ComplexityCard card)
    {
        if (card.RoutingSignals.ContextDemand is { } explicitScore)
            return Criterion("context_demand", explicitScore, 20, "Explicit pre-launch context-demand score.");
        var text = CardText(card);
        var subsystems = DistinctCount(card.ReferencedSubsystems);
        var broad = ContainsAny(text, "cross-repository", "distributed invariant", "architecture history", "broad codebase", "repository-wide");
        var historical = ContainsAny(text, "historical behavior", "reconcile", "several layers", "backtest") || subsystems >= 3;
        var adjacent = ContainsAny(text, "adjacent", "contract") || subsystems == 2 || DistinctCount(card.ReferencedFiles) > 1;
        var score = broad ? 20 : historical ? 14 : adjacent ? 8 : 0;
        var reason = broad ? "Broad codebase/history or cross-repository/distributed invariants are required."
            : historical ? "Several layers or historical behavior must be reconciled."
            : adjacent ? "An adjacent component or contract must be read."
            : "The exact file and behavior are known, or no broader reading is stated.";
        return Criterion("context_demand", score, 20, $"{reason} Intake names {DistinctCount(card.ReferencedFiles)} file(s) and {subsystems} subsystem(s).");
    }

    private static ComplexityRoutingCriterion TaskUncertainty(ComplexityCard card, IReadOnlyList<ComplexityHardFloor> floors)
    {
        if (card.RoutingSignals.TaskUncertainty is { } explicitScore)
            return Criterion("task_uncertainty", explicitScore, 10, "Explicit pre-launch task-type/uncertainty score.");
        var text = CardText(card);
        var type = card.TaskType?.Trim().ToLowerInvariant() ?? "";
        var score = floors.Any(floor => floor.Trigger == ComplexityHardFloorTrigger.UnclearBug)
            || ContainsAny(text, "unknown root cause", "architecture decision", "requirements must be derived") ? 10
            : type.Contains("bug") || type.Contains("feature") ? 6
            : type.Contains("refactor") || type.Contains("content") || type.Contains("docs") ? 3
            : ContainsAny(text, "mechanical", "copy change", "rename", "formatting") ? 0
            : 6;
        return Criterion("task_uncertainty", score, 10,
            score switch
            {
                10 => "Unknown root cause, architecture decision, or derived requirements map to 10 points.",
                6 => "A well-specified bug/feature (or otherwise non-mechanical task) maps to 6 points.",
                3 => "A clear refactor, content, or documentation task maps to 3 points.",
                _ => "A mechanical chore or copy change maps to 0 points.",
            });
    }

    private static ComplexityRoutingCriterion EmpiricalConfidence((ComplexityHistorySample Sample, double Similarity)[] neighbours)
    {
        if (neighbours.Length == 0)
            return Criterion("empirical_confidence", 10, 10, "No comparable held-out cohort; empirical uncertainty remains fully visible (10/10).");
        var known = neighbours.Sum(item => item.Sample.KnownGradeCount);
        var favorable = neighbours.Sum(item => item.Sample.FavorableGradeCount);
        var semanticAvailable = neighbours.Where(item => item.Sample.SemanticReissueCount is not null).ToArray();
        var semanticReissues = semanticAvailable.Sum(item => item.Sample.SemanticReissueCount ?? 0);
        var knownCoverage = known / (double)neighbours.Length;
        var favorableRate = known == 0 ? 0 : favorable / (double)known;
        var reissueRate = semanticAvailable.Length == 0 ? (double?)null : semanticReissues / (double)semanticAvailable.Length;
        if (neighbours.Length >= 20 && knownCoverage >= .5 && favorableRate >= .7
            && semanticAvailable.Length == neighbours.Length && reissueRate < .1)
            return Criterion("empirical_confidence", 0, 10,
                $"{neighbours.Length} comparable runs; grade coverage {knownCoverage:P0}, A/B {favorableRate:P0}, semantic reissue {reissueRate:P0}.");
        if (neighbours.Length >= 5 && known >= 5 && favorableRate >= .7 && (reissueRate is null || reissueRate < .2))
            return Criterion("empirical_confidence", 3, 10,
                $"At least five favorable comparable runs: {known} known grades, {favorableRate:P0} A/B; semantic reissue {(reissueRate is null ? "unavailable" : reissueRate.Value.ToString("P0"))}.");
        var unfavorable = known >= 3 && favorableRate < .5;
        var repeatedReissues = semanticReissues >= 2;
        if (unfavorable || repeatedReissues)
            return Criterion("empirical_confidence", 10, 10,
                $"Comparable cohort is unfavorable or repeatedly reissued: {known} known grades, {favorableRate:P0} A/B, {semanticReissues} semantic reissues.");
        return Criterion("empirical_confidence", 6, 10,
            $"Sparse or mixed held-out evidence: {neighbours.Length} comparable cards, {known} known grades, semantic reissue coverage {semanticAvailable.Length}/{neighbours.Length}.");
    }

    private static IReadOnlyList<ComplexityHardFloor> FindHardFloors(ComplexityCard card)
    {
        var triggers = card.HardFloorTriggers.ToHashSet();
        var text = CardText(card);
        AddIf(ContainsAny(text, "p0"), ComplexityHardFloorTrigger.P0);
        AddIf(ContainsAny(text, "fencing"), ComplexityHardFloorTrigger.Fencing);
        AddIf(ContainsAny(text, "lease ownership"), ComplexityHardFloorTrigger.LeaseOwnership);
        AddIf(ContainsAny(text, "stale-write", "stale write rejection"), ComplexityHardFloorTrigger.StaleWriteRejection);
        AddIf(ContainsAny(text, "distributed authority"), ComplexityHardFloorTrigger.DistributedAuthority);
        AddIf(ContainsAny(text, "security boundary"), ComplexityHardFloorTrigger.SecurityBoundary);
        AddIf(ContainsAny(text, "data loss"), ComplexityHardFloorTrigger.CredibleDataLoss);
        AddIf(ContainsAny(text, "public protocol"), ComplexityHardFloorTrigger.PublicProtocol);
        AddIf(ContainsAny(text, "persistent-state migration", "persistent state migration"), ComplexityHardFloorTrigger.PersistentStateMigration);
        AddIf(DistinctCount(card.ReferencedSubsystems) >= 3, ComplexityHardFloorTrigger.ThreeOrMoreRuntimeSubsystems);
        AddIf((card.TaskType?.Contains("bug", StringComparison.OrdinalIgnoreCase) ?? false)
            && ContainsAny(text, "unclear", "unknown root cause", "investigate"), ComplexityHardFloorTrigger.UnclearBug);
        var boundedDecision = (card.TaskType?.Contains("decision", StringComparison.OrdinalIgnoreCase) ?? false)
            || ContainsAny(text, "bounded decision", "orchestrator decision", "pipeline decision");
        var consequentialAuthorization = ContainsAny(text, "authorize destructive", "authorize security", "lane-affecting", "lane affecting");
        var ambiguousEvidence = ContainsAny(text, "ambiguous evidence", "unbounded evidence", "evidence is ambiguous", "evidence is unbounded");
        AddIf(boundedDecision && consequentialAuthorization && ambiguousEvidence,
            ComplexityHardFloorTrigger.DestructiveOrSecurityCriticalBoundedDecision);

        return triggers.OrderBy(trigger => trigger).Select(trigger => new ComplexityHardFloor(
            trigger,
            trigger switch
            {
                ComplexityHardFloorTrigger.P0 or ComplexityHardFloorTrigger.Fencing
                    or ComplexityHardFloorTrigger.LeaseOwnership or ComplexityHardFloorTrigger.StaleWriteRejection
                    or ComplexityHardFloorTrigger.DistributedAuthority or ComplexityHardFloorTrigger.SecurityBoundary
                    or ComplexityHardFloorTrigger.CredibleDataLoss => TaskComplexityLevel.Critical,
                ComplexityHardFloorTrigger.UnclearBug => TaskComplexityLevel.Standard,
                _ => TaskComplexityLevel.Demanding,
            },
            card.HardFloorTriggers.Contains(trigger)
                ? "Explicit pre-launch hard-floor trigger."
                : "Deterministic pre-launch card evidence matched the canonical hard floor."))
            .ToArray();

        void AddIf(bool condition, ComplexityHardFloorTrigger trigger)
        {
            if (condition) triggers.Add(trigger);
        }
    }

    private static ComplexityRoutingCriterion Criterion(string name, double score, double maximum, string evidence)
        => new(name, Round(Clamp(score, 0, maximum)), maximum, evidence);

    private static TokenForecastRange TokenRange(long expected, (ComplexityHistorySample Sample, double Similarity)[] rows, double confidence)
    {
        var factor = .25 + (1 - confidence) * .9;
        var confidenceLower = (long)Math.Round(expected * (1 - Math.Min(.8, factor)));
        var confidenceUpper = (long)Math.Round(expected * (1 + factor));
        var lower = rows.Length == 0 ? confidenceLower : Math.Min(confidenceLower, rows.Min(item => item.Sample.ActualTokens));
        var upper = rows.Length == 0 ? confidenceUpper : Math.Max(confidenceUpper, rows.Max(item => item.Sample.ActualTokens));
        return new(Math.Max(1, lower), Math.Max(1, expected), Math.Max(expected, upper),
            rows.Length == 0 ? "Heuristic range widened because no complete comparable token history exists." : $"Confidence envelope plus observed span of {rows.Length} complete comparable token histories.");
    }

    private static DurationForecastRange DurationRange(TimeSpan expected, (ComplexityHistorySample Sample, double Similarity)[] rows, double confidence)
    {
        var factor = .25 + (1 - confidence) * .9;
        var confidenceLower = TimeSpan.FromTicks((long)(expected.Ticks * (1 - Math.Min(.8, factor))));
        var confidenceUpper = TimeSpan.FromTicks((long)(expected.Ticks * (1 + factor)));
        var lower = rows.Length == 0 ? confidenceLower : new[] { confidenceLower, rows.Min(item => item.Sample.ActualDuration) }.Min();
        var upper = rows.Length == 0 ? confidenceUpper : new[] { confidenceUpper, rows.Max(item => item.Sample.ActualDuration) }.Max();
        return new(lower < TimeSpan.Zero ? TimeSpan.Zero : lower, expected, upper < expected ? expected : upper,
            rows.Length == 0 ? "Heuristic range widened because no complete comparable duration history exists." : $"Confidence envelope plus observed span of {rows.Length} complete comparable duration histories.");
    }

    private static ReissueForecastRange ReissueRange(double expected, (ComplexityHistorySample Sample, double Similarity)[] rows, double confidence)
    {
        var factor = .25 + (1 - confidence) * .9;
        var confidenceLower = Math.Max(0, expected - factor);
        var confidenceUpper = expected + factor;
        var lower = rows.Length == 0 ? confidenceLower : Math.Min(confidenceLower, rows.Min(item => (double)item.Sample.ReissueCount));
        var upper = rows.Length == 0 ? confidenceUpper : Math.Max(confidenceUpper, rows.Max(item => (double)item.Sample.ReissueCount));
        return new(Round(lower), Round(Math.Max(0, expected)), Round(upper),
            rows.Length == 0 ? "Heuristic range widened because no comparable reissue history exists." : $"Confidence envelope plus observed span of {rows.Length} comparable reissue histories.");
    }

    private static string HistoryEvidenceText(
        (ComplexityHistorySample Sample, double Similarity)[] all,
        (ComplexityHistorySample Sample, double Similarity)[] tokens,
        (ComplexityHistorySample Sample, double Similarity)[] durations,
        (ComplexityHistorySample Sample, double Similarity)[] reissues)
        => all.Length == 0
            ? "No comparable held-out cards. Missing history is not represented as a zero outcome."
            : $"{all.Length} comparable held-out cards; complete token {tokens.Length}, duration {durations.Length}, reissue {reissues.Length}. Missing measurements remain excluded and visible.";

    private static IReadOnlyList<ComplexityDimension> ForecastDimensions(ComplexityCard card)
    {
        var words = WordRegex().Matches(card.Prompt).Count;
        var touched = Clamp01((DistinctCount(card.ReferencedFiles) * .10)
            + (DistinctCount(card.ReferencedSubsystems) * .18)
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
        var repositoryRetrieval = card.RepositoryFileCount is null ? 0
            : Math.Min(.12, Math.Log10(Math.Max(10, card.RepositoryFileCount.Value)) * .03);
        var reading = card.Signals.RequiredReading ?? Clamp01(
            words / 1600d + (string.IsNullOrWhiteSpace(card.EpicContext) ? 0 : .12)
            + DistinctCount(card.ReferencedSubsystems) * .07 + repositoryRetrieval);
        return
        [
            new("touched_surface", Round(touched * 100), .20, "forecast-only intake feature"),
            new("novelty", Round(Clamp01(novelty) * 100), .18, "forecast-only intake feature"),
            new("constraint_density", Round(Clamp01(constraints) * 100), .18, "forecast-only intake feature"),
            new("specification_ambiguity", Round(Clamp01(ambiguity) * 100), .15, "forecast-only intake feature"),
            new("verification_cost", Round(Clamp01(verification) * 100), .14, "forecast-only intake feature"),
            new("required_reading", Round(Clamp01(reading) * 100), .15, "forecast-only intake feature"),
        ];
    }

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
        var leftDimensions = ForecastDimensions(left);
        var rightDimensions = ForecastDimensions(right);
        score += .10 * (1 - leftDimensions.Zip(rightDimensions)
            .Average(pair => Math.Abs(pair.First.Score - pair.Second.Score)) / 100);
        return Clamp01(score);
    }

    private static double ScoreFromActual(long tokens, TimeSpan duration, int reissues)
        => Clamp(Math.Log10(Math.Max(1, tokens) / 10_000d) * 28
            + Math.Log2(1 + Math.Max(0, duration.TotalHours)) * 7 + reissues * 12, 0, 100);
    private static long TokensFromScore(double score) => (long)Math.Round(10_000 * Math.Pow(10, score / 32));
    private static TaskComplexityLevel Level(double score) => score switch
    {
        <= 20 => TaskComplexityLevel.Trivial,
        <= 50 => TaskComplexityLevel.Standard,
        <= 69 => TaskComplexityLevel.Demanding,
        _ => TaskComplexityLevel.Critical,
    };

    private static double WeightedAverage<T>((T Sample, double Similarity)[] values, Func<(T Sample, double Similarity), double> selector)
    {
        var weight = values.Sum(value => value.Similarity * value.Similarity);
        return values.Sum(value => selector(value) * value.Similarity * value.Similarity) / weight;
    }

    private static bool IsBehavioral(ComplexityCard card)
        => (card.TaskType?.Contains("feature", StringComparison.OrdinalIgnoreCase) ?? false)
            || (card.TaskType?.Contains("bug", StringComparison.OrdinalIgnoreCase) ?? false)
            || ContainsAny(CardText(card), "implement", "behavior", "migration", "refactor");
    private static string CardText(ComplexityCard card)
        => string.Join('\n', new[] { card.Prompt, card.EpicContext ?? "" }.Concat(card.AcceptanceCriteria));
    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    private static bool Same(string? left, string? right) => !string.IsNullOrWhiteSpace(left)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static int DistinctCount(IEnumerable<string> values) => values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    private static double Jaccard(IEnumerable<string> left, IEnumerable<string> right)
    {
        var a = left.Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = right.Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (a.Count == 0 || b.Count == 0) return 0;
        return (double)a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count() / a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
    }
    private static double KeywordScore(string prompt, params string[] keywords)
        => Clamp01(keywords.Count(keyword => prompt.Contains(keyword, StringComparison.OrdinalIgnoreCase)) * .16);
    private static double Clamp01(double value) => Clamp(value, 0, 1);
    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));
    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"\b[\p{L}\p{N}_-]+\b")]
    private static partial Regex WordRegex();
}
