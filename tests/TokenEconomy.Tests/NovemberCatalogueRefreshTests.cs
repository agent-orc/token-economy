using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class NovemberCatalogueRefreshTests
{
    private static readonly DateTime After = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CorrectedSonnetReadRepricesHistoricalUsage()
    {
        var at = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var cost = ModelPriceCatalog.Default.ComputeCost("claude-sonnet-5-5",
            new TokenUsage(0, 0, CacheRead: 1_000_000), at);
        Assert.Equal(.10m, cost.Total);
        Assert.Equal(.10m, cost.Price!.CacheReadPerMTok);
    }

    [Fact]
    public void AlternateCacheRetentionUsesDatedRatesAndRejectsUnsupportedDuration()
    {
        var catalog = ModelPriceCatalog.Default;
        var after = catalog.ComputeCost("gpt-6.1-terra",
            new TokenUsage(0, 0, CacheWrite: 1_000_000, PromptTokens: 20_000,
                CacheWriteRetention: CacheWriteRetention.TwelveHours), After);
        Assert.Equal(2.5m, after.Total);
        var longPrompt = catalog.ComputeCost("gpt-6.1-terra",
            new TokenUsage(0, 0, CacheWrite: 1_000_000, PromptTokens: 272_001,
                CacheWriteRetention: CacheWriteRetention.TwelveHours), After);
        Assert.Equal(5m, longPrompt.Total);
        var unsupported = catalog.ComputeCost("claude-sonnet-5-5",
            new TokenUsage(0, 0, CacheWrite: 1_000_000,
                CacheWriteRetention: CacheWriteRetention.TwelveHours), After);
        Assert.Equal(PriceStatus.UnsupportedCacheRetention, unsupported.Status);
        Assert.Null(unsupported.Total);
        var oneHour = catalog.ComputeCost("claude-sonnet-5-5",
            new TokenUsage(0, 0, CacheWrite: 1_000_000,
                CacheWriteRetention: CacheWriteRetention.OneHour), After);
        Assert.Equal(4m, oneHour.Total);
        var rosalindWrite = catalog.ComputeCost("gpt-rosalind-discovery",
            new TokenUsage(0, 0, CacheWrite: 1_000_000), After);
        Assert.Equal(PriceStatus.UnsupportedCacheRetention, rosalindWrite.Status);
        Assert.Null(rosalindWrite.Total);
        Assert.Null(catalog.ResolvePrice("gpt-6.1-terra", new DateTime(2026, 10, 25)).Price);
    }

    [Fact]
    public void RestrictedListingsAndCarbonCannotBecomeRecommendations()
    {
        var restricted = new[] { "gpt-6-rosalind", "gpt-5.6-cyber", "gpt-rosalind-research",
            "gpt-rosalind-discovery", "claude-mythos-5-1", "claude-mythos-5", "gemini-4-carbon" };
        var knowledge = ModelRoutingKnowledgeBase.Default;
        foreach (var id in restricted)
        {
            Assert.Equal(ModelRoutingStatus.Restricted, knowledge.FindModel(id)!.RoutingStatus);
            Assert.DoesNotContain(knowledge.Routes, route => route.ModelId == id);
            Assert.DoesNotContain(knowledge.ProviderFallbacks, route => route.ModelId == id);
            Assert.All(TaskClassRecommendationCatalog.Default.Recommendations,
                recommendation => Assert.DoesNotContain(recommendation.Candidates,
                    candidate => candidate.Model.Value == id));
        }
        Assert.Equal(PriceStatus.NoPriceForDate, ModelPriceCatalog.Default.ResolvePrice("gemini-4-carbon", After).Status);
    }

    [Theory]
    [InlineData("gpt-6.1-sol")]
    [InlineData("gpt-6.1-terra")]
    public void ConservativeCodexFloorRequiresObservedWorkingVersion(string model)
    {
        var knowledge = ModelRoutingKnowledgeBase.Default;
        Assert.Equal(ModelRouteResolutionStatus.CliVersionTooOld,
            knowledge.Resolve(model, "medium", RoutingWorkflowRole.CoreTask, new Version(0, 155, 0)).Status);
        Assert.True(knowledge.Resolve(model, "medium", RoutingWorkflowRole.CoreTask,
            new Version(0, 166, 0)).IsResolved);
    }
}
