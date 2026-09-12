using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class AstraSupportTests
{
    private static readonly DateTime At = new(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(EffortLevel.Low, "low")]
    [InlineData(EffortLevel.Medium, "medium")]
    [InlineData(EffortLevel.High, "high")]
    [InlineData(EffortLevel.XHigh, "xhigh")]
    [InlineData(EffortLevel.Max, "max")]
    public void TypedEvaluationAndAliasResolutionSupportTheDeclaredLadder(EffortLevel effort, string level)
    {
        var suggestion = ModelEfficiencyMatrix.Default.EvaluateModel(
            KnownModels.Gpt6Astra, TaskClass.Feature, BudgetPressure.Tight, At, effort);
        var resolution = ModelRoutingKnowledgeBase.Default.Resolve("astra", level, RoutingWorkflowRole.CoreTask);

        Assert.NotNull(suggestion);
        Assert.Equal(effort, suggestion.SuggestedEffort);
        Assert.Equal(Cli.Codex, suggestion.Cli);
        Assert.True(suggestion.Provisional);
        Assert.Equal(PolicyEvidenceStatus.Provisional, suggestion.EvidenceStatus);
        Assert.True(resolution.IsResolved, resolution.Reason);
        Assert.Equal(KnownModels.Gpt6Astra.Value, resolution.Model!.CanonicalId);
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("ultra")]
    public void RoutingResolutionRejectsLevelsOutsideAstraLadder(string level)
        => Assert.Equal(ModelRouteResolutionStatus.UnsupportedThinkingLevel,
            ModelRoutingKnowledgeBase.Default.Resolve("astra", level, RoutingWorkflowRole.CoreTask).Status);

    [Fact]
    public void CompatibilityComparisonIncludesAstraWithoutReplacingEstablishedTiePreference()
    {
        var matrix = ModelEfficiencyMatrix.Default;
        var ranked = matrix.SuggestModel(TaskClass.HeavyDesign, BudgetPressure.Tight, [Cli.Codex], At);
        Assert.Contains(ranked, candidate => candidate.ModelId == KnownModels.Gpt6Astra.Value);
        Assert.Equal(KnownModels.Gpt56Sol.Value, ranked[0].ModelId);
        Assert.Null(matrix.SuitabilityOf(KnownModels.Gpt6Astra, TaskClass.Review));
        Assert.Null(matrix.EvaluateModel(KnownModels.Gpt6Astra, TaskClass.Review, BudgetPressure.Tight, At));
    }

    [Fact]
    public void SupportDoesNotInventAutomaticRoutesFallbacksOrTaskClassEvidence()
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        Assert.Equal(new[] { "luna-medium", "terra-medium", "sol-medium", "sol-xhigh" },
            knowledge.Routes.Where(route => route.WorkflowRole == RoutingWorkflowRole.CoreTask)
                .OrderBy(route => route.Rank).Select(route => route.Id));
        Assert.DoesNotContain(knowledge.ProviderFallbacks, route => route.ModelId == KnownModels.Gpt6Astra.Value);
        Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
            recommendation => Assert.DoesNotContain(recommendation.Candidates,
                candidate => candidate.Model == KnownModels.Gpt6Astra));
    }

    [Theory]
    [InlineData("low", true)]
    [InlineData("xhigh", false)]
    public void ExplicitPinSelectsAstraAndRetainsCorrectnessRecommendationAndUncertainty(string level, bool belowPolicy)
    {
        var task = new ComplexityCard
        {
            TaskKey = "astra-support", Prompt = "Repair the security boundary.", TaskType = "feature",
            HardFloorTriggers = [ComplexityHardFloorTrigger.SecurityBoundary],
        };
        var snapshot = new ProviderQuotaDashboardBuilder().BuildSnapshot([], new(
            At, TimeSpan.FromHours(1), TimeSpan.FromMinutes(15),
            [new("openai", "codex", ProviderCliAvailability.Available, At,
                [KnownModels.Gpt6Astra, KnownModels.Gpt56Sol])],
            [new("openai", "codex", "five-hour", 10_000, 100_000, At, At.AddHours(4))]));
        var request = new ModelRoutingSelectionRequest
        {
            Task = task, UpfrontEstimate = new TaskComplexityEstimator().Estimate(task),
            AvailableClis = [Cli.Codex], Capacity = new() { ProviderAvailability = snapshot },
            OperatorPin = new("astra", level),
        };
        var pinned = ModelRouter.Default.Route(request);
        var automatic = ModelRouter.Default.Route(request with { OperatorPin = null });

        Assert.Equal(ModelRoutingDisposition.Selected, pinned.Disposition);
        Assert.Equal(KnownModels.Gpt6Astra.Value, pinned.SelectedRoute!.ModelId);
        Assert.Equal(level, pinned.SelectedRoute.ThinkingLevel);
        Assert.Equal(ModelRouteSelectionSource.OperatorPin, pinned.SelectionSource);
        Assert.True(pinned.SelectedRoute.Provisional);
        Assert.Null(pinned.SelectedRoute.BenchmarkQualification);
        Assert.Equal(belowPolicy, pinned.OperatorPinBelowPolicy);
        Assert.Contains(pinned.Uncertainty.Reasons, reason => reason.Contains("provisional"));
        Assert.Contains(pinned.Uncertainty.Reasons, reason => reason.Contains("gpt-6-astra") && reason.Contains("benchmark"));
        Assert.Equal("sol-xhigh", pinned.CorrectnessFloor.RouteId);
        Assert.Equal("sol-xhigh", pinned.RecommendedRoute.RouteId);
        Assert.Equal("sol-xhigh", automatic.SelectedRoute!.RouteId);
    }
}
