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
    public int? DependencyFanOut { get; init; }
    /// <summary>Total repository files. This deliberately has only a small, indirect influence.</summary>
    public int? RepositoryFileCount { get; init; }
    public ComplexitySignals Signals { get; init; } = new();
}

/// <summary>An already observed task used for calibration and nearest-neighbour inheritance.</summary>
public sealed record ComplexityHistorySample
{
    public required ComplexityCard Card { get; init; }
    public required long ActualTokens { get; init; }
    public required TimeSpan ActualDuration { get; init; }
    public required int ReissueCount { get; init; }
}

/// <summary>Optional result of a cheap rubric call. The caller owns the provider invocation.</summary>
public sealed record LlmComplexityAssessment(double Score, double Confidence, string? RubricVersion = null);

public sealed record ComplexityDimension(string Name, double Score, double Weight, string Evidence);
public sealed record ComplexityNeighbour(string TaskKey, double Similarity, long ActualTokens, int ReissueCount);
public sealed record TaskComplexityEstimationEvent(string Name, IReadOnlyDictionary<string, object?> Context);

/// <summary>Serializable, per-card routing input. SchemaVersion allows durable stores to evolve safely.</summary>
public sealed record TaskComplexityEstimate
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string TaskKey { get; init; }
    public required TaskComplexityLevel Level { get; init; }
    public required double Score { get; init; }
    public required double Confidence { get; init; }
    public required long PredictedTokens { get; init; }
    public required TimeSpan PredictedDuration { get; init; }
    public required double PredictedReissues { get; init; }
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
/// Dependency-free upfront estimator. Card signals form the baseline; sufficiently similar historical
/// tasks calibrate token/reissue predictions, and an optional mini-model rubric score can be blended in.
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
        var deterministicScore = dimensions.Sum(d => d.Score * d.Weight);
        var neighbours = (history ?? [])
            .Where(h => !string.Equals(h.Card.TaskKey, card.TaskKey, StringComparison.Ordinal))
            .Select(h => (Sample: h, Similarity: Similarity(card, h.Card)))
            .Where(n => n.Similarity >= MinNeighbourSimilarity)
            .OrderByDescending(n => n.Similarity)
            .ThenBy(n => n.Sample.Card.TaskKey, StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        var historyScore = neighbours.Length == 0 ? (double?)null
            : WeightedAverage(neighbours, n => ScoreFromActual(n.Sample.ActualTokens, n.Sample.ActualDuration, n.Sample.ReissueCount));
        var score = deterministicScore;
        if (historyScore is not null) score = score * .55 + historyScore.Value * .45;
        if (llmAssessment is not null)
            score = score * (1 - .2 * Clamp01(llmAssessment.Confidence))
                + Clamp(llmAssessment.Score, 0, 100) * .2 * Clamp01(llmAssessment.Confidence);
        score = Clamp(score, 0, 100);

        var predictedTokens = neighbours.Length == 0
            ? TokensFromScore(score)
            : (long)Math.Round(WeightedAverage(neighbours, n => n.Sample.ActualTokens));
        var predictedDuration = neighbours.Length == 0
            ? TimeSpan.FromMinutes(5 * Math.Pow(1.04, score))
            : TimeSpan.FromTicks((long)Math.Round(WeightedAverage(neighbours, n => n.Sample.ActualDuration.Ticks)));
        var predictedReissues = neighbours.Length == 0
            ? Math.Max(0, (score - 35) / 35)
            : WeightedAverage(neighbours, n => n.Sample.ReissueCount);

        var measurable = new[]
        {
            card.Signals.Novelty, card.Signals.ConstraintDensity, card.Signals.SpecificationAmbiguity,
            card.Signals.VerificationCost, card.Signals.RequiredReading,
        }.Count(v => v is not null);
        var confidence = .42 + measurable * .045 + Math.Min(.23, neighbours.Length * .046);
        if (llmAssessment is not null)
        {
            var agreement = 1 - Math.Abs(Clamp(llmAssessment.Score, 0, 100) - deterministicScore) / 100;
            confidence += .1 * Clamp01(llmAssessment.Confidence) * agreement;
        }

        var result = new TaskComplexityEstimate
        {
            TaskKey = card.TaskKey,
            Level = Level(score),
            Score = Round(score),
            Confidence = Round(Clamp01(confidence)),
            PredictedTokens = Math.Max(1, predictedTokens),
            PredictedDuration = predictedDuration,
            PredictedReissues = Round(Math.Max(0, predictedReissues)),
            Dimensions = dimensions,
            Neighbours = neighbours.Select(n => new ComplexityNeighbour(
                n.Sample.Card.TaskKey, Round(n.Similarity), n.Sample.ActualTokens, n.Sample.ReissueCount)).ToArray(),
            LlmRubricVersion = llmAssessment?.RubricVersion,
        };
        timer.Stop();
        EventOccurred?.Invoke(new("task_complexity.estimated", new Dictionary<string, object?>
        {
            ["taskKey"] = card.TaskKey, ["level"] = result.Level.ToString().ToLowerInvariant(),
            ["score"] = result.Score, ["confidence"] = result.Confidence,
            ["neighbourCount"] = result.Neighbours.Count, ["usedLlmAssessment"] = llmAssessment is not null,
            ["elapsedMs"] = timer.Elapsed.TotalMilliseconds,
        }));
        return result;
    }

    private static IReadOnlyList<ComplexityDimension> Dimensions(ComplexityCard card)
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
        // Repository size is intentionally capped at 0.12 here. Cross-linking/touched surface dominates.
        var repositoryRetrieval = card.RepositoryFileCount is null ? 0 : Math.Min(.12, Math.Log10(Math.Max(10, card.RepositoryFileCount.Value)) * .03);
        var reading = card.Signals.RequiredReading ?? Clamp01(
            words / 1600d + (string.IsNullOrWhiteSpace(card.EpicContext) ? 0 : .12)
            + DistinctCount(card.ReferencedSubsystems) * .07 + repositoryRetrieval);

        return
        [
            Dimension("touched_surface", touched, .20, $"{DistinctCount(card.ReferencedFiles)} files, {DistinctCount(card.ReferencedSubsystems)} subsystems, fan-out {card.DependencyFanOut ?? 0}"),
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

    private static double ScoreFromActual(long tokens, TimeSpan duration, int reissues)
        => Clamp(Math.Log10(Math.Max(1, tokens) / 10_000d) * 28
            + Math.Log2(1 + Math.Max(0, duration.TotalHours)) * 7 + reissues * 12, 0, 100);

    private static long TokensFromScore(double score)
        => (long)Math.Round(10_000 * Math.Pow(10, score / 32));

    private static TaskComplexityLevel Level(double score) => score switch
    {
        < 25 => TaskComplexityLevel.Trivial,
        < 55 => TaskComplexityLevel.Standard,
        < 80 => TaskComplexityLevel.Demanding,
        _ => TaskComplexityLevel.Critical,
    };

    private static double WeightedAverage<T>((T Sample, double Similarity)[] values, Func<(T Sample, double Similarity), double> selector)
    {
        var weight = values.Sum(v => v.Similarity * v.Similarity);
        return values.Sum(v => selector(v) * v.Similarity * v.Similarity) / weight;
    }

    private static bool Same(string? left, string? right) => !string.IsNullOrWhiteSpace(left)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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
