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

    [Fact]
    public void EveryListing_HasADisplayName()
    {
        // A consumer resolving cost through this catalog can always render a friendly label from
        // the same lookup instead of falling back to a raw id or maintaining its own naming table.
        foreach (var listing in Catalog.Listings)
            Assert.False(string.IsNullOrWhiteSpace(listing.DisplayName), $"{listing.ModelId} has no DisplayName");
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
    [InlineData("gpt-5.5", 5.00, 30.00, 0.50)]
    [InlineData("gpt-5.5-pro", 30.00, 180.00, null)]
    [InlineData("gpt-5.5-cyber-preview", 12.50, 75.00, 1.25)]
    public void ConfirmedGpt55Family_HasPublishedStandardRates(
        string model, double input, double output, double? cacheRead)
    {
        var price = Catalog.ResolvePrice(model, new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)).Price;

        Assert.NotNull(price);
        Assert.Equal((decimal)input, price!.InputPerMTok);
        Assert.Equal((decimal)output, price.OutputPerMTok);
        Assert.Equal(cacheRead is null ? null : (decimal)cacheRead.Value, price.CacheReadPerMTok);
        Assert.Null(price.CacheWritePerMTok);
        Assert.False(price.Unconfirmed);
        Assert.Contains("https://", price.Source);
        Assert.Contains("retrieved 2026-08-11", price.Source);
    }

    [Fact]
    public void Gpt55Family_AliasesResolveToTheirPublishedListings()
    {
        Assert.Equal("gpt-5.5", Catalog.Find("GPT-5.5-2026-04-23")?.ModelId);
        Assert.Equal("gpt-5.5-pro", Catalog.Find("gpt-5.5-pro-2026-04-23")?.ModelId);
        Assert.Equal("gpt-5.5-cyber-preview", Catalog.Find("GPT-5.5-CYBER")?.ModelId);
    }

    [Fact]
    public void AstraAndCurrentSol_HaveDatedOfficialRates()
    {
        var at = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);
        var astra = Catalog.ResolvePrice(KnownModels.Gpt6Astra, at).Price;
        var sol = Catalog.ResolvePrice(KnownModels.Gpt56Sol, at).Price;

        Assert.NotNull(astra);
        Assert.Equal((10m, 50m, 1m, 12.5m),
            (astra!.InputPerMTok, astra.OutputPerMTok, astra.CacheReadPerMTok, astra.CacheWritePerMTok));
        Assert.NotNull(sol);
        Assert.Equal((4m, 20m, 0.4m, 5m),
            (sol!.InputPerMTok, sol.OutputPerMTok, sol.CacheReadPerMTok, sol.CacheWritePerMTok));
        Assert.Contains("retrieved 2026-09-11", astra.Source);
        Assert.Contains("retrieved 2026-09-11", sol.Source);
        Assert.Equal(2, Catalog.PriceDevelopment(KnownModels.Gpt56Sol).Count);
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
            [
                new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
            ],
            Catalog.PriceDevelopment("gpt-5.6-sol").Select(price => price.ValidFrom));
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
        Assert.Equal(
            new DateTime(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc),
            Catalog.PriceDevelopment("gpt-5.5").Single().ValidFrom);
        Assert.Equal(
            new DateTime(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc),
            Catalog.PriceDevelopment("gpt-5.5-pro").Single().ValidFrom);
        Assert.Equal(
            new DateTime(2026, 5, 7, 0, 0, 0, DateTimeKind.Utc),
            Catalog.PriceDevelopment("gpt-5.5-cyber-preview").Single().ValidFrom);

        foreach (var id in new[] { "gpt-5.6-terra", "gpt-5.6-luna", "gpt-5.4-mini" })
            foreach (var price in Catalog.PriceDevelopment(id))
            {
                Assert.Contains("https://", price.Source);
                Assert.Contains("retrieved 2026-08-09", price.Source);
            }

        Assert.All(Catalog.PriceDevelopment("gpt-5.6-sol"), price => Assert.Contains("https://", price.Source));
        Assert.Contains(Catalog.PriceDevelopment("gpt-5.6-sol"), price => price.Source!.Contains("retrieved 2026-08-09"));
        Assert.Contains(Catalog.PriceDevelopment("gpt-5.6-sol"), price => price.Source!.Contains("retrieved 2026-09-11"));

        foreach (var id in new[] { "gpt-5.5", "gpt-5.5-pro", "gpt-5.5-cyber-preview" })
            foreach (var price in Catalog.PriceDevelopment(id))
            {
                Assert.Contains("https://", price.Source);
                Assert.Contains("retrieved 2026-08-11", price.Source);
            }
    }

    [Theory]
    [InlineData("claude-opus-5", "Claude Opus 5")]
    [InlineData("claude-sonnet-5", "Claude Sonnet 5")]
    [InlineData("claude-sonnet-4-6", "Claude Sonnet 4.6")]
    public void ClaudeFiveFamily_HasTheCanonicalDisplayName(string model, string expectedDisplayName)
        => Assert.Equal(expectedDisplayName, Catalog.Find(model)?.DisplayName);

    [Fact]
    public void ClaudeSonnet46_DatedSnapshotAlias_ResolvesToTheBareListing()
    {
        // Recording CLIs report the dated snapshot id, not the bare one; without this alias
        // ComputeCost resolves to UnknownModel and a "Recorded model usage" panel shows $0/Unknown
        // even though the model is fully priced under its bare id.
        Assert.Equal("claude-sonnet-4-6", Catalog.Find("claude-sonnet-4-6-20260301")?.ModelId);

        var breakdown = Catalog.ComputeCost(
            "claude-sonnet-4-6-20260301",
            new TokenUsage(Input: 1_000_000, Output: 200_000),
            new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PriceStatus.Resolved, breakdown.Status);
        Assert.Equal(6.00m, breakdown.Total);
    }

    [Fact]
    public void ClaudeFiveFamily_HasADatedOfficialSource()
    {
        foreach (var id in new[] { "claude-opus-5", "claude-sonnet-5", "claude-sonnet-4-6" })
            foreach (var price in Catalog.PriceDevelopment(id))
            {
                Assert.Contains("https://www.anthropic.com/pricing", price.Source);
                Assert.Contains("retrieved 2026-08-18", price.Source);
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
