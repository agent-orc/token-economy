using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public class ModelPriceProvenanceTests
{
    [Fact]
    public void EveryDefaultModel_HasPositiveDatedPricesAndPrimarySources()
    {
        foreach (var listing in ModelPriceCatalog.Default.Listings)
        {
            Assert.NotEmpty(listing.History);
            foreach (var price in listing.History)
            {
                Assert.True(price.ValidFrom > DateTime.UnixEpoch);
                Assert.Equal(DateTimeKind.Utc, price.ValidFrom.Kind);
                Assert.Equal(new DateOnly(2026, 9, 12), price.VerifiedOn);
                Assert.NotEmpty(price.SourceUrls);
                Assert.All(price.SourceUrls, source => Assert.True(Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == "https"));
                Assert.False(price.Unconfirmed);
                Assert.NotNull(price.ValidFromBasis);
            }

            var first = listing.History.MinBy(price => price.ValidFrom)!;
            Assert.Equal(PriceStatus.NoPriceForDate,
                ModelPriceCatalog.Default.ResolvePrice(listing.ModelId, first.ValidFrom.AddTicks(-1)).Status);
            Assert.Equal(PriceStatus.Resolved,
                ModelPriceCatalog.Default.ResolvePrice(listing.ModelId, first.ValidFrom).Status);
        }
    }

    [Theory]
    [InlineData("gpt-5", 2025, 8, 7)]
    [InlineData("gpt-5-codex", 2025, 9, 23)]
    [InlineData("gpt-5.5", 2026, 4, 24)]
    [InlineData("gpt-5.5-pro", 2026, 4, 24)]
    [InlineData("claude-opus-4-1", 2025, 8, 5)]
    public void BackfilledPrice_StartsAtApiPublicationDay(string model, int year, int month, int day)
    {
        var price = Assert.Single(ModelPriceCatalog.Default.PriceDevelopment(model));
        Assert.Equal(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), price.ValidFrom);
        Assert.Equal("provider-announced-date", price.ValidFromBasis);
    }

    [Fact]
    public void Cyber_SeparatesConfirmedNumericalPriceFromInferredHistoricalStart()
    {
        var price = Assert.Single(ModelPriceCatalog.Default.PriceDevelopment("gpt-5.5-cyber-preview"));
        Assert.False(price.Unconfirmed);
        Assert.Equal("release-date-inference", price.ValidFromBasis);
        Assert.Contains("no numeric tariff", price.Note);
    }

    [Fact]
    public void OptionalProvenance_PreservesOlderHostSuppliedJsonAndRoundTripsNewFields()
    {
        var legacy = JsonSerializer.Deserialize<ModelPrice>("""{"InputPerMTok":1,"OutputPerMTok":2}""")!;
        Assert.Empty(legacy.SourceUrls);
        Assert.Null(legacy.VerifiedOn);
        Assert.Null(legacy.ValidFromBasis);

        var sourced = legacy with
        {
            SourceUrls = ["https://example.com/prices"],
            VerifiedOn = new DateOnly(2026, 9, 12),
            ValidFromBasis = "provider-announced-date"
        };
        var copy = JsonSerializer.Deserialize<ModelPrice>(JsonSerializer.Serialize(sourced))!;
        Assert.Equal(sourced.SourceUrls, copy.SourceUrls);
        Assert.Equal(sourced.VerifiedOn, copy.VerifiedOn);
        Assert.Equal(sourced.ValidFromBasis, copy.ValidFromBasis);
    }
}

