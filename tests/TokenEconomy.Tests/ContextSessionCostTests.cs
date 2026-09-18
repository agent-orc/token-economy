using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ContextSessionCostTests
{
    private static ModelPriceCatalog Catalog => new([new ModelListing { ModelId = "test", History =
        [new ModelPrice { InputPerMTok = 2, CacheReadPerMTok = .2m, CacheWritePerMTok = 2.5m, OutputPerMTok = 10 }] }]);
    private static ContextSessionOptions Options => new()
    {
        ModelId = "test", StartUtc = new(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
        InitialContext = 10_000, ToolTokensPerTurn = 900, OutputTokensPerTurn = 100,
        Turns = 3, CacheTtlMinutes = 5
    };

    [Fact]
    public void WarmGrowthUsesDisjointKinds()
    {
        var f = ContextSessionCost.Forecast(Options, Catalog);
        Assert.Equal(new TokenUsage(0, 100, 0, 10_000), f.Turns[0].Usage);
        Assert.Equal(new TokenUsage(0, 100, 10_000, 1_000), f.Turns[1].Usage);
        // .026 + .0055 + .0057
        Assert.Equal(.0372m, f.Total);
    }

    [Theory]
    [InlineData(4.999, 10000)]
    [InlineData(5, 0)]
    [InlineData(6, 0)]
    public void ExpiryBoundaryAndRewrite(double gap, long read)
    {
        var f = ContextSessionCost.Forecast(Options with { GapMinutes = gap }, Catalog);
        Assert.Equal(read, f.Turns[1].Usage.CacheRead);
        if (read == 0) Assert.Equal(.0855m, f.Total); // .026 + .0285 + .031
    }

    [Fact]
    public void HitsRefreshLifetimeAndCustomGapsExpire()
    {
        var f = ContextSessionCost.Forecast(Options with { GapsMinutes = [0, 4, 4] }, Catalog);
        Assert.False(f.Turns[2].CacheExpired);
        f = ContextSessionCost.Forecast(Options with { GapsMinutes = [0, 1, 5] }, Catalog);
        Assert.Equal(12_000, f.Turns[2].Usage.CacheWrite);
    }

    [Fact]
    public void CompactionChargesAdditionalCallAndColdSummary()
    {
        var f = ContextSessionCost.Forecast(Options with { Turns = 2, CompactionThreshold = 11_000,
            CompactedContext = 2_000, CompactionOutputTokens = 200 }, Catalog);
        // First .026; summary: .002 read + .0025 write + .002 output;
        // ordinary turn: .005 rewrite + .001 output.
        Assert.Equal(.0385m, f.Total);
        Assert.True(f.Turns[1].Compacted);
        Assert.Equal(11_000, f.Turns[1].ContextBeforeCompaction);
        Assert.Equal(new TokenUsage(0, 300, 10_000, 3_000), f.Turns[1].Usage);
        Assert.Equal(4, ContextSessionCost.PaybackTurns(.0065m, 9_000, .2m));
    }

    [Fact]
    public void FreshSessionsCanReuseSharedPrefix()
    {
        var f = ContextSessionCost.Forecast(Options with { RestartEveryTurns = 1,
            RestartContext = 10_000, SharedPrefixTokens = 8_000 }, Catalog);
        Assert.Equal(new TokenUsage(0, 100, 8_000, 2_000), f.Turns[1].Usage);
        Assert.Equal(.0412m, f.Total);
    }

    [Fact]
    public void ReasoningIsBilledAndRetentionIsExplicit()
    {
        var f = ContextSessionCost.Forecast(Options with { ReasoningTokensPerTurn = 500,
            RetainedReasoningTokensPerTurn = 200 }, Catalog);
        Assert.Equal(600, f.Turns[0].Usage.Output);
        Assert.Equal(11_200, f.Turns[1].ContextTokens);
    }

    [Fact]
    public void UnknownsRemainUnknownAndInvalidInputsFail()
    {
        Assert.Null(ContextSessionCost.Forecast(Options with { CacheTtlMinutes = null }, Catalog).Total);
        Assert.Null(ContextSessionCost.Forecast(Options with { ModelId = "missing" }, Catalog).Total);
        Assert.Throws<ArgumentOutOfRangeException>(() => ContextSessionCost.Forecast(Options with { ToolTokensPerTurn = -1 }));
        Assert.Throws<ArgumentException>(() => ContextSessionCost.Forecast(Options with { GapsMinutes = [1] }));
        Assert.Throws<ArgumentException>(() => ContextSessionCost.Forecast(Options with { CompactionThreshold = 1000, CompactedContext = 1000 }));
        Assert.Equal(0m, ContextSessionCost.Forecast(Options with { Turns = 0 }, Catalog).Total);
    }

    [Fact]
    public void DisabledCacheAndMinimumAreUncached()
    {
        Assert.Equal(.069m, ContextSessionCost.Forecast(Options with { CacheTtlMinutes = 0 }, Catalog).Total);
        Assert.Equal(.069m, ContextSessionCost.Forecast(Options with { MinimumCacheTokens = 20_000 }, Catalog).Total);
    }

    [Fact]
    public void DateChangeAndWriteOverrideApplyToEachCall()
    {
        var catalog = new ModelPriceCatalog([new ModelListing { ModelId = "test", History = [
            new ModelPrice { InputPerMTok = 1, OutputPerMTok = 2, ValidTo = Options.StartUtc.AddSeconds(30) },
            new ModelPrice { InputPerMTok = 2, OutputPerMTok = 4, ValidFrom = Options.StartUtc.AddSeconds(31) }] }]);
        var f = ContextSessionCost.Forecast(Options with { Turns = 2, CacheTtlMinutes = 0 }, catalog);
        Assert.Equal(.0326m, f.Total);
        Assert.Equal(.041m, ContextSessionCost.Forecast(Options with { Turns = 1, CacheWritePerMTokOverride = 4 }, Catalog).Total);
    }

    [Fact]
    public void CrossingIsOnlyAtCompletedTaskAndNotInitialEquality()
    {
        var a = ContextSessionCost.Forecast(Options with { Turns = 50 }, Catalog);
        var b = ContextSessionCost.Forecast(Options with { Turns = 50, RestartEveryTurns = 1, RestartContext = 10_000, SharedPrefixTokens = 10_000 }, Catalog);
        Assert.Equal(2, ContextSessionCost.FirstMoreExpensiveTask(a, b, 1));
        Assert.Null(ContextSessionCost.FirstMoreExpensiveTask(a, a, 1));
    }
    [Fact]
    public void MissingCacheTariffAndForeignCurrencyAreExplicitlyUnavailable()
    {
        var noCache = new ModelPriceCatalog([new ModelListing { ModelId = "test", History =
            [new ModelPrice { InputPerMTok = 1, OutputPerMTok = 2 }] }]);
        Assert.Null(ContextSessionCost.Forecast(Options, noCache).Total);
        Assert.NotNull(ContextSessionCost.Forecast(Options with { CacheTtlMinutes = 0 }, noCache).Total);
        var euro = new ModelPriceCatalog([new ModelListing { ModelId = "test", History =
            [new ModelPrice { InputPerMTok = 1, OutputPerMTok = 2, Currency = "EUR" }] }]);
        Assert.Null(ContextSessionCost.Forecast(Options with { CacheTtlMinutes = 0 }, euro).Total);
    }

    [Fact]
    public void SharedPrefixBelowMinimumCannotHit()
    {
        var f = ContextSessionCost.Forecast(Options with { Turns = 2, RestartEveryTurns = 1,
            RestartContext = 10_000, SharedPrefixTokens = 500, MinimumCacheTokens = 1024 }, Catalog);
        Assert.Equal(0, f.Turns[1].Usage.CacheRead);
        Assert.Equal(10_000, f.Turns[1].Usage.CacheWrite);
    }
}
