using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class TaskComplexityEstimatorTests
{
    [Fact]
    public void Estimate_UsesCardSignalsAndKeepsRepositorySizeIndirect()
    {
        var estimator = new TaskComplexityEstimator();
        var small = estimator.Estimate(Card("small") with { RepositoryFileCount = 100 });
        var huge = estimator.Estimate(Card("huge") with { RepositoryFileCount = 1_000_000 });
        Assert.Equal(small.Level, huge.Level);
        Assert.InRange(huge.Score - small.Score, 0, 2);
        Assert.Equal(6, huge.Dimensions.Count);
        Assert.InRange(huge.Confidence, 0, 1);
    }

    [Fact]
    public void Estimate_InheritsMeasuredCostFromSimilarHistoryAndCanBlendLlmRubric()
    {
        var history = new[]
        {
            Sample(Card("near-1"), 420_000, 2),
            Sample(Card("near-2"), 380_000, 1),
            Sample(Card("different") with { Project = "Other", Area = "UI", TaskType = "docs" }, 8_000, 0),
        };
        var estimator = new TaskComplexityEstimator();
        var withoutLlm = estimator.Estimate(Card("target"), history);
        var withLlm = estimator.Estimate(Card("target"), history, new LlmComplexityAssessment(95, .9, "routing-rubric-v1"));
        Assert.Equal(2, withoutLlm.Neighbours.Count);
        Assert.InRange(withoutLlm.PredictedTokens, 380_000, 420_000);
        Assert.True(withoutLlm.PredictedDuration > TimeSpan.Zero);
        Assert.True(withLlm.Score > withoutLlm.Score);
        Assert.Equal("routing-rubric-v1", withLlm.LlmRubricVersion);
    }

    [Fact]
    public void Estimate_IsVersionedAndUpsertableForRoutingPolicy()
    {
        TaskComplexityEstimationEvent? observed = null;
        var estimator = new TaskComplexityEstimator();
        estimator.EventOccurred += item => observed = item;
        var estimate = estimator.Estimate(Card("TE-7"));
        var store = new InMemoryTaskComplexityEstimateStore();
        store.Upsert(estimate);
        store.Upsert(estimate with { Confidence = .99 });
        var stored = Assert.Single(store.Estimates);
        Assert.Equal(TaskComplexityEstimate.CurrentSchemaVersion, stored.SchemaVersion);
        Assert.Equal(.99, stored.Confidence);
        Assert.Equal("task_complexity.estimated", observed!.Name);
        Assert.Equal("TE-7", observed.Context["taskKey"]);
    }

    [Fact]
    public void Backtest_AppliesLeaveOneOutEstimatorToThirtyCards()
    {
        var samples = Enumerable.Range(0, 30).Select(index =>
        {
            var group = index % 3;
            return Sample(Card($"historic-{index}") with
            {
                Area = $"area-{group}", TaskType = group == 0 ? "fix" : "feature",
                ReferencedSubsystems = [$"system-{group}"],
                Signals = new ComplexitySignals
                {
                    Novelty = group * .35, ConstraintDensity = .2 + group * .25,
                    SpecificationAmbiguity = .15 + group * .2, VerificationCost = .2 + group * .3,
                    RequiredReading = .15 + group * .25,
                },
            }, 20_000 + group * 180_000 + index * 1_000, group);
        }).ToArray();
        var report = ComplexityBacktester.Run(samples);
        Assert.Equal(30, report.SampleCount);
        Assert.InRange(report.LevelAccuracy, 0, 1);
        Assert.InRange(report.TokenMedianAbsolutePercentageError, 0, 1);
        Assert.InRange(report.ReissueMeanAbsoluteError, 0, 2);
        Assert.InRange(report.TokenRankCorrelation, -1, 1);
    }

    [Fact]
    public void ImportedRunsRetainCalibrationFeatures()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
        { "id":"TE-7", "run":2, "project":"Token-Economy", "area":"routing", "model":"gpt-5.6-terra",
          "prompt":"Implement the estimator", "taskType":"feature", "acceptanceCriteria":["backtest","store"],
          "referencedFiles":["src/A.cs"], "referencedSubsystems":["routing"], "dependencyFanOut":3,
          "repositoryFileCount":900, "startedAt":"2026-07-23T20:00:00Z", "completedAt":"2026-07-23T20:30:00Z",
          "tokenSummary":{"inputTokens":100000,"outputTokens":20000} }
        """);
        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);
        var sample = Assert.Single(ComplexityHistory.FromRunRecords([record]));
        Assert.Equal("Implement the estimator", sample.Card.Prompt);
        Assert.Equal(2, sample.Card.AcceptanceCriteria.Count);
        Assert.Equal(120_000, sample.ActualTokens);
        Assert.Equal(1, sample.ReissueCount);
        Assert.Equal(TimeSpan.FromMinutes(30), sample.ActualDuration);
    }

    [Fact]
    public void ImportedRunsAggregateAttemptsIntoOneCardWithoutDoubleCountingDuplicates()
    {
        var first = Run("TE-7", 1, 40_000, DateTime.Parse("2026-07-23T20:00:00Z"));
        var staleDuplicate = first with
        {
            Usage = new TokenUsage(999_000, 0),
            ObservedAtUtc = first.ObservedAtUtc.AddMinutes(-1),
        };
        var second = Run("TE-7", 2, 60_000, DateTime.Parse("2026-07-23T21:00:00Z")) with
        {
            TaskPrompt = null,
        };

        var sample = Assert.Single(ComplexityHistory.FromRunRecords([staleDuplicate, first, second]));

        Assert.Equal(100_000, sample.ActualTokens);
        Assert.Equal(TimeSpan.FromMinutes(20), sample.ActualDuration);
        Assert.Equal(1, sample.ReissueCount);
    }

    [Fact]
    public void Backtest_RejectsMultipleSamplesForTheSameCard()
    {
        var samples = new[] { Sample(Card("same"), 10_000, 0), Sample(Card("same"), 20_000, 1) };
        var error = Assert.Throws<ArgumentException>(() => ComplexityBacktester.Run(samples));
        Assert.Contains("one aggregated sample per task key", error.Message);
    }

    private static ComplexityCard Card(string key) => new()
    {
        TaskKey = key, Project = "Token-Economy", Area = "routing", TaskType = "feature",
        Prompt = "Implement a new routing estimator. Verify the behavior with integration tests.",
        AcceptanceCriteria = ["produce a score", "store confidence"],
        ReferencedFiles = ["src/Estimator.cs", "tests/EstimatorTests.cs"],
        ReferencedSubsystems = ["routing"], DependencyFanOut = 2,
    };

    private static ComplexityHistorySample Sample(ComplexityCard card, long tokens, int reissues) => new()
    {
        Card = card, ActualTokens = tokens, ReissueCount = reissues, ActualDuration = TimeSpan.FromMinutes(tokens / 10_000d),
    };

    private static AgentStudioRunRecord Run(string taskKey, int run, long tokens, DateTime observedAt) => new()
    {
        TaskKey = taskKey, Run = run, Project = "Token-Economy", Model = "gpt-5-mini",
        TaskPrompt = "Implement the estimator", Usage = new TokenUsage(tokens, 0),
        StartedAtUtc = observedAt.AddMinutes(-10), ExecutedAtUtc = observedAt,
        ObservedAtUtc = observedAt, CostStatus = PriceStatus.Resolved, Outcome = OutcomeQualitySignal.Successful,
    };
}
