using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class Fable51SupportTests
{
    private static readonly DateTime At = new(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PricingStartsAtReleaseAndUsesThePublishedCacheReadException()
    {
        var catalog = ModelPriceCatalog.Default;
        var model = KnownModels.ClaudeFable51;
        var price = catalog.ResolvePrice(model, At).Price!;
        Assert.Equal(new DateOnly(2026, 9, 1), catalog.Find(model)!.ReleaseDate);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), price.ValidFrom);
        Assert.Equal((10m, 50m, .25m, 12.5m),
            (price.InputPerMTok, price.OutputPerMTok, price.CacheReadPerMTok, price.CacheWritePerMTok));
        Assert.False(price.Unconfirmed);
        Assert.Equal(PriceStatus.NoPriceForDate,
            catalog.ResolvePrice(model, price.ValidFrom.AddTicks(-1)).Status);
        var cost = catalog.ComputeCost(model, new TokenUsage(0, 0, CacheRead: 1_000_000), At);
        Assert.Equal(.25m, cost.Total);
        Assert.NotEqual(price.InputPerMTok * .1m, cost.CacheReadCost);
        Assert.Empty(catalog.Find(model)!.Aliases);
    }

    [Theory]
    [InlineData(EffortLevel.Low, "low")]
    [InlineData(EffortLevel.Medium, "medium")]
    [InlineData(EffortLevel.High, "high")]
    [InlineData(EffortLevel.XHigh, "xhigh")]
    [InlineData(EffortLevel.Max, "max")]
    public void EvaluationAndPolicyResolutionRetainProvisionalLocalFit(EffortLevel effort, string level)
    {
        var model = KnownModels.ClaudeFable51;
        var result = ModelEfficiencyMatrix.Default.EvaluateModel(model, TaskClass.Feature,
            BudgetPressure.Tight, At, effort);
        Assert.NotNull(result);
        Assert.Equal(effort, result.SuggestedEffort);
        Assert.Equal(Cli.Claude, result.Cli);
        Assert.True(result.Provisional);
        Assert.Equal(PolicyEvidenceStatus.Provisional, result.EvidenceStatus);
        Assert.True(ModelRoutingKnowledgeBase.Default.Resolve(model, level, RoutingWorkflowRole.CoreTask).IsResolved);
        Assert.Null(ModelEfficiencyMatrix.Default.SuitabilityOf(model, TaskClass.Review));
    }

    [Fact]
    public void ClaudeMenuSupportsFableWithoutInventingAutomaticFallbackEquivalence()
    {
        var matrix = ModelEfficiencyMatrix.Default;
        var model = KnownModels.ClaudeFable51;
        Assert.Equal(model.Value, Assert.Single(matrix.SuggestModel(
            TaskClass.Feature, BudgetPressure.Tight, [Cli.Claude], At)).ModelId);
        Assert.Equal(KnownModels.Gpt56Terra.Value, matrix.SuggestModel(
            TaskClass.Feature, BudgetPressure.Tight, [Cli.Claude, Cli.Codex], At)[0].ModelId);
        Assert.DoesNotContain(ModelRoutingKnowledgeBase.Default.Routes, route => route.ModelId == model.Value);
        Assert.DoesNotContain(ModelRoutingKnowledgeBase.Default.ProviderFallbacks, route => route.ModelId == model.Value);
        Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
            advice => Assert.DoesNotContain(advice.Candidates, candidate => candidate.Model == model));
    }
}
