using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class Sonnet55SupportTests
{
    private static readonly DateTime At = new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PricingStartsAtReleaseWithTheSonnet5Rates()
    {
        var catalog = ModelPriceCatalog.Default;
        var listing = catalog.Find(KnownModels.ClaudeSonnet55)!;
        var price = catalog.ResolvePrice(KnownModels.ClaudeSonnet55, At).Price!;
        var sonnet5 = catalog.ResolvePrice(KnownModels.ClaudeSonnet5, At).Price!;

        Assert.Equal("Claude Sonnet 5.5", listing.DisplayName);
        Assert.Equal(new DateOnly(2026, 9, 28), listing.ReleaseDate);
        Assert.Equal((2m, 10m, .1m, 2.5m),
            (price.InputPerMTok, price.OutputPerMTok, price.CacheReadPerMTok, price.CacheWritePerMTok));
        Assert.Equal(sonnet5.CacheReadPerMTok / 2, price.CacheReadPerMTok);
        Assert.Equal(4m, price.CacheWriteOneHourPerMTok);
        Assert.Equal(new DateOnly(2026, 11, 2), price.VerifiedOn);
        Assert.Equal("provider-announced-date", price.ValidFromBasis);
        Assert.False(price.Unconfirmed);
        Assert.Equal(PriceStatus.NoPriceForDate,
            catalog.ResolvePrice(KnownModels.ClaudeSonnet55, new DateTime(2026, 9, 27, 23, 59, 59, DateTimeKind.Utc)).Status);
    }

    [Fact]
    public void UpdatedRunnerMakesSonnetSelectableWithTheDocumentedLadder()
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        var model = knowledge.FindModel(KnownModels.ClaudeSonnet55.Value)!;

        Assert.Equal(new[] { "low", "medium", "high", "xhigh", "max" }, model.SupportedThinkingLevels);
        Assert.Equal(ModelRoutingStatus.Selectable, model.RoutingStatus);
        Assert.Equal("2.1.284", model.MinimumCliVersion);
        Assert.Equal(ModelRouteResolutionStatus.CliVersionTooOld,
            knowledge.Resolve(KnownModels.ClaudeSonnet55, "medium", RoutingWorkflowRole.CoreTask, new Version(2, 1, 281)).Status);
        Assert.True(knowledge.Resolve(KnownModels.ClaudeSonnet55, "medium", RoutingWorkflowRole.CoreTask,
            new Version(2, 1, 284)).IsResolved);
        Assert.True(knowledge.Resolve(KnownModels.ClaudeSonnet55, "medium", RoutingWorkflowRole.CoreTask,
            new Version(2, 1, 305)).IsResolved);
    }

    [Fact]
    public void AdditionLeavesTheSonnet5FallbackAndRecommendationsUnchanged()
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        var fallback = Assert.Single(knowledge.ProviderFallbacks);

        Assert.Equal(KnownModels.ClaudeSonnet5.Value, fallback.ModelId);
        Assert.DoesNotContain(knowledge.Routes, route => route.ModelId == KnownModels.ClaudeSonnet55.Value);
        Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
            recommendation => Assert.DoesNotContain(recommendation.Candidates,
                candidate => candidate.Model == KnownModels.ClaudeSonnet55));
    }
}
