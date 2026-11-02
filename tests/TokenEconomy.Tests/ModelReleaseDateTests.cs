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
            Assert.Equal("https", source!.Scheme);
            Assert.Contains(source.Host, new[] { "platform.claude.com", "openai.com", "developers.openai.com", "ai.google.dev", "www.antigravity.google", "blog.google" });
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
        Assert.Equal(new DateOnly(2026, 9, 1), catalog.Find(KnownModels.Gemini38Flash)!.ReleaseDate);
        Assert.Equal(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), catalog.PriceDevelopment(KnownModels.Gemini38Flash)[0].ValidFrom);
        Assert.Equal(new DateOnly(2026, 10, 13), catalog.Find("gemini-4-argon-high")!.ReleaseDate);
        Assert.Equal(new DateOnly(2026, 10, 13), catalog.Find(KnownModels.Gemini4Argon)!.ReleaseDate);
        Assert.Equal(new DateOnly(2026, 10, 7), catalog.Find(KnownModels.ClaudeHaiku55)!.ReleaseDate);
        var newest = catalog.Listings.OrderByDescending(listing => listing.ReleaseDate)
            .ThenBy(listing => listing.ModelId, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            [KnownModels.Gpt6Rosalind.Value, KnownModels.Gemini4Carbon.Value,
                KnownModels.Gpt61Terra.Value, KnownModels.Gemini4Argon.Value,
                KnownModels.ClaudeHaiku55.Value, KnownModels.Gpt61Sol.Value,
                KnownModels.ClaudeSonnet55.Value, KnownModels.ClaudeOpus55.Value,
                KnownModels.Gpt6Luna.Value, KnownModels.Gpt6Sol.Value,
                KnownModels.Gpt6Astra.Value],
            newest.Take(11).Select(listing => listing.ModelId));
        Assert.Equal([KnownModels.ClaudeFable51.Value, KnownModels.ClaudeMythos51.Value,
                KnownModels.Gemini38Flash.Value],
            newest.Skip(11).Take(3).Select(listing => listing.ModelId));
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
