#pragma warning disable CS1591
namespace TokenEconomy;

public sealed record ComplexityBacktestResult(
    int SampleCount,
    double LevelAccuracy,
    double TokenMedianAbsolutePercentageError,
    double ReissueMeanAbsoluteError,
    double TokenRankCorrelation)
{
    public int HeldOutNeighbourLeakageCount { get; init; }
    public IReadOnlyList<ComplexityBacktestRow> Rows { get; init; } = [];
}

/// <summary>Auditable held-out row, including the exact neighbours visible to that estimate.</summary>
public sealed record ComplexityBacktestRow(
    string TaskKey,
    double EstimatedScore,
    TaskComplexityLevel EstimatedLevel,
    TaskComplexityLevel ActualLevel,
    IReadOnlyList<string> NeighbourTaskKeys,
    IReadOnlyList<string> HardFloorTriggers,
    IReadOnlyList<string> AppliedHardFloors,
    ComplexityHistoryEvidenceStatus HistoryEvidenceStatus);

/// <summary>Attempt-level input coverage reported before history is collapsed into backtest samples.</summary>
public sealed record ComplexityBacktestCoverage(
    int TaskCount,
    int AttemptCount,
    int AttemptLevelRouteCount,
    decimal TokenCoverage,
    decimal DurationCoverage,
    decimal SemanticReissueCoverage);

/// <summary>Leave-one-out backtest: every card is estimated using only the other historical cards.</summary>
public static class ComplexityBacktester
{
    /// <summary>
    /// Describes whether imported attempt history is complete enough for calibration. Missing
    /// telemetry stays in the denominator and is not converted to a measured zero.
    /// </summary>
    public static ComplexityBacktestCoverage MeasureCoverage(IEnumerable<AgentStudioRunRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var attempts = records.GroupBy(record => (record.TaskKey, record.Run))
            .Select(group => group.OrderByDescending(record => record.ObservedAtUtc).First()).ToArray();
        var count = attempts.Length;
        static decimal Coverage(int available, int total) => total == 0 ? 0
            : Math.Round((decimal)available / total, 6, MidpointRounding.AwayFromZero);
        return new(
            attempts.Select(record => record.TaskKey).Distinct(StringComparer.Ordinal).Count(), count,
            attempts.Count(record => record.RouteGranularity == AgentStudioRouteGranularity.Attempt),
            Coverage(attempts.Count(record => record.TokenUsageAvailable), count),
            Coverage(attempts.Count(record => record.StartedAtUtc is { } started && record.ExecutedAtUtc >= started), count),
            Coverage(attempts.Count(record => record.SemanticReissue is not null), count));
    }

    public static ComplexityBacktestResult Run(
        IReadOnlyList<ComplexityHistorySample> samples,
        TaskComplexityEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 2) throw new ArgumentException("Backtesting requires at least two historical cards.", nameof(samples));
        if (samples.Select(sample => sample.Card.TaskKey).Distinct(StringComparer.Ordinal).Count() != samples.Count)
            throw new ArgumentException("Backtesting requires one aggregated sample per task key.", nameof(samples));
        estimator ??= new TaskComplexityEstimator();
        var rows = samples.Select(sample =>
        {
            var estimate = estimator.Estimate(sample.Card,
                samples.Where(other => !string.Equals(other.Card.TaskKey, sample.Card.TaskKey, StringComparison.Ordinal)));
            var actualLevel = ActualLevel(sample.ActualTokens, sample.ActualDuration, sample.ReissueCount);
            var percentageError = Math.Abs(estimate.PredictedTokens - sample.ActualTokens) / (double)Math.Max(1, sample.ActualTokens);
            return (Estimate: estimate, Sample: sample, ActualLevel: actualLevel, PercentageError: percentageError);
        }).ToArray();

        var backtestRows = rows.Select(row => new ComplexityBacktestRow(
            row.Sample.Card.TaskKey, row.Estimate.Score, row.Estimate.Level, row.ActualLevel,
            row.Estimate.Neighbours.Select(neighbour => neighbour.TaskKey).ToArray(),
            row.Estimate.HardFloorTriggers, row.Estimate.AppliedHardFloors,
            row.Estimate.HistoryEvidenceStatus)).ToArray();
        return new(
            rows.Length,
            Round(rows.Count(r => r.Estimate.Level == r.ActualLevel) / (double)rows.Length),
            Round(Median(rows.Select(r => r.PercentageError))),
            Round(rows.Average(r => Math.Abs(r.Estimate.PredictedReissues - r.Sample.ReissueCount))),
            Round(Spearman(rows.Select(r => (double)r.Estimate.PredictedTokens).ToArray(), rows.Select(r => (double)r.Sample.ActualTokens).ToArray())))
        {
            HeldOutNeighbourLeakageCount = backtestRows.Sum(row => row.NeighbourTaskKeys.Count(key => string.Equals(key, row.TaskKey, StringComparison.Ordinal))),
            Rows = backtestRows,
        };
    }

    private static TaskComplexityLevel ActualLevel(long tokens, TimeSpan duration, int reissues)
    {
        var score = Math.Min(100, Math.Max(0, Math.Log10(Math.Max(1, tokens) / 10_000d) * 28
            + Math.Log2(1 + Math.Max(0, duration.TotalHours)) * 7 + reissues * 12));
        return score switch { <= 20 => TaskComplexityLevel.Trivial, <= 50 => TaskComplexityLevel.Standard, <= 69 => TaskComplexityLevel.Demanding, _ => TaskComplexityLevel.Critical };
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
    }

    private static double Spearman(double[] predicted, double[] actual)
    {
        var x = Ranks(predicted); var y = Ranks(actual);
        var meanX = x.Average(); var meanY = y.Average();
        var covariance = x.Zip(y).Sum(pair => (pair.First - meanX) * (pair.Second - meanY));
        var denominator = Math.Sqrt(x.Sum(v => Math.Pow(v - meanX, 2)) * y.Sum(v => Math.Pow(v - meanY, 2)));
        return denominator == 0 ? 0 : covariance / denominator;
    }

    private static double[] Ranks(double[] values)
    {
        var result = new double[values.Length];
        foreach (var group in values.Select((value, index) => (value, index)).OrderBy(x => x.value).Select((x, rank) => (x.value, x.index, rank)).GroupBy(x => x.value))
        {
            var rank = group.Average(x => x.rank) + 1;
            foreach (var item in group) result[item.index] = rank;
        }
        return result;
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
