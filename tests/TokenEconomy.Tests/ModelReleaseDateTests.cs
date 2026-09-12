using System.Text.Json;
using TokenEconomy;
using Xunit;

namespace TokenEconomy.Tests;

public sealed class ModelReleaseDateTests
{
    [Fact]
    public void SeededReleaseDatesRetainPrimarySourcesAndSerializeAsDates()
    {
        foreach (var listing in ModelPriceCatalog.Default.Listings)
        {
            Assert.NotNull(listing.ReleaseDate);
            Assert.True(Uri.TryCreate(listing.ReleaseDateSource, UriKind.Absolute, out var source));
            Assert.Contains(source!.Host, new[] { "platform.claude.com", "openai.com" });
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(listing, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Assert.Equal(listing.ReleaseDate.Value.ToString("yyyy-MM-dd"), json.RootElement.GetProperty("releaseDate").GetString());
            Assert.Equal(listing.ReleaseDateSource, json.RootElement.GetProperty("releaseDateSource").GetString());
        }
    }

    [Fact]
    public void PublicationDateIsIndependentOfPriceHistoryAndSnapshotSuffix()
    {
        var catalog = ModelPriceCatalog.Default;
        Assert.Equal(new DateOnly(2025, 11, 24), catalog.Find(KnownModels.ClaudeOpus45)!.ReleaseDate);
        Assert.Equal(new DateOnly(2026, 4, 23), catalog.Find(KnownModels.Gpt55)!.ReleaseDate);
        Assert.Equal(new DateTime(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc), catalog.PriceDevelopment(KnownModels.Gpt55)[0].ValidFrom);
        var newest = catalog.Listings.OrderByDescending(listing => listing.ReleaseDate).ToArray();
        Assert.Equal(KnownModels.Gpt6Astra.Value, newest[0].ModelId);
        Assert.Equal(KnownModels.ClaudeFable51.Value, newest[1].ModelId);
        Assert.Equal(KnownModels.ClaudeOpus41.Value, newest[^1].ModelId);
    }

    [Fact]
    public void UnknownCustomReleaseDateIsNotInferredFromModelNameOrPrice()
    {
        var custom = new ModelListing
        {
            ModelId = "custom-20990101",
            History = [new() { InputPerMTok = 1, OutputPerMTok = 1, ValidFrom = new DateTime(2026, 1, 1) }],
        };
        Assert.Null(custom.ReleaseDate);
        Assert.Null(custom.ReleaseDateSource);
    }
}
