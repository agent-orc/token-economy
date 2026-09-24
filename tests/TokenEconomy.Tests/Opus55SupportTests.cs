using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class Opus55SupportTests
{
    private static readonly DateTime At = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PricingStartsAtReleaseAndDottedAliasResolves()
    {
        var catalog = ModelPriceCatalog.Default;
        var price = catalog.ResolvePrice(KnownModels.ClaudeOpus55, At).Price!;

        Assert.Equal("claude-opus-5-5", catalog.Find("claude-opus-5.5")!.ModelId);
        Assert.Equal((4m, 20m, .20m, 5m),
            (price.InputPerMTok, price.OutputPerMTok, price.CacheReadPerMTok, price.CacheWritePerMTok));
        Assert.False(price.Unconfirmed);
        Assert.Equal(PriceStatus.NoPriceForDate,
            catalog.ResolvePrice(KnownModels.ClaudeOpus55, new DateTime(2026, 9, 21, 23, 59, 59, DateTimeKind.Utc)).Status);
    }

    [Theory]
    [InlineData(EffortLevel.Low, "low")]
    [InlineData(EffortLevel.Medium, "medium")]
    [InlineData(EffortLevel.High, "high")]
    [InlineData(EffortLevel.XHigh, "xhigh")]
    [InlineData(EffortLevel.Max, "max")]
    public void TypedEvaluationAndAliasResolutionSupportTheDocumentedLadder(EffortLevel effort, string level)
    {
        var suggestion = ModelEfficiencyMatrix.Default.EvaluateModel(
            KnownModels.ClaudeOpus55, TaskClass.Feature, BudgetPressure.Tight, At, effort)!;
        var resolution = ModelRoutingKnowledgeBase.Default.Resolve(
            "claude-opus-5.5", level, RoutingWorkflowRole.CoreTask);

        Assert.Equal(effort, suggestion.SuggestedEffort);
        Assert.Equal(Cli.Claude, suggestion.Cli);
        Assert.True(suggestion.Provisional);
        Assert.Equal(PolicyEvidenceStatus.Provisional, suggestion.EvidenceStatus);
        Assert.True(resolution.IsResolved, resolution.Reason);
        Assert.Equal(KnownModels.ClaudeOpus55.Value, resolution.Model!.CanonicalId);
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("ultra")]
    public void ResolutionRejectsLevelsOutsideTheDocumentedLadder(string level)
        => Assert.Equal(ModelRouteResolutionStatus.UnsupportedThinkingLevel,
            ModelRoutingKnowledgeBase.Default.Resolve(
                KnownModels.ClaudeOpus55, level, RoutingWorkflowRole.CoreTask).Status);

    [Fact]
    public void ProvisionalSupportAddsNoAutomaticRouteRecommendationOrFallback()
    {
        var model = KnownModels.ClaudeOpus55;
        var knowledge = ModelRoutingKnowledgeBase.Default;

        Assert.DoesNotContain(knowledge.Routes, route => route.ModelId == model.Value);
        Assert.DoesNotContain(knowledge.ProviderFallbacks, fallback => fallback.ModelId == model.Value);
        Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
            recommendation => Assert.DoesNotContain(recommendation.Candidates,
                candidate => candidate.Model == model));
    }
}
