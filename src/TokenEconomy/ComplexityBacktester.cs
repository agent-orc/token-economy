#pragma warning disable CS1591
namespace TokenEconomy;

public sealed record ComplexityBacktestResult(
    int SampleCount,
    int BandEvaluationCount,
    int TokenEvaluationCount,
    int ReissueEvaluationCount,
    double? LevelAccuracy,
    double? TokenMedianAbsolutePercentageError,
    double? ReissueMeanAbsoluteError,
    double? TokenRankCorrelation);

/// <summary>Attempt-level input coverage reported before history is collapsed into backtest samples.</summary>
public sealed record ComplexityBacktestCoverage(
    int TaskCount,
    int AttemptCount,
    int AttemptLevelRouteCount,
    decimal TokenCoverage,
    decimal DurationCoverage,
    decimal SemanticReissueCoverage);

/// <summary>Leakage-safe leave-one-card-out and explicit held-out evaluation.</summary>
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

    /// <summary>Leave one entire card out. Attempt-derived rows for the evaluated key cannot enter history.</summary>
    public static ComplexityBacktestResult Run(
        IReadOnlyList<ComplexityHistorySample> samples,
        TaskComplexityEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 2) throw new ArgumentException("Backtesting requires at least two historical cards.", nameof(samples));
        EnsureUniqueCards(samples, nameof(samples));
        estimator ??= new TaskComplexityEstimator();
        var rows = samples.Select((sample, index) => Evaluate(
            sample,
            estimator.Estimate(sample.Card, samples.Where((_, other) => other != index)))).ToArray();
        return Summarize(rows);
    }

    /// <summary>
    /// Evaluates an explicit temporal/project/scenario holdout. Training and evaluation keys must be
    /// disjoint, which prevents any attempt of an evaluated card from leaking through aggregation.
    /// </summary>
    public static ComplexityBacktestResult RunHeldOut(
        IReadOnlyList<ComplexityHistorySample> training,
        IReadOnlyList<ComplexityHistorySample> evaluation,
        TaskComplexityEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(evaluation);
        if (training.Count == 0) throw new ArgumentException("Held-out backtesting requires training cards.", nameof(training));
        if (evaluation.Count == 0) throw new ArgumentException("Held-out backtesting requires evaluation cards.", nameof(evaluation));
        EnsureUniqueCards(training, nameof(training));
        EnsureUniqueCards(evaluation, nameof(evaluation));
        var trainingKeys = training.Select(sample => sample.Card.TaskKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = evaluation.Select(sample => sample.Card.TaskKey).Where(trainingKeys.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        if (overlap.Length > 0)
            throw new ArgumentException("Held-out training history contains evaluated card key(s): " + string.Join(", ", overlap), nameof(training));
        estimator ??= new TaskComplexityEstimator();
        return Summarize(evaluation.Select(sample => Evaluate(sample, estimator.Estimate(sample.Card, training))).ToArray());
    }

    private static BacktestRow Evaluate(ComplexityHistorySample sample, TaskComplexityEstimate estimate)
    {
        var completeBand = sample.TokenHistoryComplete && sample.DurationHistoryComplete && sample.ReissueHistoryAvailable;
        TaskComplexityLevel? actualLevel = completeBand ? ActualLevel(sample.ActualTokens, sample.ActualDuration, sample.ReissueCount) : null;
        double? percentageError = sample.TokenHistoryComplete
            ? Math.Abs(estimate.PredictedTokens - sample.ActualTokens) / (double)Math.Max(1, sample.ActualTokens)
            : null;
        return new(estimate, sample, actualLevel, percentageError);
    }

    private static ComplexityBacktestResult Summarize(BacktestRow[] rows)
    {
        var bandRows = rows.Where(row => row.ActualLevel is not null).ToArray();
        var tokenRows = rows.Where(row => row.PercentageError is not null).ToArray();
        var reissueRows = rows.Where(row => row.Sample.ReissueHistoryAvailable).ToArray();
        return new(
            rows.Length,
            bandRows.Length,
            tokenRows.Length,
            reissueRows.Length,
            bandRows.Length == 0 ? null : Round(bandRows.Count(row => row.Estimate.Level == row.ActualLevel) / (double)bandRows.Length),
            tokenRows.Length == 0 ? null : Round(Median(tokenRows.Select(row => row.PercentageError!.Value))),
            reissueRows.Length == 0 ? null : Round(reissueRows.Average(row => Math.Abs(row.Estimate.PredictedReissues - row.Sample.ReissueCount))),
            tokenRows.Length < 2 ? null : Round(Spearman(
                tokenRows.Select(row => (double)row.Estimate.PredictedTokens).ToArray(),
                tokenRows.Select(row => (double)row.Sample.ActualTokens).ToArray())));
    }

    private static void EnsureUniqueCards(IReadOnlyList<ComplexityHistorySample> samples, string parameterName)
    {
        if (samples.Select(sample => sample.Card.TaskKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != samples.Count)
            throw new ArgumentException("Backtesting requires one aggregated sample per task key.", parameterName);
    }

    private static TaskComplexityLevel ActualLevel(long tokens, TimeSpan duration, int reissues)
    {
        var score = Math.Min(100, Math.Max(0, Math.Log10(Math.Max(1, tokens) / 10_000d) * 28
            + Math.Log2(1 + Math.Max(0, duration.TotalHours)) * 7 + reissues * 12));
        return score switch
        {
            <= 20 => TaskComplexityLevel.Trivial,
            <= 50 => TaskComplexityLevel.Standard,
            <= 69 => TaskComplexityLevel.Demanding,
            _ => TaskComplexityLevel.Critical,
        };
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
        var denominator = Math.Sqrt(x.Sum(value => Math.Pow(value - meanX, 2)) * y.Sum(value => Math.Pow(value - meanY, 2)));
        return denominator == 0 ? 0 : covariance / denominator;
    }

    private static double[] Ranks(double[] values)
    {
        var result = new double[values.Length];
        foreach (var group in values.Select((value, index) => (value, index)).OrderBy(item => item.value)
                     .Select((item, rank) => (item.value, item.index, rank)).GroupBy(item => item.value))
        {
            var rank = group.Average(item => item.rank) + 1;
            foreach (var item in group) result[item.index] = rank;
        }
        return result;
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
    private sealed record BacktestRow(TaskComplexityEstimate Estimate, ComplexityHistorySample Sample, TaskComplexityLevel? ActualLevel, double? PercentageError);
}
