using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class Gpt6SolLunaSupportTests
{
    private static readonly DateTime At = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, EffortLevel, string> DeclaredLadders => new()
    {
        { KnownModels.Gpt6Sol.Value, EffortLevel.Low, "low" },
        { KnownModels.Gpt6Sol.Value, EffortLevel.Medium, "medium" },
        { KnownModels.Gpt6Sol.Value, EffortLevel.High, "high" },
        { KnownModels.Gpt6Sol.Value, EffortLevel.XHigh, "xhigh" },
        { KnownModels.Gpt6Sol.Value, EffortLevel.Max, "max" },
        { KnownModels.Gpt6Sol.Value, EffortLevel.Ultra, "ultra" },
        { KnownModels.Gpt6Luna.Value, EffortLevel.Low, "low" },
        { KnownModels.Gpt6Luna.Value, EffortLevel.Medium, "medium" },
        { KnownModels.Gpt6Luna.Value, EffortLevel.High, "high" },
        { KnownModels.Gpt6Luna.Value, EffortLevel.XHigh, "xhigh" },
        { KnownModels.Gpt6Luna.Value, EffortLevel.Max, "max" },
    };

    [Theory]
    [InlineData("gpt-6-sol", 2.00, 0.20, 2.50, 10.00)]
    [InlineData("gpt-6-luna", 0.10, 0.01, 0.125, 0.50)]
    public void DatedPricesStartAtRelease(
        string model, double input, double cacheRead, double cacheWrite, double output)
    {
        var catalog = ModelPriceCatalog.Default;
        var price = catalog.ResolvePrice(model, At).Price!;

        Assert.Equal(((decimal)input, (decimal)cacheRead, (decimal)cacheWrite, (decimal)output),
            (price.InputPerMTok, price.CacheReadPerMTok, price.CacheWritePerMTok, price.OutputPerMTok));
        Assert.False(price.Unconfirmed);
        Assert.Equal(PriceStatus.NoPriceForDate,
            catalog.ResolvePrice(model, new DateTime(2026, 9, 21, 23, 59, 59, DateTimeKind.Utc)).Status);
    }

    [Theory]
    [MemberData(nameof(DeclaredLadders))]
    public void TypedEvaluationAndCanonicalResolutionSupportObservedLadders(
        string modelId, EffortLevel effort, string level)
    {
        var model = modelId == KnownModels.Gpt6Sol.Value ? KnownModels.Gpt6Sol : KnownModels.Gpt6Luna;
        var suggestion = ModelEfficiencyMatrix.Default.EvaluateModel(
            model, TaskClass.Feature, BudgetPressure.Tight, At, effort)!;
        var resolution = ModelRoutingKnowledgeBase.Default.Resolve(model, level, RoutingWorkflowRole.CoreTask);

        Assert.Equal(effort, suggestion.SuggestedEffort);
        Assert.Equal(Cli.Codex, suggestion.Cli);
        Assert.True(suggestion.Provisional);
        Assert.Equal(PolicyEvidenceStatus.Provisional, suggestion.EvidenceStatus);
        Assert.True(resolution.IsResolved, resolution.Reason);
        Assert.Equal(model.Value, resolution.Model!.CanonicalId);
    }

    [Theory]
    [InlineData("gpt-6-sol", "minimal")]
    [InlineData("gpt-6-luna", "minimal")]
    [InlineData("gpt-6-luna", "ultra")]
    public void ResolutionRejectsLevelsOutsideEachObservedLadder(string model, string level)
        => Assert.Equal(ModelRouteResolutionStatus.UnsupportedThinkingLevel,
            ModelRoutingKnowledgeBase.Default.Resolve(model, level, RoutingWorkflowRole.CoreTask).Status);

    [Fact]
    public void ProvisionalSupportAddsNoDefaultsRecommendationsFallbacksOrBareAliasChanges()
    {
        var models = new[] { KnownModels.Gpt6Sol, KnownModels.Gpt6Luna };
        var knowledge = ModelRoutingKnowledgeBase.Default;

        Assert.Equal(KnownModels.Gpt56Sol.Value, knowledge.FindModel("sol")!.CanonicalId);
        Assert.Equal(KnownModels.Gpt56Luna.Value, knowledge.FindModel("luna")!.CanonicalId);
        Assert.All(models, model =>
        {
            Assert.DoesNotContain(knowledge.Routes, route => route.ModelId == model.Value);
            Assert.DoesNotContain(knowledge.ProviderFallbacks, fallback => fallback.ModelId == model.Value);
            Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
                recommendation => Assert.DoesNotContain(recommendation.Candidates,
                    candidate => candidate.Model == model));
        });
    }
}
