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
    public void Estimate_InheritsMeasuredCostFromSimilarHistoryAndRubricOnlyChangesConfidence()
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
        Assert.Equal(withoutLlm.Score, withLlm.Score);
        Assert.True(withLlm.Confidence > withoutLlm.Confidence);
        Assert.Equal("routing-rubric-v1", withLlm.LlmRubricVersion);
    }

    [Fact]
    public void Estimate_ExposesAuditablePolicyFeaturesRangesAndMissingHistory()
    {
        var estimate = new TaskComplexityEstimator().Estimate(Card("audit") with
        {
            ExpectedChangedLines = 180,
            ExpectedChangedFiles = ["src/A.cs", "tests/A.Tests.cs"],
            ExpectedRuntimeSubsystems = ["routing", "storage"],
            QuotaAndCostHeadroom = 3,
        });

        Assert.Equal(6, estimate.RoutingFeatures.Count);
        Assert.All(estimate.RoutingFeatures, feature =>
        {
            Assert.InRange(feature.Score, 0, feature.MaximumScore);
            Assert.False(string.IsNullOrWhiteSpace(feature.Evidence));
        });
        Assert.Equal(8, estimate.ExpectedScope.Score);
        Assert.Equal(10, estimate.EmpiricalConfidence.Score);
        Assert.Equal(ComplexityHistoryEvidenceStatus.Missing, estimate.HistoryEvidenceStatus);
        Assert.Contains("No comparable historical cohort", estimate.HistoryEvidence);
        Assert.InRange(estimate.PredictedTokens, estimate.PredictedTokenRange.Lower, estimate.PredictedTokenRange.Upper);
        Assert.InRange(estimate.PredictedDuration, estimate.PredictedDurationRange.Lower, estimate.PredictedDurationRange.Upper);
        Assert.InRange(estimate.PredictedReissues, estimate.PredictedReissueRange.Lower, estimate.PredictedReissueRange.Upper);
        Assert.True(estimate.Confidence < .75);
    }

    [Theory]
    [InlineData(0, TaskComplexityLevel.Trivial, "luna-medium")]
    [InlineData(20, TaskComplexityLevel.Trivial, "luna-medium")]
    [InlineData(21, TaskComplexityLevel.Standard, "terra-medium")]
    [InlineData(50, TaskComplexityLevel.Standard, "terra-medium")]
    [InlineData(51, TaskComplexityLevel.Demanding, "sol-medium")]
    [InlineData(69, TaskComplexityLevel.Demanding, "sol-medium")]
    [InlineData(70, TaskComplexityLevel.Critical, "sol-xhigh")]
    [InlineData(100, TaskComplexityLevel.Critical, "sol-xhigh")]
    public void Estimate_BoundaryScoresMatchAuthoritativeBands(int score, TaskComplexityLevel band, string route)
    {
        var estimate = new TaskComplexityEstimator().Estimate(ScoredCard($"boundary-{score}", score));
        Assert.Equal(score, estimate.Score);
        Assert.Equal(band, estimate.ComplexityBand);
        Assert.Equal(route, estimate.RecommendedRouteId);
    }

    [Theory]
    [InlineData("p0", "correctnessCritical", "sol-xhigh")]
    [InlineData("fencing", "correctnessCritical", "sol-xhigh")]
    [InlineData("leaseOwnership", "correctnessCritical", "sol-xhigh")]
    [InlineData("staleWriteRejection", "correctnessCritical", "sol-xhigh")]
    [InlineData("distributedAuthority", "correctnessCritical", "sol-xhigh")]
    [InlineData("securityBoundary", "correctnessCritical", "sol-xhigh")]
    [InlineData("credibleDataLoss", "correctnessCritical", "sol-xhigh")]
    [InlineData("publicProtocol", "broadContract", "sol-medium")]
    [InlineData("persistentStateMigration", "broadContract", "sol-medium")]
    [InlineData("threeOrMoreRuntimeSubsystems", "broadContract", "sol-medium")]
    [InlineData("unclearBug", "unclearBug", "terra-medium")]
    public void Estimate_EmitsEveryExplicitHardFloor(string trigger, string floor, string route)
    {
        var estimate = new TaskComplexityEstimator().Estimate(ScoredCard($"floor-{trigger}", 0) with
        {
            HardFloorTriggers = [trigger],
            QuotaAndCostHeadroom = 0,
        });

        Assert.Contains(trigger, estimate.HardFloorTriggers);
        Assert.Contains(floor, estimate.AppliedHardFloors);
        Assert.Equal(route, estimate.RecommendedRouteId);
    }

    [Fact]
    public void Estimate_ExcludesEveryAttemptFromTheEvaluatedCardBeforeNeighbourSelection()
    {
        var target = Card("held-out");
        var estimate = new TaskComplexityEstimator().Estimate(target,
        [
            Sample(target, 9_000_000, 8),
            Sample(target, 8_000_000, 7),
            Sample(Card("other"), 42_000, 0),
        ]);

        var neighbour = Assert.Single(estimate.Neighbours);
        Assert.Equal("other", neighbour.TaskKey);
        Assert.Equal(42_000, estimate.PredictedTokens);
    }

    [Fact]
    public void Estimate_RecognizesSufficientFavorableHistoricalEvidence()
    {
        var history = Enumerable.Range(0, 20).Select(index => Sample(Card($"good-{index}"), 25_000 + index, 0) with
        {
            FinalGrade = index % 5 == 0 ? "B" : "A",
            SemanticReissueEvidenceAvailable = true,
        }).ToArray();

        var estimate = new TaskComplexityEstimator().Estimate(Card("target"), history);

        Assert.Equal(0, estimate.EmpiricalConfidence.Score);
        Assert.Equal(ComplexityHistoryEvidenceStatus.Sufficient, estimate.HistoryEvidenceStatus);
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
        Assert.Equal(0, report.HeldOutNeighbourLeakageCount);
        Assert.All(report.Rows, row => Assert.DoesNotContain(row.TaskKey, row.NeighbourTaskKeys));
    }

    [Fact]
    public void Backtest_HeldOutRowsExerciseEveryBoundaryAndHardFloor()
    {
        var boundaries = new[] { 0, 20, 21, 50, 51, 69, 70, 100 }
            .Select(score => Sample(ScoredCard($"boundary-{score}", score), 10_000 + score * 1_000, 0));
        var triggers = new[]
        {
            "p0", "fencing", "leaseOwnership", "staleWriteRejection", "distributedAuthority", "securityBoundary", "credibleDataLoss",
            "publicProtocol", "persistentStateMigration", "threeOrMoreRuntimeSubsystems", "unclearBug",
        }.Select(trigger => Sample(ScoredCard($"floor-{trigger}", 0) with { HardFloorTriggers = [trigger] }, 20_000, 0));

        var report = ComplexityBacktester.Run(boundaries.Concat(triggers).ToArray());

        Assert.Equal(0, report.HeldOutNeighbourLeakageCount);
        Assert.Equal([0d, 20d, 21d, 50d, 51d, 69d, 70d, 100d],
            report.Rows.Where(row => row.TaskKey.StartsWith("boundary-", StringComparison.Ordinal)).Select(row => row.EstimatedScore).Order().ToArray());
        Assert.Equal(11, report.Rows.Count(row => row.TaskKey.StartsWith("floor-", StringComparison.Ordinal) && row.AppliedHardFloors.Count > 0));
    }

    [Fact]
    public void ImportedRunsRetainCalibrationFeatures()
    {
        using var json = System.Text.Json.JsonDocument.Parse("""
        { "id":"TE-7", "run":2, "project":"Token-Economy", "area":"routing", "model":"gpt-5.6-terra",
          "prompt":"Implement the estimator", "taskType":"feature", "acceptanceCriteria":["backtest","store"],
          "referencedFiles":["src/A.cs"], "referencedSubsystems":["routing"], "dependencyFanOut":3,
          "upfrontComplexity":{"expectedChangedLines":120,"expectedChangedFiles":["src/A.cs"],
            "expectedRuntimeSubsystems":["routing","storage"],"hardFloorTriggers":["publicProtocol"],
            "routingFeatures":{"correctnessRisk":{"score":24,"evidence":"public API"}}},
          "changedLines":9000,"changedFiles":["eventual/leak.cs"],
          "repositoryFileCount":900, "startedAt":"2026-07-23T20:00:00Z", "completedAt":"2026-07-23T20:30:00Z",
          "tokenSummary":{"inputTokens":100000,"outputTokens":20000} }
        """);
        var record = new AgentStudioTaskStorageImporter().Parse(json.RootElement);
        var sample = Assert.Single(ComplexityHistory.FromRunRecords([record]));
        Assert.Equal("Implement the estimator", sample.Card.Prompt);
        Assert.Equal(2, sample.Card.AcceptanceCriteria.Count);
        Assert.Equal(120_000, sample.ActualTokens);
        Assert.Equal(120, sample.Card.ExpectedChangedLines);
        Assert.Equal(["src/A.cs"], sample.Card.ExpectedChangedFiles);
        Assert.DoesNotContain("eventual/leak.cs", sample.Card.ExpectedChangedFiles);
        Assert.Equal(24, sample.Card.RoutingOverrides.CorrectnessRisk!.Score);
        Assert.Contains("publicProtocol", sample.Card.HardFloorTriggers);
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

    private static ComplexityCard ScoredCard(string key, int total)
    {
        var remaining = total;
        ComplexityCriterionOverride Take(int maximum, string criterion)
        {
            var score = Math.Min(remaining, maximum);
            remaining -= score;
            return new(score, $"deterministic {criterion} boundary fixture");
        }
        var card = Card(key) with
        {
            RoutingOverrides = new ComplexityRoutingOverrides
            {
                CorrectnessRisk = Take(35, "risk"),
                ExpectedScope = Take(20, "scope"),
                ContextDemand = Take(20, "context"),
                TaskTypeAndUncertainty = Take(10, "uncertainty"),
                EmpiricalConfidence = Take(10, "empirical"),
                QuotaAndCostHeadroom = Take(5, "quota"),
            },
        };
        Assert.Equal(0, remaining);
        return card;
    }

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
