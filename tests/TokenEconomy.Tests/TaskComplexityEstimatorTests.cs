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
        Assert.Equal(6, new[]
        {
            huge.CorrectnessRisk.Evidence, huge.ExpectedScope.Evidence, huge.ContextDemand.Evidence,
            huge.TaskUncertainty.Evidence, huge.EmpiricalConfidence.Evidence, huge.QuotaAndCostHeadroom.Evidence,
        }.Count(value => !string.IsNullOrWhiteSpace(value)));
        Assert.False(string.IsNullOrWhiteSpace(huge.ScoreEvidence));
        Assert.False(string.IsNullOrWhiteSpace(huge.ConfidenceEvidence));
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
        Assert.Equal(withoutLlm.Score, withLlm.Score);
        Assert.True(withLlm.Confidence > withoutLlm.Confidence);
        Assert.Equal("routing-rubric-v1", withLlm.LlmRubricVersion);
        Assert.Equal(withoutLlm.PredictedTokens, withoutLlm.TokenForecast.Expected);
        Assert.InRange(withoutLlm.PredictedTokens, withoutLlm.TokenForecast.LowerBound, withoutLlm.TokenForecast.UpperBound);
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
        Assert.InRange(report.LevelAccuracy!.Value, 0, 1);
        Assert.InRange(report.TokenMedianAbsolutePercentageError!.Value, 0, 1);
        Assert.InRange(report.ReissueMeanAbsoluteError!.Value, 0, 2);
        Assert.InRange(report.TokenRankCorrelation!.Value, -1, 1);
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

    [Fact]
    public void Estimate_ExcludesEveryAttemptDerivedSampleForEvaluatedCardBeforeNeighbourSelection()
    {
        var card = Card("TE-27");
        var estimate = new TaskComplexityEstimator().Estimate(card,
        [
            Sample(card with { TaskKey = "te-27" }, 9_000_000, 9),
            Sample(card, 8_000_000, 8),
            Sample(Card("safe-neighbour"), 40_000, 0),
        ]);

        var neighbour = Assert.Single(estimate.Neighbours);
        Assert.Equal("safe-neighbour", neighbour.TaskKey);
        Assert.Equal(40_000, estimate.PredictedTokens);
    }

    [Theory]
    [InlineData(20, TaskComplexityLevel.Trivial)]
    [InlineData(21, TaskComplexityLevel.Standard)]
    [InlineData(50, TaskComplexityLevel.Standard)]
    [InlineData(51, TaskComplexityLevel.Demanding)]
    [InlineData(69, TaskComplexityLevel.Demanding)]
    [InlineData(70, TaskComplexityLevel.Critical)]
    public void Estimate_UsesCanonicalBoundaryScores(int score, TaskComplexityLevel expected)
    {
        // With no comparable history empirical uncertainty contributes the canonical 10 points.
        var remaining = score - 10;
        var correctness = Math.Min(35, remaining);
        remaining -= (int)correctness;
        var scope = Math.Min(20, remaining);
        remaining -= (int)scope;
        var context = Math.Min(20, remaining);
        remaining -= (int)context;
        var uncertainty = Math.Min(10, remaining);
        remaining -= (int)uncertainty;
        var estimate = new TaskComplexityEstimator().Estimate(Card($"boundary-{score}") with
        {
            Prompt = "Deterministic boundary fixture.",
            TaskType = "chore",
            AcceptanceCriteria = [],
            ReferencedFiles = [],
            ReferencedSubsystems = [],
            RoutingSignals = new ComplexityRoutingSignals
            {
                CorrectnessRisk = correctness,
                ExpectedScope = scope,
                ContextDemand = context,
                TaskUncertainty = uncertainty,
                QuotaAndCostHeadroom = remaining,
            },
        });

        Assert.Equal(score, estimate.Score);
        Assert.Equal(expected, estimate.ComplexityBand);
    }

    [Theory]
    [InlineData(ComplexityHardFloorTrigger.P0, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.Fencing, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.LeaseOwnership, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.StaleWriteRejection, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.DistributedAuthority, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.SecurityBoundary, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.CredibleDataLoss, TaskComplexityLevel.Critical)]
    [InlineData(ComplexityHardFloorTrigger.PublicProtocol, TaskComplexityLevel.Demanding)]
    [InlineData(ComplexityHardFloorTrigger.PersistentStateMigration, TaskComplexityLevel.Demanding)]
    [InlineData(ComplexityHardFloorTrigger.ThreeOrMoreRuntimeSubsystems, TaskComplexityLevel.Demanding)]
    [InlineData(ComplexityHardFloorTrigger.UnclearBug, TaskComplexityLevel.Standard)]
    [InlineData(ComplexityHardFloorTrigger.DestructiveOrSecurityCriticalBoundedDecision, TaskComplexityLevel.Demanding)]
    public void Estimate_AppliesAndExplainsEveryHardFloor(
        ComplexityHardFloorTrigger trigger,
        TaskComplexityLevel minimumBand)
    {
        var estimate = new TaskComplexityEstimator().Estimate(Card("floor") with
        {
            Prompt = "Mechanical fixture.", TaskType = "chore", AcceptanceCriteria = [],
            ReferencedFiles = [], ReferencedSubsystems = [],
            RoutingSignals = new ComplexityRoutingSignals
            {
                CorrectnessRisk = 0, ExpectedScope = 0, ContextDemand = 0,
                TaskUncertainty = 0, QuotaAndCostHeadroom = 0,
            },
            HardFloorTriggers = [trigger],
        });

        var floor = Assert.Single(estimate.HardFloors);
        Assert.Equal(trigger, floor.Trigger);
        Assert.Equal(minimumBand, floor.MinimumBand);
        Assert.True(estimate.Level >= minimumBand);
        Assert.False(string.IsNullOrWhiteSpace(floor.Evidence));
    }

    [Fact]
    public void Estimate_LeavesMissingHistoryAndMeasurementsVisible()
    {
        var incomplete = Sample(Card("history"), 0, 0) with
        {
            TokenHistoryComplete = false,
            DurationHistoryComplete = false,
            ReissueHistoryAvailable = false,
        };

        var estimate = new TaskComplexityEstimator().Estimate(Card("target"), [incomplete]);

        Assert.Equal(1, estimate.HistoricalEvidence.ComparableCards);
        Assert.Equal(0, estimate.HistoricalEvidence.TokenCompleteCards);
        Assert.Equal(0, estimate.HistoricalEvidence.DurationCompleteCards);
        Assert.Equal(0, estimate.HistoricalEvidence.ReissueAvailableCards);
        Assert.Equal(6, estimate.EmpiricalConfidence.Score);
        Assert.Null(Assert.Single(estimate.Neighbours).ActualTokens);
        Assert.Contains("Missing measurements", estimate.HistoricalEvidence.Evidence);
        Assert.True(estimate.TokenForecast.UpperBound > estimate.TokenForecast.LowerBound);
    }

    [Fact]
    public void Estimate_UsesCanonicalEmpiricalConfidenceAnchors()
    {
        var card = Card("target");
        var favorable = Enumerable.Range(1, 20).Select(index => Sample(Card($"good-{index}"), 50_000, 0) with
        {
            KnownGradeCount = 1,
            FavorableGradeCount = 1,
            SemanticReissueCount = 0,
        }).ToArray();
        var unfavorable = Enumerable.Range(1, 3).Select(index => Sample(Card($"bad-{index}"), 50_000, 1) with
        {
            KnownGradeCount = 1,
            FavorableGradeCount = 0,
            SemanticReissueCount = 1,
        }).ToArray();
        var estimator = new TaskComplexityEstimator();

        Assert.Equal(10, estimator.Estimate(card).EmpiricalConfidence.Score);
        Assert.Equal(3, estimator.Estimate(card, favorable.Take(5)).EmpiricalConfidence.Score);
        Assert.Equal(0, estimator.Estimate(card, favorable).EmpiricalConfidence.Score);
        Assert.Equal(10, estimator.Estimate(card, unfavorable).EmpiricalConfidence.Score);
        Assert.True(estimator.Estimate(card).Confidence < .5);
    }

    [Fact]
    public void HeldOutBacktest_RejectsAnyEvaluatedCardInTrainingHistory()
    {
        var evaluated = Sample(Card("held-out"), 50_000, 0);
        var error = Assert.Throws<ArgumentException>(() => ComplexityBacktester.RunHeldOut(
            [Sample(Card("training"), 40_000, 0), Sample(Card("HELD-OUT"), 30_000, 0)],
            [evaluated]));

        Assert.Contains("evaluated card", error.Message);
    }

    [Fact]
    public void HeldOutBacktest_ReportsMissingOutcomeCoverageInsteadOfZeroMetrics()
    {
        var evaluation = Sample(Card("held-out"), 0, 0) with
        {
            TokenHistoryComplete = false,
            DurationHistoryComplete = false,
            ReissueHistoryAvailable = false,
        };

        var report = ComplexityBacktester.RunHeldOut([Sample(Card("training"), 40_000, 0)], [evaluation]);

        Assert.Equal(0, report.BandEvaluationCount);
        Assert.Equal(0, report.TokenEvaluationCount);
        Assert.Equal(0, report.ReissueEvaluationCount);
        Assert.Null(report.LevelAccuracy);
        Assert.Null(report.TokenMedianAbsolutePercentageError);
        Assert.Null(report.ReissueMeanAbsoluteError);
        Assert.Null(report.TokenRankCorrelation);
    }

    [Fact]
    public void HeldOutBacktest_AcceptsDeterministicBoundaryAndHardFloorFixtures()
    {
        var boundaryCards = new[] { 20, 21, 50, 51, 69, 70 }.Select(score =>
        {
            var remaining = score - 10;
            var correctness = Math.Min(35, remaining); remaining -= correctness;
            var scope = Math.Min(20, remaining); remaining -= scope;
            var context = Math.Min(20, remaining); remaining -= context;
            var uncertainty = Math.Min(10, remaining); remaining -= uncertainty;
            return Card($"held-boundary-{score}") with
            {
                Prompt = "Held-out deterministic fixture.", TaskType = "chore",
                AcceptanceCriteria = [], ReferencedFiles = [], ReferencedSubsystems = [],
                RoutingSignals = new ComplexityRoutingSignals
                {
                    CorrectnessRisk = correctness, ExpectedScope = scope, ContextDemand = context,
                    TaskUncertainty = uncertainty, QuotaAndCostHeadroom = remaining,
                },
            };
        });
        var floorCards = Enum.GetValues<ComplexityHardFloorTrigger>().Select(trigger => Card($"held-floor-{trigger}") with
        {
            Prompt = "Held-out deterministic fixture.", TaskType = "chore",
            AcceptanceCriteria = [], ReferencedFiles = [], ReferencedSubsystems = [],
            RoutingSignals = new ComplexityRoutingSignals
            {
                CorrectnessRisk = 0, ExpectedScope = 0, ContextDemand = 0,
                TaskUncertainty = 0, QuotaAndCostHeadroom = 0,
            },
            HardFloorTriggers = [trigger],
        });
        var evaluation = boundaryCards.Concat(floorCards).Select(card => Sample(card, 50_000, 0)).ToArray();

        var training = Card("training") with { Project = "Other", Area = "other", TaskType = "other" };
        var report = ComplexityBacktester.RunHeldOut([Sample(training, 40_000, 0)], evaluation);

        Assert.Equal(6 + Enum.GetValues<ComplexityHardFloorTrigger>().Length, report.SampleCount);
        Assert.All(boundaryCards, card => Assert.Equal(
            int.Parse(card.TaskKey.Split('-')[^1]),
            new TaskComplexityEstimator().Estimate(card).Score));
        Assert.All(floorCards, card => Assert.NotEmpty(new TaskComplexityEstimator().Estimate(card).HardFloors));
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
        TaskPrompt = "Implement the estimator", Usage = new TokenUsage(tokens, 0), TokenUsageAvailable = true,
        StartedAtUtc = observedAt.AddMinutes(-10), ExecutedAtUtc = observedAt,
        ObservedAtUtc = observedAt, CostStatus = PriceStatus.Resolved, Outcome = OutcomeQualitySignal.Successful,
    };
}
