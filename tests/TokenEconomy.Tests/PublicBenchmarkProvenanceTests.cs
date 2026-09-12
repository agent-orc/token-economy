using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class PublicBenchmarkProvenanceTests
{
    [Fact]
    public void PublicSnapshotRetainsMeasuredCountsIntervalsCostAndUnknownRunner()
    {
        var result = BenchmarkEvidenceCatalog.Default.Results.Single(r =>
            r.Id == "tb40-official-claude-opus-5-max-2026-09-12");

        Assert.Equal(51.82m, result.Score);
        Assert.Equal(EffortLevel.Max, result.ReasoningEffort);
        var context = Assert.IsType<BenchmarkEvidenceContext>(result.Context);
        Assert.Equal("officialLeaderboard", context.SourceKind);
        Assert.Null(context.RunnerOrganization);
        Assert.Equal("firstObservedPublicSnapshot", context.DateBasis);
        Assert.Equal(new DateOnly(2026, 9, 12), result.PublishedAt);
        Assert.Equal(result.PublishedAt, context.ObservedAt);
        Assert.Equal(66, context.TaskCount);
        Assert.Equal(330, context.TrialCount);
        Assert.Null(context.AttemptsPerTask);
        Assert.Equal(0.95m, context.ConfidenceIntervalLevel);
        Assert.Equal(3.39m, context.ConfidenceIntervalHalfWidth);
        Assert.Equal(5969.11m, context.TotalCostUsd);
        Assert.Equal(18.088212m, result.SecondaryMetrics?.CostPerTaskUsd);
        Assert.NotNull(context.SourceCreatedAt);
        Assert.NotNull(context.SourceUpdatedAt);
    }

    [Fact]
    public void SourceAndHarnessDefinitionsKeepProviderAndOwnerResultsSeparate()
    {
        var catalog = BenchmarkEvidenceCatalog.Default;
        Assert.NotEmpty(catalog.ResultsFor("terminal-bench-v4.0"));
        var owner = catalog.ResultsFor("terminal-bench-v4.0-official-native-agents");
        Assert.Contains(owner, r => r.ModelId.Value == "claude-opus-5");
        Assert.Contains(owner, r => r.ModelId.Value == "claude-fable-5-1");
        Assert.Contains(owner, r => r.ModelId.Value == "gpt-6-astra" && r.ReasoningEffort == EffortLevel.High);
        Assert.All(owner, r => Assert.Equal("officialLeaderboard", r.Context?.SourceKind));
    }

    [Fact]
    public void DatedIndependentAssessmentKeepsPublicationDateAndTrialAccounting()
    {
        var result = BenchmarkEvidenceCatalog.Default.Results.Single(r =>
            r.Id == "aa-coding-v15-claude-opus-5-max-2026-09-09");
        Assert.Equal(60m, result.Score);
        Assert.Equal(new DateOnly(2026, 9, 9), result.PublishedAt);
        Assert.Equal("publishedDate", result.Context?.DateBasis);
        Assert.Equal("independentEvaluator", result.Context?.SourceKind);
        Assert.Equal(303, result.Context?.TaskCount);
        Assert.Equal(909, result.Context?.TrialCount);
        Assert.Equal(3, result.Context?.AttemptsPerTask);
        Assert.Null(result.Context?.ConfidenceIntervalLevel);
    }

    [Fact]
    public void FirstObservedSnapshotsDoNotAppearInEarlierAsOfResults()
    {
        const string type = "terminal-bench-v4.0-official-native-agents";
        var matrix = ModelBenchmarkMatrix.Default;
        var tokens = new BenchmarkTokenAssumption(100_000, 10_000);
        Assert.Empty(matrix.Build(type, tokens, null,
            new DateTime(2026, 9, 11, 23, 59, 59, DateTimeKind.Utc)).Cells);
        Assert.NotEmpty(matrix.Build(type, tokens, null,
            new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc)).Cells);
    }

    [Fact]
    public void InconsistentFirstObservedDateIsRejected()
    {
        var original = BenchmarkEvidenceCatalog.Default.Results.Single(r =>
            r.Id == "tb40-official-claude-opus-5-max-2026-09-12");
        var changed = original with { PublishedAt = original.PublishedAt.AddDays(-1) };
        Assert.Throws<ArgumentException>(() => new BenchmarkEvidenceCatalog(
            BenchmarkEvidenceCatalog.Default.Types, [changed]));
    }
}
