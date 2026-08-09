using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelPriceSeedTests
{
    private static readonly ModelPriceCatalog Catalog = ModelPriceCatalog.Default;

    [Fact]
    public void DefaultCatalog_BuildsWithoutDuplicateKeys()
        => Assert.NotEmpty(Catalog.Listings);   // constructor throws on a duplicate id/alias

    [Fact]
    public void EveryListing_HasAnIdAndEveryPricedEntryIsPositive()
    {
        foreach (var listing in Catalog.Listings)
        {
            Assert.False(string.IsNullOrWhiteSpace(listing.ModelId));
            foreach (var price in listing.History)
            {
                // A seeded price never accidentally reads as $0 for input or output.
                Assert.True(price.InputPerMTok > 0m, $"{listing.ModelId} input rate");
                Assert.True(price.OutputPerMTok > 0m, $"{listing.ModelId} output rate");
                if (price.CacheReadPerMTok is { } cr) Assert.True(cr > 0m);
                if (price.CacheWritePerMTok is { } cw) Assert.True(cw > 0m);
                Assert.Equal("USD", price.Currency);
            }
        }
    }

    [Fact]
    public void EveryAlias_ResolvesToItsListing()
    {
        foreach (var listing in Catalog.Listings)
            foreach (var alias in listing.Aliases)
                Assert.Equal(listing.ModelId, Catalog.Find(alias)?.ModelId);
    }

    [Theory]
    [InlineData("claude-fable-5", 10.00, 50.00)]
    [InlineData("claude-opus-5", 5.00, 25.00)]
    [InlineData("claude-opus-4-8", 5.00, 25.00)]
    [InlineData("claude-opus-4-7", 5.00, 25.00)]
    [InlineData("claude-opus-4-6", 5.00, 25.00)]
    [InlineData("claude-sonnet-4-6", 3.00, 15.00)]
    [InlineData("claude-haiku-4-5", 1.00, 5.00)]
    public void ConfirmedClaudeModels_HaveTheKnownRates(string model, double input, double output)
    {
        var price = Catalog.ResolvePrice(model, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)).Price;
        Assert.NotNull(price);
        Assert.Equal((decimal)input, price!.InputPerMTok);
        Assert.Equal((decimal)output, price.OutputPerMTok);
        Assert.False(price.Unconfirmed);
        // Anthropic cache rates are the documented multiples of the input rate.
        Assert.Equal((decimal)input * 0.10m, price.CacheReadPerMTok);
        Assert.Equal((decimal)input * 1.25m, price.CacheWritePerMTok);
    }

    [Theory]
    [InlineData("gpt-5.5")]
    [InlineData("gpt-5")]
    [InlineData("gpt-5-codex")]
    public void RemainingGpt5Placeholders_AreKnownButUnpriced(string model)
    {
        var listing = Catalog.Find(model);
        Assert.NotNull(listing);
        Assert.Equal("openai", listing!.Vendor);
        Assert.Empty(listing.History);   // present, but no invented number
        Assert.NotNull(listing.Note);
    }

    [Theory]
    [InlineData("gpt-5.6-sol", 5.00, 30.00, 0.50, 6.25)]
    [InlineData("gpt-5.6-terra", 2.00, 12.00, 0.20, 2.50)]
    [InlineData("gpt-5.6-luna", 0.20, 1.20, 0.02, 0.25)]
    public void ConfirmedGpt56Models_HaveCurrentPublishedStandardRates(
        string model, double input, double output, double cacheRead, double cacheWrite)
    {
        var price = Catalog.ResolvePrice(model, new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)).Price;

        Assert.NotNull(price);
        Assert.Equal((decimal)input, price!.InputPerMTok);
        Assert.Equal((decimal)output, price.OutputPerMTok);
        Assert.Equal((decimal)cacheRead, price.CacheReadPerMTok);
        Assert.Equal((decimal)cacheWrite, price.CacheWritePerMTok);
        Assert.False(price.Unconfirmed);
        Assert.Contains("https://", price.Source);
        Assert.Contains("retrieved 2026-08-09", price.Source);
    }

    [Fact]
    public void Gpt54Mini_HasPublishedRates_AndNoSeparateCacheWriteRate()
    {
        var price = Catalog.ResolvePrice("gpt-5.4-mini", new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)).Price!;

        Assert.Equal(0.75m, price.InputPerMTok);
        Assert.Equal(4.50m, price.OutputPerMTok);
        Assert.Equal(0.075m, price.CacheReadPerMTok);
        Assert.Null(price.CacheWritePerMTok);
        Assert.Equal(
            0.75m,
            Catalog.ComputeCost("gpt-5.4-mini", new TokenUsage(0, 0, CacheWrite: 1_000_000),
                new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)).CacheWriteCost);
        Assert.False(price.Unconfirmed);
    }

    [Fact]
    public void ConfirmedOpenAiHistory_StartsAtPublishedLaunchDates()
    {
        Assert.Equal(
            new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc),
            Catalog.PriceDevelopment("gpt-5.6-sol").Single().ValidFrom);
        Assert.Equal(
            [
                new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            ],
            Catalog.PriceDevelopment("gpt-5.6-terra").Select(price => price.ValidFrom));
        Assert.Equal(
            [
                new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            ],
            Catalog.PriceDevelopment("gpt-5.6-luna").Select(price => price.ValidFrom));
        Assert.Equal(
            new DateTime(2026, 3, 17, 0, 0, 0, DateTimeKind.Utc),
            Catalog.PriceDevelopment("gpt-5.4-mini").Single().ValidFrom);

        foreach (var id in new[] { "gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna", "gpt-5.4-mini" })
            foreach (var price in Catalog.PriceDevelopment(id))
            {
                Assert.Contains("https://", price.Source);
                Assert.Contains("retrieved 2026-08-09", price.Source);
            }
    }

    [Fact]
    public void UnconfirmedEntries_AreFlagged_NotInvented()
    {
        // The models we couldn't confirm against an authoritative table carry the flag rather than a silent claim.
        foreach (var id in new[] { "claude-opus-4-5", "claude-sonnet-4-5" })
        {
            var price = Catalog.ResolvePrice(id, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)).Price;
            Assert.NotNull(price);
            Assert.True(price!.Unconfirmed);
            Assert.NotNull(price.Note);
        }
    }
}
